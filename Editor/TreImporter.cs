using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace net.koodaa.TreeItImporter.Editor
{
    [ScriptedImporter(1, "tre")]
    public class TreImporter : ScriptedImporter
    {
        [SerializeField] internal bool useNormal = true;
        [SerializeField] internal bool useRoughness = true;
        [SerializeField] internal List<MaterialOverride> materialOverrides;
        [SerializeField] internal Shader mainShader;
        [SerializeField] private Color color = Color.white;

        [Header("LOD")]
        [SerializeField] private float lod0End = 50f;
        [SerializeField] private float cullStart = 10f;

        [Header("Imposter")]
        [SerializeField] private Shader imposterShader;
        [SerializeField] private Color imposterColor = Color.white;
        [Range(0f, 1f)]
        [SerializeField] private float alphaCutoff = 0.25f;

        [Header("SpeedTree Imposter")]
        [SerializeField] private bool useSpeedTreeImposter = true;
        [SerializeField] private int imageCount = 2;
        [SerializeField] private Color imposterHue = new Color(1f, .5f, 0f, 0.1019608f);


        public override void OnImportAsset(AssetImportContext ctx)
        {
            string directory = FindFbxDirectory(ctx);
            string filename = Path.GetFileNameWithoutExtension(ctx.assetPath);

            // If the directory is not found, throw an exception
            if (directory == null)
            {
                ctx.LogImportError("Can't find any of these:\n" + string.Join("\n", PossiblePaths(ctx).Select(p => $"- {p}\\{filename}.fbx").ToList()));
                return;
            }

            // Creating settings instance
            var settings = new ImportSettings(ctx)
            {
                Directory = directory,
                Filename = filename,
                AssetPathWithoutExtension = Path.Combine(directory, filename),
                Color = color,
                ImposterColor = imposterColor,
                ImposterHue = imposterHue
            };

            // Loading the original model
            var originalModel = AssetDatabase.LoadAssetAtPath<GameObject>(settings.AssetPathWithoutExtension + ".fbx");

            // Processing materials
            Dictionary<string, Material> materialsCreated = new Dictionary<string, Material>();
            CreateMaterials(settings, originalModel, ref materialsCreated);

            // Adding material overrides to dictionary
            Dictionary<string, Material> materialsOverride = new();
            foreach (var mat in materialOverrides)
            {
                if(mat != null && mat.material != null)
                    materialsOverride.Add(mat.name, mat.material);
            }

            // Creating LOD objects
            List<GameObject> lodObjects = new List<GameObject>();
            for (int i = 1; i <= 5; i++)
            {
                string path;
                if (i == 0)
                    path = settings.AssetPathWithoutExtension;
                else
                    path = settings.AssetPathWithoutExtension + "_LOD" + i;

                if (File.Exists(path + ".fbx"))
                {
                    GameObject lodObject;
                    lodObject = LoadLodObject(settings, i);

                    if (i == 5)
                    {
                        if (useSpeedTreeImposter) // TreeIt LOD5 is always the imposter
                        {
                            lodObject = CreateSpeedtreeImposter(settings, originalModel);
                        }
                        else
                        {
                            CreateMaterials(settings, lodObject, ref materialsCreated, true);
                        }
                    }

                    if (lodObject != null)
                        lodObjects.Add(lodObject);
                }
            }


            // Prepopulating material overrides
            foreach (var materiaName in materialsCreated.Keys)
            {
                if (!materialOverrides.Any(o => o.name == materiaName))
                    materialOverrides.Add(new MaterialOverride() { name = materiaName, material = null });
            }

            // Creating MainObject
            GameObject main = new GameObject();
            main.name = filename;

            // Creating LOD objects and group
            var lodGroup = main.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;

            lod0End = Mathf.Clamp(lod0End, 0, 100);
            cullStart = Mathf.Clamp(cullStart, 0, 100);

            float start = lod0End / 100f;
            float end = cullStart / 100f;

            LOD[] lodsArray = new LOD[lodObjects.Count];
            for (int i = 0; i < lodObjects.Count; i++)
            {
                GameObject go = Instantiate(lodObjects[i], main.transform, false);
                go.name = filename + "_LOD" + i;

                var renderers = go.GetComponentsInChildren<Renderer>();
                float rate = start - ((float)i / (lodObjects.Count - 1) * (start - end));

                lodsArray[i] = new LOD(rate, renderers);
                ctx.AddObjectToAsset(filename + "_LOD" + i, go);

                // SpeedTree impostor material is already set to the imposter
                if (i == lodObjects.Count - 1 && useSpeedTreeImposter)
                    continue;

                foreach (var renderer in renderers)
                {
                    Material[] newMaterials = new Material[renderer.sharedMaterials.Length];
                    for (int j = 0; j < renderer.sharedMaterials.Length; j++)
                    {
                        Material originalMaterial = renderer.sharedMaterials[j];
                        if (materialsOverride.ContainsKey(originalMaterial.name))
                            newMaterials[j] = materialsOverride[originalMaterial.name];
                        else if (materialsCreated.ContainsKey(originalMaterial.name))
                            newMaterials[j] = materialsCreated[originalMaterial.name];
                        else
                            newMaterials[j] = originalMaterial;
                    }
                    renderer.sharedMaterials = newMaterials;
                }
            }
            lodGroup.SetLODs(lodsArray);

            // Adding Main to asset with icon
            Texture2D icon = Resources.Load<Texture2D>("TreeIt_icon");
            if (icon != null)
                ctx.AddObjectToAsset(main.name, main, icon);
            else
                ctx.AddObjectToAsset(main.name, main);

            // Adding root object to asset
            ctx.SetMainObject(main);
        }

        private void CreateMaterials(ImportSettings settings, GameObject originalModel, ref Dictionary<string, Material> materialsCreated, bool imposter = false)
        {
            Renderer[] originalRenderers = originalModel.GetComponentsInChildren<Renderer>(true);
            List<Material> originalMaterials = new List<Material>();

            // Finding all textures used by the model and generating materials
            foreach (var renderer in originalRenderers)
            {
                foreach (var originalMaterial in renderer.sharedMaterials)
                {
                    // Is material already generated
                    if (originalMaterials.Contains(originalMaterial))
                        continue;

                    Material material = CreateUrpLitMaterial(settings, settings.Directory, settings.Filename + " - " + originalMaterial.name, originalMaterial.mainTexture.name, imposter);

                    if (!materialsCreated.ContainsKey(originalMaterial.name))
                    {
                        materialsCreated.Add(originalMaterial.name, material);
                        settings.ctx.AddObjectToAsset(settings.Filename + "_" + originalMaterial.name, material);
                    }
                }
            }
        }

        private void FindDimensions(GameObject model, out float height, out float width, out float bottom)
        {
            height = 0;
            width = 0;
            bottom = 0;

            // Find tree dimensions
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                height = Mathf.Max(height, r.bounds.max.y);
                width = Mathf.Max(width, r.bounds.size.x);
                bottom = Mathf.Min(bottom, r.bounds.min.y);
            }
        }


        // Finding the directory where the fbx files are located
        private string FindFbxDirectory(AssetImportContext ctx)
        {
            string filename = Path.GetFileNameWithoutExtension(ctx.assetPath);

            foreach (var possibleDir in PossiblePaths(ctx))
            {
                if (!Directory.Exists(possibleDir))
                    continue;
                if (File.Exists(Path.Combine(possibleDir, filename + ".fbx")))
                    return possibleDir;
            }
            return null;
        }
        private IEnumerable<string> PossiblePaths(AssetImportContext ctx)
        {
            string assetDirectory = Path.GetDirectoryName(ctx.assetPath);
            yield return Path.Combine(assetDirectory, "Export");
            yield return Path.Combine(assetDirectory, "Assets");
            yield return assetDirectory;
        }


        private Texture2D AddTextureAsset(ImportSettings settings, string textureFile)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(settings.Directory, textureFile));
            if (texture != null)
                settings.ctx.AddObjectToAsset(textureFile, texture);
            return texture;
        }

        private GameObject LoadLodObject(ImportSettings settings, int i)
        {
            string filename = settings.Filename + "_LOD" + i;
            string fullPath = Path.Combine(settings.Directory, filename).Replace("\\", "/");

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath + ".fbx");

            return go;
        }

        private GameObject CreateSpeedtreeImposter(ImportSettings settings, GameObject originalModel)
        {
            string filename = settings.Filename + "_LOD5";
            GameObject lod5go = LoadLodObject(settings, 5);


            FindDimensions(originalModel, out float height, out float width, out float bottom);

            Material mat_Lod5 = CreateUrpLitMaterial(settings, settings.Directory, filename, settings.Filename + "_LOD5", true);

            BillboardAsset bb = CreateBillBoard(settings, mat_Lod5, height, width, bottom, imageCount);
            bb.material = mat_Lod5;

            var go = new GameObject();
            go.name = settings.Filename + "_LOD5";

            var bbRenderer = go.AddComponent<BillboardRenderer>();
            bbRenderer.billboard = bb;
            bbRenderer.material = mat_Lod5;

            settings.ctx.AddObjectToAsset(settings.Filename + "_LOD5_Material", mat_Lod5);
            settings.ctx.AddObjectToAsset(settings.Filename + "_LOD5_BillboardAsset", bb);
            settings.ctx.AddObjectToAsset(settings.Filename + "_LOD5", go);
            return go;
        }

        private Material CreateUrpLitMaterial(ImportSettings settigns, string path, string materialName, string textureName, bool imposter = false)
        {
            Material material;

            // Getting shaders
            if (mainShader == null)
                mainShader = Shader.Find("Universal Render Pipeline/Lit");
            if (imposterShader == null)
                imposterShader = Shader.Find("Universal Render Pipeline/Nature/SpeedTree7 Billboard");

            if (imposter && useSpeedTreeImposter)
                material = new Material(imposterShader);
            else
                material = new Material(mainShader);

            material.name = materialName;

            string texturePathAndName = Path.Combine(path, textureName);
            if (true)
            {
                Texture2D mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePathAndName + ".png");
                if (mainTex != null)
                {
                    material.SetTexture("_MainTex", mainTex);
                    material.SetTexture("_BaseMap", mainTex);

                    material.doubleSidedGI = true;
                    material.enableInstancing = true;
                    material.renderQueue = 2450;
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetFloat("_AlphaClip", 1f);
                    material.SetFloat("_AlphaToMask", 1f);
                    material.SetColor("_Color", settigns.Color);
                    material.SetColor("_BaseColor", settigns.Color);
                    material.SetFloat("_Cutoff", alphaCutoff);
                    material.SetFloat("_Glossiness", 1f);
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_Smoothness", 0f);
                    material.doubleSidedGI = true;
                }
            }

            if (imposter)
            {
                material.SetFloat("_DoubleSidedGI", 0);
                material.SetFloat("_Cull", 0);

                material.SetFloat("_Cutoff", alphaCutoff);
                material.SetFloat("_WindQuality", 0);
                material.SetColor("_Color", settigns.ImposterColor);
                material.SetColor("_BaseColor", settigns.ImposterColor);
                material.SetColor("_HueVariation", settigns.ImposterHue);
            }

            if (useNormal && File.Exists(texturePathAndName + "_Normal.png"))
            {
                // Loading normalTexture
                Texture2D normalTexture = new Texture2D(0, 0, TextureFormat.DXT5, true, false);
                normalTexture.name = textureName + "_Normal";
                normalTexture.LoadImage(File.ReadAllBytes(texturePathAndName + "_Normal.png"));

                if (normalTexture != null)
                {
                    material.SetTexture("_BumpMap", normalTexture);
                    material.EnableKeyword("_NORMALMAP");
                    settigns.ctx.AddObjectToAsset(textureName + "_Normal", normalTexture);
                }
            }

            if (useRoughness && File.Exists(texturePathAndName + "_Roughness.png"))
            {
                // Loading roughness texture for image size
                Texture2D roughnessTexture = new Texture2D(0, 0);
                roughnessTexture.LoadImage(File.ReadAllBytes(texturePathAndName + "_Roughness.png"));

                if (roughnessTexture != null)
                {
                    // Smoothness is stored to metallic alpha channel
                    Texture2D metallicTexture = new Texture2D(roughnessTexture.width, roughnessTexture.height);
                    metallicTexture.name = textureName + "_Metallic";

                    // Iterate through each pixel in the roughness texture
                    for (int y = 0; y < roughnessTexture.height; y++)
                    {
                        for (int x = 0; x < roughnessTexture.width; x++)
                        {
                            Color roughnessColor = roughnessTexture.GetPixel(x, y);
                            float metallicValue = 1.0f - roughnessColor.r;
                            metallicTexture.SetPixel(x, y, new Color(0, 0, 0, metallicValue));
                        }
                    }
                    metallicTexture.Apply();

                    // Set the texture to material
                    material.SetTexture("_MetallicGlossMap", metallicTexture);
                    material.EnableKeyword("_METALLICSPECGLOSSMAP");
                    settigns.ctx.AddObjectToAsset(textureName + "_Metallic", metallicTexture);
                }

                // Roughness texture is not used anymore
                DestroyImmediate(roughnessTexture);
            }

            //if(useTranslucency)
            //{
            //    Texture2D _translucencyTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureName + "_Translucency");
            //}
            material.enableInstancing = true;
            return material;
        }

        private BillboardAsset CreateBillBoard(ImportSettings settings, Material material, float height, float width, float bottom, int imageCount = 1)
        {
            var bb = new BillboardAsset();
            bb.name = settings.Filename + "_LOD5_billboard";
            bb.height = height;
            bb.width = width;
            bb.bottom = bottom;
            if (material != null)
            {
                bb.material = material;
                bb.material.color = Color.white;
            }

            bb.SetVertices(new Vector2[] {
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f)
        });

            bb.SetIndices(new ushort[] {
                0, 1, 3,
                0, 3, 2,
        });

            var texCoords = new List<Vector4>();
            for (var i = 0; i < imageCount; i++)
            {
                float offset = (1f / imageCount);
                texCoords.Add(new Vector4(offset * i, 0f, offset, 1f));
            }
            bb.SetImageTexCoords(texCoords);

            return bb;
        }

        private class ImportSettings
        {
            public ImportSettings(AssetImportContext ctx)
            {
                this.ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            }

            public AssetImportContext ctx { get; private set; }
            public string Directory { get; set; }
            public string Filename { get; set; }
            public string AssetPathWithoutExtension { get; set; }

            public Color ImposterColor { get; set; } = Color.white;
            public Color ImposterHue { get; set; } = new Color(1f, .5f, 0f, 0.1019608f);
            public Color Color { get; internal set; } = Color.white;
        }
    }
}
