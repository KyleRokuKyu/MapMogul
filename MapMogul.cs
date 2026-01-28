using BepInEx;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using HarmonyLib;
using System.IO;
using System.Linq;
using System;

namespace MapMogul
{
	[BepInPlugin("com.kylerokukyu.mapmogul", "MapMogul", "0.1.0")]
	public class MapMogul : BaseUnityPlugin
	{
		protected struct RigidbodyState
		{
			public Rigidbody rigidbody;
			public bool isKinematic;
		}

		protected struct OreWeight
		{
			public BaseSellableItem item;
			public float weight;
			public BuildingObject crateParent;
		}

		private List<string> namesToKill = new() { "rock", "lush", "invisible", "terrain", "stalac", "cwall", "junk", "singlesupport", "supportlog", "supportplank", "supportgantry", "plane",
													"bridge", "pebble", "cliff", "grass", "shroom", "sign", "minecart", "dust", "cable", "vine", "plank", "scaffold", "outdoor test", "light",
													"chest", "lamp", "cave", "debug", "dynamite", "ore", "plane", "test", "lantern", "elevatormodule", "desposiotr_base", "depositor_shaft" };

		public static string worldName = "SkyBlock";
		public List<BuildingPlacementNode> clonableNodes = new();
		private static List<BuildingPlacementNode> spawnedNode = new();
		private List<RigidbodyState> rigidbodyStates = new();
		private PlayerController player;
		private List<GameObject> oreNodeMeshes = new();
		private List<GameObject> loadedAssets = new();
		private GameObject ghostPrefab = null;
		OrePiece templateOre = null;
		Transform pickaxe = null;
		static GameObject autoMinerNodeGameObject = null;
		static GameObject oreNodeGameObject = null;

		void Awake ()
		{
			new Harmony("com.kylerokukyu.mapmogul").PatchAll();
		}

		private void OnEnable ()
		{
			StartCoroutine(WaitForGameplay());
		}

		private void LoadResources ()
		{
			string bundleFolderPath = Path.Combine(
									Paths.PluginPath,   // BepInEx/plugins
									"MapMogul",
									"Resources",
									"mapmogul.assets");
			// Check if the directory exists
			if (!Directory.Exists(bundleFolderPath))
			{
				Debug.LogError("Bundle folder not found at: " + bundleFolderPath);
				return;
			}

			// Get all file paths in the directory (you may need to filter for specific extensions if necessary)
			string[] filePaths = Directory.GetFiles(bundleFolderPath);

			foreach (string path in filePaths)
			{
				// Skip manifest files or meta files if they exist in the directory listing
				if (path.Contains(".manifest") || path.Contains(".meta")) continue;

				AssetBundle bundle = AssetBundle.LoadFromFile(path);

				if (bundle == null)
				{
					Debug.LogError("Failed to load AssetBundle from: " + path);
					continue;
				}

				GameObject[] loadedObjects = bundle.LoadAllAssets<GameObject>();

				foreach (GameObject g in loadedObjects)
				{
					foreach (Transform t in g.GetComponentsInChildren<Transform>())
					{
						if (t.gameObject.name.ToLower().Trim().StartsWith("model_autominernode"))
						{
							if (autoMinerNodeGameObject != null) continue;
							autoMinerNodeGameObject = t.gameObject;
							autoMinerNodeGameObject.transform.position = Vector3.up * -5000;
							autoMinerNodeGameObject.hideFlags = HideFlags.HideAndDontSave;
							DontDestroyOnLoad(autoMinerNodeGameObject);
						}
						if (t.gameObject.name.ToLower().Trim().StartsWith("model_orenode"))
						{
							if (oreNodeGameObject != null) continue;
							oreNodeGameObject = t.gameObject;
							oreNodeGameObject.transform.position = Vector3.up * -5000;
							oreNodeGameObject.hideFlags = HideFlags.HideAndDontSave;
							DontDestroyOnLoad(oreNodeGameObject);
						}
					}
				}
			}
		}

		IEnumerator WaitForGameplay ()
		{
			clonableNodes.Clear();

			yield return null;
			yield return null;
			SetupMenu();
			Logger.LogInfo("Starting Scene Wait Loop");
			while (SceneManager.GetActiveScene() == null)
			{
				yield return null;
			}
			while (SceneManager.GetActiveScene().name.ToLower() != "gameplay")
			{
				yield return null;
			}

			yield return StartCoroutine(SetupGame());

			StartCoroutine(WaitForMenu());
		}

		IEnumerator WaitForMenu ()
		{
			while (SceneManager.GetActiveScene().name.ToLower() == "gameplay")
			{
				yield return null;
			}
			StartCoroutine(WaitForGameplay());
		}

		private void SetupMenu ()
		{
			Destroy(GameObject.Find("MenuEnvironment"));
			try
			{
				Destroy(GameObject.Find("cwall9"));
			}
			catch
			{

			}
			SetupEnvironment();

			Transform menuUI = null;
			foreach (Image c in FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (c.gameObject.name == "MainMenu")
				{
					menuUI = c.transform;
				}
			}
			GameObject flatLabGo = new GameObject("Map Mogul Logo", typeof(RectTransform), typeof(TextMeshProUGUI));
			TextMeshProUGUI logoText = flatLabGo.GetComponent<TextMeshProUGUI>();
			logoText.text = "<b>Map Mogul!<b>";
			logoText.alignment = TextAlignmentOptions.Center;
			logoText.color = new Color(0.7765f, 0.6196f, 0.2588f);
			logoText.autoSizeTextContainer = true;
			RectTransform rectTransform = flatLabGo.GetComponent<RectTransform>();
			rectTransform.SetParent(menuUI);
			rectTransform.anchorMin = new Vector2(0.33f, 0.88f);
			rectTransform.anchorMax = new Vector2(0.66f, 0.97f);
			rectTransform.offsetMin = Vector2.zero;
			rectTransform.offsetMax = Vector2.zero;
			rectTransform.localScale = Vector3.one;

			Bobble bobble = flatLabGo.AddComponent<Bobble>();
			bobble.target = flatLabGo.transform;
			bobble.bounce = new Vector3(0, 15, 0);
			bobble.bounceTime = 3.2345f;
			bobble.rotate = new Vector3(0, 0, 1);
			bobble.rotateTime = 1.11756f;
			bobble.scale = new Vector3(.2f, .2f, .2f);
			bobble.scaleTime = 2.2524524f;
		}

		private IEnumerator SetupGame ()
		{
			player = FindFirstObjectByType<PlayerController>();
			CharacterController c = FindFirstObjectByType<CharacterController>();
			player.enabled = false;
			c.enabled = false;
			Camera cam = Camera.main;
			CameraFadeUtil.FadeToBlack(cam);
			cam.useOcclusionCulling = false;

			foreach (Rigidbody r in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
			{
				RigidbodyState state = new()
				{
					rigidbody = r,
					isKinematic = r.isKinematic
				};
				r.isKinematic = true;
			}

			foreach (AutoMiner a in FindObjectsByType<AutoMiner>(FindObjectsSortMode.None))
			{
				a.ResourceDefinition = null;
				a.enabled = false;
				a.gameObject.SetActive(false);
			}

			foreach (RapidAutoMiner a in FindObjectsByType<RapidAutoMiner>(FindObjectsSortMode.None))
			{
				a.ResourceDefinition = null;
				a.enabled = false;
				a.gameObject.SetActive(false);
			}

			yield return null;
			KillStuff();
			SetupNodes();
			//SetupEnvironment();
			yield return null;

			foreach (AutoMiner a in FindObjectsByType<AutoMiner>(FindObjectsSortMode.None))
			{
				a.gameObject.SetActive(true);
				a.enabled = true;
				a.SendMessage("OnEnable");
			}

			foreach (RapidAutoMiner a in FindObjectsByType<RapidAutoMiner>(FindObjectsSortMode.None))
			{
				a.gameObject.SetActive(true);
				a.enabled = true;
				a.SendMessage("OnEnable");
			}

			foreach (RigidbodyState r in rigidbodyStates)
			{
				r.rigidbody.isKinematic = r.isKinematic;
			}

			CameraFadeUtil.FadeFromBlack();
			c.enabled = true;
			player.enabled = true;
		}

		private void KillStuff ()
		{
			ghostPrefab = Instantiate(FindFirstObjectByType<BuildingPlacementNode>().GhostPrefab);
			ghostPrefab.transform.position = Vector3.up * -1000;
			ghostPrefab.SetActive(false);

			List<Transform> checkedTransforms = new();
			Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (Transform t in allTransforms)
			{
				if (t == null) continue;
				if (checkedTransforms.Contains(t.root)) continue;

				MonoBehaviour[] scripts = t.root.GetComponentsInChildren<MonoBehaviour>();

				bool dontKill = false;
				foreach (MonoBehaviour script in scripts)
				{
					var namespaceName = script.GetType().Namespace;

					if (namespaceName == null || (!namespaceName.Contains("UnityEngine") && !namespaceName.Contains("UnityEditor")))
					{
						dontKill = true;
						break;
					}
				}
				if (!dontKill)
				{
					if (t.GetComponentInChildren<MeshFilter>())
						Destroy(t.root.gameObject);
				}
			}

			foreach (OreNode o in FindObjectsByType<OreNode>(FindObjectsSortMode.None))
			{
				Destroy(o.gameObject);
			}
			foreach (GameObject g in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
			{
				if (g.hideFlags != HideFlags.None || g.scene.buildIndex == -1) continue;

				foreach (string s in namesToKill)
				{
					if (g.name.ToLower().Contains(s.ToLower()))
					{
						Destroy(g);
					}
				}
			};
			BaseSellableItem[] sellables = FindObjectsByType<BaseSellableItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (BaseSellableItem s  in sellables)
			{
				if (s.gameObject.name.ToLower().Contains("pickaxe"))
				{
					pickaxe = s.transform;
					continue;
				}
				Destroy (s.gameObject);
			}
		}

		private void SetupNodes ()
		{
			GameObject ghostPrefab = null;
			BuildingPlacementNode[] nodes = FindObjectsByType<BuildingPlacementNode>(FindObjectsSortMode.None);
			foreach (BuildingPlacementNode b in nodes)
			{
				if (!ghostPrefab)
				{
					ghostPrefab = Instantiate(b.GhostPrefab);
				}
				foreach (Transform t in b.GetComponentsInChildren<Transform>())
				{
					if (t.name.ToLower().Contains("ore"))
					{
						bool found = false;
						foreach (GameObject g in oreNodeMeshes)
						{
							if (g.name.ToLower() == t.name.ToLower())
							{
								found = true;
								break;
							}
						}
						if (!found)
						{
							t.parent = null;
							t.transform.position = Vector3.up * -1000;
							t.gameObject.SetActive(false);
							t.gameObject.isStatic = false;
							oreNodeMeshes.Add(t.gameObject);
						}
					}
				}
				Destroy (b.gameObject);
			}
			ghostPrefab.transform.position = Vector3.up * -1000;
			ghostPrefab.SetActive(false);
			BuildingPlacementNode.All.Clear();

			LoadWorld();
		}

		private void LoadWorld()
		{
			string fileName = "target.map";
			string filePath = Path.Combine(Paths.PluginPath,   // BepInEx/plugins
									"MapMogul",
									fileName);
			string targetMap = File.ReadAllText(filePath);

			string bundleFolderPath = Path.Combine(Paths.PluginPath, targetMap);

			// Check if the directory exists
			if (!Directory.Exists(bundleFolderPath))
			{
				Debug.LogError("Bundle folder not found at: " + bundleFolderPath);
				return;
			}

			// Get all file paths in the directory (you may need to filter for specific extensions if necessary)
			string[] filePaths = Directory.GetFiles(bundleFolderPath);

			foreach (string path in filePaths)
			{
				// Skip manifest files or meta files if they exist in the directory listing
				if (path.Contains(".manifest") || path.Contains(".meta")) continue;

				AssetBundle bundle = AssetBundle.LoadFromFile(path);

				if (bundle == null)
				{
					Debug.LogError("Failed to load AssetBundle from: " + path);
					continue;
				}

				LoadAndSpawnAssetsFromBundle(bundle);
			}

			foreach (GameObject g in loadedAssets)
			{
				foreach (Transform t in g.GetComponentsInChildren<Transform>())
				{
					if (t.gameObject.name.ToLower().Trim() == "computer")
					{
						GameObject computer = FindFirstObjectByType<ComputerTerminal>().transform.root.gameObject;
						foreach (Transform ct in computer.GetComponentsInChildren<Transform>())
						{
							ct.gameObject.isStatic = false;
						}

						FindFirstObjectByType<ComputerTerminal>().transform.root.position = t.gameObject.transform.position;
						FindFirstObjectByType<ComputerTerminal>().transform.root.rotation = t.gameObject.transform.rotation;
					}
					if (t.gameObject.name.ToLower().Trim() == "entryelevator")
					{
						FindFirstObjectByType<ShopSpawnPoint>().transform.root.position = t.gameObject.transform.position;
						FindFirstObjectByType<ShopSpawnPoint>().transform.root.rotation =	t.gameObject.transform.rotation;
					}
					if (t.gameObject.name.ToLower().Trim() == "depositbox")
					{
						foreach (MeshFilter mf in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
						{
							if (mf.gameObject.name == "Depositor_T1")
							{
								Destroy(mf.GetComponent<MeshRenderer>());
								Destroy(mf);
								break;
							}
						}
						// Depositor_T1
						FindFirstObjectByType<DepositBox>().transform.root.position = t.gameObject.transform.position;
						FindFirstObjectByType<DepositBox>().transform.root.rotation = t.gameObject.transform.rotation;
					}
					if (t.gameObject.name.ToLower().Trim().StartsWith("buildablenode"))
					{
						CreateAutoMiner(t.gameObject.name, t);
					}
					if (t.gameObject.name.ToLower().Trim().StartsWith("orenode"))
					{
						CreateOreNode(t.gameObject.name, t);
					}
					if (pickaxe && t.gameObject.name.ToLower().Trim().StartsWith("pickaxe"))
					{
						pickaxe.position = t.position;
						pickaxe.rotation = t.rotation;
					}
				}
			}
		}

		public void CreateAutoMiner(string name, Transform trans)
		{
			GameObject nodeGo = new(name, typeof(BuildingPlacementNode));
			nodeGo.transform.position = trans.position;
			nodeGo.transform.rotation = trans.rotation;
			trans.parent = nodeGo.transform;

			/*
			GameObject chosenNodeMesh = oreNodeMeshes[0];
			GameObject nodeVisual = new(chosenNodeMesh.name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
			nodeVisual.transform.parent = nodeGo.transform;
			nodeVisual.transform.localPosition = Vector3.zero;
			nodeVisual.transform.localEulerAngles = Vector3.zero;
			nodeVisual.GetComponent<MeshFilter>().mesh = chosenNodeMesh.GetComponent<MeshFilter>().sharedMesh;
			nodeVisual.GetComponent<MeshRenderer>().materials = chosenNodeMesh.GetComponent<MeshRenderer>().materials;
			nodeVisual.GetComponent<MeshCollider>().sharedMesh = chosenNodeMesh.GetComponent<MeshCollider>().sharedMesh;
			nodeVisual.SetActive(true);
			*/

			bool visualIncluded = false;
			foreach (Transform t in nodeGo.GetComponentsInChildren<Transform>())
			{
				if (t.name.ToLower().Contains("Visual") || t.GetComponent<MeshRenderer>())
				{
					visualIncluded = true;
					break;
				}
			}
			if (!visualIncluded)
			{
				GameObject nodeVisual = Instantiate(autoMinerNodeGameObject);
				nodeVisual.name = "Visual";
				nodeVisual.transform.parent = nodeGo.transform;
				nodeVisual.transform.localPosition = Vector3.up * 0.25f;
				nodeVisual.transform.localEulerAngles = Vector3.zero;
				nodeVisual.transform.localScale = new Vector3(3, 0.5f, 3);
				nodeVisual.hideFlags = HideFlags.None;
				SceneManager.MoveGameObjectToScene(nodeVisual, SceneManager.GetActiveScene());
				if (nodeVisual.GetComponent<Collider>())
				{
					Destroy(nodeVisual.GetComponent<Collider>());
				}
			}

			BuildingPlacementNode testNode = nodeGo.GetComponent<BuildingPlacementNode>();
			testNode.RequirementType = PlacementNodeRequirement.AutoMiner;
			testNode.GhostPrefab = Instantiate(ghostPrefab, nodeGo.transform);
			testNode.GhostPrefab.transform.localPosition = Vector3.zero;
			testNode.GhostPrefab.transform.localEulerAngles = Vector3.zero;

			if (templateOre == null)
			{
				foreach (OrePiece o in Resources.FindObjectsOfTypeAll<OrePiece>())
				{
					if (o.gameObject.scene.buildIndex != -1) continue;
					if (o.name.ToLower().Contains("ore"))
					{
						templateOre = o;
						break;
					}
				}
			}
			List<OreWeight> possibleOres = new();
			string[] nameSplit = nodeGo.name.Split('|');
			for (int i = 1; i < nameSplit.Length; i += 2)
			{
				OreWeight tempWeight = new();
				if (nameSplit[i].ToLower().Trim().StartsWith("buildingcrate"))
				{
					foreach (BuildingObject b in Resources.FindObjectsOfTypeAll<BuildingObject>())
					{
						if (b.gameObject.scene.buildIndex != -1) continue;
						if (b.name.ToLower().Trim() == nameSplit[i].Split('_')[1].ToLower().Trim())
						{
							tempWeight.crateParent = b;
							break;
						}
					}
				}
				foreach (BaseSellableItem b in Resources.FindObjectsOfTypeAll<BaseSellableItem>())
				{
					if (b.gameObject.scene.buildIndex != -1) continue;
					if (nameSplit[i].ToLower().Trim().StartsWith("buildingcrate"))
					{
						if (b.name.ToLower().Trim() == nameSplit[i].Split('_')[0].ToLower().Trim())
						{
							tempWeight.item = b;
							tempWeight.weight = float.Parse(nameSplit[i + 1].Split(' ')[0]);
							break;
						}
					}
					else
					{
						if (b.name.ToLower().Trim() == nameSplit[i].ToLower().Trim())
						{

							tempWeight.item = b;
							tempWeight.weight = float.Parse(nameSplit[i + 1].Split(' ')[0]);
							break;
						}
					}
				}
				possibleOres.Add(tempWeight);
			}

			List<WeightedOreChance> oreChanges = new();
			foreach (OreWeight weight in possibleOres)
			{
				oreChanges.Add(new()
				{
					OrePrefab = SellableProxyFactory.GetOrCreateProxy(templateOre, weight.item, weight.crateParent),
					Weight = weight.weight
				});
			}

			AutoMinerResourceDefinition autoMinerRD = ScriptableObject.CreateInstance<AutoMinerResourceDefinition>();
			AutoMinerResourceDefinitionOverride.SetPossibleOres(autoMinerRD, oreChanges);
			testNode.AutoMinerResourceDefinition = autoMinerRD;
		}

		public void CreateOreNode(string name, Transform trans)
		{
			GameObject nodeGo = new(name, typeof(OreNode), typeof(BoxCollider));
			nodeGo.transform.position = trans.position;
			nodeGo.transform.rotation = trans.rotation;
			trans.parent = nodeGo.transform;

			/*
			GameObject chosenNodeMesh = oreNodeMeshes[0];
			GameObject nodeVisual = new(chosenNodeMesh.name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
			nodeVisual.transform.parent = nodeGo.transform;
			nodeVisual.transform.localPosition = Vector3.zero;
			nodeVisual.transform.localEulerAngles = Vector3.zero;
			nodeVisual.GetComponent<MeshFilter>().mesh = chosenNodeMesh.GetComponent<MeshFilter>().sharedMesh;
			nodeVisual.GetComponent<MeshRenderer>().materials = chosenNodeMesh.GetComponent<MeshRenderer>().materials;
			nodeVisual.GetComponent<MeshCollider>().sharedMesh = chosenNodeMesh.GetComponent<MeshCollider>().sharedMesh;
			nodeVisual.SetActive(true);
			*/

			bool visualIncluded = false;
			foreach (Transform t in nodeGo.GetComponentsInChildren<Transform>())
			{
				if (t.name.ToLower().Contains("Visual") || t.GetComponent<MeshRenderer>())
				{
					visualIncluded = true;
					break;
				}
			}
			if (!visualIncluded)
			{
				GameObject nodeVisual = Instantiate(oreNodeGameObject);
				nodeVisual.name = "Visual";
				nodeVisual.transform.parent = nodeGo.transform;
				nodeVisual.transform.localPosition = Vector3.up * 0.25f;
				nodeVisual.transform.localEulerAngles = Vector3.zero;
				nodeVisual.transform.localScale = new Vector3(3, 0.5f, 3);
				nodeVisual.hideFlags = HideFlags.None;
				SceneManager.MoveGameObjectToScene(nodeVisual, SceneManager.GetActiveScene());
				if (nodeVisual.GetComponent<Collider>())
				{
					Destroy(nodeVisual.GetComponent<Collider>());
				}
			}

			if (templateOre == null)
			{
				foreach (OrePiece o in Resources.FindObjectsOfTypeAll<OrePiece>())
				{
					if (o.gameObject.scene.buildIndex != -1) continue;
					if (o.name.ToLower().Contains("ore"))
					{
						templateOre = o;
						break;
					}
				}
			}
			List<OreWeight> possibleOres = new();
			string[] nameSplit = nodeGo.name.Split('|');
			for (int i = 1; i < nameSplit.Length; i += 2)
			{
				OreWeight tempWeight = new();
				if (nameSplit[i].ToLower().Trim().StartsWith("buildingcrate"))
				{
					foreach (BuildingObject b in Resources.FindObjectsOfTypeAll<BuildingObject>())
					{
						if (b.gameObject.scene.buildIndex != -1) continue;
						if (b.name.ToLower().Trim() == nameSplit[i].Split('_')[1].ToLower().Trim())
						{
							tempWeight.crateParent = b;
							break;
						}
					}
				}
				foreach (BaseSellableItem b in Resources.FindObjectsOfTypeAll<BaseSellableItem>())
				{
					if (b.gameObject.scene.buildIndex != -1) continue;
					if (nameSplit[i].ToLower().Trim().StartsWith("buildingcrate"))
					{
						if (b.name.ToLower().Trim() == nameSplit[i].Split('_')[0].ToLower().Trim())
						{
							tempWeight.item = b;
							tempWeight.weight = float.Parse(nameSplit[i + 1].Split(' ')[0]);
							break;
						}
					}
					else
					{
						if (b.name.ToLower().Trim() == nameSplit[i].ToLower().Trim())
						{
							tempWeight.item = b;
							tempWeight.weight = float.Parse(nameSplit[i + 1].Split(' ')[0]);
							break;
						}
					}
				}
				possibleOres.Add(tempWeight);
			}

			List<WeightedOreChance> oreChanges = new();
			foreach (OreWeight weight in possibleOres)
			{
				oreChanges.Add(new()
				{
					OrePrefab = SellableProxyFactory.GetOrCreateProxy(templateOre, weight.item, weight.crateParent),
					Weight = weight.weight
				});
			}

			OreNode oreNode = nodeGo.GetComponent<OreNode>();
			OreNodeDropOverride.SetPossibleDrops(oreNode, oreChanges);
			oreNode.MinDrops = oreChanges.Count;
			oreNode.MaxDrops = oreChanges.Count;
		}

		private void LoadAndSpawnAssetsFromBundle(AssetBundle bundle)
		{
			// Load all GameObjects from the bundle. You could also use LoadAsset<T>(assetName) 
			// if you know the names of the assets you want.
			GameObject[] assets = bundle.LoadAllAssets<GameObject>();

			foreach (GameObject asset in assets)
			{
				// Instantiate the loaded asset in the scene
				loadedAssets.Add(Instantiate(asset, Vector3.zero, Quaternion.identity));
				//Debug.Log("Spawned asset: " + asset.name);
			}
		}

		private void SetupEnvironment ()
		{
			GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
			plane.transform.position = Vector3.zero;
			plane.transform.localScale = new Vector3(1000f, 0.1f, 1000f);
			plane.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			plane.GetComponent<MeshRenderer>().material.color = new Color(0.3f, 0.3f, 0.3f);

			bool found = false;
			foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include ,FindObjectsSortMode.None))
			{
				if (l.type == LightType.Directional)
				{
					l.gameObject.SetActive(true);
					l.enabled = true;
					l.intensity = 1.5f;
					l.shadowStrength = 0.4f;
					found = true;
				}
			}
			if (!found)
			{
				GameObject directionalLight = new GameObject();
				directionalLight.name = "DirectionalLight";
				directionalLight.AddComponent<Light>().enabled = true;
				directionalLight.GetComponent<Light>().type = LightType.Directional;
				directionalLight.GetComponent<Light>().shadows = LightShadows.Soft;
				directionalLight.GetComponent<Light>().intensity = 1.5f;
				directionalLight.GetComponent<Light>().shadowStrength = 0.4f;
				directionalLight.transform.localEulerAngles = Vector3.one * 45;
			}
		}

		[HarmonyPatch(typeof(AutoMiner), "TrySpawnOre")]
		static class Patch_AutoMiner_TrySpawnOre
		{
			static bool Prefix(AutoMiner __instance)
			{
				if (UnityEngine.Random.Range(0f, 100f) > __instance.SpawnProbability)
					return false;

				OrePiece orePiece = __instance.ResourceDefinition.GetOrePrefab(__instance.CanProduceGems);
				if (orePiece == null)
					orePiece = __instance.FallbackOrePrefab;

				if (orePiece == null)
					return false;

				var proxy = orePiece.GetComponent<SellableProxy>();
				if (proxy != null)
				{
					GameObject spawned = GameObject.Instantiate(
						proxy.TargetPrefab.gameObject,
						__instance.OreSpawnPoint.position,
						__instance.OreSpawnPoint.rotation
					);
					if (spawned.GetComponent<BuildingCrate>())
						spawned.GetComponent<BuildingCrate>().Definition = orePiece.GetComponent<SellableProxy>().CrateParent.Definition;
					spawned.SetActive(true);

					return false;
				}

				return true;
			}
		}

		/*
		[HarmonyPatch(typeof(BuildingCrate), "Start")]
		static class Patch_BuildingCrate_Start
		{
			static void Prefix(BuildingCrate __instance)
			{
				string input = __instance.gameObject.name;
				string nameSplit = Regex.Replace(input, @"^BuildingCrate", "");

				foreach (BuildingObject b in Resources.FindObjectsOfTypeAll<BuildingObject>())
				{
					if (b.gameObject.scene.buildIndex != -1) continue;
					for (int i = 1; i < nameSplit.Length; i++)
					{
						if (b.name.ToLower().Trim() == nameSplit.ToLower().Trim())
						{
							__instance.Definition = b.Definition;
							return;
						}
					}
				}
			}
		}
		*/

		[HarmonyPatch(typeof(OreNode), nameof(OreNode.BreakNode))]
		static class Patch_OreNode_BreakNode
		{
			static bool Prefix(OreNode __instance, Vector3 position)
			{
				// Replicate vanilla logic
				int num = UnityEngine.Random.Range(__instance.MinDrops, __instance.MaxDrops + 1);
				Vector3 position2 = (__instance.transform.position + position) * 0.5f;

				for (int i = 0; i < num; i++)
				{
					position2 += UnityEngine.Random.insideUnitSphere * 0.15f;

					// Vanilla chooses an OrePiece prefab here
					OrePiece orePrefab = __instance.GetOrePrefab();
					if (orePrefab == null)
						continue;

					// Detect proxy (be a little forgiving where the component lives)
					var proxy = orePrefab.GetComponent<SellableProxy>()
								?? orePrefab.GetComponentInChildren<SellableProxy>(true)
								?? orePrefab.GetComponentInParent<SellableProxy>();

					GameObject spawnedGO;

					if (proxy != null && proxy.TargetPrefab != null)
					{
						// Spawn the target item directly (bypass OrePiecePoolManager)
						var spawned = UnityEngine.Object.Instantiate(proxy.TargetPrefab, position2, Quaternion.identity);
						spawnedGO = spawned.gameObject;

						if (spawned.GetComponent<BuildingCrate>())
							spawned.GetComponent<BuildingCrate>().Definition = orePrefab.GetComponent<SellableProxy>().CrateParent.Definition;

						spawnedGO.SetActive(true);
					}
					else
					{
						// Vanilla pooled ore spawn
						var spawnedOre = Singleton<OrePiecePoolManager>.Instance
							.SpawnPooledOre(orePrefab, position2, Quaternion.identity);

						if (spawnedOre == null)
							continue;

						spawnedGO = spawnedOre.gameObject;
					}

					// Apply physics kick like vanilla
					// (Some prefabs may have RB on root or child)
					var rb = spawnedGO.GetComponent<Rigidbody>() ?? spawnedGO.GetComponentInChildren<Rigidbody>();
					if (rb != null)
					{
						rb.linearVelocity = new Vector3(
							UnityEngine.Random.Range(-1.5f, 1.5f),
							UnityEngine.Random.Range(2f, 4f),
							UnityEngine.Random.Range(-1.5f, 1.5f)
						);
						rb.angularVelocity = UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(1f, 50f);
					}
				}

				// Keep vanilla SFX/VFX/support updates/destroy
				Singleton<SoundManager>.Instance.PlaySoundAtLocation(
					Singleton<SoundManager>.Instance.Sound_Node_Break,
					__instance.transform.position
				);
				Singleton<ParticleManager>.Instance.CreateParticle(
					Singleton<ParticleManager>.Instance.BreakOreNodeParticlePrefab,
					position
				);

				__instance.UpdateSupportsAbove();
				__instance.MarkStaticPositionAsBroken();
				UnityEngine.Object.Destroy(__instance.gameObject);

				// Skip original (we already did everything)
				return false;
			}
		}

		[HarmonyPatch(typeof(SavingLoadingManager), "LoadSceneThenRunLoadGame")]
		internal static class Patch_SLM_LoadSceneThenRunLoadGame
		{
			private const int DelayFramesAfterSceneLoad = 5;

			static bool Prefix(
				SavingLoadingManager __instance,
				string fullFilePath,
				string sceneName,
				ref IEnumerator __result)
			{
				__result = PatchedCoroutine(__instance, fullFilePath, sceneName);
				return false;
			}

			private static IEnumerator PatchedCoroutine(
				SavingLoadingManager slm,
				string fullFilePath,
				string sceneName)
			{
				MainMenu mainMenu = UnityEngine.Object.FindFirstObjectByType<MainMenu>();
				if (mainMenu != null)
				{
					yield return slm.StartCoroutine(mainMenu.PlayElevatorLowerAnimation());
				}

				AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
				while (!asyncLoad.isDone)
					yield return null;

				for (int i = 0; i < DelayFramesAfterSceneLoad; i++)
					yield return null;

				slm.LoadGame(fullFilePath);
			}
		}
	}
}
