using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Schema;
using System.Runtime.Serialization;
using Frontiers.World;
using Frontiers.World.Gameplay;
using ExtensionMethods;
using Frontiers.Data;
using System.Text;
using System.Reflection;

namespace Frontiers.World
{
		[ExecuteInEditMode]
		public partial class WorldItems : Manager
		{
				public static WorldItems Get;
				public GameObject EditorStructureParent = null;
				public string EditorSelectedItem = string.Empty;
				public string EditorSepectedPack = string.Empty;
				public string LocalWIPacksPath = "Artwork\\Packs\\WorldItemPacks\\";
				public string MaterialsFolder = "Materials";
				public string PrefabsFolder = "Prefabs";
				public string MeshesFolder = "Meshes";
				public string TexturesFolder = "Textures";
				public static bool ObjectShadows = true;
				protected bool mUpdatingActiveStates = false;
				//[HideInInspector]
				public List <WorldItemPackPaths> PackPaths = new List<WorldItemPackPaths>();
				//[HideInInspector]
				public List <WorldItemPack> WorldItemPacks = new List <WorldItemPack>();
				//[HideInInspector]
				public List <WICategory> Categories = new List <WICategory>();
				public List <KeyValuePair <WIGroup, Queue <StackItem>>> StackItemsToLoad = new List <KeyValuePair <WIGroup, Queue <StackItem>>>();

				public override void WakeUp()
				{
						Get = this;

						#if UNITY_EDITOR
						HugeSize = HugeSizeObject.bounds.size.x;
						LargeSize = LargeSizeObject.bounds.size.x;
						MediumSize = MediumSizeObject.bounds.size.x;
						SmallSize = SmallSizeObject.bounds.size.x;
						TinySize = TinySizeObject.bounds.size.x;
						#endif

						if (!Application.isPlaying && mWorldItemPackLookup.Count == 0) {
								foreach (WorldItemPack pack in WorldItemPacks) {
										if (!mWorldItemPackLookup.ContainsKey(pack.Name)) {
												mWorldItemPackLookup.Add(pack.Name, pack);
										}
										pack.RefreshLookup();
								}
						}
				}

				public override void OnModsLoadStart()
				{
						int numItems = 0;

						List <WorldItemPack> packs = new List<WorldItemPack>(WorldItemPacks);
						//structures will have set this by now
						packs.AddRange(Structures.Get.DynamicWorldItemPacks);

						foreach (WorldItemPack pack in packs) {
								if (!mWorldItemPackLookup.ContainsKey(pack.Name)) {
										mWorldItemPackLookup.Add(pack.Name, pack);
								}
								pack.RefreshLookup();
								numItems += pack.Prefabs.Count;

								int index = 0;
								foreach (GameObject prefab in pack.Prefabs) {
										if (prefab == null) {
												//////////////Debug.Log ("Prefab was null in pack " + pack.Name + " at index " + index);
										}
										index++;
										WorldItem worlditem = null;
										WIStates states = null;

										if (prefab.HasComponent <WorldItem>(out worlditem)) {
												WITemplate template = null;
												if (Mods.Get.Runtime.LoadMod <WITemplate>(
														    ref template,
														    System.IO.Path.Combine("WorldItem", pack.Name), worlditem.name)) {
														worlditem.Props = template.Props;
														//set script states, etc
														//for now this is just about loading the right props
												}
												worlditem.IsTemplate = true;
												worlditem.ClearStackItem();
												worlditem.ClearStackContainer();
												worlditem.InitializeTemplate();

//						if (string.IsNullOrEmpty (worlditem.Props.Name.PrefabName)) {
//							worlditem.Props.Name.PrefabName = prefab.name;
//						}
//						worlditem.Props.Name.PackName = pack.Name;
//						if (string.IsNullOrEmpty (worlditem.Props.Name.FileName)) {
//							worlditem.Props.Name.FileName = prefab.name;
//						}
//						if (string.IsNullOrEmpty (worlditem.Props.Name.DisplayName)) {
//							worlditem.Props.Name.DisplayName = WorldItems.CleanWorldItemName (worlditem.Props.Name.PrefabName);
//						}
//						worlditem.Props.Local.PreviousMode = WIMode.Frozen;
//						worlditem.Props.Local.Mode = WIMode.Frozen;
//					}
//					if (worlditem.rigidbody != null) {
//						worlditem.Props.Global.UseRigidBody = true;
//					} else {
//						worlditem.Props.Global.UseRigidBody = false;
//					}
//
//					if (worlditem.gameObject.tag == "Untagged") {
//						worlditem.gameObject.tag = Globals.TagGroundStone;
//					}
//					worlditem.Props.Global.ParentTag = worlditem.gameObject.tag;
//					worlditem.Props.Global.ParentLayer = worlditem.gameObject.layer;
//
//					if (worlditem.collider != null) {
//						Type type = worlditem.collider.GetType ();
//						switch (type.ToString ()) {
//						case "BoxCollider":
//							worlditem.Props.Global.ParentColliderType = WIColliderType.Box;
//							break;
//
//						case "SphereCollider":
//							worlditem.Props.Global.ParentColliderType = WIColliderType.Capsule;
//							break;
//
//						case "CapsuleCollider":
//							worlditem.Props.Global.ParentColliderType = WIColliderType.Sphere;
//							break;
//
//						case "MeshCollider":
//							MeshCollider mc = worlditem.GetComponent <MeshCollider> ();
//							if (mc.convex) {
//								worlditem.Props.Global.ParentColliderType = WIColliderType.ConvexMesh;
//							} else {
//								worlditem.Props.Global.ParentColliderType = WIColliderType.Mesh;
//							}
//							break;
//						}
//					} else {
//						worlditem.Props.Global.ParentColliderType = WIColliderType.None;
										}
								}
						}
						WorldItemPacks.Sort();
						LoadCategoriesFromDisk();
						System.GC.Collect();
				}

				public override void OnGameStart()
				{
						if (!GameManager.Get.TestingEnvironment && !mUpdatingActiveStates) {
								mUpdatingActiveStates = true;
								LastPlayerPosition = SpawnManager.Get.CurrentStartupPosition.WorldPosition.Position;
								StartCoroutine(UpdateStackItemsToLoad());
						}
				}

				public override void OnLocalPlayerSpawn()
				{
						//this will force the active states to update
						LastPlayerPosition = Player.Local.Position;
						LastPlayerSortPosition = Vector3.zero;
				}

				public bool PackPrefab(string worldItemPackName, string prefabName, out WorldItem prefab)
				{
						prefab = null;
						WorldItemPack pack = null;
						bool result = false;

						if (string.IsNullOrEmpty(worldItemPackName) || string.IsNullOrEmpty(prefabName)) {
								return false;
						}

						if (mWorldItemPackLookup.TryGetValue(worldItemPackName, out pack)) {
								if (pack.GetWorldItemPrefab(prefabName, out prefab)) {
										result = true;
								}
						}
						if (!result) {
								////Debug.Log ("Didn't find pack prefab " + worldItemPackName + "," + prefabName);
						}
						return result;
				}

				public Material PackMaterialVariation(string worldItemPackName, string materialName)
				{
						WorldItemPack pack = null;
						if (mWorldItemPackLookup.TryGetValue(worldItemPackName, out pack)) {
								foreach (Material variation in pack.MaterialVariations) {
										if (variation.name == materialName) {
												return variation;
										}
								}
						} else {
								//////////////Debug.Log ("Pack " + worldItemPackName + " doesn't exist");
						}
						return null;
				}

				protected Dictionary <string, WorldItemPack> mWorldItemPackLookup = new Dictionary <string, WorldItemPack>();
				protected Dictionary <string, WICategory> mWICategoryLookup = new Dictionary <string, WICategory>();

				public List <WICategory> Category(List <string> wiCatNames)
				{
						List <WICategory> categories = new List <WICategory>();
						WICategory cat = null;
						foreach (string wiCatName in wiCatNames) {
								if (Category(wiCatName, out cat)) {
										categories.Add(cat);
								}
						}
						return categories;
				}

				public bool Category(string wiCatName, out WICategory category)
				{
						category = null;
						//////////////Debug.Log ("Num in lookup: " + mWICategoryLookup.Count.ToString ());
						if (!mWICategoryLookup.TryGetValue(wiCatName, out category)) {
								category = WICategory.Empty;
								return false;
						}
						return true;
				}

				public static bool WorldItemExists(string packName, string prefabName)
				{
						WorldItemPack pack = null;
						WorldItem worlditemPrefab = null;
						return Get.mWorldItemPackLookup.TryGetValue(packName, out pack) &&
						pack.GetWorldItemPrefab(prefabName, out worlditemPrefab);
				}

				public WIGlobalProps GlobalPropsFromName(string packName, string prefabName)
				{
						if (string.IsNullOrEmpty(packName) || string.IsNullOrEmpty(prefabName)) {
								return null;
						}

						WorldItemPack pack = null;
						if (mWorldItemPackLookup.TryGetValue(packName, out pack)) {
								WorldItem worldItemPrefab = null;
								if (pack.GetWorldItemPrefab(prefabName, out worldItemPrefab)) {
										return worldItemPrefab.Props.Global;
								}
						}
						return null;
				}

				public void GlobalProps(WIProps props)
				{
						props.Global = GlobalPropsFromName(props.Name.PackName, props.Name.PrefabName);
				}

				public static string WIDisplayName(WorldItem worlditem)
				{
						if (worlditem.DisplayNamer == null) {
								if (!string.IsNullOrEmpty(worlditem.Props.Global.ExamineInfo.OverrideDescriptionName)) {
										worlditem.Props.Name.DisplayName = worlditem.Props.Global.ExamineInfo.OverrideDescriptionName;
								}
						} else {
								worlditem.Props.Name.DisplayName = worlditem.DisplayNamer(0);
						}
						return worlditem.Props.Name.DisplayName;
				}

				#region load / save / initialize stack items / worlditems

				//used to destroy both worlditems and stackitems
				//this is done here to ensure that mobile references are updated
				public static void RemoveItemFromGame(IWIBase item)
				{
						if (item.IsWorldItem) {
								//this will update mobile references automatically
								item.worlditem.SetMode(WIMode.RemovedFromGame);
						} else {
								//TODO get state of mobile reference
								//update it manually
						}
				}

				public static void RemoveWorldItemFromGame(WorldItem worlditem)
				{
						GameObject.Destroy(worlditem.gameObject);
				}

				public void Save(WorldItem worlditem)
				{
						if (worlditem == null || !worlditem.SaveItemOnUnloaded) {
								////Debug.Log ("Couldn't save world item, it was null");
								return;
						}
						if (worlditem.Group == null) {
								////Debug.Log ("Couldn't save worlditem " + worlditem.name + ", group was null");
						}
						//////////////Debug.Log ("Saving world item state for " + worlditem.FileName);
						Mods.Get.Runtime.SaveStackItemToGroup(worlditem.GetStackItem(WIMode.None), worlditem.Group.Props.UniqueID);
				}

				public void Save(WIGroup group)
				{
						Mods.Get.Runtime.SaveMod <WIGroupProps>(group.Props, "Group", group.Props.PathName);
				}

				public static void Unload(WorldItem worlditem)
				{
						if (!worlditem.Is(WIMode.RemovedFromGame) && worlditem.SaveItemOnUnloaded) {
								//we only want to save world items that aren't removed from the game
								Get.Save(worlditem);
						}

						GameObject.Destroy(worlditem.gameObject);
				}

				public static void LoadStackItems(Queue <StackItem> stackItems, WIGroup group)
				{
						////Debug.Log ("Calling load stack items in WORLDITEMS");
						KeyValuePair <WIGroup, Queue<StackItem>> groupPair = new KeyValuePair <WIGroup, Queue<StackItem>>(group, stackItems);
						Get.StackItemsToLoad.SafeAdd(groupPair);
				}

				public bool StackItemFromGenericWorldItem(GenericWorldItem genericWorldItem, out StackItem stackItem)
				{
						bool result = false;
						stackItem = null;
						WorldItemPack pack = null;
						if (mWorldItemPackLookup.TryGetValue(genericWorldItem.PackName, out pack)) {
								WorldItem worlditem = null;
								if (pack.GetWorldItemPrefab(genericWorldItem.PrefabName, out worlditem)) {
										stackItem = worlditem.GetStackItem(WIMode.Stacked);
										if (!string.IsNullOrEmpty(genericWorldItem.State)) {
												//Debug.Log ("Copying state from generic worlditem state " + genericWorldItem.State);
												stackItem.SaveState.LastState = genericWorldItem.State;
										}
										if (!string.IsNullOrEmpty(genericWorldItem.Subcategory)) {
												////////Debug.Log ("Setting stack item subcategory to " + genericWorldItem.Subcategory);
												stackItem.Props.Local.Subcategory = genericWorldItem.Subcategory;
										}
										if (!string.IsNullOrEmpty(genericWorldItem.StackName)) {
												stackItem.Props.Name.StackName = genericWorldItem.StackName;
										}
										if (!string.IsNullOrEmpty(genericWorldItem.DisplayName)) {
												stackItem.Props.Name.DisplayName = genericWorldItem.DisplayName;
										}
										result = true;
								}
						}
						return result;
				}

				public StackItem StackItemFromName(string worldItemName)
				{
						StackItem newTemplate = null;
						foreach (WorldItemPack pack in WorldItemPacks) {
								//////////////Debug.Log ("Trying pack " + pack.Name + "...");
								WorldItem worlditem = null;
								if (pack.GetWorldItemPrefab(worldItemName, out worlditem)) {
										return worlditem.GetStackItem(WIMode.Stacked);
								}
						}
						//////////////Debug.Log ("Couldn't find a template for " + worldItemName);
						return null;
				}

				public bool StackItemFromPack(string packName, string prefabName, out StackItem stackItem)
				{
						stackItem = null;
						WorldItemPack pack = null;
						if (mWorldItemPackLookup.TryGetValue(packName, out pack)) {
								//////////////Debug.Log ("Found pack name " + packName);
								WorldItem worlditem = null;
								//TEMP
								if (pack.GetWorldItemPrefab(prefabName, out worlditem)) {
										//////////////Debug.Log ("Found prefab name " + prefabName);
										stackItem = worlditem.GetTemplate();
										return true;
								}
						}
						//////////////Debug.Log ("Couldn't find pack " + packName + " or prefab " + prefabName);
						return false;
				}

				#endregion

				public void RefreshWorlditemShadowSettings(bool objectShadows)
				{
						if (ObjectShadows != objectShadows) {
								ObjectShadows = objectShadows;
								for (int i = 0; i < ActiveWorldItems.Count; i++) {
										if (ActiveWorldItems[i] != null) {
												ActiveWorldItems[i].RefreshShadowCasters(true);
										}
								}
								for (int i = 0; i < VisibleWorldItems.Count; i++) {
										if (VisibleWorldItems[i] != null) {
												VisibleWorldItems[i].RefreshShadowCasters(true);
										}
								}
								for (int i = 0; i < InvisibleWorldItems.Count; i++) {
										if (InvisibleWorldItems[i] != null) {
												InvisibleWorldItems[i].RefreshShadowCasters(false);
										}
								}
								for (int i = 0; i < LockedWorldItems.Count; i++) {
										if (LockedWorldItems[i] != null) {
												LockedWorldItems[i].RefreshShadowCasters(true);
										}
								}
						}
				}

				protected WIProps mShadowProps = null;
				protected Renderer mShadowRenderer = null;

				#region dopplegangers

				public static void ReturnDoppleganger(GameObject doppleganger)
				{
						if (doppleganger != null) {
								//TODO create an object pool
								GameObject.Destroy(doppleganger);
						}
				}

				public static void GetDopplegangerBounds(GameObject doppleganger, ref Bounds bounds)
				{
						bounds.center = doppleganger.transform.position;
						Renderer dopRenderer = doppleganger.renderer;
						if (dopRenderer != null) {
								bounds = dopRenderer.bounds;
								//don't bother with child renderers
								return;
						}
						//if that renderer was null get all the renderers in the children
						Renderer[] renderers = doppleganger.GetComponentsInChildren <Renderer>(false);
						for (int i = 0; i < renderers.Length; i++) {
								if (i == 0) {
										bounds.center = renderers[i].bounds.center;
								}
								bounds.Encapsulate(renderers[i].bounds);
						}
				}

				public static GameObject GetDoppleganger(IWIBase item, Transform dopplegangerParent, GameObject currentDoppleganger, WIMode mode)
				{
						return GetDoppleganger(item.PackName, item.PrefabName, dopplegangerParent, currentDoppleganger, mode, item.StackName, item.State, item.Subcategory, 1f, WorldClock.TimeOfDayCurrent, WorldClock.TimeOfYearCurrent);
				}

				public static GameObject GetDoppleganger(IWIBase item, Transform dopplegangerParent, GameObject currentDoppleganger, WIMode mode, float scaleMultiplier)
				{
						return GetDoppleganger(item.PackName, item.PrefabName, dopplegangerParent, currentDoppleganger, mode, item.StackName, item.State, item.Subcategory, scaleMultiplier, WorldClock.TimeOfDayCurrent, WorldClock.TimeOfYearCurrent);
				}

				public static GameObject GetDoppleganger(WorldItem worlditem, Transform dopplegangerParent, GameObject currentDoppleganger)
				{
						return GetDoppleganger(worlditem.PackName, worlditem.PrefabName, dopplegangerParent, currentDoppleganger, worlditem.Mode, worlditem.StackName, worlditem.State, worlditem.Subcategory, 1f, WorldClock.TimeOfDayCurrent, WorldClock.TimeOfYearCurrent);
				}

				public static GameObject GetDoppleganger(GenericWorldItem props, Transform dopplegangerParent, GameObject currentDoppleganger, WIMode mode)
				{
						return GetDoppleganger(props.PackName, props.PrefabName, dopplegangerParent, currentDoppleganger, mode, props.StackName, props.State, props.Subcategory, 1f, props.TOD, props.TOY);
				}

				public static GameObject GetDoppleganger(GenericWorldItem props, Transform dopplegangerParent, GameObject currentDoppleganger, WIMode mode, float scaleMultiplier)
				{
						return GetDoppleganger(props.PackName, props.PrefabName, dopplegangerParent, currentDoppleganger, mode, props.StackName, props.State, props.Subcategory, scaleMultiplier, props.TOD, props.TOY);
				}

				public static GameObject GetDoppleganger(string packName, string prefabName, Transform dopplegangerParent, GameObject currentDoppleganger, WIMode mode)
				{
						return GetDoppleganger(packName, prefabName, dopplegangerParent, currentDoppleganger, mode, prefabName, "Default", string.Empty, 1f, WorldClock.TimeOfDayCurrent, WorldClock.TimeOfYearCurrent);
				}

				public static GameObject GetDoppleganger(string packName, string prefabName, Transform dopplegangerParent, GameObject currentDoppleganger, string stackName, WIMode mode, string state)
				{
						return GetDoppleganger(packName, prefabName, dopplegangerParent, currentDoppleganger, mode, stackName, state, string.Empty, 1f, WorldClock.TimeOfDayCurrent, WorldClock.TimeOfYearCurrent);
				}

				public static StringBuilder gDopplegangerNameBuilder = new StringBuilder();
				public static string gDopplegangerName = string.Empty;

				public static GameObject GetDoppleganger(
						string packName, 
						string prefabName, 
						Transform dopplegangerParent, 
						GameObject currentDoppleganger, 
						WIMode mode, 
						string stackName, 
						string state, 
						string subcat,
						float scaleMultiplier, 
						TimeOfDay tod, 
						TimeOfYear toy)
				{
						//if it's *exactly* the same on all counts then we don't need to check for custom anything
						//just return the doppleganger as is
						gDopplegangerNameBuilder.Clear();
						gDopplegangerNameBuilder.Append(packName);
						gDopplegangerNameBuilder.Append(prefabName);
						gDopplegangerNameBuilder.Append(mode.ToString());
						gDopplegangerNameBuilder.Append(state);
						gDopplegangerNameBuilder.Append(subcat);
						gDopplegangerNameBuilder.Append(tod.ToString());
						gDopplegangerNameBuilder.Append(toy.ToString());
						gDopplegangerName = gDopplegangerNameBuilder.ToString();

						if (currentDoppleganger != null) {
								if (currentDoppleganger.name == gDopplegangerName) {
										return currentDoppleganger;
								} else {
										if (Application.isPlaying) {
												GameObject.Destroy(currentDoppleganger);
										} else {
												GameObject.DestroyImmediate(currentDoppleganger);
										}
								}
						}

						WorldItem item = null;
						if (!Get.PackPrefab(packName, prefabName, out item)) {
								return null;
						}

						//does the worlditem use a custom doppleganger function? if so use that instead
						if (item.UseCustomDoppleganger) {
								return item.Doppleganger.GetDoppleganger(item, dopplegangerParent, gDopplegangerName, mode, state, subcat, scaleMultiplier, tod, toy);
						}

						GameObject doppleGanger = dopplegangerParent.gameObject.FindOrCreateChild(gDopplegangerName).gameObject;
						ApplyDopplegangerRenderers(item, doppleGanger, state, mode);
						ApplyDopplegangerMode(item, doppleGanger, mode, scaleMultiplier);
						return doppleGanger;
				}

				public static void AutoScaleDoppleganger(Transform dopplegangerParent, GameObject doppleganger, Bounds baseObjectBounds, ref float scaleMultiplier, ref Vector3 dopplegangerOffset)
				{
						//this is necessary to get accurate bounds
						doppleganger.transform.parent = null;
						doppleganger.transform.localScale = Vector3.one;
						float objectMaxScale;
						float objectBaseScale = Mathf.Max(Mathf.Max(baseObjectBounds.size.x, baseObjectBounds.size.y), baseObjectBounds.size.z);
						float scaleAdjustment;
						//find the object bounds by encapsulating all of its render bounds
						Bounds objectBounds = new Bounds(doppleganger.transform.position, Vector3.zero);
						List <Renderer> objectRenderers = new List<Renderer>(doppleganger.GetComponentsInChildren <Renderer>(false));
						Renderer baseRenderer = null;
						if (doppleganger.HasComponent <Renderer>(out baseRenderer)) {
								objectRenderers.Add(baseRenderer);
						}
						for (int i = 0; i < objectRenderers.Count; i++) {
								objectBounds.Encapsulate(objectRenderers[i].bounds);
						}
						//find the max scale of the item from the bounds
						objectMaxScale = Mathf.Max(Mathf.Max(objectBounds.size.x, objectBounds.size.y), objectBounds.size.z);
						//Debug.Log ("Max scale of the doppleganger object was " + objectMaxScale.ToString ());
						//now figure out how big this is relative to the base scale
						//this will tell us how much we need to adjust the scale multiplier
						scaleAdjustment = objectMaxScale / objectBaseScale;
						dopplegangerOffset = (baseObjectBounds.center - objectBounds.center).WithZ(0f);
						//Debug.Log ("Doppleganger is " + scaleAdjustment.ToString () + " times the base scale " + objectBaseScale.ToString () + " original scale multiplier was " + scaleMultiplier.ToString ());
						scaleMultiplier = scaleMultiplier / scaleAdjustment;
						//Debug.Log ("So the scale multiplier is now " + scaleMultiplier.ToString () + " and there's an offset of " + dopplegangerOffset.ToString ());
						doppleganger.transform.parent = dopplegangerParent;
				}

				public static void ApplyDopplegangerRenderers(WorldItem item, GameObject doppleGanger, string state, WIMode mode)
				{
						List <GameObject> renderersToCreate = new List <GameObject>();
						List <Renderer> renderersToCopy = new List <Renderer>();
						bool transformEachRenderer = false;
						if (!item.HasStates && item.Renderers.Count == 1) {
								//this is a standard world item
								renderersToCreate.Add(doppleGanger);
								renderersToCopy.AddRange(item.Renderers);
						} else {
								//this worlditem has multiple renderers
								//OR it has states
								if (item.HasStates) {
										if (string.IsNullOrEmpty(state) || state == "Default") {
												state = item.States.DefaultState;
												//Debug.Log ("Getting default state " + state);
										}
										//find the child object with the current state name
										Renderer stateRenderer = null;
										for (int i = 0; i < item.States.States.Count; i++) {
												if (item.States.States[i].Name == state) {
														stateRenderer = item.States.States[i].StateRenderer;
														//Debug.Log ("Found state " + state);
														break;
												}
										}
										if (stateRenderer != null) {
												renderersToCreate.Add(doppleGanger.CreateChild(stateRenderer.name).gameObject);
												renderersToCopy.Add(stateRenderer);
										} else {
												//Debug.Log ("State renderer was null for state " + state + " in " + item.name);
										}
								} else {
										//if it doesn't have states, all of its renderers will be kept in the Renderers list
										for (int i = 0; i < item.Renderers.Count; i++) {
												renderersToCreate.Add(doppleGanger.CreateChild(item.Renderers[i].name).gameObject);
										}
										renderersToCopy.AddRange(item.Renderers);
								}
						}
						transformEachRenderer = renderersToCopy.Count > 0;

						Bounds rendererBounds = new Bounds();

						for (int i = 0; i < renderersToCopy.Count; i++) {
								GameObject dopGameObject = renderersToCreate[i];
								Renderer wiMr = renderersToCopy[i];
								MeshFilter wiMf = wiMr.gameObject.GetComponent <MeshFilter>();
								MeshRenderer dopMr = dopGameObject.GetOrAdd <MeshRenderer>();
								MeshFilter dopMf = dopGameObject.GetOrAdd <MeshFilter>();

								if (transformEachRenderer) {
										dopGameObject.transform.localPosition = wiMr.transform.localPosition;
										dopGameObject.transform.localScale = wiMr.transform.localScale;
										dopGameObject.transform.localRotation = wiMr.transform.localRotation;
								}

								if (wiMf == null) {
										SkinnedMeshRenderer sMr = wiMr.GetComponent <SkinnedMeshRenderer>();
										dopMf.sharedMesh = sMr.sharedMesh;
								} else {
										dopMf.sharedMesh = wiMf.sharedMesh;
								}
								List <Material> materials = new List <Material>();

								switch (mode) {
										case WIMode.Stacked:
										case WIMode.Selected:
												dopMr.castShadows = false;
												dopMr.receiveShadows = false;
												dopGameObject.layer = Globals.LayerNumWorldItemInventory;
												materials.AddRange(wiMr.sharedMaterials);
												for (int j = 0; j < wiMr.sharedMaterials.Length; j++) {
														if (item.Props.Global.UseCutoutShader) {
																Material baseMat = wiMr.sharedMaterials[j];
																Material customMat = new Material(Mats.Get.InventoryRimCutoutMaterial);
																customMat.SetTexture("_MainTex", baseMat.GetTexture("_MainTex"));
																materials.Add(customMat);
														} else {
																materials.Add(Mats.Get.InventoryRimMaterial);
														}
												}
												break;

										case WIMode.Placing:
												dopMr.castShadows = false;
												dopMr.receiveShadows = false;
												dopGameObject.layer = Globals.LayerNumWorldItemActive;
												for (int j = 0; j < wiMr.sharedMaterials.Length; j++) {
//						if (item.Props.Global.UseCutoutShader) {
//							Material baseMat = wiMr.sharedMaterials [j];
//							Material customMat = new Material (Mats.Get.InventoryRimCutoutMaterial);
//							customMat.SetTexture ("_MainTex", baseMat.GetTexture ("_MainTex"));
//							materials.Add (customMat);
//						} else {
//							materials.Add (Mats.Get.ItemPlacementOutlineMaterial);
//						}
														materials.Add(Mats.Get.ItemPlacementMaterial);
												}
												break;

										case WIMode.Equipped:
												dopMr.castShadows = false;
												dopMr.receiveShadows = false;
												dopGameObject.layer = Globals.LayerNumPlayerTool;
												materials.AddRange(wiMr.sharedMaterials);
												break;

										case WIMode.Crafting:
												dopMr.castShadows = false;
												dopMr.receiveShadows = false;
												dopGameObject.layer = Globals.LayerNumWorldItemInventory;
					//materials.AddRange (wiMr.sharedMaterials);
												materials.Add(Mats.Get.CraftingDoppleGangerMaterial);
												break;

										case WIMode.Placed:
										case WIMode.World:
												dopMr.castShadows = true;
												dopMr.receiveShadows = true;
												dopGameObject.layer = Globals.LayerNumWorldItemActive;
												materials.AddRange(wiMr.sharedMaterials);
												break;

										default:
												dopMr.castShadows = false;
												dopMr.receiveShadows = false;
												dopGameObject.layer = Globals.LayerNumWorldItemInventory;
												materials.AddRange(wiMr.sharedMaterials);
												break;
								}

								rendererBounds.Encapsulate(dopMr.bounds);

								materials.Remove(Mats.Get.FocusOutlineMaterial);
								materials.Remove(Mats.Get.FocusOutlineCutoutMaterial);
								materials.Remove(Mats.Get.FocusHighlightMaterial);

								dopMr.sharedMaterials = materials.ToArray();
								dopMr.enabled = true;
						}

						//add a collider to the doppleganger using bounds
						//this will be used to scale the object
						//disable it so it doesn't muck with stuff
						//BoxCollider bc = doppleGanger.AddComponent <BoxCollider> ();
						//bc.enabled = false;
						//bc.size = rendererBounds.size;
						//bc.center = rendererBounds.center;
				}

				public static void ApplyDopplegangerMode(WorldItem item, GameObject doppleGanger, WIMode mode, float scaleMultiplier)
				{
						ApplyDopplegangerMode(item, doppleGanger, mode, scaleMultiplier, Vector3.zero);
				}

				public static void ApplyDopplegangerMode(WorldItem item, GameObject doppleGanger, WIMode mode, float scaleMultiplier, Vector3 offset)
				{
						Vector3 dopPos = item.Props.Global.PivotOffset;
						Vector3 dopRot = item.Props.Global.BaseRotation;
						Vector3 dopScale = Vector3.one;

						Stackable stackable = null;
						Equippable equippable = null;
						switch (mode) {
								case WIMode.Stacked:
								case WIMode.Crafting:
										ApplyLayer(doppleGanger, Globals.LayerNumWorldItemInventory);
										if (item.Is <Stackable>(out stackable)) {
												dopPos = stackable.SquareOffset;
												dopRot = stackable.SquareRotation;
												dopScale = Vector3.one * stackable.SquareScale;
										}
										break;

								case WIMode.Selected:
										ApplyLayer(doppleGanger, Globals.LayerNumWorldItemInventory);
										if (item.Is <Stackable>(out stackable)) {
												dopPos = stackable.SquareOffset;
												dopRot = stackable.SquareRotation;
												dopScale = Vector3.one * stackable.SquareScale;
										}
										dopScale *= 2;
										break;

								case WIMode.Equipped:
										ApplyLayer(doppleGanger, Globals.LayerNumPlayerTool);
										if (item.Is<Equippable>(out equippable)) {
												dopPos += equippable.EquipOffset;
												dopRot += equippable.EquipRotation;
										}
										foreach (Transform lightSource in item.transform) {
												if (lightSource.light != null) {
														GameObject clonedLightSource = GameObject.Instantiate(lightSource.gameObject) as GameObject;
														clonedLightSource.transform.parent = doppleGanger.transform;
														clonedLightSource.transform.localPosition = lightSource.transform.localPosition;
												}
										}
				//since it's equipped, we'll need to copy over any mesh modifiers
										MegaMorph itemMegaMorph = null;
										if (item.gameObject.HasComponent <MegaMorph>(out itemMegaMorph)) {
												MegaMorph dopMegaMorph = CopyComponent <MegaMorph>(itemMegaMorph, doppleGanger);
										}
										break;

								case WIMode.Placing:
										ApplyLayer(doppleGanger, Globals.LayerNumScenery);
										break;

								case WIMode.Placed:
								case WIMode.World:
										ApplyLayer(doppleGanger, Globals.LayerNumWorldItemActive);
										break;

								default:
										ApplyLayer(doppleGanger, Globals.LayerNumWorldItemInventory);
										break;
						}

						doppleGanger.transform.localPosition = dopPos + offset;
						doppleGanger.transform.localRotation = Quaternion.Euler(dopRot);
						doppleGanger.transform.localScale = dopScale * Mathf.Clamp(scaleMultiplier, 0.0001f, 1000f);
				}

				public static void FitDopplegangerToBounds(Transform dopplegangerParent, GameObject doppleganger, Bounds parentBounds)
				{
						if (doppleganger == null || dopplegangerParent == null)
								return;

						doppleganger.transform.localPosition = Vector3.zero;
						doppleganger.transform.localScale = Vector3.one;
						Vector3 parentCenter = parentBounds.center;

						Bounds objectBounds = new Bounds(doppleganger.transform.position, Vector3.zero);
						List <Renderer> renderers = new List<Renderer>();
						if (doppleganger.renderer != null) {
								renderers.Add(doppleganger.renderer);
						} else {
								renderers.AddRange(doppleganger.GetComponentsInChildren <Renderer>(false));
						}

						if (renderers.Count < 1) {
								//nothing to do here
								return;
						}

						for (int i = 0; i < renderers.Count; i++) {
								objectBounds.Encapsulate(renderers[i].bounds);
						}
						Vector3 objectCenter = objectBounds.center;

						float parentMaxScale = Mathf.Max(new float [] {
								parentBounds.size.x,
								parentBounds.size.y,
								parentBounds.size.z
						});
						float objectMaxScale = Mathf.Max(new float [] {
								objectBounds.size.x,
								objectBounds.size.y,
								objectBounds.size.z
						});

						try {
								doppleganger.transform.localScale = (Vector3.one * parentMaxScale / objectMaxScale);
						} catch (Exception e) {
								Debug.LogError("Error while creating doppleganger: " + e.ToString());
						}
						//now that we've resized it, it will have a different position
						//so recalculate the bounds
						for (int i = 0; i < renderers.Count; i++) {
								objectBounds.Encapsulate(renderers[i].bounds);
						}
						objectCenter = objectBounds.center;
						//move the item so its center is the same as the parent center
						Vector3 offset = parentCenter - objectCenter;

						doppleganger.transform.localPosition = offset;
				}

				protected static void ApplyLayer(GameObject rootObject, int layer)
				{
						rootObject.SetLayerRecursively(layer);
				}

				#endregion

				#region clone world item / prefab

				public static bool CloneWorldItem(WorldItem worlditem, out WorldItem newWorldItem)
				{
						return CloneFromStackItem(worlditem.GetStackItem(WIMode.None), worlditem.Group, out newWorldItem);
				}

				public static bool CloneWorldItem(WorldItem worlditem, WIGroup group, out WorldItem newWorldItem)
				{
						return CloneFromStackItem(worlditem.GetStackItem(WIMode.None), group, out newWorldItem);
				}

				public static bool CloneWorldItem(string packName, string prefabName, STransform position, bool applyOffset, WIGroup group, out WorldItem worlditem)
				{
						worlditem = null;
						WorldItem prefab = null;
						if (Get.PackPrefab(packName, prefabName, out prefab)) {
								return CloneFromPrefab(prefab, position, applyOffset, group, out worlditem);
						}
						return false;
				}

				public static bool CloneWorldItem(GenericWorldItem genericItem, STransform position, bool applyOffset, WIGroup group, out WorldItem worlditem)
				{
						worlditem = null;
						WorldItem prefab = null;
						if (Get.PackPrefab(genericItem.PackName, genericItem.PrefabName, out prefab)) {
								return CloneFromPrefab(prefab, position, applyOffset, genericItem.DisplayName, genericItem.Subcategory, genericItem.State, group, out worlditem);
						}
						return false;
				}

				public static bool CloneFromStackItem(StackItem stackItem, WIGroup group, out WorldItem worlditem)
				{
						worlditem = null;
						WorldItem prefab = null;
						bool result = false;

						if (stackItem != null && Get.PackPrefab(stackItem.PackName, stackItem.PrefabName, out prefab)) {
								GameObject newWorldItemGameObject = GameObject.Instantiate(prefab.tr.gameObject, Globals.WorldItemInstantiationOffset * (1f + UnityEngine.Random.value), Quaternion.identity) as GameObject;
								newWorldItemGameObject.name = stackItem.Props.Name.FileName;
								if (!newWorldItemGameObject.HasComponent <WorldItem>(out worlditem)) {
										DynamicPrefab dp = null;
										if (newWorldItemGameObject.HasComponent <DynamicPrefab>(out dp)) {
												worlditem = dp.worlditem;
										}
								}
								worlditem.ClearStackContainer();
								worlditem.IsTemplate = false;
								worlditem.Group = group;
								worlditem.tr.parent = group.tr;
								stackItem.Props.Local.Transform.ApplyTo(newWorldItemGameObject.transform);
								//copy global properties into new worlditem
								//copy local and stack item props to worlditem
								worlditem.ReceiveState(stackItem);
								worlditem.Props.Global = prefab.Props.Global;
								if (string.IsNullOrEmpty(worlditem.Props.Name.StackName)) {
										worlditem.Props.Name.StackName = prefab.Props.Name.StackName;
								}
								if (string.IsNullOrEmpty(worlditem.Props.Name.DisplayName)) {
										worlditem.Props.Name.DisplayName = prefab.Props.Name.DisplayName;
								}
								//Debug.Log ("File name is now " + worlditem.Props.Name.FileName);
								InitializeWorldItem(worlditem);
								result = true;
						}
						return result;
				}

				public static bool CloneFromPrefab(WorldItem prefab, WIGroup group, out WorldItem worlditem)
				{
						//////////////Debug.Log (prefab.Props.Name.FileName + " being created");
						return CloneFromPrefab(prefab, STransform.zero, true, string.Empty, string.Empty, string.Empty, group, out worlditem);
				}

				public static bool CloneFromPrefab(WorldItem prefab, bool applyOffset, WIGroup group, out WorldItem worlditem)
				{
						//////////////Debug.Log (prefab.Props.Name.FileName + " being created");
						return CloneFromPrefab(prefab, STransform.zero, applyOffset, string.Empty, string.Empty, string.Empty, group, out worlditem);
				}

				public static bool CloneFromPrefab(WorldItem prefab, STransform position, bool applyOffset, WIGroup group, out WorldItem worlditem)
				{
						//////////////Debug.Log (prefab.Props.Name.FileName + " being created");
						return CloneFromPrefab(prefab, position, applyOffset, string.Empty, string.Empty, string.Empty, group, out worlditem);
				}

				public static bool CloneFromPrefab(WorldItem prefab, STransform position, bool applyOffset, string displayName, string subcategory, string state, WIGroup group, out WorldItem worlditem)
				{	
						worlditem = null;
						if (prefab == null) {
								//Debug.Log ("Prefab was null in clone from prefab");
								return false;
						}
						if (group == null) {
								//Debug.LogWarning ("Group was null when trying to clone prefab " + prefab.name);
								return false;
						}

						GameObject newWorldItemGameObject = GameObject.Instantiate(prefab.tr.gameObject, position.Position, Quaternion.Euler(position.Rotation)) as GameObject;
						newWorldItemGameObject.name = prefab.tr.name;
						newWorldItemGameObject.transform.parent = group.transform.parent;
						if (!newWorldItemGameObject.HasComponent <WorldItem>(out worlditem)) {
								DynamicPrefab dp = null;
								if (newWorldItemGameObject.HasComponent <DynamicPrefab>(out dp)) {
										worlditem = dp.worlditem;
								}
								//otherwise we're screwed, but that should never happen
						}
						worlditem.IsTemplate = false;
						worlditem.Group = group;
						worlditem.tr.parent = group.tr;
						worlditem.Props = new WIProps();
						worlditem.Props.CopyLocal(prefab.Props);
						worlditem.Props.CopyGlobal(prefab.Props);
						worlditem.Props.CopyLocalNames(prefab.Props);
						worlditem.Props.CopyGlobalNames(prefab.Props);
						//pass along display name / subcat / state
						//this will usually come from a generic worlditem
						//more often they'll be empty
						//TODO look for a better place to do this
						if (!string.IsNullOrEmpty(displayName)) {
								worlditem.Props.Name.DisplayName = displayName;
						}
						if (!string.IsNullOrEmpty(subcategory)) {
								worlditem.Props.Local.Subcategory = subcategory;
						}
						if (!string.IsNullOrEmpty(state)) {
								//this will automatically be loaded into the 'last state'
								//and used on initialization
								worlditem.State = state;
						}
						if (applyOffset) {
								worlditem.Props.Local.Transform = new STransform(position.Position + worlditem.Props.Global.PivotOffset, position.Rotation + worlditem.Props.Global.BaseRotation, Vector3.one);
						} else {
								worlditem.Props.Local.Transform.CopyFrom(position);// = new STransform (position);
						}
						InitializeWorldItem(worlditem);

						return true;
				}

				#endregion

				#region clone from category

				public static bool GetRandomGenericWorldItemFromCatgeory(string categoryName, out GenericWorldItem genitem)
				{
						genitem = null;
						WICategory category = null;
						if (Get.mWICategoryLookup.TryGetValue(categoryName, out category)) {	
								int randomIndex = UnityEngine.Random.Range(0, category.NumItems - 1);
								genitem = category.GenericWorldItems[randomIndex];
								return true;
						}
						return false;
				}

				public static bool CloneRandomFromCategory(string categoryName, WIGroup group, out WorldItem worlditem)
				{
						worlditem = null;
						WICategory cat = null;
						if (Get.Category(categoryName, out cat)) {
								return CloneRandomFromCategory(cat, group, out worlditem);
						}
						return false;
				}

				public static bool CloneRandomFromCategory(string categoryName, WIGroup group, STransform position, WIFlags flags, int spawnCode, int spawnIndex, out WorldItem worlditem)
				{
						worlditem = null;
						WICategory cat = null;
						if (Get.Category(categoryName, out cat)) {
								return CloneRandomFromCategory(cat, group, position, flags, spawnCode, spawnIndex, out worlditem);
						}
						return false;
				}

				public static bool CloneRandomFromCategory(string categoryName, WIGroup group, STransform position, out WorldItem worlditem)
				{
						worlditem = null;
						WICategory cat = null;
						if (Get.Category(categoryName, out cat)) {
								return CloneRandomFromCategory(cat, group, position, out worlditem);
						}
						return false;
				}

				public static bool CloneRandomFromCategory(WICategory category, WIGroup group, out WorldItem worlditem)
				{
						worlditem = null;
						if (category.NumItems > 0) {
								int randomIndex = UnityEngine.Random.Range(0, category.NumItems);
								GenericWorldItem genitem = category.GenericWorldItems[randomIndex];
								CloneWorldItem(genitem.PackName, genitem.PrefabName, STransform.zero, false, group, out worlditem);//TEMP TODO
								return true;
						}
						//////////////Debug.Log ("Category was empty");
						return false;
				}

				public static bool CloneRandomFromCategory(WICategory category, WIGroup group, STransform position, out WorldItem worlditem)
				{
						worlditem = null;
						if (category.NumItems > 0) {
								int randomIndex = UnityEngine.Random.Range(0, category.NumItems);
								GenericWorldItem genitem = category.GenericWorldItems[randomIndex];
								CloneWorldItem(genitem.PackName, genitem.PrefabName, position, true, group, out worlditem);
								return true;
						}
						//////////////Debug.Log ("Category was empty");
						return false;
				}

				public static bool CloneRandomFromCategory(WICategory category, WIGroup group, STransform position, WIFlags flags, int spawnCode, int spawnIndex, out WorldItem worlditem)
				{
						worlditem = null;
						GenericWorldItem genericItem = null;
						if (category.GetItem(flags, spawnCode, ref spawnIndex, out genericItem)) {
								return CloneWorldItem(genericItem, position, true, group, out worlditem);
						} else {
								//Debug.Log ("Couldn't get item from category " + category.Name);
						}
						//////////Debug.Log ("Category was empty");
						return false;
				}

				public static bool RandomStackItemFromCategory(WICategory category, out StackItem stackItem)
				{
						stackItem = null;
						if (category.NumItems > 0) {
								int randomIndex = UnityEngine.Random.Range(0, category.NumItems - 1);
								GenericWorldItem genericWorldItem = category.GenericWorldItems[randomIndex];
								stackItem = genericWorldItem.ToStackItem();
						}
						return stackItem != null;
				}

				#endregion

				#region load / save categories

				public void SaveCategoresToDisk()
				{
						Mods.Get.Runtime.SaveMods(Categories, "Category");
				}

				public void LoadCategoriesFromDisk()
				{
						Categories.Clear();
						Mods.Get.Runtime.LoadAvailableMods <WICategory>(Categories, "Category");
						mWICategoryLookup.Clear();
						for (int i = 0; i < Categories.Count; i++) {
								mWICategoryLookup.Add(Categories[i].Name, Categories[i]);
								Categories[i].Initialize();
						}
						//Debug.Log ("Loaded " + Categories.Count.ToString () + " categories from disk");
						Categories.Sort();
						#if UNITY_EDITOR
						categoryNames = null;
						#endif
				}

				#endregion

				#region static helper functions

				public static float HugeSize = 4.0f;
				public static float LargeSize = 2.0f;
				public static float MediumSize = 1.0f;
				public static float SmallSize = 0.5f;
				public static float TinySize = 0.25f;

				public static float WISizeToFloat(WISize size)
				{
						switch (size) {
								case WISize.Huge:
										return HugeSize;

								case WISize.Large:
										return LargeSize;

								case WISize.Medium:
										return MediumSize;

								case WISize.Small:
										return SmallSize;

								case WISize.Tiny:
										return TinySize;
						}

						return 0f;
				}

				public static float ItemWeightToKG(ItemWeight weight)
				{
						float weightInKG = 1.0f;

						switch (weight) {
								case ItemWeight.Weightless:
										weightInKG = Globals.WeightInKgWeightless;// 0.1f;
										break;

								case ItemWeight.Light:
										weightInKG = Globals.WeightInKgLight;// 1.0f;
										break;

								case ItemWeight.Medium:
										weightInKG = Globals.WeightInKgMedium;// 10.0f;
										break;

								case ItemWeight.Heavy:
										weightInKG = Globals.WeightInKgHeavy;// 100.0f;
										break;

								case ItemWeight.Unliftable:
										weightInKG = Globals.WeightInKgUnliftable;// 1000.0f;
										break;

								default:
										break;
						}

						return weightInKG;
				}

				public static void GenerateCollider(WorldItem worlditem)
				{
						switch (worlditem.Props.Global.ParentColliderType) {
								case WIColliderType.ConvexMesh:
										MeshCollider mc = worlditem.gameObject.GetOrAdd <MeshCollider>();
										mc.enabled = false;
										mc.sharedMesh = worlditem.gameObject.GetOrAdd <MeshFilter>().mesh;
										mc.convex = true;
										break;

								case WIColliderType.Box:
										worlditem.gameObject.GetOrAdd <BoxCollider>().enabled = false;
										break;

								case WIColliderType.Sphere:
										worlditem.gameObject.GetOrAdd <SphereCollider>().enabled = false;
										break;

								case WIColliderType.Capsule:
										worlditem.gameObject.GetOrAdd <CapsuleCollider>().enabled = false;
										break;

								case WIColliderType.Mesh:
										worlditem.gameObject.GetOrAdd <MeshCollider>().enabled = false;
										break;

								case WIColliderType.UseExisting:
										break;

								default:
										break;
						}
						worlditem.Colliders.Add(worlditem.gameObject.collider);
				}

				public static float ConvertKGToRigidBodyWeight(float weightInKG)
				{
						return weightInKG;
				}

				public static bool IsEquippedAsTool(WorldItem worlditem)
				{
						if (!Manager.IsAwake <Player>()) {
								return false;
						}

						if (Player.Local.Tool.IsEquipped) {
								if (Player.Local.Tool.worlditem.gameObject == worlditem.gameObject) {
										return true;
								}
						}
						return false;
				}

				public static bool IsBeingCarried(WorldItem worldItem)
				{
						return Player.Local.ItemPlacement.IsCarryingSomething && Player.Local.ItemPlacement.CarryObject == worldItem;
				}

				public static string CleanWorldItemName(string itemName)
				{
						if (!string.IsNullOrEmpty(itemName)) {
								return System.Text.RegularExpressions.Regex.Replace(itemName, @" \d", "");
						}
						return itemName;
				}

				protected static Rigidbody mIoiRBCheck = null;
				protected static WorldItem mWorlditemCheck = null;
				protected static BodyPart mBodyPartCheck = null;
				protected static RaycastHit mLineOfSightHit;

				public static bool HasLineOfSight(Vector3 startPosition, IItemOfInterest target, ref Vector3 targetPosition, ref Vector3 hitPosition, out IItemOfInterest hitIOI)
				{
						hitIOI = null;
						//if we hit something while trying to look
						//this is a total kludge - treat the player differently
						if (target.IOIType == ItemOfInterestType.Player) {
								targetPosition = target.player.ChestPosition;
								//ColoredDebug.Log ("Our target is player, using chest position", "Cyan");
						} else {
								targetPosition = target.Position;
						}
						hitPosition = targetPosition;

						if (Physics.Linecast(startPosition, targetPosition, out mLineOfSightHit, Globals.LayersItemOfInterest)) {
								hitPosition = mLineOfSightHit.point;
								//ColoredDebug.Log ("HasLineOfSight: Hit a thing! " + mLineOfSightHit.collider.name, "Green");
								if (GetIOIFromCollider(mLineOfSightHit.collider, out hitIOI)) {
										//ColoredDebug.Log ("HasLineOfSight: the thing we hit was the thing we wanted to hit!", "Green");
										return target == hitIOI;
								} else {
										//ColoredDebug.Log ("HasLineOfSight: hit something but it wasn't an item of interest! it's: " + mLineOfSightHit.collider.name, "Yellow");
								}
						} else {
								//ColoredDebug.Log ("HasLineOfSight: Did NOT hit a thing!", "Red");
						}
						return false;
				}

				public static bool GetIOIFromCollider(Collider other, out IItemOfInterest ioi)
				{
						ioi = null;
						if (other.isTrigger) {
								return false;
						}

						ioi = (IItemOfInterest)other.gameObject.GetComponent(typeof(IItemOfInterest));
						//nothing on the collider?
						//try the attached rigid body
						if (ioi == null) {
								mIoiRBCheck = other.attachedRigidbody;
								if (mIoiRBCheck != null) {
										//if the tag is BodyGeneral then it's a world body
										ioi = (IItemOfInterest)mIoiRBCheck.GetComponent(typeof(IItemOfInterest));
								}
						}
						//still nothing? try a worlditem for good measure
						//this will check for body parts
						//(which are not guaranteed to be kept under the worlditem's attached rigidbody)
						if (ioi == null) {
								GetIOIFromGameObject(other.gameObject, out ioi);
						}
						return ioi != null;
				}

				public static bool GetIOIFromGameObject(GameObject go, out IItemOfInterest ioi)
				{
						ioi = (IItemOfInterest)go.GetComponent(typeof(IItemOfInterest));
						if (ioi == null) {
								switch (go.tag) {
										case "ColliderFluid":
					//collider fluid objects are immediately parented under worlidtem
												ioi = (IItemOfInterest)go.transform.parent.GetComponent(typeof(IItemOfInterest));
												break;
								//body parts are kept separate - they store a link to their worlditem
										case "BodyLeg":
										case "BodyArm":
										case "BodyHead":
										case "BodyTorso":
										case "BodyGeneral":
												if (go.HasComponent <BodyPart>(out mBodyPartCheck)) {
														ioi = mBodyPartCheck.Owner;
												}
												break;
								//state child object
										case "StateChild":
					//state child objects are immediately parented under worlditem
												ioi = go.transform.parent.GetComponent <WorldItem>();
												break;

										default:
												break;
								}
						}
						return ioi != null;
				}

				public static void ReplaceWorldItem(WorldItem worlditem, StackItem replacement)
				{
						if (worlditem.Is(WIMode.Equipped)) {
								//ok, we can get away with equipping the stack item
								//presumably it won't stack or we would have stacked it
								WIStackError error = WIStackError.None;
								Player.Local.Inventory.AddItems(replacement, ref error);
						} else {
								WorldItem replacementWorldItem = null;
								if (CloneFromStackItem(replacement, worlditem.Group, out replacementWorldItem)) {
										//initialize immediately
										replacementWorldItem.Props.Local.Transform.CopyFrom(worlditem.transform);
										replacementWorldItem.Initialize();				
								}
						}
						//in all cases, kill the original
						worlditem.SetMode(WIMode.RemovedFromGame);
				}

				public static void ReplaceWorldItem(WorldItem worlditem, GenericWorldItem replacementTemplate)
				{
						ReplaceWorldItem(worlditem, replacementTemplate.ToStackItem());
				}

				#endregion

				public static T CopyComponent<T>(T original, GameObject destination) where T : Component
				{
						System.Type type = original.GetType();
						Component copy = destination.AddComponent(type);
						System.Reflection.FieldInfo[] fields = type.GetFields();
						foreach (System.Reflection.FieldInfo field in fields) {
								field.SetValue(copy, field.GetValue(original));
						}
						return copy as T;
				}
		}
}