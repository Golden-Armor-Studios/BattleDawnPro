using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.AnimatedValues;
using UnityEngine.Events;
using UnityEngine.Tilemaps;
using System.Text.RegularExpressions;
using System.Collections;
using UnityEngine.Networking;
using System.Text;
using System;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Auth;
using DB;
using System.IO;

internal static class MapControllerJson
{
    public static string Serialize(object value)
    {
        if (value == null)
        {
            return "null";
        }

        if (value is string s)
        {
            return "\"" + Escape(s) + "\"";
        }

        if (value is bool b)
        {
            return b ? "true" : "false";
        }

        if (value is Enum e)
        {
            return "\"" + Escape(e.ToString()) + "\"";
        }

        if (value is IFormattable f)
        {
            return f.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (value is IDictionary dict)
        {
            return SerializeDictionary(dict);
        }

        if (value is IEnumerable enumerable)
        {
            return SerializeEnumerable(enumerable);
        }

        return "\"" + Escape(value.ToString()) + "\"";
    }

    private static string SerializeDictionary(IDictionary dict)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        bool first = true;
        foreach (DictionaryEntry entry in dict)
        {
            if (entry.Key is not string key)
            {
                continue;
            }

            if (!first)
            {
                sb.Append(',');
            }
            first = false;
            sb.Append('"').Append(Escape(key)).Append('"').Append(':');
            sb.Append(Serialize(entry.Value));
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static string SerializeEnumerable(IEnumerable enumerable)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        bool first = true;
        foreach (var item in enumerable)
        {
            if (!first)
            {
                sb.Append(',');
            }
            first = false;
            sb.Append(Serialize(item));
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string Escape(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (char c in input)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (char.IsControl(c))
                    {
                        sb.AppendFormat("\\u{0:X4}", (int)c);
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }

        return sb.ToString();
    }
}

public class MapController : EditorWindow
{
    
    private GameObject PlanetTileMapObj;
    public Tilemap PlanetTileMap;
    private GameObject PlanetTile_BaseObj;
    private GameObject PlanetTile_TreesObj;
    private Sprite PlanetTile_BaseObjSprite;
    private Texture2D PlanetTile_BaseTileTexture;
    public Tile PlanetTile_BaseTile;
    public Tile PlanetTile_TreesTile;
    private VisualElement ViElement;
    private TextField MapName;
    private SliderInt sliderInt;

    void OnEnable()
    {

    }

    public void CreateGUI()
    {
        ViElement = rootVisualElement;

        Label label = new Label("Add and Remove Maps to the Map DB");
        ViElement.Add(label);

        MapName = new TextField();
        MapName.label = "Enter the Map Name";
        ViElement.Add(MapName);

        sliderInt = new SliderInt();
        sliderInt.lowValue = 100;
        sliderInt.highValue = 1000;
        sliderInt.direction = SliderDirection.Horizontal;
        sliderInt.showInputField = true;
        sliderInt.label = "Number of Tiles Wide";
        ViElement.Add(sliderInt);

        Button button = new Button();
        button.text = "Create Map";
        button.clicked += createMap;
        ViElement.Add(button);

        ScrollView scrollView = new ScrollView();
        scrollView.style.height = 500;
        scrollView.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
        ViElement.Add(scrollView);

        CollectionReference maps = DB.Default.maps;

        ListenerRegistration listener = maps.Listen(snapshot => {
            scrollView.Clear();
            if (snapshot == null || snapshot.Documents == null) {
                return;
            }
            foreach (DocumentSnapshot documentSnapshot in snapshot.Documents) {
            Map map = documentSnapshot.ConvertTo<Map>();
            if (map == null) {
                continue;
            }

        Box box = new Box();
        box.style.height = 80;
        box.style.display = DisplayStyle.Flex;
        box.style.flexDirection = FlexDirection.Row;
        box.style.justifyContent = Justify.SpaceBetween;
        box.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
        box.style.borderTopColor = new Color(0f, 0f, 0f, 1f);
        box.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
        scrollView.Add(box);

        Box TextBox = new Box();
        TextBox.style.height = 80;
        TextBox.style.width = 200;
        TextBox.style.display = DisplayStyle.Flex;
        TextBox.style.flexDirection = FlexDirection.Column;
        TextBox.style.justifyContent = Justify.FlexStart;
        TextBox.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
        TextBox.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
        box.Add(TextBox);

        Label MapID = new Label("Id: " + map.Id);
        MapID.style.display = DisplayStyle.Flex;
        MapID.style.flexDirection = FlexDirection.Column;
        MapID.style.justifyContent = Justify.Center;
        MapID.style.color = new Color(0f, 0f, 0f, 1f);
        TextBox.Add(MapID);

        Label Title = new Label("Planet Name: " + map.PlanetName);
        Title.style.display = DisplayStyle.Flex;
        Title.style.flexDirection = FlexDirection.Column;
        Title.style.justifyContent = Justify.Center;
        Title.style.color = new Color(0f, 0f, 0f, 1f);
        TextBox.Add(Title);

        Label MapSize = new Label("Planet Size: "+ map.PlanetSize);
        MapSize.style.display = DisplayStyle.Flex;
        MapSize.style.flexDirection = FlexDirection.Column;
        MapSize.style.justifyContent = Justify.Center;
        MapSize.style.color = new Color(0f, 0f, 0f, 1f);
        TextBox.Add(MapSize);

        Box ButtonBox = new Box();
        ButtonBox.style.height = 80;
        ButtonBox.style.width = 100;
        ButtonBox.style.display = DisplayStyle.Flex;
        ButtonBox.style.flexDirection = FlexDirection.Column;
        ButtonBox.style.justifyContent = Justify.Center;
        ButtonBox.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
        ButtonBox.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
        box.Add(ButtonBox);

        Button button1 = new Button();
        button1.text = "Load Map";
        button1.style.display = DisplayStyle.Flex;
        button1.clicked += map.LoadMap;
        ButtonBox.Add(button1);

        var saveButton = new Button(map.SaveMap)
        {
            text = "Save Map",
            style =
            {
                display = DisplayStyle.Flex
            }
        };
        ButtonBox.Add(saveButton);

        Button button3 = new Button();
        button3.text = "Delete Map";
        button3.style.display = DisplayStyle.Flex;
        button3.clicked += map.DeleteMap;
        ButtonBox.Add(button3);

        }
        });
        
    }


    async void OnGUI() {

        // lisener function and for loop
        // EditorGUILayout.BeginVertical();
        // QuerySnapshot snapshot = await DB.Default.maps.GetSnapshotAsync();
        // foreach (DocumentSnapshot document in snapshot.Documents)
        //         {
        //             Debug.Log(document.Exists);
        //             if (document.Exists)
        //             {
        //                 Map map = document.ConvertTo<Map>();
        //                 EditorGUILayout.BeginHorizontal();
        //                 GUILayout.Label("map");
        //                 if (GUILayout.Button("Delete"))
        //                 {
        //                     map.DeleteMap();
        //                 }
        //                 EditorGUILayout.EndHorizontal();
        //             }
        //     }

        // EditorGUILayout.EndVertical();

    }

    private void createMap() {
        Map map = new Map();
        Debug.Log(sliderInt.value);
        map.createMap(MapName.text, sliderInt.value);
    }

}

[FirestoreData]
public class Maps {
    [FirestoreProperty]
        public List<Map> maps { get; set; }
}

[FirestoreData]
public class Map {
    [FirestoreProperty]
        public string Id { get; set; }
    [FirestoreProperty]
        public string PlanetName { get; set; }
    [FirestoreProperty]
        public int PlanetSize { get; set; }
    [FirestoreProperty]
        public string PlanetSurface { get; set; }
    

    private static bool isLoadingMap;

    public async void LoadMap () {
        if (isLoadingMap)
        {
            Debug.LogWarning($"A map load is already running. Skipping request for '{Id}'.");
            return;
        }

        isLoadingMap = true;
        try
        {
            Debug.Log($"Loading map '{Id}'...");
            EditorUtility.DisplayProgressBar("Load Map", "Preparing scene...", 0.05f);

            GameObject GridObject = new GameObject();
            GridObject.name = "Planet";
            Grid Planet = GridObject.AddComponent<Grid>();
            Planet.name = "Planet-" + Id ;
            Planet.cellLayout = GridLayout.CellLayout.Rectangle;
            
            GameObject PlanetSurfaceObject = new GameObject();
            PlanetSurfaceObject.name = "PlanetSurface";
            Tilemap PlanetSurfaceTilemap = PlanetSurfaceObject.AddComponent<Tilemap>();
            PlanetSurfaceTilemap.name = "PlanetSurface";
            TilemapRenderer PlanetSurfaceRenderer = PlanetSurfaceObject.AddComponent<TilemapRenderer>();
            PlanetSurfaceRenderer.sortingLayerName = "Default";
            PlanetSurfaceRenderer.sortingOrder = 0;

            GameObject ClearedGroundObject = new GameObject();
            ClearedGroundObject.name = "ClearedGround";
            Tilemap ClearedGroundTilemap = ClearedGroundObject.AddComponent<Tilemap>();
            ClearedGroundTilemap.name = "ClearedGround";
            ClearedGroundTilemap.transform.position = new Vector3(ClearedGroundTilemap.transform.position.x, ClearedGroundTilemap.transform.position.y, -1);
            TilemapRenderer ClearedGroundRenderer = ClearedGroundObject.AddComponent<TilemapRenderer>();
            ClearedGroundRenderer.sortingLayerName = PlanetSurfaceRenderer.sortingLayerName;
            ClearedGroundRenderer.sortingOrder = PlanetSurfaceRenderer.sortingOrder + 1;

            PlanetSurfaceObject.transform.SetParent(Planet.transform);
            ClearedGroundObject.transform.SetParent(Planet.transform);

            EditorUtility.DisplayProgressBar("Load Map", "Fetching map data...", 0.2f);
            DocumentSnapshot MapDataSnapshot = await DB.Default.MapData.Document(Id).GetSnapshotAsync();
            MapData mapData = null;
            if (MapDataSnapshot.Exists) {
                mapData = MapDataSnapshot.ConvertTo<MapData>();
            } else {
                Debug.LogWarning($"Map data for id {Id} was not found.");
            }

            EditorUtility.DisplayProgressBar("Load Map", "Loading base surface...", 0.3f);
            Tile planetSurfaceTile = Resources.Load<Tile>(PlanetSurface);
            if (planetSurfaceTile == null) {
                // Attempt to load a sprite and wrap it in a runtime Tile if a Tile asset could not be found.
                Sprite sprite = Resources.Load<Sprite>(PlanetSurface);

                if (sprite == null && !PlanetSurface.StartsWith("MapPallet/Grass/", StringComparison.OrdinalIgnoreCase))
                {
                    string candidate = PlanetSurface.StartsWith("MapPallet/", StringComparison.OrdinalIgnoreCase)
                        ? PlanetSurface.Insert("MapPallet/".Length, "Grass/")
                        : $"MapPallet/Grass/{PlanetSurface}";
                    sprite = Resources.Load<Sprite>(candidate);
                    if (sprite != null)
                    {
                        PlanetSurface = candidate;
                    }
                }

                if (sprite != null)
                {
                    planetSurfaceTile = ScriptableObject.CreateInstance<Tile>();
                    planetSurfaceTile.sprite = sprite;
                }
            }

            if (planetSurfaceTile == null) {
                Debug.LogError($"Unable to load planet surface tile or sprite at path '{PlanetSurface}'.");
            } else {
                for (int i = 0; i < PlanetSize; i++) {
                    for (int y = 0; y < PlanetSize; y++ ) {
                        PlanetSurfaceTilemap.SetTile(new Vector3Int(y,i,0), planetSurfaceTile);
                    }

                    if (i % 16 == 0)
                    {
                        float surfaceProgress = 0.35f + 0.2f * ((float)(i + 1) / Mathf.Max(1, PlanetSize));
                        EditorUtility.DisplayProgressBar("Load Map", $"Painting planet surface... ({i + 1}/{PlanetSize})", Mathf.Clamp01(surfaceProgress));
                        await Task.Yield();
                    }
                }
            }

            EditorUtility.DisplayProgressBar("Load Map", "Resolving cleared tiles...", 0.55f);
            var tilesToApply = new List<_Tile>();
        if (mapData != null)
        {
            if (mapData.Tiles != null && mapData.Tiles.Count > 0)
            {
                tilesToApply.AddRange(NormalizeStoredTiles(mapData.Tiles));
            }
            else if (mapData.ChunkIds != null && mapData.ChunkIds.Count > 0)
            {
                var mapDataRef = DB.Default.MapData.Document(Id);
                var chunkCollection = mapDataRef.Collection("Chunks");
                    var chunkTasks = new List<Task<DocumentSnapshot>>();
                    foreach (var chunkId in mapData.ChunkIds)
                    {
                        chunkTasks.Add(chunkCollection.Document(chunkId).GetSnapshotAsync());
                    }

                    var chunkSnapshots = await Task.WhenAll(chunkTasks);
                    foreach (var chunkSnapshot in chunkSnapshots)
                    {
                    if (!chunkSnapshot.Exists) continue;
                    var chunk = chunkSnapshot.ConvertTo<MapChunk>();
                    if (chunk?.Tiles != null)
                    {
                        tilesToApply.AddRange(NormalizeStoredTiles(chunk.Tiles));
                    }
                }
            }
        }

            int applied = 0;
            int totalToApply = tilesToApply.Count;
            foreach (_Tile tile in tilesToApply) {
                var cellPosition = new Vector3Int(tile.x, tile.y, 0);
                TileBase clearedTile = ResolveStoredTile(tile);
                if (clearedTile == null) {
                    Debug.LogWarning($"Unable to resolve tile '{tile.TileName}' (asset '{tile.TileObjectPath}').");
                } else {
                    ClearedGroundTilemap.SetTile(cellPosition, clearedTile);
                    ApplyStoredTransform(ClearedGroundTilemap, cellPosition, tile.Transform);
                }

                applied++;
                if (applied % 500 == 0)
                {
                    float overlayProgress = 0.75f + 0.2f * ((float)applied / Mathf.Max(1, totalToApply));
                    EditorUtility.DisplayProgressBar("Load Map", $"Applying cleared tiles... ({applied}/{totalToApply})", Mathf.Clamp01(overlayProgress));
                    await Task.Yield();
                }
            }
            PlanetSurfaceTilemap.RefreshAllTiles();

            Debug.Log($"Loaded map '{Id}' with {totalToApply} overlay tiles.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load map '{Id}': {ex}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            isLoadingMap = false;
        }
        // Camera.main.transform.position = new Vector3(PlanetTileMap.cellBounds.center.x, PlanetTileMap.cellBounds.center.y, Camera.main.transform.position.z);
    }
    public static void UnLoadMap () {

    }
    public async void DeleteMap () {
        DocumentReference docref = DB.Default.maps.Document(Id);
        DocumentReference MapDataRef = DB.Default.MapData.Document(Id);
        await docref.DeleteAsync();
        var chunkCollection = MapDataRef.Collection("Chunks");
        var existingChunks = await chunkCollection.GetSnapshotAsync();
        foreach (var chunk in existingChunks.Documents)
        {
            await chunk.Reference.DeleteAsync();
        }
        await MapDataRef.DeleteAsync();
    }
    public async void createMap(string MapNameText, int TilesWideInput) {
        try {
            Id = DB.Default.GenerateId(15);
            PlanetName = MapNameText;
            PlanetSize = TilesWideInput;
            if (string.IsNullOrEmpty(PlanetSurface))
            {
                PlanetSurface = "MapPallet/Grass/Ocean";
            }
        } catch (FirebaseException e) {
            Debug.LogError(e);
        }
    }

    private static bool isSavingMap;

    public void SaveMap()
    {
        SaveMap(this);
    }

    public async void SaveMap(Map map)
    {
        DB.Default.Init();
        if (DB.Default.maps == null || DB.Default.MapData == null)
        {
            Debug.LogError("Firestore map collections are not initialised. Sign in before saving the map.");
            return;
        }

        if (isSavingMap)
        {
            Debug.LogWarning("A map save is already in progress. Please wait until it finishes.");
            return;
        }

        isSavingMap = true;

        try
        {
            EditorUtility.DisplayProgressBar("Save Map", "Gathering tiles...", 0.1f);

            var planetSurfaceObject = GameObject.Find("PlanetSurface");
            var clearedGroundObject = GameObject.Find("ClearedGround");
            if (planetSurfaceObject == null || clearedGroundObject == null)
            {
                Debug.LogError("Required tilemap objects not found.");
                return;
            }
            var planetSurfaceTilemap = planetSurfaceObject.GetComponent<Tilemap>();
            var clearedGroundTilemap = clearedGroundObject.GetComponent<Tilemap>();
            if (planetSurfaceTilemap == null || clearedGroundTilemap == null)
            {
                Debug.LogError("Required tilemap components missing.");
                return;
            }

            GameObject gridObject = planetSurfaceObject.transform.parent != null
                ? planetSurfaceObject.transform.parent.gameObject
                : null;
            Id = ExtractMapIdFromGrid(gridObject);
            if (string.IsNullOrEmpty(Id))
            {
                Debug.LogError("Unable to determine map id from the grid object name.");
                return;
            }

            DocumentSnapshot mapSnapshot = await DB.Default.maps.Document(Id).GetSnapshotAsync();
            Map mapDocument = mapSnapshot.Exists ? mapSnapshot.ConvertTo<Map>() : null;
            if (mapDocument != null)
            {
                Id = !string.IsNullOrEmpty(mapSnapshot.Id) ? mapSnapshot.Id : Id;
                map.PlanetName = mapDocument.PlanetName;
                map.PlanetSize = mapDocument.PlanetSize;
                map.PlanetSurface = mapDocument.PlanetSurface;
            }
            else if (map.PlanetSize <= 0)
            {
                Debug.LogWarning($"Map '{Id}' was not found in Firestore; retaining current planet size {map.PlanetSize}.");
            }

            var mapDataDoc = await DB.Default.MapData.Document(Id).GetSnapshotAsync();
            MapData existingData = mapDataDoc.Exists ? mapDataDoc.ConvertTo<MapData>() : null;
            int chunkSize = existingData != null && existingData.ChunkSize > 0
                ? existingData.ChunkSize
                : MapData.DefaultChunkSize;

            var chunks = new Dictionary<Vector2Int, List<_Tile>>();
            int totalTiles = 0;

            void AddTile(Vector3Int cell, TileBase tileBase, string tileLayer, Tilemap sourceTilemap)
            {
                if (tileBase == null)
                {
                    return;
                }

                string resourcePath = ResolveTileResourcePath(tileBase);
                string assetPath = ResolveTileAssetPath(tileBase);
                string serializedName = !string.IsNullOrEmpty(resourcePath) ? resourcePath : tileBase.name;

                List<double> transform = null;
                if (TrySerializeTransform(sourceTilemap, cell, out var serializedTransform))
                {
                    transform = serializedTransform;
                }

                var coord = new Vector2Int(Mathf.FloorToInt((float)cell.x / chunkSize), Mathf.FloorToInt((float)cell.y / chunkSize));
                if (!chunks.TryGetValue(coord, out var tiles))
                {
                    tiles = new List<_Tile>();
                    chunks.Add(coord, tiles);
                }

                tiles.Add(new _Tile
                {
                    TileName = serializedName,
                    TileObjectPath = assetPath,
                    TileLayer = tileLayer,
                    Transform = transform,
                    x = cell.x,
                    y = cell.y
                });

                totalTiles++;
            }

            planetSurfaceTilemap.CompressBounds();
            clearedGroundTilemap.CompressBounds();

            var overlayBounds = clearedGroundTilemap.cellBounds;
            for (int x = overlayBounds.xMin; x < overlayBounds.xMax; x++)
            {
                for (int y = overlayBounds.yMin; y < overlayBounds.yMax; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    TileBase tileBase = clearedGroundTilemap.GetTile(cell);
                    if (tileBase == null)
                    {
                        continue;
                    }
                    AddTile(cell, tileBase, "Overlay", clearedGroundTilemap);
                }
            }

            bool cancelRequested = false;

            bool UpdateProgress(string message, float progress)
            {
                if (cancelRequested)
                {
                    return true;
                }

                if (EditorUtility.DisplayCancelableProgressBar("Save Map", message, Mathf.Clamp01(progress)))
                {
                    cancelRequested = true;
                    Debug.LogWarning("Save Map cancelled by user.");
                    return true;
                }

                return false;
            }

            UpdateProgress("Preparing payload...", 0.6f);
            if (cancelRequested)
            {
                return;
            }

            var chunkPayloads = new List<(Dictionary<string, object> payload, int tileCount)>();
            foreach (var kvp in chunks)
            {
                var tiles = new List<Dictionary<string, object>>();
                foreach (var tile in kvp.Value)
                {
                    var tilePayload = new Dictionary<string, object>
                    {
                        {"x", tile.x},
                        {"y", tile.y},
                        {"TileName", tile.TileName},
                        {"TileObjectPath", tile.TileObjectPath ?? (object)null},
                        {"TileLayer", string.IsNullOrEmpty(tile.TileLayer) ? "Overlay" : tile.TileLayer}
                    };

                    if (tile.Transform != null && tile.Transform.Count == 16)
                    {
                        tilePayload["transform"] = tile.Transform;
                    }

                    tiles.Add(tilePayload);
                }

                var payload = new Dictionary<string, object>
                {
                    {"id", $"{kvp.Key.x}_{kvp.Key.y}"},
                    {"Tiles", tiles},
                    {"tiles", tiles}
                };

                chunkPayloads.Add((payload, tiles.Count));
            }

            var chunkIds = chunkPayloads
                .Select(chunk => chunk.payload.TryGetValue("id", out var idObj) ? idObj as string : null)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            Debug.Log($"SaveMap preparing {chunkPayloads.Count} chunks and {totalTiles} tiles (chunkSize {chunkSize}).");

            const int MaxTilesPerRequest = 1000;
            var chunkGroups = new List<List<Dictionary<string, object>>>();
            var currentGroup = new List<Dictionary<string, object>>();
            int currentTileCount = 0;

            foreach (var (payload, tileCount) in chunkPayloads)
            {
                int effectiveCount = Mathf.Max(tileCount, 1);
                if (effectiveCount >= MaxTilesPerRequest)
                {
                    if (currentGroup.Count > 0)
                    {
                        chunkGroups.Add(currentGroup);
                        currentGroup = new List<Dictionary<string, object>>();
                        currentTileCount = 0;
                    }
                    chunkGroups.Add(new List<Dictionary<string, object>> { payload });
                    continue;
                }

                if (currentTileCount + effectiveCount > MaxTilesPerRequest && currentGroup.Count > 0)
                {
                    chunkGroups.Add(currentGroup);
                    currentGroup = new List<Dictionary<string, object>>();
                    currentTileCount = 0;
                }

                currentGroup.Add(payload);
                currentTileCount += effectiveCount;
            }

            if (currentGroup.Count > 0)
            {
                chunkGroups.Add(currentGroup);
            }

            Debug.Log($"SaveMap grouping into {chunkGroups.Count} batches (max {MaxTilesPerRequest} tiles per batch).");

            var user = await Auth.User.EnsureLoggedInAsync();
            if (user == null)
            {
                throw new InvalidOperationException("User must be signed in before saving the map.");
            }

            string idToken = await user.TokenAsync(true);
            string projectId = Auth.User.app?.Options?.ProjectId
                ?? FirebaseApp.DefaultInstance?.Options?.ProjectId
                ?? DB.Default.Database?.App?.Options?.ProjectId;

            if (string.IsNullOrEmpty(projectId))
            {
                throw new InvalidOperationException("Unable to determine Firebase project id for saveMap call.");
            }

            string url = $"https://us-central1-{projectId}.cloudfunctions.net/saveMap";

            async Task<bool> PostBatchAsync(List<Dictionary<string, object>> chunkBatch, bool includeChunkIdList, bool deleteMissing, int batchIndex, int totalBatches)
            {
                var requestPayload = new Dictionary<string, object>
                {
                    {"mapId", Id},
                    {"planetName", PlanetName ?? string.Empty},
                    {"planetSurface", PlanetSurface ?? string.Empty},
                    {"planetSize", PlanetSize},
                    {"chunkSize", chunkSize},
                    {"tileCount", totalTiles},
                    {"chunks", chunkBatch},
                    {"deleteMissingChunks", deleteMissing}
                };

                if (includeChunkIdList)
                {
                    requestPayload["chunkIds"] = chunkIds;
                    Debug.Log($"SaveMap -> including {chunkIds.Count} chunk ids in metadata call.");
                }

                #if UNITY_EDITOR
                Debug.Log($"SaveMap -> sending batch {batchIndex}/{totalBatches}: mapId='{Id}', chunks={chunkBatch.Count}, includeChunkIds={includeChunkIdList}, deleteMissing={deleteMissing}");
                #endif

                string statusLabel = chunkBatch.Count > 0
                    ? $"Uploading chunk batch {batchIndex}/{totalBatches} ({chunkBatch.Count} chunks)"
                    : "Finalising map metadata";
                float progress = 0.6f + 0.4f * (batchIndex / (float)totalBatches);
                if (UpdateProgress(statusLabel, progress))
                {
                    return false;
                }

                var callablePayload = new Dictionary<string, object>
                {
                    {"data", requestPayload}
                };

                string jsonBody = MapControllerJson.Serialize(callablePayload);

                using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {idToken}");

                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        string responseText = request.downloadHandler?.text;
                        Debug.LogError($"SaveMap batch {batchIndex}/{totalBatches} failed. Payload: {jsonBody}");
                        string message = $"saveMap request failed ({request.responseCode}): {request.error}\n{responseText}";
                        throw new InvalidOperationException(message);
                    }

                    Debug.Log($"Cloud function saveMap batch {batchIndex}/{totalBatches} response: {request.downloadHandler?.text}");
                }

                return true;
            }

            int totalRequests = chunkGroups.Count + 1;
            if (totalRequests <= 0)
            {
                totalRequests = 1;
            }

            int requestNumber = 0;
            foreach (var batch in chunkGroups)
            {
                requestNumber++;
                if (!await PostBatchAsync(batch, false, false, requestNumber, totalRequests))
                {
                    break;
                }
            }

            if (!cancelRequested)
            {
                requestNumber++;
                await PostBatchAsync(
                    new List<Dictionary<string, object>>(),
                    true,
                    true,
                    requestNumber,
                    totalRequests
                );
            }
            else
            {
                Debug.LogWarning("Save Map cancelled before completion. No further batches sent.");
                return;
            }

            if (!cancelRequested)
            {
                UpdateProgress("Completed", 1f);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
        finally
        {
            isSavingMap = false;
            EditorUtility.ClearProgressBar();
        }
    }

    private static string ResolveTileResourcePath(TileBase tileBase)
    {
        if (tileBase == null)
        {
            return null;
        }

        string assetPath = AssetDatabase.GetAssetPath(tileBase);
        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

        assetPath = assetPath.Replace("\\", "/");
        const string resourcesToken = "/Resources/";
        int index = assetPath.IndexOf(resourcesToken, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        int start = index + resourcesToken.Length;
        string relative = assetPath.Substring(start);
        relative = Path.ChangeExtension(relative, null);
        return relative?.Replace("\\", "/");
    }

    private static string ResolveTileAssetPath(TileBase tileBase)
    {
        if (tileBase == null)
        {
            return null;
        }

        string assetPath = AssetDatabase.GetAssetPath(tileBase);
        return string.IsNullOrEmpty(assetPath) ? null : assetPath.Replace("\\", "/");
    }

    private static IEnumerable<_Tile> NormalizeStoredTiles(IEnumerable<_Tile> tiles)
    {
        if (tiles == null)
        {
            yield break;
        }

        foreach (var tile in tiles)
        {
            if (tile == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(tile.TileName) && !string.IsNullOrEmpty(tile.TileObjectPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<TileBase>(tile.TileObjectPath);
                string resourcePath = ResolveTileResourcePath(asset);
                if (!string.IsNullOrEmpty(resourcePath))
                {
                    tile.TileName = resourcePath;
                }
            }

            yield return tile;
        }
    }

    private static TileBase ResolveStoredTile(_Tile tile)
    {
        if (tile == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(tile.TileObjectPath))
        {
            TileBase assetTile = AssetDatabase.LoadAssetAtPath<TileBase>(tile.TileObjectPath);
            if (assetTile != null)
            {
                return assetTile;
            }
        }

        if (!string.IsNullOrEmpty(tile.TileName))
        {
            TileBase resourceTile = LoadTileFromResources(tile.TileName);
            if (resourceTile != null)
            {
                return resourceTile;
            }
        }

        return null;
    }

    private static TileBase LoadTileFromResources(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName))
        {
            return null;
        }

        TileBase tile = Resources.Load<TileBase>(resourceName);
        if (tile != null)
        {
            return tile;
        }

        tile = Resources.Load<Tile>(resourceName);
        if (tile != null)
        {
            return tile;
        }

        RuleTile ruleTile = Resources.Load<RuleTile>(resourceName);
        if (ruleTile != null)
        {
            return ruleTile;
        }

        if (resourceName.StartsWith("MapPallet/", StringComparison.OrdinalIgnoreCase) &&
            !resourceName.StartsWith("MapPallet/Grass/", StringComparison.OrdinalIgnoreCase))
        {
            string candidate = resourceName.Insert("MapPallet/".Length, "Grass/");
            tile = Resources.Load<TileBase>(candidate);
            if (tile != null)
            {
                return tile;
            }

            ruleTile = Resources.Load<RuleTile>(candidate);
            if (ruleTile != null)
            {
                return ruleTile;
            }
        }

        return null;
    }

    private static void ApplyStoredTransform(Tilemap tilemap, Vector3Int cell, List<double> transform)
    {
        if (tilemap == null)
        {
            return;
        }

        tilemap.SetTileFlags(cell, TileFlags.None);

        if (transform != null && transform.Count == 16 && TryDeserializeTransform(transform, out var matrix) && !IsIdentityMatrix(matrix))
        {
            tilemap.SetTransformMatrix(cell, matrix);
        }
        else
        {
            tilemap.SetTransformMatrix(cell, Matrix4x4.identity);
        }
    }

    private static bool TrySerializeTransform(Tilemap tilemap, Vector3Int cell, out List<double> serialized)
    {
        serialized = null;
        if (tilemap == null)
        {
            return false;
        }

        Matrix4x4 matrix = tilemap.GetTransformMatrix(cell);
        if (IsIdentityMatrix(matrix))
        {
            return false;
        }

        serialized = SerializeMatrix(matrix);
        return true;
    }

    private static bool IsIdentityMatrix(Matrix4x4 matrix, float epsilon = 0.0001f)
    {
        return Mathf.Abs(matrix.m00 - 1f) < epsilon &&
               Mathf.Abs(matrix.m11 - 1f) < epsilon &&
               Mathf.Abs(matrix.m22 - 1f) < epsilon &&
               Mathf.Abs(matrix.m33 - 1f) < epsilon &&
               Mathf.Abs(matrix.m01) < epsilon &&
               Mathf.Abs(matrix.m02) < epsilon &&
               Mathf.Abs(matrix.m03) < epsilon &&
               Mathf.Abs(matrix.m10) < epsilon &&
               Mathf.Abs(matrix.m12) < epsilon &&
               Mathf.Abs(matrix.m13) < epsilon &&
               Mathf.Abs(matrix.m20) < epsilon &&
               Mathf.Abs(matrix.m21) < epsilon &&
               Mathf.Abs(matrix.m23) < epsilon &&
               Mathf.Abs(matrix.m30) < epsilon &&
               Mathf.Abs(matrix.m31) < epsilon &&
               Mathf.Abs(matrix.m32) < epsilon;
    }

    private static List<double> SerializeMatrix(Matrix4x4 matrix)
    {
        return new List<double>
        {
            matrix.m00, matrix.m01, matrix.m02, matrix.m03,
            matrix.m10, matrix.m11, matrix.m12, matrix.m13,
            matrix.m20, matrix.m21, matrix.m22, matrix.m23,
            matrix.m30, matrix.m31, matrix.m32, matrix.m33
        };
    }

    private static bool TryDeserializeTransform(IList<double> values, out Matrix4x4 matrix)
    {
        matrix = Matrix4x4.identity;
        if (values == null || values.Count != 16)
        {
            return false;
        }

        matrix.m00 = (float)values[0];
        matrix.m01 = (float)values[1];
        matrix.m02 = (float)values[2];
        matrix.m03 = (float)values[3];
        matrix.m10 = (float)values[4];
        matrix.m11 = (float)values[5];
        matrix.m12 = (float)values[6];
        matrix.m13 = (float)values[7];
        matrix.m20 = (float)values[8];
        matrix.m21 = (float)values[9];
        matrix.m22 = (float)values[10];
        matrix.m23 = (float)values[11];
        matrix.m30 = (float)values[12];
        matrix.m31 = (float)values[13];
        matrix.m32 = (float)values[14];
        matrix.m33 = (float)values[15];

        return true;
    }

    private static string ExtractMapIdFromGrid(GameObject gridObject)
    {
        if (gridObject == null)
        {
            return null;
        }

        string name = gridObject.name;
        const string prefix = "Planet-";
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return name.Substring(prefix.Length);
        }

        return name;
    }



}

[FirestoreData]
public class MapData
{
    public const int DefaultChunkSize = 32;

    [FirestoreProperty]
    public List<_Tile> Tiles { get; set; }

    [FirestoreProperty]
    public List<string> ChunkIds { get; set; } = new List<string>();

    [FirestoreProperty]
    public int ChunkSize { get; set; } = DefaultChunkSize;

    [FirestoreProperty]
    public int TileCount { get; set; }
}

[FirestoreData]
public class MapChunk
{
    [FirestoreProperty]
    public List<_Tile> Tiles { get; set; }
}

[FirestoreData]
public class _Tile {
    [FirestoreProperty]
        public string Id { get; set; }
    [FirestoreProperty]
        public string TileName { get; set; }
    [FirestoreProperty]
        public string TileObjectPath { get; set; }
    [FirestoreProperty]
        public string TileLayer { get; set; }
    [FirestoreProperty]
        public List<double> Transform { get; set; }
    [FirestoreProperty]
        public int x{ get; set; }
    [FirestoreProperty]
        public int y{ get; set; }

}
