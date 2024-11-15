/*
 * Created by SharpDevelop.
 * User: Reika
 * Date: 04/11/2019
 * Time: 11:28 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.IO;    //For data read/write methods
using System.Collections;   //Working with Lists and Collections
using System.Collections.Generic;   //Working with Lists and Collections
using System.Linq;   //More advanced manipulation of lists/collections
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using UnityEngine;  //Needed for most Unity Enginer manipulations: Vectors, GameObjects, Audio, etc.
using ReikaKalseki.FortressCore;

namespace ReikaKalseki.Resilimination {
	
public class ResinBomberFalcor : FCoreMachine, PowerConsumerInterface {
		
	public static readonly float ANGLE_STEP = 7.5F;
	public static readonly int MAX_RANGE = 384;
	public static readonly float MAX_Y_RISE = 18;

	public static readonly int BLAST_RADIUS = 8; //cryo bombers are 6
	
	//private static List<Coordinate> activeFlights = new List<Coordinate>();
		
	public ResinBomberFalcor(Segment segment, long x, long y, long z, ushort cube, byte flags, ushort lValue, bool loadFromDisk) : base(eSegmentEntity.FALCOR_Bomber, SpawnableObjectEnum.FALCOR_Bomber, x, y, z, cube, flags, lValue, Vector3.zero, segment) {
		this.mbNeedsLowFrequencyUpdate = true;
		this.mbNeedsUnityUpdate = true;
		this.mForwards = SegmentCustomRenderer.GetRotationQuaternion(flags) * Vector3.forward;
		this.mForwards.Normalize();
		this.maAttachedHoppers = new StorageMachineInterface[6];
	}

	public override void SpawnGameObject() {
		this.mObjectType = SpawnableObjectEnum.FALCOR_Bomber;
		base.SpawnGameObject();
	}

	public override void DropGameObject() {
		this.mbLinkedToGO = false;
		base.DropGameObject();
	}

	public override void UnityUpdate() {
		if (!this.mbLinkedToGO) {
			if (this.mWrapper == null || !this.mWrapper.mbHasGameObject) {
				return;
			}
			if (this.mWrapper.mGameObjectList == null) {
				Debug.LogError("RA missing game object #0?");
			}
			if (this.mWrapper.mGameObjectList[0].gameObject == null) {
				Debug.LogError("RA missing game object #0 (GO)?");
			}
			this.DroneObject = this.mWrapper.mGameObjectList[0].gameObject.transform.Search("FALCORDRONE").gameObject;
			this.EngineObject = this.DroneObject.transform.Search("Auto Upgrader Drone Jets").gameObject;
			this.ThrustObject = this.DroneObject.transform.Search("_ExhaustMesh").gameObject;
			this.ThrustAudio = this.EngineObject.transform.Search("Audio").GetComponent<AudioSource>();
			this.WorkLight = this.mWrapper.mGameObjectList[0].gameObject.transform.Search("Upgrader Light").GetComponent<Light>();
			this.mVisDroneOffset = new Vector3(0f, 5f, 0f);
			this.ExplosionObject = this.mWrapper.mGameObjectList[0].gameObject.transform.Search("_Explosion").gameObject;
			this.DishObject = this.mWrapper.mGameObjectList[0].gameObject.transform.Search("Threat_Scanner_Dish").gameObject;
			this.mbLinkedToGO = true;
			this.mbBombVisualRequested = false;
		}
		if (!this.mSegment.mbOutOfView && this.mDistanceToPlayer < 32f) {
			Vector3 a = (searchQueue.getPosition()*16).asVector3();
			this.DishObject.transform.forward += (a - this.DishObject.transform.forward) * Time.deltaTime;
		}
		if (this.mbBombVisualRequested) {
			Coordinate c = bombPosition == null ? Coordinate.ZERO : bombPosition;
			Vector3 position = WorldScript.instance.mPlayerFrustrum.GetCoordsToUnity(c.xCoord, c.yCoord, c.zCoord) + WorldHelper.DefaultBlockOffset;
			this.ExplosionObject.transform.position = position;
			this.ExplosionObject.SetActive(true);
			SurvivalParticleManager.instance.CryoDust.transform.position = position;
			SurvivalParticleManager.instance.CryoDust.Emit(60);
			this.mbBombVisualRequested = false;
		}
		Vector3 a2 = this.mDroneOffset - this.mVisDroneOffset;
		this.mVisDroneOffset += a2 * Time.deltaTime * 2.5f;
		this.mVisualDroneLocation = this.mWrapper.mGameObjectList[0].gameObject.transform.position + this.mVisDroneOffset;
		this.DroneObject.transform.position = this.mVisualDroneLocation;
		Vector3 a3 = -this.mVTT;
		if (this.mFlyState == eFlyState.eTravelling) {
			a3 += new Vector3(0f, 0.75f, 0f);
		}
		this.DroneObject.transform.forward += (a3 - this.DroneObject.transform.forward) * Time.deltaTime * 1f;
		Vector3 a4;
		if (this.mFlyState == eFlyState.eTravelling) {
			a4 = this.DroneObject.transform.forward;
			a4.y = 0.1f;
		}
		else {
			a4 = Vector3.down + this.DroneObject.transform.forward * 0.1f;
		}
		this.EngineObject.transform.forward += (a4 - this.EngineObject.transform.forward) * Time.deltaTime * 0.5f;
		if (this.mState == eState.eTravellingToTarget || this.mState == eState.eReturningToReload) {
			this.ThrustObject.SetActive(true);
			if (this.mFlyState == eFlyState.eLowering) {
				if (this.ThrustAudio.pitch > 0.25f) {
					this.ThrustAudio.pitch -= Time.deltaTime;
				}
			}
			else if (this.ThrustAudio.pitch < 3f) {
				this.ThrustAudio.pitch += Time.deltaTime;
			}
			this.ThrustAudio.volume = this.ThrustAudio.pitch / 3f;
		}
		else if (this.ThrustObject.activeSelf) {
			AudioHUDManager.instance.PlayMachineAudio(AudioHUDManager.instance.DroneDock, 1f, 1f, this.mVisualDroneLocation, 16f);
			this.ThrustObject.SetActive(false);
		}
		if (this.mState == eState.eReturningToReload && this.mFlyState == eFlyState.eLowering) {
			this.WorkLight.enabled = true;
		}
		else if (this.mState == eState.eTravellingToTarget && this.mFlyState == eFlyState.eRising) {
			this.WorkLight.enabled = true;
		}
		else {
			this.WorkLight.enabled = false;
		}
	}

	private void SetNewState(eState leNewState) {
		if (leNewState != this.mState) {
			this.RequestImmediateNetworkUpdate();
			this.MarkDirtyDelayed();
		}
		this.mState = leNewState;
		this.mrStateTimer = 0f;
		this.mRequest = null;
		this.mRequestB = null;
		if (leNewState == eState.eVerifyingLOS) {
			this.mnOurRise = 2;
			this.mnTargetRise = 2;
			this.mFlyState = eFlyState.eParked;
		}
		if (leNewState == eState.eVerifyRise) {
			this.mnOurRise = 2;
			this.mnTargetRise = 2;
			this.mFlyState = eFlyState.eParked;
		}
		if (leNewState == eState.eTravellingToTarget) {
			this.TargetPaged = false;
			this.mTargetDroneOffset = this.target == null ? Vector3.zero : target.toWorld(this).asVector3();
			this.mFlyState = eFlyState.eRising;
		}
		if (leNewState == eState.eDroppingBomb) {
			this.mFlyState = eFlyState.eParked;
		}
		if (leNewState == eState.eWaitingToReload) {
			this.mFlyState = eFlyState.eParked;
		}
	}

	private void ScanForTarget() {/*
		if (this.ScanRadius >= MAX_RANGE) {
			this.ScanAngle += ANGLE_STEP;
			this.ScanRadius = 16f;
		}
		else {
			this.ScanRadius += 16f;
		}
		Vector3 vector = new Vector3(Mathf.Sin(Mathf.Deg2Rad * this.ScanAngle), 0f, Mathf.Cos(Mathf.Deg2Rad * this.ScanAngle));
		vector *= this.ScanRadius;
		long x = this.mnX + (long)vector.x;
		long y = this.mnY + (long)vector.y;
		long z = this.mnZ + (long)vector.z;
		Segment segment = base.AttemptGetSegment(x, y, z);
		if (segment == null) {
			this.ScanRadius -= 16f;
			return;
		}*/
			
		Coordinate offset = searchQueue.getPosition()*16;
		
		long x = this.mnX + offset.xCoord;
		long y = this.mnY + offset.yCoord;
		long z = this.mnZ + offset.zCoord;
		Segment s = AttemptGetSegment(x, y, z);
		
		//activeFlights.Remove(target);
		target = WorldUtil.checkSegmentForCube(s, eCubeTypes.Giger, 32767);
		//if (s == null)
		//	FUtil.log(this+" got null segment @ "+offset+" (xyz= "+(x-WorldUtil.COORD_OFFSET)+", "+(y-WorldUtil.COORD_OFFSET)+", "+(z-WorldUtil.COORD_OFFSET)+") F="+mFrustrum+", "+mFrustrum.GetSegment(x, y, z)+"/"+WorldScript.instance.GetSegment(x, y, z));
		//else
		//	FUtil.log(this+" checking segment "+(s.baseX-WorldUtil.COORD_OFFSET)+", "+(s.baseY-WorldUtil.COORD_OFFSET)+", "+(s.baseZ-WorldUtil.COORD_OFFSET)+" @ "+offset+" (found '"+target+"')");
		
		if (s == null) {
			segmentFailCount++;
			if (segmentFailCount < 10)
				return;
		}
		else {
			segmentFailCount = 0;
		}
		
		searchQueue.step();
		if (offset.xCoord > MAX_RANGE || offset.zCoord > MAX_RANGE)
			searchQueue.reset();
		
		if (target != null && !isTooClose()) {
			this.SetNewState(eState.eVerifyingLOS);
			//activeFlights.Add(target);
			
			//ARTHERPetSurvival.instance.SetARTHERReadoutText("Bombing resin @ "+target, 15, false, true);
		}
	}
	
	private bool isTooClose() {/*
		foreach (Coordinate c in activeFlights) {
			if (c.getTaxicabDistance(target) < BLAST_RADIUS*3/2)
				return true;
		}*/
		return false;
	}

	private void DoBombClear() {
		if (!this.TargetPaged) {
			
		}
		Action<long, long, long> func = (dx, dy, dz) => {
			Segment segment = base.AttemptGetSegment(dx, dy, dz);
				if (segment != null) {
					ushort cube = segment.GetCube(dx, dy, dz);
					if (cube == eCubeTypes.Giger) {
						WorldScript.instance.BuildFromEntity(segment, dx, dy, dz, eCubeTypes.Air);
						DroppedItemData stack = ItemManager.DropNewCubeStack(eCubeTypes.Giger, 0, 1, dx, dy, dz, Vector3.zero);
					}
				}
				else if (this.TargetPaged) {
					Debug.LogError("Nasty error - bomber thinks it prepaged segments, but get segment failed?");
				}
		};
		doForBlastRadius(func);
	}
	
	private void doForBlastRadius(Action<long, long, long> action) {
		Coordinate c = bombPosition == null ? Coordinate.ZERO : bombPosition;
		doForBlastRadius(c.xCoord, c.yCoord, c.zCoord, action);
	}
	
	private void doForBlastRadius(long x0, long y0, long z0, Action<long, long, long> action) {
		for (int i = -BLAST_RADIUS; i <= BLAST_RADIUS; i++) {
			for (int j = -BLAST_RADIUS; j <= BLAST_RADIUS; j++) {
				for (int k = -BLAST_RADIUS; k <= BLAST_RADIUS; k++) {
					Vector3 vector = new Vector3((float)i, (float)j, (float)k);
					if (vector.sqrMagnitude <= (float)(BLAST_RADIUS * BLAST_RADIUS)) {
						action.Invoke(x0 + (long)i, y0 + (long)j, z0 + (long)k);
					}
				}
			}
		}
	}

	public override void LowFrequencyUpdate() {
		this.mrStateTimer += LowFrequencyThread.mrPreviousUpdateTimeStep;
		switch (this.mState) {
		case eState.eDocked:
			this.mDroneOffset *= 0.9f;
			this.mbBombVisualRequested = false;
			if (WorldScript.mbIsServer) {
				this.SetNewState(eState.eWaitingToReload);
			}
			break;
		case eState.eLookingForTarget:
			this.mDroneOffset *= 0.9f;
			if (WorldScript.mbIsServer && this.mState == eState.eLookingForTarget) {
				this.ScanForTarget();
			}
			break;
		case eState.eVerifyingLOS:
			this.mDroneOffset *= 0.9f;
			if (WorldScript.mbIsServer) {
				this.VerifyLOS();
			}
			break;
		case eState.eVerifyRise:
			this.mDroneOffset *= 0.9f;
			if (WorldScript.mbIsServer) {
				this.VerifyRise();
			}
			break;
		case eState.eTravellingToTarget:
			if (WorldScript.mbIsServer && !this.TargetPaged) {
				this.TargetPaged = true;
				Action<long, long, long> func = (dx, dy, dz) => {
					if (base.AttemptGetSegment(dx, dy, dz) == null)
						this.TargetPaged = false;
				};
				doForBlastRadius(func);
			}
			if (this.mFlyState == eFlyState.eRising) {
				this.mTargetDroneOffset = new Vector3(0f, (float)this.mnOurRise, 0f);
				this.mVTT = this.mTargetDroneOffset - this.mDroneOffset;
				if (this.mDroneOffset.y < this.mTargetDroneOffset.y) {
					this.mDroneOffset.y = this.mDroneOffset.y + LowFrequencyThread.mrPreviousUpdateTimeStep * this.mrStateTimer;
				}
				else {
					this.mTargetDroneOffset = this.target == null ? Vector3.zero : target.toWorld(this).offset(0, mnTargetRise, 0).asVector3();
					this.mFlyState = eFlyState.eTravelling;
					this.mrStateTimer = 0f;
				}
			}
			if (this.mFlyState == eFlyState.eTravelling) {
				this.mVTT.x = this.mTargetDroneOffset.x - this.mDroneOffset.x;
				this.mVTT.y = this.mTargetDroneOffset.y - this.mDroneOffset.y;
				this.mVTT.z = this.mTargetDroneOffset.z - this.mDroneOffset.z;
				if (this.mVTT.sqrMagnitude < 16f) {
					this.mFlyState = eFlyState.eLowering;
				}
				float num = 2.5f + this.mrStateTimer / 2f;
				if (num > 5f) {
					num = 5f;
				}
				if (num > this.mVTT.magnitude) {
					num = this.mVTT.magnitude;
				}
				this.mVTT.Normalize();
				this.mDroneOffset += this.mVTT * LowFrequencyThread.mrPreviousUpdateTimeStep * num;
			}
			if (this.mFlyState == eFlyState.eLowering && WorldScript.mbIsServer) {
				this.SetNewState(eState.eDroppingBomb);
			}
			break;
		case eState.eDroppingBomb:
			if (this.mrStateTimer > 0f) {
				if (WorldScript.mbIsServer) {
					this.mbBombVisualRequested = true;
					this.DoBombClear();
				}
				this.SetNewState(eState.eReturningToReload);
				this.mTargetDroneOffset = Vector3.up;
				this.mFlyState = eFlyState.eRising;
				this.mVTT.x = this.mTargetDroneOffset.x - this.mDroneOffset.x;
				this.mVTT.z = this.mTargetDroneOffset.z - this.mDroneOffset.z;
				this.mVTT.y = 0f;
				if (this.mbLinkedToGO && !this.mbWellBehindPlayer && this.mCarriedItem != null) {
					FloatingCombatTextManager.instance.QueueText(this.mVisualDroneLocation + Vector3.up, 1f, PersistentSettings.GetString("Falcor_boom"), Color.green, 1f, 50f);
				}
			}
			break;
		case eState.eReturningToReload:
			if (this.mFlyState == eFlyState.eRising) {
				this.mVTT = this.mTargetDroneOffset - this.mDroneOffset;
				float num2 = this.mTargetDroneOffset.y + (float)this.mnOurRise - this.mDroneOffset.y;
				if (num2 > -0.25f || num2 < 0.25f) {
					this.mFlyState = eFlyState.eTravelling;
					this.mrStateTimer = 0f;
					this.mbBombVisualRequested = false;
				}
				else {
					this.mDroneOffset.y = this.mDroneOffset.y + LowFrequencyThread.mrPreviousUpdateTimeStep * this.mrStateTimer * num2;
				}
			}
			if (this.mFlyState == eFlyState.eTravelling) {
				this.mbBombVisualRequested = false;
				this.mVTT.x = this.mTargetDroneOffset.x - this.mDroneOffset.x;
				this.mVTT.z = this.mTargetDroneOffset.z - this.mDroneOffset.z;
				this.mVTT.y = 0f;
				if (this.mVTT.sqrMagnitude < 0.75f) {
					this.mFlyState = eFlyState.eLowering;
					this.mrStateTimer = 0f;
				}
				float num3 = 5f + this.mrStateTimer / 2f;
				if (num3 > 10f) {
					num3 = 10f;
				}
				if (num3 > this.mVTT.magnitude) {
					num3 = this.mVTT.magnitude;
				}
				this.mVTT.Normalize();
				this.mDroneOffset += this.mVTT * LowFrequencyThread.mrPreviousUpdateTimeStep * num3;
			}
			if (this.mFlyState == eFlyState.eLowering) {
				this.mVTT = -this.mForwards;
				this.mTargetDroneOffset.y = 0f;
				Vector3 zero = Vector3.zero;
				zero.x = this.mTargetDroneOffset.x - this.mDroneOffset.x;
				zero.z = this.mTargetDroneOffset.z - this.mDroneOffset.z;
				this.mDroneOffset += zero * LowFrequencyThread.mrPreviousUpdateTimeStep;
				float num4 = this.mDroneOffset.y - this.mTargetDroneOffset.y;
				this.mDroneOffset.y = this.mDroneOffset.y - num4 * LowFrequencyThread.mrPreviousUpdateTimeStep;
				if (WorldScript.mbIsServer && num4 < 0.1f) {
					this.SetNewState(eState.eWaitingToReload);
				}
			}
			break;
		case eState.eWaitingToReload:
			if (WorldScript.mbIsServer) {
				this.UpdateAttachedHoppers(false);
				if (this.mnNumValidAttachedHoppers > 0) {
					for (int l = 0; l < this.mnNumValidAttachedHoppers; l++) {
						if (this.maAttachedHoppers[l].TryExtractItems(this, ItemEntry.mEntriesByKey["ReikaKalseki.ResinBomb"].ItemID, 1)) {
							if (this.mbLinkedToGO && !this.mbWellBehindPlayer) {
								FloatingCombatTextManager.instance.QueueText(this.mVisualDroneLocation + Vector3.up, 1f, PersistentSettings.GetString("Falcor_reloading"), Color.cyan, 1f, 50f);
							}
							this.SetNewState(eState.eLookingForTarget);
							base.DropExtraSegments(this.mSegment);
							this.TargetPaged = false;
							return;
						}
					}
				}
			}
			break;
		}
	}

	private void VerifyRise() {
		if (this.mRequest == null && this.mRequestB == null) {
			this.mRequest = RaycastManager.instance.RequestRaycast(this.mnX, this.mnY, this.mnZ, Vector3.zero, this.mnX, this.mnY + (long)this.mnOurRise, this.mnZ, Vector3.zero);
			this.mRequest.mbHitStartCube = false;
			Coordinate c = target == null ? Coordinate.ZERO : target;
			this.mRequestB = RaycastManager.instance.RequestRaycast(c.xCoord, c.yCoord + (long)this.mnTargetRise, c.zCoord, Vector3.zero, c.xCoord, c.yCoord, c.zCoord, Vector3.zero);
			this.mRequestB.mbHitStartCube = false;
			RaycastManager.instance.RenderDebugRay(this.mRequest, 1f, 0f);
			RaycastManager.instance.RenderDebugRay(this.mRequestB, 1f, 0f);
			return;
		}
		if (this.mRequest.mResult == null || this.mRequestB.mResult == null) {
			return;
		}
		if (this.mRequestB.mResult.mbHitSomething && this.mRequestB.mResult.mType == eCubeTypes.Giger) {
			target = new Coordinate(this.mRequestB.mResult);
			this.SetNewState(eState.eTravellingToTarget);
			bombPosition = target;
			return;
		}
		if (this.mRequest.mResult.mbHitSomething) {
			this.SetNewState(eState.eDocked);
			return;
		}
		RaycastManager.instance.RenderDebugRay(this.mRequest, 0f, 1f);
		this.SetNewState(eState.eTravellingToTarget);
		bombPosition = target;
		this.mRequest = null;
	}

	private void VerifyLOS() {
		if (this.mRequest == null) {
			Coordinate c = target == null ? Coordinate.ZERO : target;
			this.mRequest = RaycastManager.instance.RequestRaycast(this.mnX, this.mnY + (long)this.mnOurRise, this.mnZ, Vector3.zero, c.xCoord, c.yCoord + (long)this.mnTargetRise, c.zCoord, Vector3.zero);
			this.mRequest.mbHitStartCube = false;
			return;
		}
		if (this.mRequest.mResult != null) {
			if (this.mRequest.mResult.mbHitSomething) {
				this.mnOurRise++;
				this.mnTargetRise++;
				this.mRequest = null;
				if (this.mnOurRise > MAX_Y_RISE || this.mnTargetRise > MAX_Y_RISE) {
					this.SetNewState(eState.eLookingForTarget);
				}
				return;
			}
			RaycastManager.instance.RenderDebugRay(this.mRequest, 1f, 1f);
			this.SetNewState(eState.eVerifyRise);
			this.mRequest = null;
		}
	}

	private void UpdateAttachedHoppers(bool lbInput) {
		int num = 0;
		for (int i = 0; i < 6; i++) {
			long num2 = this.mnX;
			long num3 = this.mnY;
			long num4 = this.mnZ;
			if (i == 0) {
				num2 -= 1L;
			}
			if (i == 1) {
				num2 += 1L;
			}
			if (i == 2) {
				num3 -= 1L;
			}
			if (i == 3) {
				num3 += 1L;
			}
			if (i == 4) {
				num4 -= 1L;
			}
			if (i == 5) {
				num4 += 1L;
			}
			Segment segment = base.AttemptGetSegment(num2, num3, num4);
			if (segment != null) {
				StorageMachineInterface storageMachineInterface = segment.SearchEntity(num2, num3, num4) as StorageMachineInterface;
				if (storageMachineInterface != null) {
					this.mnNumInvalidAttachedHoppers++;
					eHopperPermissions permissions = storageMachineInterface.GetPermissions();
					if (permissions != eHopperPermissions.Locked) {
						if (lbInput || permissions != eHopperPermissions.AddOnly) {
							if (!lbInput || permissions != eHopperPermissions.RemoveOnly) {
								if (!lbInput || !storageMachineInterface.IsFull()) {
									if (lbInput || !storageMachineInterface.IsEmpty()) {
										this.maAttachedHoppers[num] = storageMachineInterface;
										this.mnNumInvalidAttachedHoppers--;
										num++;
									}
								}
							}
						}
					}
				}
			}
		}
		this.mnNumValidAttachedHoppers = num;
	}

	public float GetRemainingPowerCapacity() {
		return this.mrMaxPower - this.mrCurrentPower;
	}

	public float GetMaximumDeliveryRate() {
		return this.mrMaxTransferRate;
	}

	public float GetMaxPower() {
		return this.mrMaxPower;
	}

	public bool DeliverPower(float amount) {
		if (amount > this.GetRemainingPowerCapacity()) {
			return false;
		}
		this.mrCurrentPower += amount;
		this.MarkDirtyDelayed();
		return true;
	}

	public bool WantsPowerFromEntity(SegmentEntity entity) {
		return true;
	}

	public override void Write(BinaryWriter writer) {
		try {
			writer.Write(this.mrCurrentPower);
			writer.Write(this.mTargetDroneOffset.x);
			writer.Write(this.mTargetDroneOffset.y);
			writer.Write(this.mTargetDroneOffset.z);
			writer.Write((byte)this.mnOurRise);
			writer.Write((byte)this.mnTargetRise);
			writer.Write((byte)this.mState);
			ItemFile.SerialiseItem(this.mCarriedItem, writer);
			Coordinate c = bombPosition == null ? Coordinate.ZERO : bombPosition;
			writer.Write(c.xCoord);
			writer.Write(c.yCoord);
			writer.Write(c.zCoord);
			writer.Write(this.mbBombVisualRequested);
			searchQueue.write(writer);
		}
		catch (Exception e) {
			ARTHERPetSurvival.instance.SetARTHERReadoutText("Resin bomber falcor threw exception on save: "+e.ToString());
			Debug.LogException(e);
		}
	}

	public override void Read(BinaryReader reader, int entityVersion) {
		try {
			this.mrCurrentPower = reader.ReadSingle();
			this.mTargetDroneOffset.x = reader.ReadSingle();
			this.mTargetDroneOffset.y = reader.ReadSingle();
			this.mTargetDroneOffset.z = reader.ReadSingle();
			this.mnOurRise = (int)reader.ReadByte();
			this.mnTargetRise = (int)reader.ReadByte();
			eState eState = (eState)reader.ReadByte();
			if (eState != this.mState) {
				if (!WorldScript.mbIsServer && this.mbLinkedToGO && !this.mbWellBehindPlayer && this.mState == eState.eWaitingToReload && eState == eState.eLookingForTarget && this.mCarriedItem != null) {
					FloatingCombatTextManager.instance.QueueText(this.mVisualDroneLocation + Vector3.up, 1f, this.mCarriedItem.ToString(), Color.cyan, 1f, 50f);
				}
				this.SetNewState(eState);
			}
			this.mCarriedItem = ItemFile.DeserialiseItem(reader);
			if (this.mCarriedItem == null && (this.mState == eState.eWaitingToReload || this.mState == eState.eReturningToReload)) {
				this.SetNewState(eState.eDocked);
			}
			bombPosition = new Coordinate(reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64());
			this.mbBombVisualRequested = reader.ReadBoolean();
			target = bombPosition;
			searchQueue.read(reader);
		}
		catch (Exception e) {
			ARTHERPetSurvival.instance.SetARTHERReadoutText("Resin bomber falcor threw exception on load: "+e.ToString());
			Debug.LogException(e);
		}
	}

	public override bool ShouldSave() {
		return true;
	}

	public override bool ShouldNetworkUpdate() {
		return true;
	}

	public override string GetPopupText() {
		string text = TerrainData.mEntriesByKey["ReikaKalseki.ResinBomberFalcor"].Name;
		text = text + "\n" + string.Format(PersistentSettings.GetString("UI_state_X"), this.mState.ToString());/*
		string text2 = text;
		text = string.Concat(new string[] {
			text2,
			"\n",
			PersistentSettings.GetString("Angle"),
			"  : ",
			this.ScanAngle.ToString("F2"),
			" @ ",
			this.ScanRadius.ToString("F0")
		});*/
		string text2 = text;
		text = string.Concat(new object[] {
			text2,
			"\nTR:",
			this.mnTargetRise,
			". OR:",
			this.mnOurRise
		});
		text2 = text;
		text = string.Concat(new string[] {
			text2,
			"\n",
			PersistentSettings.GetString("FlyState"),
			" : ",
			this.mFlyState.ToString()
		});
		if (mState == eState.eVerifyingLOS || mState == eState.eVerifyRise || mState == eState.eTravellingToTarget || mState == eState.eDroppingBomb)
			text += string.Format("\nTarget is {0}, {1}, {2} m away", target.xCoord-mnX, target.yCoord-mnY, target.zCoord-mnZ);
		else if (mState == eState.eDocked || mState == eState.eLookingForTarget || mState == eState.eWaitingToReload)
			text += "\nScanning position: "+(searchQueue.getPosition()*16).offset(mnX-WorldUtil.COORD_OFFSET, mnY-WorldUtil.COORD_OFFSET, mnZ-WorldUtil.COORD_OFFSET).ToString();
		return text;
	}

	private bool mbLinkedToGO;

	private Vector3 mForwards;

	private StorageMachineInterface[] maAttachedHoppers;

	public int mnNumValidAttachedHoppers;

	public int mnNumInvalidAttachedHoppers;

	private GameObject DroneObject;

	private GameObject EngineObject;

	private GameObject ThrustObject;

	private AudioSource ThrustAudio;

	private GameObject ExplosionObject;

	private GameObject DishObject;

	private Light WorkLight;

	private Vector3 mVisualDroneLocation;

	public eState mState;

	private float mrStateTimer;

	private ItemBase mCarriedItem;

	private Coordinate target;

	//private float ScanAngle;

	//private float ScanRadius = MAX_RANGE;
	
	private readonly FalcorSearchQueue searchQueue = new FalcorSearchQueue(MAX_RANGE/16, -1, 2);

	private bool mbBombVisualRequested;

	private Coordinate bombPosition;

	private bool TargetPaged;

	private eFlyState mFlyState;

	private RaycastRequest mRequest;

	private RaycastRequest mRequestB;

	public int mnOurRise = -1;

	public int mnTargetRise = -1;

	public Vector3 mVisDroneOffset;

	public Vector3 mDroneOffset;

	public Vector3 mVTT;

	public Vector3 mTargetDroneOffset;

	public float mrCurrentPower;

	public float mrMaxPower;

	public float mrMaxTransferRate = 100f;
	
	private int segmentFailCount;

	public enum eState {
		eDocked,
		eLookingForTarget,
		eVerifyingLOS,
		eVerifyRise,
		eTravellingToTarget,
		eDroppingBomb,
		eReturningToReload,
		eWaitingToReload
	}

	private enum eFlyState {
		eRising,
		eTravelling,
		eLowering,
		eParked
	}
}
	
}
