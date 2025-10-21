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
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Threading.Tasks;
using Auth;
using DB;

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

        Button button2 = new Button();
        button2.text = "Save Map";
        button2.style.display = DisplayStyle.Flex;
        button2.clicked += map.SaveMap;
        ButtonBox.Add(button2);

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
    

    public async void LoadMap () {
        GameObject GridObject = new GameObject();
        GridObject.name = "Planet";
        Grid Planet = GridObject.AddComponent<Grid>();
        Planet.name = "Planet-" + Id ;
        Planet.cellLayout = GridLayout.CellLayout.Rectangle;
        
        GameObject PlanetSurfaceObject = new GameObject();
        PlanetSurfaceObject.name = "PlanetSurface";
        Tilemap PlanetSurfaceTilemap = PlanetSurfaceObject.AddComponent<Tilemap>();
        PlanetSurfaceTilemap.name = "PlanetSurface";

        GameObject ClearedGroundObject = new GameObject();
        ClearedGroundObject.name = "ClearedGround";
        Tilemap ClearedGroundTilemap = ClearedGroundObject.AddComponent<Tilemap>();
        ClearedGroundTilemap.name = "ClearedGround";
        ClearedGroundTilemap.transform.position = new Vector3(ClearedGroundTilemap.transform.position.x, ClearedGroundTilemap.transform.position.y, -1);

        PlanetSurfaceObject.transform.SetParent(Planet.transform);
        ClearedGroundObject.transform.SetParent(Planet.transform);


        DocumentSnapshot MapDataSnapshot = await DB.Default.MapData.Document(Id).GetSnapshotAsync();
        MapData mapData = null;
        if (MapDataSnapshot.Exists) {
            mapData = MapDataSnapshot.ConvertTo<MapData>();
        } else {
            Debug.LogWarning($"Map data for id {Id} was not found.");
        }
        Tile planetSurfaceTile = Resources.Load<Tile>(PlanetSurface);
        if (planetSurfaceTile == null) {
            Debug.LogError($"Unable to load planet surface tile at path '{PlanetSurface}'.");
        } else {
            for (int i = 0; i < PlanetSize; i++) {
                for (int y = 0; y < PlanetSize; y++ ) {
                    PlanetSurfaceTilemap.SetTile(new Vector3Int(y,i,0), planetSurfaceTile);
                }
            }
        }
        var tilesToApply = new List<_Tile>();
        if (mapData != null)
        {
            if (mapData.Tiles != null && mapData.Tiles.Count > 0)
            {
                tilesToApply.AddRange(mapData.Tiles);
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
                        tilesToApply.AddRange(chunk.Tiles);
                    }
                }
            }
        }

        foreach (_Tile tile in tilesToApply) {
                Tile ClearedGroundTile = Resources.Load<Tile>(tile.TileName);
                if (ClearedGroundTile == null) {
                    RuleTile ClearedGroundRuleTile = Resources.Load<RuleTile>(tile.TileName);
                    if (ClearedGroundRuleTile != null) {
                        ClearedGroundTilemap.SetTile(new Vector3Int(tile.x,tile.y,0), ClearedGroundRuleTile);
                    } else {
                        Debug.LogWarning($"Unable to load tile or rule tile at path '{tile.TileName}'.");
                    }
                } else {
                    ClearedGroundTilemap.SetTile(new Vector3Int(tile.x,tile.y,0), ClearedGroundTile);
                }
            }
        PlanetSurfaceTilemap.RefreshAllTiles();




        
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
    public async void SaveMap () {

        GameObject PlanetSurfaceObject = GameObject.Find("PlanetSurface");
        Tilemap PlanetSurfaceTilemap = PlanetSurfaceObject.GetComponent<Tilemap>();

        GameObject GridObject = PlanetSurfaceObject.transform.parent.gameObject;
        Id = GridObject.name.Split("Planet-")[1]; 
        DocumentSnapshot mapSnapshot = await DB.Default.maps.Document(Id).GetSnapshotAsync();
        Map map = mapSnapshot.ConvertTo<Map>();
        Id = map.Id;
        PlanetName = map.PlanetName;
        PlanetSize = map.PlanetSize;
        PlanetSurface = map.PlanetSurface;

        GameObject ClearedGroundObject = GameObject.Find("ClearedGround");
        Tilemap ClearedGroundTilemap = ClearedGroundObject.GetComponent<Tilemap>();

        var mapDataDoc = await DB.Default.MapData.Document(Id).GetSnapshotAsync();
        MapData existingData = null;
        if (mapDataDoc.Exists)
        {
            existingData = mapDataDoc.ConvertTo<MapData>();
        }

        var chunkSize = existingData != null && existingData.ChunkSize > 0
            ? existingData.ChunkSize
            : MapData.DefaultChunkSize;

        var chunks = new Dictionary<Vector2Int, List<_Tile>>();
        int totalTiles = 0;

        for (int i = 0; i < PlanetSize; i++) {
                for (int y = 0; y < PlanetSize; y++ ) {
                    if (ClearedGroundTilemap.HasTile(new Vector3Int(y,i,0))) {
                        TileBase clearedGroundTilebase = ClearedGroundTilemap.GetTile(new Vector3Int(y,i,0));
                        _Tile tile = new _Tile();
                        tile.TileName = "MapPallet/" + clearedGroundTilebase.name;
                        tile.x = y;
                        tile.y = i;
                        totalTiles++;

                        var chunkCoord = new Vector2Int(tile.x / chunkSize, tile.y / chunkSize);
                        if (!chunks.TryGetValue(chunkCoord, out var chunkTiles))
                        {
                            chunkTiles = new List<_Tile>();
                            chunks.Add(chunkCoord, chunkTiles);
                        }
                        chunkTiles.Add(tile);
                    }
                }
        }
        
        DocumentReference docref = DB.Default.maps.Document(Id);
        DocumentReference MapDataRef = DB.Default.MapData.Document(Id);
        var chunkCollection = MapDataRef.Collection("Chunks");

        var existingChunks = await chunkCollection.GetSnapshotAsync();
        foreach (var chunk in existingChunks.Documents)
        {
            await chunk.Reference.DeleteAsync();
        }

        var chunkIds = new List<string>();
        foreach (var kvp in chunks)
        {
            var chunkId = $"{kvp.Key.x}_{kvp.Key.y}";
            chunkIds.Add(chunkId);
            await chunkCollection.Document(chunkId).SetAsync(new MapChunk { Tiles = kvp.Value });
        }
        chunkIds.Sort();

        var mapData = existingData ?? new MapData();
        mapData.ChunkSize = chunkSize;
        mapData.ChunkIds = chunkIds;
        mapData.TileCount = totalTiles;

        await docref.SetAsync(this);
        await MapDataRef.SetAsync(mapData);

    }
    public async void createMap(string MapNameText, int TilesWideInput) {
        try {
            Id = DB.Default.GenerateId(15);
            PlanetName = MapNameText;
            PlanetSize = TilesWideInput;
            PlanetSurface = "MapPallet/Ocean";
            
            DocumentReference docref = DB.Default.maps.Document(Id);
            DocumentReference MapDataRef = DB.Default.MapData.Document(Id);
            var mapData = new MapData
            {
                ChunkSize = MapData.DefaultChunkSize,
                ChunkIds = new List<string>(),
                TileCount = 0
            };
            await docref.SetAsync(this);
            await MapDataRef.SetAsync(mapData);
        } catch (FirebaseException e) {
            Debug.LogError(e);
        }
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
        public int x{ get; set; }
    [FirestoreProperty]
        public int y{ get; set; }

}
