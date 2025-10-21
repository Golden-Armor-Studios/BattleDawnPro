using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System;
using System.Collections;
using System.Collections.Generic;
using UI;
using Auth;

[Serializable]
public class GameController : MonoBehaviour
{
    private static string token;
    private GameObject PlanetTileMapObj;
    public Tilemap PlanetTileMap;
    private GameObject PlanetTile_BaseObj;
    private GameObject PlanetTile_TreesObj;
    private Sprite PlanetTile_BaseObjSprite;
    private Texture2D PlanetTile_BaseTileTexture;
    public Tile tile;
    public Tile PlanetTile_TreesTile;

    void Start()
    {
        Auth.User.Init();
        // Tile tile = Resources.Load<Tile>("MapPallet/Ocean");

        // for (int i = 0; i < 100; i++) {
        //     for (int y = 0; y < 100; y++ ) {
        //         PlanetTileMap.SetTile(new Vector3Int(y,i,0), tile);
        //     }
        // }
        
    }

    void Update()
    {
        if (ResearchUI.IsActive) {
            ResearchUI.Render();
        }
    }


}
