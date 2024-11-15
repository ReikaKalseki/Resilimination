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
	
	public class FalcorSearchQueue {
		
		private readonly int yStart;
		private readonly int yEnd;
		private readonly int maxRange;
		
		private int stepX = 1;
		private int stepZ;
		
		private int offsetX;
		private int offsetY;
		private int offsetZ;
		
		private int turnCount;
		private int stepsUntilTurn;
		
		public FalcorSearchQueue(int r, int minY, int maxY) {
			maxRange = r;
			yStart = minY;
			yEnd = maxY;
			reset();
		}
		
		public void step() {
			long oldX = offsetX;
			long oldY = offsetY;
			long oldZ = offsetZ;
			if (offsetY == yEnd) {
				offsetY = yStart;
				
				if (stepX == 1) {
					offsetX++;
					if (shouldTurn()) {
						stepX = 0;
						stepZ = 1;
					}
				}
				else if (stepZ == 1) {
					offsetZ++;
					if (shouldTurn()) {
						stepX = -1;
						stepZ = 0;
					}
				}
				else if (stepX == -1) {
					offsetX--;
					if (shouldTurn()) {
						stepZ = -1;
						stepX = 0;
					}
				}
				else if (stepZ == -1) {
					offsetZ--;
					if (shouldTurn()) {
						stepX = 1;
						stepZ = 0;
					}
				}
			}
			else {
				offsetY++;
			}
			if (offsetX > maxRange || offsetZ > maxRange)
				reset();
			//FUtil.log("Stepped falcor search from "+oldX+", "+oldY+", "+oldZ+" to "+offsetX+", "+offsetY+", "+offsetZ+", V="+stepX+","+stepZ);
		}
		
		private bool shouldTurn() {
			stepsUntilTurn--;
			if (stepsUntilTurn <= 0) {
				turnCount++;
				stepsUntilTurn = 1+turnCount/2; //https://i.imgur.com/YnFJalp.png
				return true;
			}
			else {
				return false;
			}
		}
		
		public Coordinate getPosition() {
			return new Coordinate(offsetX, offsetY, offsetZ);
		}
		
		public void reset() {
			offsetX = 0;
			offsetY = yStart;
			offsetZ = 0;
			stepZ = 0;
			stepX = 1;
			stepsUntilTurn = 1;
			turnCount = 0;
		}
		
		public void write(BinaryWriter writer) {
			writer.Write(offsetX);
			writer.Write(offsetY);
			writer.Write(offsetZ);
			writer.Write(stepX);
			writer.Write(stepZ);
			writer.Write(stepsUntilTurn);
			writer.Write(turnCount);
		}
		
		public void read(BinaryReader reader) {
			offsetX = reader.ReadInt32();
			offsetY = reader.ReadInt32();
			offsetZ = reader.ReadInt32();
			stepX = reader.ReadInt32();
			stepZ = reader.ReadInt32();
			stepsUntilTurn = reader.ReadInt32();
			turnCount = reader.ReadInt32();
			if (stepX == 0 && stepZ == 0) {
				FUtil.log("Falcor search Vel Reset!");
				reset();
			}
		}
		
	}
	
}
