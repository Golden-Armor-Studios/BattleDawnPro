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
        CenterCameraOnPlanet();
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


    void CenterCameraOnPlanet()
    {
        var mainCamera = Camera.main;
        if (mainCamera == null) return;

        if (!TryGetPlanetBounds(out var planetBounds)) return;

        var target = planetBounds.center;
        mainCamera.transform.position = new Vector3(target.x, target.y, mainCamera.transform.position.z);
        ClampCameraToBounds(mainCamera, planetBounds);
    }

    GameObject FindPlanetRoot()
    {
        const string MarkerDouble = "Planet--";
        const string MarkerSingle = "Planet-";

        var allObjects = FindObjectsOfType<GameObject>(includeInactive: false);
        GameObject bestMatch = null;
        foreach (var obj in allObjects)
        {
            if (!obj.scene.IsValid()) continue;
            var name = obj.name;
            if (name.Contains(MarkerDouble) || name.Contains(MarkerSingle))
            {
                if (bestMatch == null)
                {
                    bestMatch = obj;
                    continue;
                }

                // Prefer root-level objects
                var bestIsRoot = bestMatch.transform.parent == null;
                var currentIsRoot = obj.transform.parent == null;
                if (!bestIsRoot && currentIsRoot)
                {
                    bestMatch = obj;
                    continue;
                }

                if (bestIsRoot == currentIsRoot)
                {
                    // Prefer object with larger renderer bounds if both are root or non-root
                    var bestBounds = GetApproximateBounds(bestMatch);
                    var currentBounds = GetApproximateBounds(obj);
                    if (currentBounds.size.sqrMagnitude > bestBounds.size.sqrMagnitude)
                        bestMatch = obj;
                }
            }
        }

        return bestMatch;
    }

    Bounds GetApproximateBounds(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>(includeInactive: false);
        if (renderers.Length == 0)
            return new Bounds(obj.transform.position, Vector3.zero);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    bool TryGetPlanetBounds(out Bounds bounds)
    {
        bounds = default;
        var planetRoot = FindPlanetRoot();
        if (planetRoot == null) return false;

        var allRenderers = planetRoot.GetComponentsInChildren<Renderer>(includeInactive: false);
        if (allRenderers.Length == 0)
        {
            bounds = new Bounds(planetRoot.transform.position, Vector3.zero);
            return true;
        }

        bounds = allRenderers[0].bounds;
        for (int i = 1; i < allRenderers.Length; i++)
            bounds.Encapsulate(allRenderers[i].bounds);

        return true;
    }

    void ClampCameraToBounds(Camera camera, Bounds bounds)
    {
        if (camera == null) return;
        if (bounds.size == Vector3.zero) return;

        if (!camera.orthographic)
        {
            var pos = camera.transform.position;
            pos.x = Mathf.Clamp(pos.x, bounds.min.x, bounds.max.x);
            pos.y = Mathf.Clamp(pos.y, bounds.min.y, bounds.max.y);
            camera.transform.position = pos;
            return;
        }

        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;

        float minX = bounds.min.x + halfWidth;
        float maxX = bounds.max.x - halfWidth;
        float minY = bounds.min.y + halfHeight;
        float maxY = bounds.max.y - halfHeight;

        if (minX > maxX)
        {
            float midX = (bounds.min.x + bounds.max.x) * 0.5f;
            minX = maxX = midX;
        }

        if (minY > maxY)
        {
            float midY = (bounds.min.y + bounds.max.y) * 0.5f;
            minY = maxY = midY;
        }

        var clampedPosition = camera.transform.position;
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, minX, maxX);
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, minY, maxY);
        camera.transform.position = clampedPosition;
    }

}
