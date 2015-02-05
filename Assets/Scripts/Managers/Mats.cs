using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Frontiers
{
		public class Mats : Manager
		{
				public static Mats Get;
				public IconManager Icons;
				public ArtifactMatManager ArtifactMats;
				public SetupAdvancedFoliageShader AFS;
				public Cubemap FoliageShaderDiffuseMap;
				public Cubemap FoliageShaderSpecMap;
				public Vector3 DefaultLabelFontSize = new Vector3(30f, 30f, 1f);
				//set on startup
				public UIFont DefaultLabelFont;
				public List <Texture2D> TerrainGrassTextures = new List<Texture2D>();
				public List <Texture2D> TerrainGroundTextures = new List<Texture2D>();
				public List <Texture2D> GenericTerrainNormals = new List<Texture2D>();
				public List <Texture2D> AtlasTextures = new List<Texture2D>();

				public override void OnTextureLoadStart()
				{
						//put in requests for ALL ground textures
						//add all ground textures and normals to the same array - combined normals are not actually stored as normals
						Mods.Get.LoadAvailableGenericTextures("GroundTexture", false, Globals.GroundTextureResolution, Globals.GroundTextureResolution, TerrainGroundTextures);
						Mods.Get.LoadAvailableGenericTextures("GroundNormal", false, Globals.GroundCombinedNormalResolution, Globals.GroundCombinedNormalResolution, TerrainGroundTextures);
						//and ALL grass textures
						Mods.Get.LoadAvailableGenericTextures("GrassTexture", false, Globals.GrassTextureResolution, Globals.GrassTextureResolution, TerrainGrassTextures);
						//load the shared terrain normal texture
						//this one IS stored as a normal map
						Mods.Get.LoadAvailableGenericTextures("GenericTerrainNormal", "SharedNormal", true, Globals.GroundCombinedNormalResolution, Globals.GroundCombinedNormalResolution, GenericTerrainNormals);

						//now get our atlas textures
						//Mods.Get.LoadAvailableGenericTextures("Atlas", false, AtlasTextures);
				}

				public override void OnTextureLoadFinish()
				{
						for (int i = 0; i < TerrainGrassTextures.Count; i++) {
								TerrainGrassTextures[i].wrapMode = TextureWrapMode.Clamp;
						}
						mTexturesLoaded = true;
				}

				public override void WakeUp()
				{
						Get = this;
						DefaultLabelFont = PrintingPress40Font;
				}

				public override void Initialize()
				{
						//first set all the properties
						AFS.isLinear = false;
						AFS.diffuseIsHDR = false;
						AFS.specularIsHDR = false;
						AFS.UseLinearLightingFixTrees = false;
						AFS.useIBL = false;
						AFS.controlIBL = false;
						AFS.AFS_IBL_DiffuseExposure = 1f;
						AFS.AFS_IBL_SpecularExposure = 1f;
						AFS.AFS_IBL_MasterExposure = 1f;
						//we want to use the same fog settings as biomes
						AFS.AFSFog_Mode = (int)FogMode.Linear;

						//we want to use global ambient light color for shadows
						AFS.AutosyncShadowColor = true;
						//we want to use a sunlight reference
						AFS.BillboardLightReference = GameWorld.Get.Sky.Components.LightSource.gameObject;
						//no billboard shadows, just fade the shadows
						AFS.BillboardAdjustToCamera = true;
						AFS.BillboardShadowEdgeFade = false;
						AFS.TreeShadowDissolve = true;
						AFS.TreeBillboardShadows = false;
						//regular animation on grass not normal animation
						AFS.GrassAnimateNormal = false;
						//we want to render grass as a single pass
						AFS.AllGrassObjectsCombined = true;
						AFS.BillboardFadeOutLength = Globals.ChunkTerrainTreeDistance;
						//we're not using camera culling unfortunately
						AFS.EnableCameraLayerCulling = false;

						AFS.Wind = new Vector4(0.85f, 0.05f, 0.4f, 1f);
						AFS.WindFrequency = 1f;
						AFS.WaveSizeForGrassshader = 2f;
						AFS.WaveSizeFoliageShader = 10f;
						AFS.WindMultiplierForGrassshader = 5f;

						//then apply the properties
						//one-time stuff first
						AFS.afsCheckColorSpace();
						AFS.afsSetupColorSpace();

						AFS.afsLightingSettings();
						AFS.afsSetupTerrainEngine();
						AFS.afsSetupGrassShader();

						AFS.afsUpdateWind();
						AFS.afsUpdateRain();
						AFS.afsAutoSyncToTerrain();
						AFS.afsUpdateTreeAndBillboardShaders();
						AFS.afsUpdateGrassTreesBillboards();
						AFS.afsSetupCameraLayerCulling();

						mInitialized = true;
				}

				public void PushDefaultFont(string defaultFont)
				{
						DefaultLabelFont = FontByName(defaultFont);

						UILabel[] labels = GameObject.FindObjectsOfType <UILabel>();
						for (int i = 0; i < labels.Length; i++) {
								if (labels[i].useDefaultLabelFont) {
										labels[i].font = DefaultLabelFont;
								}
						}
				}

				public UIFont FontByName(string fontName)
				{
						//TODO put these in an array
						switch (fontName) {
								case "PrintingPress40":
								default:
										return PrintingPress40Font;

								case "Arimo18":
										return Arimo18Font;

								case "Arimo20":
										return Arimo20Font;

								case "TrajanPro18":
										return TrajanPro18Font;

								case "SloppyHandwriting48":
										return SloppyHandwriting48Font;
						}
				}

				public UIFont NextFont(UIFont currentFont)
				{
						//TODO put these in an array
						//ugh this is terrible
						switch (currentFont.name) {
								case "PrintingPress40":
								default:
										return FontByName("Arimo18");

								case "Arimo18":
										return FontByName("Arimo20");

								case "Arimo20":
										return FontByName("TrajanPro18");

								case "TrajanPro18":
										return FontByName("SloppyHandwriting48");

								case "SloppyHandwriting48":
										return FontByName("PrintingPress40");
						}
				}

				public bool GetTerrainGroundTexture(string terrainGroundTextureName, out Texture2D terrainGroundTexture)
				{
						terrainGroundTexture = null;
						foreach (Texture2D tgt in TerrainGroundTextures) {
								if (string.Equals(tgt.name, terrainGroundTextureName)) {
										terrainGroundTexture = tgt;
										break;
								}
						}
						return terrainGroundTexture != null;
				}

				public bool GetTerrainGrassTexture(string terrainGrassTextureName, out Texture2D terrainGrassTexture)
				{
						terrainGrassTexture = null;
						foreach (Texture2D tgt in TerrainGrassTextures) {
								if (string.Equals(tgt.name, terrainGrassTextureName)) {
										terrainGrassTexture = tgt;
										break;
								}
						}
						return terrainGrassTexture != null;
				}

				public void Update()
				{
						if (mInitialized) {
								AFS.afsUpdateGrassTreesBillboards();
								AFS.afsLightingSettings();
								AFS.afsUpdateWind();
								AFS.afsUpdateRain();

								WindowsMaterial.SetColor("_ReflectColor", Color.Lerp(Color.black, RenderSettings.ambientLight, 0.5f));
								//WaterWavesMaterial.SetFloat("_AnimSpeed", (float)WorldClock.Get.TimeScale);

								//luminite stencil materials are only bright at night
								if (WorldClock.IsDay) {
										mLuminiteStencilColor = Color.Lerp(mLuminiteStencilColor, Colors.Get.LuminiteStencilColorDay, 0.05f);
								} else {
										if (Skills.Get.IsSkillInUse("Spyglass")) {
												mLuminiteStencilColor = Color.Lerp(mLuminiteStencilColor, Colors.Get.LuminiteStencilColorNightSpyglass, 0.15f);
										} else {
												mLuminiteStencilColor = Color.Lerp(mLuminiteStencilColor, Colors.Get.LuminiteStencilColorNight, 0.05f);
										}
								}
								LuminiteStencilMaterial.color = mLuminiteStencilColor;
						}
				}

				protected Color mLuminiteStencilColor;
				public Material LightProjectorMaterial;
				public Material LuminiteStencilMaterial;
				public Material BodyOfWaterMaterial;
				public Material RiverMaterial;
				public List <Material> TimedGlowMaterials = new List<Material>();
				public UIAtlas ConditionIconsAtlas;
				public UIAtlas IconsAtlas;
				public UIAtlas MapIconsAtlas;
				public UIAtlas PrimaryAtlas;
				public UIFont DyslexiaFont;
				public UIFont Arimo14Font;
				public UIFont Arimo18Font;
				public UIFont Arimo20Font;
				public UIFont BlackChancery32Font;
				public UIFont CleanHandwriting42Font;
				public UIFont MusicNotation1Font;
				public UIFont PrintingPress40Font;
				public UIFont SloppyHandwriting48Font;
				public UIFont TrajanPro18Font;
				public UIFont VerySloppyHandwriting48Font;
				public UIFont WolgastCursive72Font;
				public Material DefaultDiffuseMaterial;
				public Material WaveOverlayMaterial;
				public Material SnowOverlayMaterial;
				public Material TrailRendererMaterial;
				public Material WorldPathGroundMaterial;
				public Material BloodSplatterMaterial;
				public Material CharacterBodyMaterial;
				public Material CharacterFaceMaterial;
				public Material ItemPlacementMaterial;
				public Material ItemPlacementOutlineMaterial;
				public Material ItemPlacementOutlineCutoutMaterial;
				public Material FocusHighlightMaterial;
				public Material FocusOutlineMaterial;
				public Material FocusOutlineCutoutMaterial;
				public Material AttentionOutlineMaterial;
				public Material CraftingDoppleGangerMaterial;
				public Material InventoryRimMaterial;
				public Material InventoryRimCutoutMaterial;
				public Material WindowsMaterial;
				public Material LuminiteGlowMaterial;
				public Material WaterSurfaceMaterial;
				public Material MagicEffectMaterial;
				public Material SpellEffectProjectorMaterial;
				public Material ReceptacleProjectorMaterial;

				public static bool IsCutoutShader(Shader shader)
				{
						return shader.name.ToLower().Contains("cutout");
				}

				public Material [] MaterialsFromList(List <string> matNames)
				{
						List <Material> matList = new List <Material>();
						return matList.ToArray();
				}
		}
}