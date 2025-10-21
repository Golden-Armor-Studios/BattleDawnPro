// Golden Armor Studio - RuleTile auto generator
// Requires: com.unity.2d.tilemap.extras (RuleTile)
// Unity 2021.3+ recommended

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.AssetImporters;
#endif

public static class SpriteSlicingUtil
{
    public static Sprite[] EnsureGridSlice(string assetPath, int columns, int rows, int pixelPerUnit = 256)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) throw new System.SystemException("Could not get TextureImporter for " + assetPath);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false;
        importer.isReadable = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = pixelPerUnit;

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (tex == null)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        int tileW = tex.width / columns;
        int tileH = tex.height / rows;

        var metas = new List<SpriteMetaData>();
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                var smd = new SpriteMetaData();
                smd.name = $"tile_{y}_{x}";
                smd.rect = new Rect(x * tileW, (rows - 1 - y) * tileH, tileW, tileH);
                smd.alignment = (int)SpriteAlignment.Center;
                metas.Add(smd);
            }
        }
        importer.spritesheet = metas.ToArray();
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        var sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        return sprites;
    }
}

public class CreateGrassAndWaterTiles : EditorWindow
{
    [Serializable]
    public class GeneratorState
    {
        public Sprite WaterSprite;
        public Texture2D GrassSheet;
        public string PaletteName = "GrassPalette";
        public int Columns = 3;
        public int Rows = 3;
        public int PixelsPerUnit = 256;
    }

    private const string WindowTitle = "Grass & Water Tiles";

    [SerializeField] private GeneratorState state = new GeneratorState();

    [MenuItem("Tools/Golden Armor/Grass & Water Tiles")]
    public static void ShowWindow()
    {
        var window = GetWindow<CreateGrassAndWaterTiles>();
        window.titleContent = new GUIContent(WindowTitle);
        window.minSize = new Vector2(320, 180);
        window.Show();
    }

    public void CreateGUI()
    {
        rootVisualElement.Clear();
        rootVisualElement.style.paddingLeft = 8;
        rootVisualElement.style.paddingRight = 8;
        rootVisualElement.style.paddingTop = 8;
        rootVisualElement.Add(CreateGeneratorUI(state));
    }

    public static VisualElement CreateGeneratorUI(GeneratorState generatorState)
    {
        generatorState ??= new GeneratorState();

        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Column;
        container.style.paddingLeft = 4;
        container.style.paddingRight = 4;
        container.style.marginBottom = 6;

        var paletteField = new TextField("Palette Name")
        {
            value = generatorState.PaletteName ?? string.Empty,
            tooltip = "Folder created under Assets/Resources/MapPallet/<PaletteName>."
        };
        paletteField.RegisterValueChangedCallback(evt => generatorState.PaletteName = evt.newValue);
        container.Add(paletteField);

        var waterField = new ObjectField("Water Sprite")
        {
            objectType = typeof(Sprite),
            allowSceneObjects = false,
            value = generatorState.WaterSprite,
            tooltip = "Sprite used for the generated WaterTile asset."
        };
        waterField.RegisterValueChangedCallback(evt => generatorState.WaterSprite = evt.newValue as Sprite);
        container.Add(waterField);

        var grassField = new ObjectField("Grass Sheet Texture")
        {
            objectType = typeof(Texture2D),
            allowSceneObjects = false,
            value = generatorState.GrassSheet,
            tooltip = "Texture containing a 4x4 grid of grass sprites."
        };
        grassField.RegisterValueChangedCallback(evt => generatorState.GrassSheet = evt.newValue as Texture2D);
        container.Add(grassField);

        var help = new HelpBox("Generates WaterTile, Grass_RuleTile, and supporting prefabs inside Assets/Resources/MapPallet/<PaletteName>.", HelpBoxMessageType.Info);
        container.Add(help);

        var generateButton = new Button(() =>
        {
            try
            {
                var folder = GenerateAssets(generatorState);
                EditorUtility.DisplayDialog("Golden Armor", $"Generated tiles in {folder}.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Golden Armor", $"Failed to generate tiles:\n{ex.Message}", "OK");
                Debug.LogException(ex);
            }
        })
        {
            text = "Generate Tiles"
        };
        generateButton.style.marginTop = 8;
        container.Add(generateButton);

        return container;
    }

    private static string GenerateAssets(GeneratorState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        if (state.WaterSprite == null)
            throw new InvalidOperationException("Assign a Water Sprite before generating.");
        if (state.GrassSheet == null)
            throw new InvalidOperationException("Assign a Grass Sheet Texture before generating.");

        var paletteName = SanitizeFolderName(string.IsNullOrWhiteSpace(state.PaletteName) ? "Palette" : state.PaletteName);
        if (string.IsNullOrEmpty(paletteName))
            throw new InvalidOperationException("Palette name cannot be empty.");

        var baseFolder = $"Assets/Resources/MapPallet/{paletteName}";
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/MapPallet");
        EnsureFolder(baseFolder);
        var prefabsFolder = $"{baseFolder}/Prefabs";
        EnsureFolder(prefabsFolder);

        var waterTilePath = $"{baseFolder}/WaterTile.asset";
        var grassRuleTilePath = $"{baseFolder}/Grass_RuleTile.asset";

        var grassSheetPath = AssetDatabase.GetAssetPath(state.GrassSheet);
        if (string.IsNullOrEmpty(grassSheetPath))
            throw new InvalidOperationException("Could not resolve the Grass Sheet asset path.");

        var sprites = SpriteSlicingUtil.EnsureGridSlice(grassSheetPath, state.Columns, state.Rows, state.PixelsPerUnit);
        if (sprites == null || sprites.Length == 0)
            throw new InvalidOperationException("The grass sheet did not produce any sprites.");
        if (sprites.Length < state.Columns * state.Rows)
            Debug.LogWarning($"Expected {state.Columns * state.Rows} sprites but only found {sprites.Length}. Using available sprites.");

        var fallbackSprite = sprites.FirstOrDefault(sprite => sprite != null);
        if (fallbackSprite == null)
            throw new InvalidOperationException("The grass sheet does not contain any usable sprites.");

        Sprite center = GetSpriteOrFallback(sprites, 5, fallbackSprite);
        Sprite topLeftCorner = GetSpriteOrFallback(sprites, 0, fallbackSprite);
        Sprite topEdge = GetSpriteOrFallback(sprites, 1, fallbackSprite);
        Sprite topRightCorner = GetSpriteOrFallback(sprites, 2, fallbackSprite);
        Sprite leftEdge = GetSpriteOrFallback(sprites, 3, fallbackSprite);
        Sprite rightEdge = GetSpriteOrFallback(sprites, 5, fallbackSprite);
        Sprite bottomLeftCorner = GetSpriteOrFallback(sprites, 6, fallbackSprite);
        Sprite bottomEdge = GetSpriteOrFallback(sprites, 7, fallbackSprite);
        Sprite bottomRightCorner = GetSpriteOrFallback(sprites, 8, fallbackSprite);

        var centerPrefab = EnsureSpritePrefab($"{prefabsFolder}/Grass_Center.prefab", center);
        var topEdgePrefab = EnsureSpritePrefab($"{prefabsFolder}/Grass_Top.prefab", topEdge);
        var bottomEdgePrefab = EnsureSpritePrefab($"{prefabsFolder}/Grass_Bottom.prefab", bottomEdge);
        var leftEdgePrefab = EnsureSpritePrefab($"{prefabsFolder}/Grass_Left.prefab", leftEdge);
        var rightEdgePrefab = EnsureSpritePrefab($"{prefabsFolder}/Grass_Right.prefab", rightEdge);
        var topLeftPrefab = EnsureSpritePrefab($"{prefabsFolder}/Grass_TopLeft.prefab", topLeftCorner);
        var topRightPrefab = EnsureSpritePrefab($"{prefabsFolder}/Grass_TopRight.prefab", topRightCorner);
        var bottomLeftPrefab = EnsureSpritePrefab($"{prefabsFolder}/Grass_BottomLeft.prefab", bottomLeftCorner);
        var bottomRightPrefab = EnsureSpritePrefab($"{prefabsFolder}/Grass_BottomRight.prefab", bottomRightCorner);

        var waterTile = AssetDatabase.LoadAssetAtPath<Tile>(waterTilePath);
        if (waterTile == null)
        {
            waterTile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(waterTile, waterTilePath);
        }
        waterTile.sprite = state.WaterSprite;
        waterTile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(waterTile);

        if (AssetDatabase.LoadAssetAtPath<RuleTile>(grassRuleTilePath) != null)
            AssetDatabase.DeleteAsset(grassRuleTilePath);

        var ruleTile = ScriptableObject.CreateInstance<RuleTile>();
        ruleTile.m_DefaultSprite = center;
        ruleTile.m_DefaultGameObject = centerPrefab;
        ruleTile.m_TilingRules = new List<RuleTile.TilingRule>();

        const int This = RuleTile.TilingRule.Neighbor.This;
        const int Not = RuleTile.TilingRule.Neighbor.NotThis;
        const int Any = 0;

        void AddRule(Sprite sprite, GameObject linkedPrefab, int[] neighborMask)
        {
            var rule = new RuleTile.TilingRule
            {
                m_Sprites = new[] { sprite },
                m_GameObject = linkedPrefab,
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single,
                m_ColliderType = Tile.ColliderType.Grid,
                m_Neighbors = new List<int>(neighborMask),
                m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed
            };

            ruleTile.m_TilingRules.Add(rule);
        }

        // Neighbor order in RuleTile defaults: TL, T, TR, L, R, BL, B, BR
        AddRule(center, centerPrefab, new[] { Not, Not, Not, Not, Not, Not, Not, Not });
        AddRule(topLeftCorner, topLeftPrefab, new[] { Not, Not, Any, Not, Any, Any, Any, Any });
        AddRule(topRightCorner, topRightPrefab, new[] { Any, Not, Not, Any, Not, Any, Any, Any });
        AddRule(bottomLeftCorner, bottomLeftPrefab, new[] { Any, Any, Any, Not, Any, Any, Not, Any });
        AddRule(bottomRightCorner, bottomRightPrefab, new[] { Any, Any, Any, Any, Not, Any, Not, Any });
        AddRule(topEdge, topEdgePrefab, new[] { Any, Not, Any, This, This, Any, This, Any });
        AddRule(bottomEdge, bottomEdgePrefab, new[] { Any, This, Any, This, This, Any, Not, Any });
        AddRule(leftEdge, leftEdgePrefab, new[] { Any, This, Any, Not, This, Any, This, Any });
        AddRule(rightEdge, rightEdgePrefab, new[] { Any, This, Any, This, Not, Any, This, Any });
        AddRule(center, centerPrefab, new[] { This, This, This, This, This, This, This, This });

        AssetDatabase.CreateAsset(ruleTile, grassRuleTilePath);
        EditorUtility.SetDirty(ruleTile);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return baseFolder;
    }

    private static Sprite GetSpriteOrFallback(IReadOnlyList<Sprite> sprites, int index, Sprite fallback)
    {
        return index >= 0 && index < sprites.Count && sprites[index] != null ? sprites[index] : fallback;
    }

    private static string SanitizeFolderName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return string.Empty;
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(rawName.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? string.Empty : clean.Trim();
    }

    private static void EnsureFolder(string folderPath)
    {
        folderPath = folderPath.Replace("\\", "/");
        if (string.IsNullOrEmpty(folderPath) || folderPath == "Assets") return;
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        var parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(parent))
            parent = "Assets";

        EnsureFolder(parent);

        var folderName = Path.GetFileName(folderPath);
        if (!AssetDatabase.IsValidFolder(folderPath) && !string.IsNullOrEmpty(folderName))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static GameObject EnsureSpritePrefab(string prefabPath, Sprite sprite)
    {
        if (sprite == null) return null;

        var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var renderer = prefabRoot.GetComponent<SpriteRenderer>() ?? prefabRoot.AddComponent<SpriteRenderer>();
                if (renderer.sprite != sprite)
                {
                    renderer.sprite = sprite;
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        var temp = new GameObject(Path.GetFileNameWithoutExtension(prefabPath) ?? "RuleTilePiece");
        var sr = temp.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        var prefabAsset = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
        UnityEngine.Object.DestroyImmediate(temp);
        return prefabAsset;
    }
}
#endif
