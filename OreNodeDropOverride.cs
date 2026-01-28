using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;

namespace MapMogul
{
	public static class OreNodeDropOverride
	{
		// Caches for speed (optional but nice)
		private static Type _weightedDropType;
		private static System.Reflection.FieldInfo _possibleDropsField;
		private static System.Reflection.FieldInfo _orePrefabField;
		private static System.Reflection.FieldInfo _weightField;

		private static void EnsureCached()
		{
			if (_weightedDropType != null) return;

			var oreNodeType = typeof(OreNode);

			// private nested class OreNode+WeightedNodeDrop
			_weightedDropType = AccessTools.Inner(oreNodeType, "WeightedNodeDrop");
			if (_weightedDropType == null)
				throw new Exception("Could not find OreNode.WeightedNodeDrop (private nested type).");

			_possibleDropsField = AccessTools.Field(oreNodeType, "_possibleDrops");
			if (_possibleDropsField == null)
				throw new Exception("Could not find OreNode._possibleDrops field.");

			_orePrefabField = AccessTools.Field(_weightedDropType, "OrePrefab");
			_weightField = AccessTools.Field(_weightedDropType, "Weight");
			if (_orePrefabField == null || _weightField == null)
				throw new Exception("Could not find fields on WeightedNodeDrop (OrePrefab/Weight).");
		}

		/// <summary>
		/// Replaces OreNode._possibleDrops with your own weighted list.
		/// </summary>
		public static void SetPossibleDrops(OreNode node, List<WeightedOreChance> drops)
		{
			if (node == null) return;

			EnsureCached();

			// Create List<WeightedNodeDrop>
			var listType = typeof(List<>).MakeGenericType(_weightedDropType);
			var list = (IList)Activator.CreateInstance(listType);

			foreach (WeightedOreChance ore in drops)
			{
				if (ore.OrePrefab == null) continue;

				// Create WeightedNodeDrop instance (private type, but Activator can still create it)
				var entry = Activator.CreateInstance(_weightedDropType, nonPublic: true);

				_orePrefabField.SetValue(entry, ore.OrePrefab);
				_weightField.SetValue(entry, ore.Weight);

				list.Add(entry);
			}

			_possibleDropsField.SetValue(node, list);
		}
	}
}