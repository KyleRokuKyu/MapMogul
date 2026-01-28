using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace MapMogul
{
	static class AutoMinerResourceDefinitionOverride
	{
		private static readonly AccessTools.FieldRef<AutoMinerResourceDefinition, List<WeightedOreChance>>
			_possibleOrePrefabsRef =
				AccessTools.FieldRefAccess<AutoMinerResourceDefinition, List<WeightedOreChance>>(
					"_possibleOrePrefabs"
				);

		public static void SetPossibleOres(
			AutoMinerResourceDefinition def,
			List<WeightedOreChance> ores)
		{
			_possibleOrePrefabsRef(def) = ores;
		}
	}
}