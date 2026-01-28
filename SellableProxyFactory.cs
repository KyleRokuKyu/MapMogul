using System.Collections.Generic;
using UnityEngine;

namespace MapMogul
{
	public static class SellableProxyFactory
	{
		private static readonly Dictionary<string, OrePiece> _proxyByTarget = new();

		public static OrePiece GetOrCreateProxy(OrePiece template, BaseSellableItem target, BuildingObject crateParent = null)
		{
			if (target == null) return null;

			string name = $"Proxy_{target.name}";
			if (crateParent)
				name += $"_{crateParent.name}";

			if (_proxyByTarget.TryGetValue(name, out var existing) && existing != null)
				return existing;

			var go = Object.Instantiate(template.gameObject);
			go.name = name;

			Object.DontDestroyOnLoad(go);
			go.SetActive(false); // important: don’t let random components run

			var proxy = go.GetComponent<SellableProxy>() ?? go.AddComponent<SellableProxy>();
			proxy.TargetPrefab = target;
			proxy.CrateParent = crateParent;

			var orePiece = go.GetComponent<OrePiece>();
			_proxyByTarget[name] = orePiece;
			return orePiece;
		}
	}
}
