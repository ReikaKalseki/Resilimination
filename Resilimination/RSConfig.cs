﻿using System;

using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Xml;
using ReikaKalseki.FortressCore;

namespace ReikaKalseki.Resilimination
{
	public class RSConfig
	{		
		public enum ConfigEntries {
			[ConfigEntry("Resin Bomb Liquid Resin Cost", typeof(int), 8, 1, 128, 0)]RESIN_COST,
			[ConfigEntry("Soft Resin Baking Ratio", typeof(int), 1, 1, 100, 0)]RESIN_BAKE_COST,
			[ConfigEntry("Soft Resin Baking Amount Per Cycle", typeof(int), 10, 1, 100, 0)]RESIN_BAKE_AMOUNT,
			[ConfigEntry("Soft Resin Baking Time (Seconds)", typeof(float), 5, 1, 600, 0)]RESIN_BAKE_TIME,
			[ConfigEntry("Soft Resin Baking PPS", typeof(int), 50, 1, 250, 0)]RESIN_BAKE_PPS,
		}
	}
}
