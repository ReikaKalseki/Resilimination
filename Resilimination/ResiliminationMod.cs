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
    public const string CUBE_KEY = "ReikaKalseki.Resilimination_Key";
    
    private static Config<RSConfig.ConfigEntries> config;

	public static ushort bomberFalcorBlockID;
    
    public ResiliminationMod() : base("Resilimination") {
    	config = new Config<RSConfig.ConfigEntries>(this);
    }
	
	public static Config<RSConfig.ConfigEntries> getConfig() {
		return config;
	}

    public override ModRegistrationData Register()
    {
        ModRegistrationData registrationData = new ModRegistrationData();
        
        config.load();
        
        //registrationData.RegisterEntityHandler(MOD_KEY);
        /*
        TerrainDataEntry entry;
        TerrainDataValueEntry valueEntry;
        TerrainData.GetCubeByKey(CUBE_KEY, out entry, out valueEntry);
        if (entry != null)
          ModCubeType = entry.CubeType;
         */
        
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
		
        return registrationData;
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

  }
}
