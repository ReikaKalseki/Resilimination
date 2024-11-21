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
	
	
	[HarmonyPatch(typeof(MobSpawnManager))]
	[HarmonyPatch("UpdateBombardment")]
	public static class OrbitalStrikeIntercept {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				FileLog.Log("Running patch "+MethodBase.GetCurrentMethod().DeclaringType);
				for (int i = 0; i < codes.Count; i++) {
					CodeInstruction ci = codes[i];
					if (ci.opcode == OpCodes.Callvirt && ((MethodInfo)ci.operand).Name == "Explode") {
						ci.opcode = OpCodes.Call;
						ci.operand = InstructionHandlers.convertMethodOperand(typeof(ResiliminationMod), "doOETBlockEffects", false, new Type[]{typeof(WorldScript), typeof(long), typeof(long), typeof(long), typeof(int), typeof(int)});
						break;
					}
				}
				FileLog.Log("Done patch "+MethodBase.GetCurrentMethod().DeclaringType);
			}
			catch (Exception e) {
				FileLog.Log("Caught exception when running patch "+MethodBase.GetCurrentMethod().DeclaringType+"!");
				FileLog.Log(e.Message);
				FileLog.Log(e.StackTrace);
				FileLog.Log(e.ToString());
			}
			return codes.AsEnumerable();
		}
	}
	
	[HarmonyPatch(typeof(OrbitalEnergyTransmitter))]
	[HarmonyPatch("LowFrequencyUpdate")]
	public static class OETChargeHook {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				FileLog.Log("Running patch "+MethodBase.GetCurrentMethod().DeclaringType);
				CodeInstruction call = InstructionHandlers.createMethodCall(typeof(ResiliminationMod), "updateOETRequiredCharge", false, new Type[0]);
				codes.Insert(0, call);
				FileLog.Log("Done patch "+MethodBase.GetCurrentMethod().DeclaringType);
			}
			catch (Exception e) {
				FileLog.Log("Caught exception when running patch "+MethodBase.GetCurrentMethod().DeclaringType+"!");
				FileLog.Log(e.Message);
				FileLog.Log(e.StackTrace);
				FileLog.Log(e.ToString());
			}
			return codes.AsEnumerable();
		}
	}
	
	[HarmonyPatch(typeof(OrbitalStrikeController))]
	[HarmonyPatch("LowFrequencyUpdate")]
	public static class OETCallHook {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				FileLog.Log("Running patch "+MethodBase.GetCurrentMethod().DeclaringType);
				int ret = InstructionHandlers.getLastOpcodeBefore(codes, codes.Count, OpCodes.Ret);
				int call = InstructionHandlers.getLastOpcodeBefore(codes, ret, OpCodes.Callvirt);
				codes[call].opcode = OpCodes.Call;
				codes[call].operand = InstructionHandlers.convertMethodOperand(typeof(ResiliminationMod), "deleteOET", false, new Type[]{typeof(WorldScript), typeof(Segment), typeof(long), typeof(long), typeof(long), typeof(ushort)});
				FileLog.Log("Done patch "+MethodBase.GetCurrentMethod().DeclaringType);
			}
			catch (Exception e) {
				FileLog.Log("Caught exception when running patch "+MethodBase.GetCurrentMethod().DeclaringType+"!");
				FileLog.Log(e.Message);
				FileLog.Log(e.StackTrace);
				FileLog.Log(e.ToString());
			}
			return codes.AsEnumerable();
		}
	}
	
}
