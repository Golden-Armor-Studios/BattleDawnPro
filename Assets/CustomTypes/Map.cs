using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Networking;
using System.Collections;

[Serializable]
public class Map
{
    public GameTile[] OceanTiles;
    public GameTile[] LandTiles;
    public string MapName;

    public void Load(string savedData)
    {
        JsonUtility.FromJsonOverwrite(savedData, this);
    }

    public IEnumerator SaveMap()
    {
        using ( UnityWebRequest www = UnityWebRequest.Post("https://vn82uoe5p3.execute-api.us-east-1.amazonaws.com/Prod/", JsonUtility.ToJson(this), "application/json") )
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(www.error);
            }
            else
            {
                Debug.Log("Form upload complete!");
            }
        }
    }
}

[Serializable]
public class GameTile
{
    public int x;
    public int y;
    public enum TileType{
        Ocean,
        Beach,
        InLand,
    }
    public bool IsOccupied;
    public Tile ActiveTile;
}


