using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class InitMap : MonoBehaviour
{
    public GameObject mapGrid;
    public Grid grid;
    public GameObject mapTilemap;
    public Tilemap tilemap;
    public Texture2D LandTexture;
    public AnimatedTile OceanTile;

    public int mapheight = 100;

    [ContextMenu("Do Something")]
    void DoSomething()
    {
        Debug.Log("Clicked");
        Map map = new Map();
        map.MapName = "Test Obj";
        map.SaveMap();
    }

    void Start()
    {
        OceanTile = Resources.Load<AnimatedTile>("MapPallet/Water");

        // GameObject mapGrid = new GameObject();
        // mapGrid.name = "mapGrid";
        // grid = mapGrid.AddComponent<Grid>();

        // GameObject mapTilemap = new GameObject();
        // mapTilemap.name = "mapTileMap";
        // tilemap = mapTilemap.AddComponent<Tilemap>();
        // tilemap.transform.SetParent(mapGrid.transform);

        mapTilemap = GameObject.Find("OceanMap");
        tilemap = mapTilemap.GetComponent<Tilemap>();
        tilemap.size = new Vector3Int(mapheight * 2, mapheight, 0);

        for (int i = 0; i < mapheight * 2; i++){
            for (int y = 0; y < mapheight; y++){
                tilemap.SetTile(new Vector3Int(i, y, 0), OceanTile); 
            }
        }

        tilemap.RefreshAllTiles();
        Debug.Log(EditorPrefs.GetString("AWS_IDTOKEN"));
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
