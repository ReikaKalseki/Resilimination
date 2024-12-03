using UnityEngine;  //Needed for most Unity Enginer manipulations: Vectors, GameObjects, Audio, etc.
using System.IO;    //For data read/write methods
using System;    //For data read/write methods
using System.Collections.Generic;   //Working with Lists and Collections
using System.Linq;   //More advanced manipulation of lists/collections
using System.Threading;
using Harmony;
using ReikaKalseki;
using ReikaKalseki.FortressCore;

namespace ReikaKalseki.Resilimination
{
  public class ResiliminationMod : FCoreMod
  {
    public const string MOD_KEY = "ReikaKalseki.Resilimination";
    
    private static Config<RSConfig.ConfigEntries> config;

	public static ushort bomberFalcorBlockID;
    
    public ResiliminationMod() : base("Resilimination") {
    	config = new Config<RSConfig.ConfigEntries>(this);
    }
	
	public static Config<RSConfig.ConfigEntries> getConfig() {
		return config;
	}

    protected override void loadMod(ModRegistrationData registrationData) {        
        config.load();
        
        runHarmony();
        
		registrationData.RegisterEntityHandler("ReikaKalseki.ResinBomberFalcor");
		TerrainDataEntry terrainDataEntry;
		TerrainDataValueEntry terrainDataValueEntry;
		TerrainData.GetCubeByKey("ReikaKalseki.ResinBomberFalcor_Key", out terrainDataEntry, out terrainDataValueEntry);
		bomberFalcorBlockID = terrainDataEntry.CubeType;
		/*
		Action<CraftData> func = (bake) => {
			bake.ScanRequirements.Add("SoftResin");
			//bake.ResearchCost = 1;
			bake.ResearchRequirements.Add("ResinHandling");
		};
		RecipeUtil.addRecipe("SoftResinBaking", "ReikaKalseki.SafeSoftResin", "decoration", config.getInt(RSConfig.ConfigEntries.RESIN_BAKE_COST), "Smelter", func);
		*/
		CraftData exp = RecipeUtil.getRecipeByKey("chargeexplosive");
		int cost = (int)exp.Costs.First(c => c.Key == "RefinedLiquidResin").Amount;
		int scale = Math.Max(1, cost/config.getInt(RSConfig.ConfigEntries.RESIN_COST));
		FUtil.log("Chargeable explosive costs "+cost+" resin. Adjusting soft resin bomb yield by "+scale+"x");
		if (scale > 1) {
			//CraftData rec = RecipeUtil.getRecipeByKey("ReikaKalseki.ResinBomberFalcor");
			//rec.
			CraftData rec = GenericAutoCrafterNew.mMachinesByKey["ReikaKalseki.SoftResinBombMaker"].Recipe;
			rec.CraftedAmount *= scale;
			rec.Costs.ForEach(c => {if (c.Key != "ChargeableExplosive"){c.Amount = (uint)Math.Min(100, c.Amount*scale);}});
		}
		
		CraftData bake = GenericAutoCrafterNew.mMachinesByKey["ReikaKalseki.ResinBaker"].Recipe;
		scale = config.getInt(RSConfig.ConfigEntries.RESIN_BAKE_AMOUNT);
		bake.Costs[0].Amount = (uint)(config.getInt(RSConfig.ConfigEntries.RESIN_BAKE_COST)*scale);
		bake.CraftedAmount = scale;
		bake.CraftTime = (uint)config.getInt(RSConfig.ConfigEntries.RESIN_BAKE_TIME);
		GenericAutoCrafterNew.mMachinesByKey["ReikaKalseki.ResinBaker"].PowerUsePerSecond = config.getInt(RSConfig.ConfigEntries.RESIN_BAKE_PPS);
    }
    
	public override ModCreateSegmentEntityResults CreateSegmentEntity(ModCreateSegmentEntityParameters parameters) {
		ModCreateSegmentEntityResults modCreateSegmentEntityResults = new ModCreateSegmentEntityResults();
		try {
			if (parameters.Cube == bomberFalcorBlockID)
				modCreateSegmentEntityResults.Entity = new ResinBomberFalcor(parameters.Segment, parameters.X, parameters.Y, parameters.Z, parameters.Cube, parameters.Flags, parameters.Value, parameters.LoadFromDisk);
		}
		catch (Exception e) {
			FUtil.log(e.ToString());
		}
		return modCreateSegmentEntityResults;
	}
    
    public static void updateOETRequiredCharge() {
    	if (!MobSpawnManager.mbSurfaceAttacksActive/* && config.getBoolean(RSConfig.ConfigEntries.OET)*/) {
    		OrbitalEnergyTransmitter.mrMaxPower = Math.Min(OrbitalEnergyTransmitter.mrMaxPower, config.getInt(RSConfig.ConfigEntries.OET_WEAK_COST));
    	}
    }
    
    public static bool deleteOET(WorldScript world, Segment segment, long x, long y, long z, ushort leType)
    {
    	if (!MobSpawnManager.mbSurfaceAttacksActive/* && config.getBoolean(RSonfig.ConfigEntries.OET)*/) {
    		return true;
    	}
        return world.BuildFromEntity(segment,x,y,z,leType);
    }
    
    public static bool doOETBlockEffects(WorldScript world, long x0, long y0, long z0, int size, int hardness) {
    	if (MobSpawnManager.mbSurfaceAttacksActive/* || !config.getBoolean(FTConfig.ConfigEntries.OET)*/) {
    	//	WorldScript.instance.Explode(x0, y0, z0, size, hardness);
    		return WorldScript.instance.SafeExplode(x0, y0, z0, size);
    	}
    	else {
    		clearSoftResin(x0, y0, z0, size);
    		killWorms(x0, y0, z0, size);
    		return true;
    	}
    }
    
    private static void killWorms(long x0, long y0, long z0, int size) {
		int count = MobManager.instance.mActiveMobs.Count;
		for (int index = 0; index < count; index++) {
			MobEntity e = MobManager.instance.mActiveMobs[index];
			if (e != null && e.mType == MobType.WormBoss && e.mnHealth > 0) {
				Vector3 vec = Vector3.zero;
				vec.x = (float) (e.mnX - x0);
				vec.y = (float) (e.mnY - y0);
				vec.z = (float) (e.mnZ - z0);
				if (vec.magnitude <= size*1.25) {
					e.TakeDamage(Int32.MaxValue); //DIE DIE DIE DIE DIE
					FloatingCombatTextManager.instance.QueueText(e.mnX, e.mnY + 4L, e.mnZ, 1.5f, "Worm Killed!", Color.magenta, 2F, 4096F);
				}
			}
		}
    }
    
    private static void clearSoftResin(long x0, long y0, long z0, int size) {
    	size = (int)(size*2.5); //up to 120
    	int sizey = size/3; //up to 40
		int maxrSq = size + 1;
		maxrSq *= maxrSq;
		HashSet<Segment> hashSet = new HashSet<Segment>();
		try {
			for (int i = -size; i <= size; i++) {
				for (int j = -size; j <= sizey; j++) {
					for (int k = -size; k <= size; k++) {
						Vector3 vector = new Vector3((float)j, (float)i, (float)k);
						int num4 = (int)vector.sqrMagnitude;
						if (num4 < maxrSq) {
							long x = x0 + (long)j;
							long y = y0 + (long)i;
							long z = z0 + (long)k;
							Segment segment = WorldScript.instance.GetSegment(x, y, z);
							if (segment.isSegmentValid()) {
								if (!segment.mbIsEmpty) {
									if (!hashSet.Contains(segment)) {
										hashSet.Add(segment);
										segment.BeginProcessing();
									}
									ushort cube = segment.GetCube(x, y, z);
									if (cube == eCubeTypes.Giger) {
										if (WorldScript.instance.BuildFromEntity(segment, x, y, z, eCubeTypes.Air, global::TerrainData.DefaultAirValue)) {
											DroppedItemData stack = ItemManager.DropNewCubeStack(eCubeTypes.Giger, 0, 1, x, y, z, Vector3.zero);
										}
									}
								}
							}
						}
					}
				}
			}
		}
		finally {
			foreach (Segment current in hashSet) {
				if (current.mbHasFluid) {
					current.FluidSleepTicks = 1;
				}
				current.EndProcessing();
			}
			WorldScript.instance.mNodeWorkerThread.KickNodeWorkerThread();
		}
    }

  }
}
