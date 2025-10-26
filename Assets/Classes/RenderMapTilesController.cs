using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.Tilemaps;
using static MapTileLayers;
using Auth;
using System.Linq;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class RenderMapTilesController : MonoBehaviour
{
    private const string SurfaceTilemapName = "SurfaceTilemap";
    private const string OverlayTilemapName = "OverlayTilemap";
    private const int DefaultChunkSize = 32;

    [Header("Map Source")]
    [Tooltip("Firestore document id for the map to render.")]
    [SerializeField]
    private string mapId;

    [Tooltip("Automatically fetch and render the assigned map when the component starts.")]
    [SerializeField]
    private bool loadOnStart = true;

    [Header("Rendering")]
    [Tooltip("Number of extra tiles to render beyond the camera view on each side.")]
    [SerializeField]
    [Min(0)]
    private int tilePadding = 2;

    [Tooltip("World-space size of a single tile. Must match the tile assets used by the map.")]
    [SerializeField]
    [Min(0.01f)]
    private float tileSize = 1f;

    [Tooltip("Optional parent used to host the runtime tilemap objects. Leave empty to parent under the camera.")]
    [SerializeField]
    private Transform tileRoot;

    [Tooltip("Minimum movement (world units) before the visible tile window is recomputed.")]
    [SerializeField]
    [Min(0f)]
    private float movementThreshold = 0.05f;

    [Header("Camera Settings")]
    [SerializeField]
    [Min(0.01f)]
    private float minCameraSize = 2.93f;

    [SerializeField]
    [Min(0.01f)]
    private float maxCameraSize = 4.72f;

    [SerializeField]
    [Min(0.01f)]
    private float defaultCameraSize = 4.72f;

    private Camera targetCamera;
    private bool isOrthographic;
    private bool mapLoaded;
    private bool isLoading;

    private Grid grid;
    private Tilemap surfaceTilemap;
    private Tilemap overlayTilemap;

    private readonly Dictionary<Vector2Int, TileVisualData> overrideTiles = new Dictionary<Vector2Int, TileVisualData>();
    private readonly Dictionary<string, TileBase> tileCache = new Dictionary<string, TileBase>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GameObject> objectCache = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Vector3Int> activeSurfaceCells = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> activeOverlayCells = new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, ActiveTileObject> activeTileObjects = new Dictionary<Vector3Int, ActiveTileObject>();
    private readonly List<Vector3Int> scratchCells = new List<Vector3Int>();
    private readonly Dictionary<string, MapCacheEntry> mapCache = new Dictionary<string, MapCacheEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CameraOverride> cameraOverrides = new Dictionary<string, CameraOverride>(StringComparer.OrdinalIgnoreCase);

    private BoundsInt currentViewBounds;
    private string baseTilePath;
    private TileBase cachedBaseTile;
    private int planetSize;
    private int chunkSize = DefaultChunkSize;
    private Vector3 lastCameraPosition = Vector3.positiveInfinity;
    private float lastOrthographicSize = float.NaN;
    private bool cameraPositioned;
    private string activeMapId;
    private MapCacheEntry activeCacheEntry;
    private CameraOverride? activeCameraOverride;
    private bool mapVisible = true;

    private static readonly int SurfaceSortingOrder = 0;
    private static readonly int OverlaySortingOrder = 1;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            Debug.LogError($"{nameof(RenderMapTilesController)} requires a Camera reference. Disabling component.");
            enabled = false;
            return;
        }

        isOrthographic = targetCamera.orthographic;
        EnsureTileInfrastructure();

        if (isOrthographic)
        {
            targetCamera.orthographicSize = Mathf.Clamp(defaultCameraSize, minCameraSize, maxCameraSize);
        }
    }

    private void OnValidate()
    {
        if (maxCameraSize < minCameraSize)
        {
            maxCameraSize = minCameraSize;
        }
        defaultCameraSize = Mathf.Clamp(defaultCameraSize, minCameraSize, maxCameraSize);
    }

    private async void Start()
    {
        if (loadOnStart && !string.IsNullOrEmpty(mapId))
        {
            await LoadMapAsync();
        }
    }

    private void LateUpdate()
    {
        if (!mapLoaded || targetCamera == null || !mapVisible)
        {
            return;
        }

        bool movedEnough = (targetCamera.transform.position - lastCameraPosition).sqrMagnitude > movementThreshold * movementThreshold;
        bool zoomChanged = isOrthographic && !Mathf.Approximately(targetCamera.orthographicSize, lastOrthographicSize);

        if (movedEnough || zoomChanged)
        {
            UpdateVisibleTiles(force: false);
        }
    }

    public async Task<bool> LoadMapAsync(string explicitMapId = null)
    {
        if (isLoading)
        {
            Debug.LogWarning("Map load already in progress; skipping additional request.");
            return false;
        }

        if (!string.IsNullOrEmpty(explicitMapId))
        {
            mapId = explicitMapId;
        }

        string targetMapId = mapId?.Trim();
        if (string.IsNullOrEmpty(targetMapId))
        {
            Debug.LogWarning($"{nameof(RenderMapTilesController)} cannot load map data because no map id was provided.");
            return false;
        }

        if (!await EnsureFirestoreReadyAsync())
        {
            return false;
        }

        isLoading = true;
        bool success = false;

        try
        {
            EnsureTileInfrastructure();
            ResetRuntimeState();

            activeMapId = targetMapId;
            activeCameraOverride = GetCameraOverrideFor(targetMapId);

            if (mapCache.TryGetValue(targetMapId, out var cachedEntry))
            {
                ApplyCacheEntry(cachedEntry);
                if (mapVisible)
                {
                    UpdateVisibleTiles(force: true);
                }
                else
                {
                    lastCameraPosition = Vector3.positiveInfinity;
                    lastOrthographicSize = float.NaN;
                }
                success = true;
                return success;
            }

            var mapSnapshot = await DB.Default.maps.Document(targetMapId).GetSnapshotAsync();
            if (!mapSnapshot.Exists)
            {
                Debug.LogWarning($"Map document '{targetMapId}' was not found.");
                return false;
            }

            var mapDocument = mapSnapshot.ConvertTo<FirestoreMapDocument>();
            if (mapDocument != null && !string.IsNullOrWhiteSpace(mapDocument.PlanetName))
            {
                SyncCameraOverrideKeys(mapDocument.PlanetName, targetMapId);
                activeCameraOverride = GetCameraOverrideFor(targetMapId);
            }
            planetSize = Mathf.Max(0, mapDocument?.PlanetSize ?? 0);
            baseTilePath = NormalizeResourcePath(mapDocument?.PlanetSurface) ?? mapDocument?.PlanetSurface ?? string.Empty;
            cachedBaseTile = null;

            var mapDataSnapshot = await DB.Default.MapData.Document(targetMapId).GetSnapshotAsync();
            if (mapDataSnapshot.Exists)
            {
                var mapData = mapDataSnapshot.ConvertTo<FirestoreMapDataDocument>();
                if (mapData != null)
                {
                    chunkSize = Mathf.Max(1, mapData.ChunkSize);
                    if (mapData.Tiles != null && mapData.Tiles.Count > 0)
                    {
                        foreach (var tile in mapData.Tiles)
                        {
                            UpsertTileVisualData(tile);
                        }
                    }
                    else if (mapData.ChunkIds != null && mapData.ChunkIds.Count > 0)
                    {
                        await LoadChunksAsync(mapData.ChunkIds);
                    }
                }
            }

            mapLoaded = true;
            cameraPositioned = false;
            lastCameraPosition = Vector3.positiveInfinity;
            lastOrthographicSize = float.NaN;
            if (mapVisible)
            {
                UpdateVisibleTiles(force: true);
            }
            CacheCurrentMap(targetMapId, mapDocument?.PlanetName);
            success = true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load map '{targetMapId}'. {ex}");
            ResetRuntimeState();
        }
        finally
        {
            isLoading = false;
        }

        return success;
    }

    public async Task<bool> LoadMapByNameAsync(string mapName)
    {
        if (isLoading)
        {
            Debug.LogWarning("A map load is already running. Warp request will be ignored.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(mapName))
        {
            Debug.LogWarning("Cannot warp to an empty map name.");
            return false;
        }

        if (!await EnsureFirestoreReadyAsync())
        {
            return false;
        }

        try
        {
            string trimmedName = mapName.Trim();

            // First allow direct map id usage.
            var directSnapshot = await DB.Default.maps.Document(trimmedName).GetSnapshotAsync();
            DocumentSnapshot targetDocument = directSnapshot.Exists ? directSnapshot : null;

            if (targetDocument == null)
            {
                Query query = DB.Default.maps.WhereEqualTo("PlanetName", trimmedName).Limit(1);
                QuerySnapshot snapshot = await query.GetSnapshotAsync();
                targetDocument = snapshot?.Documents?.FirstOrDefault(doc => doc.Exists);
            }

            if (targetDocument == null || !targetDocument.Exists)
            {
                Debug.LogWarning($"Map with name or id '{trimmedName}' was not found in Firestore.");
                return false;
            }

            Debug.Log($"Warp loading map '{trimmedName}' (document id '{targetDocument.Id}').");
            SyncCameraOverrideKeys(trimmedName, targetDocument.Id);

            bool loaded = await LoadMapAsync(targetDocument.Id);
            if (!loaded)
            {
                Debug.LogWarning($"Map '{trimmedName}' was found but failed to load.");
            }
            return loaded;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load map by name '{mapName}'. {ex}");
            return false;
        }
    }

    public void SetMapVisibility(bool visible)
    {
        mapVisible = visible;
        ApplyMapVisibility(visible);

        if (visible && mapLoaded)
        {
            lastCameraPosition = Vector3.positiveInfinity;
            lastOrthographicSize = float.NaN;
            UpdateVisibleTiles(force: true);
        }
    }

    private void ApplyMapVisibility(bool visible)
    {
        EnsureTileInfrastructure();

        if (grid != null)
        {
            grid.enabled = visible;
        }

        if (surfaceTilemap != null)
        {
            surfaceTilemap.gameObject.SetActive(visible);
        }

        if (overlayTilemap != null)
        {
            overlayTilemap.gameObject.SetActive(visible);
        }

        foreach (var kvp in activeTileObjects)
        {
            if (kvp.Value?.Instance != null)
            {
                kvp.Value.Instance.SetActive(visible);
            }
        }
    }

    public void SetMapCameraOverride(string mapIdentifier, Vector3 worldPosition, float orthographicSize)
    {
        if (string.IsNullOrWhiteSpace(mapIdentifier) || orthographicSize <= 0f)
        {
            return;
        }

        string key = mapIdentifier.Trim();
        var overrideData = new CameraOverride
        {
            Position = worldPosition,
            Size = ClampCameraSize(Mathf.Max(orthographicSize, 0.01f))
        };

        cameraOverrides[key] = overrideData;

        foreach (var entry in mapCache.Values)
        {
            if (!string.IsNullOrEmpty(entry.MapName) && string.Equals(entry.MapName, key, StringComparison.OrdinalIgnoreCase))
            {
                cameraOverrides[entry.MapId] = overrideData;
                entry.CameraStart = overrideData;
            }
            else if (!string.IsNullOrEmpty(entry.MapId) && string.Equals(entry.MapId, key, StringComparison.OrdinalIgnoreCase))
            {
                entry.CameraStart = overrideData;
            }
        }

        if (!string.IsNullOrEmpty(activeMapId) && string.Equals(activeMapId, key, StringComparison.OrdinalIgnoreCase))
        {
            activeCameraOverride = overrideData;
            cameraPositioned = false;
            UpdateVisibleTiles(force: true);
        }
        else if (!string.IsNullOrEmpty(activeMapId))
        {
            foreach (var entry in mapCache.Values)
            {
                if (!string.IsNullOrEmpty(entry.MapName) && string.Equals(entry.MapName, key, StringComparison.OrdinalIgnoreCase) && string.Equals(entry.MapId, activeMapId, StringComparison.OrdinalIgnoreCase))
                {
                    activeCameraOverride = overrideData;
                    cameraPositioned = false;
                    UpdateVisibleTiles(force: true);
                    break;
                }
            }
        }
    }

    public void ClearMapCameraOverride(string mapIdentifier)
    {
        if (string.IsNullOrWhiteSpace(mapIdentifier))
        {
            return;
        }

        string key = mapIdentifier.Trim();
        cameraOverrides.Remove(key);

        foreach (var entry in mapCache.Values)
        {
            if (!string.IsNullOrEmpty(entry.MapName) && string.Equals(entry.MapName, key, StringComparison.OrdinalIgnoreCase))
            {
                entry.CameraStart = null;
                cameraOverrides.Remove(entry.MapId);
            }

            if (!string.IsNullOrEmpty(entry.MapId) && string.Equals(entry.MapId, key, StringComparison.OrdinalIgnoreCase))
            {
                entry.CameraStart = null;
            }
        }

        bool affectsActive = (!string.IsNullOrEmpty(activeMapId) && string.Equals(activeMapId, key, StringComparison.OrdinalIgnoreCase))
            || (activeCacheEntry != null && !string.IsNullOrEmpty(activeCacheEntry.MapName) && string.Equals(activeCacheEntry.MapName, key, StringComparison.OrdinalIgnoreCase));

        if (affectsActive)
        {
            activeCameraOverride = null;
            cameraPositioned = false;
            UpdateVisibleTiles(force: true);
        }
    }

    private async Task<bool> EnsureFirestoreReadyAsync()
    {
        DB.Default.Init();
        if (DB.Default.maps != null && DB.Default.MapData != null)
        {
            return true;
        }

        var authenticatedUser = await User.EnsureLoggedInAsync();
        if (authenticatedUser == null)
        {
            Debug.LogError("Firestore map collections are not initialised. Sign in before loading map tiles.");
            return false;
        }

        DB.Default.Init();
        if (DB.Default.maps == null || DB.Default.MapData == null)
        {
            Debug.LogError("Firestore map collections are not initialised. Sign in before loading map tiles.");
            return false;
        }

        return true;
    }

    private async Task LoadChunksAsync(IReadOnlyList<string> chunkIds)
    {
        if (chunkIds == null || chunkIds.Count == 0)
        {
            return;
        }

        var mapDataRef = DB.Default.MapData.Document(mapId);
        var chunkTasks = new List<Task<DocumentSnapshot>>(chunkIds.Count);
        foreach (var chunkId in chunkIds)
        {
            chunkTasks.Add(mapDataRef.Collection("Chunks").Document(chunkId).GetSnapshotAsync());
        }

        var snapshots = await Task.WhenAll(chunkTasks);
        foreach (var snapshot in snapshots)
        {
            if (!snapshot.Exists)
            {
                continue;
            }

            var chunk = snapshot.ConvertTo<FirestoreMapChunk>();
            if (chunk?.Tiles == null)
            {
                continue;
            }

            foreach (var tile in chunk.Tiles)
            {
                UpsertTileVisualData(tile);
            }
        }
    }

    private void ResetRuntimeState()
    {
        ClearActiveTileObjects();
        if (surfaceTilemap != null)
        {
            surfaceTilemap.ClearAllTiles();
        }
        if (overlayTilemap != null)
        {
            overlayTilemap.ClearAllTiles();
        }

        activeSurfaceCells.Clear();
        activeOverlayCells.Clear();
        overrideTiles.Clear();
        tileCache.Clear();
        objectCache.Clear();
        currentViewBounds = default;
        baseTilePath = null;
        cachedBaseTile = null;
        planetSize = 0;
        chunkSize = DefaultChunkSize;
        lastCameraPosition = Vector3.positiveInfinity;
        lastOrthographicSize = float.NaN;
        mapLoaded = false;
        cameraPositioned = false;
        activeMapId = null;
        activeCacheEntry = null;
        activeCameraOverride = null;
    }

    private void EnsureTileInfrastructure()
    {
        if (tileRoot == null)
        {
            var rootObject = new GameObject("RenderedMapTiles");
            rootObject.transform.SetParent(null, worldPositionStays: false);
            rootObject.transform.position = Vector3.zero;
            tileRoot = rootObject.transform;
        }

        grid = tileRoot.GetComponent<Grid>();
        if (grid == null)
        {
            grid = tileRoot.gameObject.AddComponent<Grid>();
        }
        grid.cellSize = new Vector3(tileSize, tileSize, 1f);
        grid.cellLayout = GridLayout.CellLayout.Rectangle;

        surfaceTilemap = FindOrCreateTilemap(SurfaceTilemapName, SurfaceSortingOrder);
        overlayTilemap = FindOrCreateTilemap(OverlayTilemapName, OverlaySortingOrder);
        overlayTilemap.transform.localPosition = new Vector3(0f, 0f, -0.01f);
    }

    private Tilemap FindOrCreateTilemap(string name, int sortingOrder)
    {
        var child = tileRoot.Find(name);
        Tilemap tilemap;
        if (child != null && child.TryGetComponent(out tilemap))
        {
            var renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = sortingOrder;
            }
            else
            {
                renderer = tilemap.gameObject.AddComponent<TilemapRenderer>();
                renderer.sortingOrder = sortingOrder;
            }
            return tilemap;
        }

        var go = new GameObject(name);
        go.transform.SetParent(tileRoot, false);
        tilemap = go.AddComponent<Tilemap>();
        tilemap.tileAnchor = Vector3.zero;
        tilemap.orientation = Tilemap.Orientation.XY;
        var tileRenderer = go.AddComponent<TilemapRenderer>();
        tileRenderer.sortingOrder = sortingOrder;
        tileRenderer.mode = TilemapRenderer.Mode.Chunk;
        tileRenderer.sortOrder = TilemapRenderer.SortOrder.TopRight;
        return tilemap;
    }

    private void UpdateVisibleTiles(bool force)
    {
        if (!mapLoaded || targetCamera == null)
        {
            return;
        }

        if (!isOrthographic)
        {
            Debug.LogWarning($"{nameof(RenderMapTilesController)} currently supports orthographic cameras only.");
            return;
        }

        var visibleBounds = CalculateVisibleBounds();
        if (!force && visibleBounds.Equals(currentViewBounds))
        {
            return;
        }

        var desiredSurfaceCells = HashSetPool<Vector3Int>.Get();

        try
        {
            TileBase baseTile = GetBaseTile();

            for (int x = visibleBounds.xMin; x < visibleBounds.xMax; x++)
            {
                for (int y = visibleBounds.yMin; y < visibleBounds.yMax; y++)
                {
                    if (planetSize > 0 && (x < 0 || y < 0 || x >= planetSize || y >= planetSize))
                    {
                        continue;
                    }

                    var cell = new Vector3Int(x, y, 0);
                    desiredSurfaceCells.Add(cell);

                    if (baseTile != null)
                    {
                        surfaceTilemap.SetTile(cell, baseTile);
                        ApplyTileTransform(surfaceTilemap, cell, null);
                        activeSurfaceCells.Add(cell);
                    }

                    var coord = new Vector2Int(x, y);
                    overrideTiles.TryGetValue(coord, out var tileData);

                    if (tileData != null && !string.IsNullOrEmpty(tileData.SurfaceTilePath))
                    {
                        var surfaceOverride = GetTileAsset(tileData.SurfaceTilePath);
                        if (surfaceOverride != null)
                        {
                            surfaceTilemap.SetTile(cell, surfaceOverride);
                            ApplyTileTransform(surfaceTilemap, cell, null);
                            activeSurfaceCells.Add(cell);
                        }
                    }

                    var overlayPath = tileData?.OverlayTilePath;
                    if (!string.IsNullOrEmpty(overlayPath))
                    {
                        var overlayTile = GetTileAsset(overlayPath);
                        if (overlayTile != null)
                        {
                            overlayTilemap.SetTile(cell, overlayTile);
                            Matrix4x4? transform = (tileData != null && tileData.HasOverlayTransform) ? tileData.OverlayTransform : (Matrix4x4?)null;
                            ApplyTileTransform(overlayTilemap, cell, transform);
                            activeOverlayCells.Add(cell);
                        }
                        else
                        {
                            overlayTilemap.SetTile(cell, null);
                            ApplyTileTransform(overlayTilemap, cell, null);
                            activeOverlayCells.Remove(cell);
                        }
                    }
                    else
                    {
                        overlayTilemap.SetTile(cell, null);
                        ApplyTileTransform(overlayTilemap, cell, null);
                        activeOverlayCells.Remove(cell);
                    }

                    UpdateTileObject(cell, tileData?.ObjectPath);
                }
            }

            scratchCells.Clear();
            foreach (var cell in activeSurfaceCells)
            {
                if (!desiredSurfaceCells.Contains(cell))
                {
                    scratchCells.Add(cell);
                }
            }
            foreach (var cell in scratchCells)
            {
                surfaceTilemap.SetTile(cell, null);
                activeSurfaceCells.Remove(cell);
            }

            scratchCells.Clear();
            foreach (var cell in activeOverlayCells)
            {
                if (!desiredSurfaceCells.Contains(cell))
                {
                    scratchCells.Add(cell);
                }
            }
            foreach (var cell in scratchCells)
            {
                overlayTilemap.SetTile(cell, null);
                activeOverlayCells.Remove(cell);
                UpdateTileObject(cell, null);
            }

            scratchCells.Clear();
            foreach (var kvp in activeTileObjects)
            {
                if (!desiredSurfaceCells.Contains(kvp.Key))
                {
                    scratchCells.Add(kvp.Key);
                }
            }
            foreach (var cell in scratchCells)
            {
                UpdateTileObject(cell, null);
            }

            currentViewBounds = visibleBounds;
            MaybePositionCamera(visibleBounds);
            lastCameraPosition = targetCamera.transform.position;
            if (isOrthographic)
            {
                lastOrthographicSize = targetCamera.orthographicSize;
            }
        }
        finally
        {
            HashSetPool<Vector3Int>.Release(desiredSurfaceCells);
            scratchCells.Clear();
        }
    }

    private BoundsInt CalculateVisibleBounds()
    {
        var camPosition = targetCamera.transform.position;
        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;

        int minX = Mathf.FloorToInt((camPosition.x - halfWidth) / tileSize) - tilePadding;
        int maxX = Mathf.CeilToInt((camPosition.x + halfWidth) / tileSize) + tilePadding;
        int minY = Mathf.FloorToInt((camPosition.y - halfHeight) / tileSize) - tilePadding;
        int maxY = Mathf.CeilToInt((camPosition.y + halfHeight) / tileSize) + tilePadding;

        if (planetSize > 0)
        {
            minX = Mathf.Clamp(minX, 0, planetSize);
            minY = Mathf.Clamp(minY, 0, planetSize);
            maxX = Mathf.Clamp(maxX, 0, planetSize);
            maxY = Mathf.Clamp(maxY, 0, planetSize);
        }

        var min = new Vector3Int(minX, minY, 0);
        var size = new Vector3Int(Mathf.Max(0, maxX - minX), Mathf.Max(0, maxY - minY), 1);
        return new BoundsInt(min, size);
    }

    private TileBase GetBaseTile()
    {
        if (cachedBaseTile != null)
        {
            return cachedBaseTile;
        }

        cachedBaseTile = GetTileAsset(baseTilePath);
        return cachedBaseTile;
    }

    private TileBase GetTileAsset(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return null;
        }

        if (tileCache.TryGetValue(resourcePath, out var cachedTile))
        {
            return cachedTile;
        }

        TileBase tile = Resources.Load<TileBase>(resourcePath);
        if (tile == null)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                var runtimeTile = ScriptableObject.CreateInstance<Tile>();
                runtimeTile.sprite = sprite;
                runtimeTile.name = $"RuntimeTile_{sprite.name}";
                tile = runtimeTile;
            }
        }

        if (tile == null)
        {
            Debug.LogWarning($"Unable to load tile asset at '{resourcePath}'.");
        }

        tileCache[resourcePath] = tile;
        return tile;
    }

    private static string NormalizeResourcePath(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return null;
        }

        resourcePath = resourcePath.Replace('\\', '/');
        if (resourcePath.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
        {
            resourcePath = resourcePath.Substring("Assets/Resources/".Length);
        }

        resourcePath = resourcePath.TrimStart('/');
        resourcePath = Path.ChangeExtension(resourcePath, null);
        return resourcePath;
    }

    private static Matrix4x4? DecodeTransform(IList<double> values)
    {
        if (values == null || values.Count != 16)
        {
            return null;
        }

        var matrix = new Matrix4x4
        {
            m00 = (float)values[0],
            m01 = (float)values[1],
            m02 = (float)values[2],
            m03 = (float)values[3],
            m10 = (float)values[4],
            m11 = (float)values[5],
            m12 = (float)values[6],
            m13 = (float)values[7],
            m20 = (float)values[8],
            m21 = (float)values[9],
            m22 = (float)values[10],
            m23 = (float)values[11],
            m30 = (float)values[12],
            m31 = (float)values[13],
            m32 = (float)values[14],
            m33 = (float)values[15]
        };

        return IsIdentityMatrix(matrix) ? (Matrix4x4?)null : matrix;
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

    private static bool IsSurfaceLayer(string layer)
    {
        return !string.IsNullOrEmpty(layer) && string.Equals(layer, Surface, StringComparison.OrdinalIgnoreCase);
    }

    private void UpsertTileVisualData(FirestoreTile tile)
    {
        if (tile == null)
        {
            return;
        }

        var coord = new Vector2Int(tile.x, tile.y);
        if (!overrideTiles.TryGetValue(coord, out var data))
        {
            data = new TileVisualData();
            overrideTiles[coord] = data;
        }

        var layer = string.IsNullOrEmpty(tile.TileLayer) ? Overlay : tile.TileLayer;
        var tilePath = NormalizeResourcePath(tile.TileName);
        var objectPath = NormalizeResourcePath(tile.TileObjectPath);
        var transform = DecodeTransform(tile.Transform);

        if (IsSurfaceLayer(layer))
        {
            if (!string.IsNullOrEmpty(tilePath))
            {
                data.SurfaceTilePath = tilePath;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(tilePath))
            {
                data.OverlayTilePath = tilePath;
            }
            if (transform.HasValue)
            {
                data.HasOverlayTransform = true;
                data.OverlayTransform = transform.Value;
            }
            else
            {
                data.HasOverlayTransform = false;
                data.OverlayTransform = Matrix4x4.identity;
            }
            data.ObjectPath = objectPath;
        }
    }

    private GameObject GetObjectPrefab(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return null;
        }

        if (objectCache.TryGetValue(resourcePath, out var cachedObject))
        {
            return cachedObject;
        }

        var prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"Unable to load tile object at '{resourcePath}'.");
        }

        objectCache[resourcePath] = prefab;
        return prefab;
    }

    private static void ApplyTileTransform(Tilemap tilemap, Vector3Int cell, Matrix4x4? matrix)
    {
        if (tilemap == null)
        {
            return;
        }

        tilemap.SetTileFlags(cell, TileFlags.None);
        tilemap.SetTransformMatrix(cell, matrix ?? Matrix4x4.identity);
    }

    private void UpdateTileObject(Vector3Int cell, string resourcePath)
    {
        if (activeTileObjects.TryGetValue(cell, out var active) && active != null && active.Instance == null)
        {
            activeTileObjects.Remove(cell);
            active = null;
        }

        if (string.IsNullOrEmpty(resourcePath))
        {
            if (active != null && active.Instance != null)
            {
                DestroyRuntimeObject(active.Instance);
            }

            if (active != null)
            {
                activeTileObjects.Remove(cell);
            }

            return;
        }

        if (active != null && active.Instance != null && active.ResourcePath == resourcePath)
        {
            return;
        }

        if (active != null && active.Instance != null)
        {
            DestroyRuntimeObject(active.Instance);
            activeTileObjects.Remove(cell);
        }

        var prefab = GetObjectPrefab(resourcePath);
        if (prefab == null)
        {
            return;
        }

        var instance = Instantiate(prefab, overlayTilemap.transform);
        instance.transform.position = overlayTilemap.GetCellCenterWorld(cell);

        activeTileObjects[cell] = new ActiveTileObject
        {
            Instance = instance,
            ResourcePath = resourcePath
        };
    }

    private void ClearActiveTileObjects()
    {
        foreach (var entry in activeTileObjects)
        {
            if (entry.Value?.Instance != null)
            {
                DestroyRuntimeObject(entry.Value.Instance);
            }
        }
        activeTileObjects.Clear();
    }

    private static void DestroyRuntimeObject(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(instance);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private void MaybePositionCamera(BoundsInt visibleBounds)
    {
        if (targetCamera == null)
        {
            return;
        }

        if (!cameraPositioned && activeCameraOverride.HasValue)
        {
            var overrideData = activeCameraOverride.Value;
            var desiredPosition = overrideData.Position;
            desiredPosition.z = targetCamera.transform.position.z;

            targetCamera.transform.position = desiredPosition;
            if (isOrthographic)
            {
                targetCamera.orthographicSize = ClampCameraSize(overrideData.Size);
            }

            cameraPositioned = true;
            activeCameraOverride = null;
            return;
        }

        if (cameraPositioned)
        {
            return;
        }

        if (!TryGetMapExtents(out var worldCenter, out _, out _))
        {
            if (visibleBounds.size.x == 0 || visibleBounds.size.y == 0)
            {
                return;
            }

            float halfTile = tileSize * 0.5f;
            float centerX = (visibleBounds.xMin + visibleBounds.xMax - 1) * tileSize * 0.5f + halfTile;
            float centerY = (visibleBounds.yMin + visibleBounds.yMax - 1) * tileSize * 0.5f + halfTile;
            worldCenter = new Vector3(centerX, centerY, 0f);
        }

        worldCenter.z = targetCamera.transform.position.z;

        targetCamera.transform.position = worldCenter;

        if (isOrthographic)
        {
            targetCamera.orthographicSize = ClampCameraSize(defaultCameraSize);
        }

        cameraPositioned = true;
    }

    private bool TryGetMapExtents(out Vector3 center, out float width, out float height)
    {
        center = Vector3.zero;
        width = height = 0f;

        if (planetSize > 0)
        {
            float size = Math.Max(1, planetSize) * tileSize;
            width = height = size;
            float half = (Math.Max(1, planetSize) - 1) * tileSize * 0.5f + tileSize * 0.5f;
            center = new Vector3(half, half, 0f);
            return true;
        }

        if (overrideTiles.Count > 0)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var coord in overrideTiles.Keys)
            {
                if (coord.x < minX) minX = coord.x;
                if (coord.x > maxX) maxX = coord.x;
                if (coord.y < minY) minY = coord.y;
                if (coord.y > maxY) maxY = coord.y;
            }

            if (minX <= maxX && minY <= maxY)
            {
                width = (maxX - minX + 1) * tileSize;
                height = (maxY - minY + 1) * tileSize;
                float centerX = (minX + maxX) * 0.5f * tileSize + tileSize * 0.5f;
                float centerY = (minY + maxY) * 0.5f * tileSize + tileSize * 0.5f;
                center = new Vector3(centerX, centerY, 0f);
                return true;
            }
        }

        return false;
    }

    private float ClampCameraSize(float size)
    {
        return Mathf.Clamp(size, minCameraSize, maxCameraSize);
    }

    private void OnDisable()
    {
        ClearActiveTileObjects();
        cameraPositioned = false;
    }

    private void OnDestroy()
    {
        ClearActiveTileObjects();
        cameraPositioned = false;
    }

    private struct CameraOverride
    {
        public Vector3 Position;
        public float Size;
    }

    private CameraOverride? GetCameraOverrideFor(string mapIdentifier)
    {
        if (!string.IsNullOrEmpty(mapIdentifier) && cameraOverrides.TryGetValue(mapIdentifier, out var overrideData))
        {
            return overrideData;
        }

        if (!string.IsNullOrEmpty(mapIdentifier) && mapCache.TryGetValue(mapIdentifier, out var cached) && cached.CameraStart.HasValue)
        {
            return cached.CameraStart;
        }

        return null;
    }

    private static Dictionary<Vector2Int, TileVisualData> CloneOverrideTilesDictionary(Dictionary<Vector2Int, TileVisualData> source)
    {
        var clone = new Dictionary<Vector2Int, TileVisualData>(source.Count);
        foreach (var kvp in source)
        {
            clone[kvp.Key] = kvp.Value?.Clone();
        }
        return clone;
    }

    private void CacheCurrentMap(string mapIdentifier, string displayName = null)
    {
        if (string.IsNullOrEmpty(mapIdentifier))
        {
            return;
        }

        var entry = new MapCacheEntry
        {
            MapId = mapIdentifier,
            MapName = displayName ?? mapIdentifier,
            BaseTilePath = baseTilePath,
            CachedBaseTile = cachedBaseTile,
            PlanetSize = planetSize,
            ChunkSize = chunkSize,
            OverrideTiles = CloneOverrideTilesDictionary(overrideTiles),
            TileCache = new Dictionary<string, TileBase>(tileCache, StringComparer.OrdinalIgnoreCase),
            ObjectCache = new Dictionary<string, GameObject>(objectCache, StringComparer.OrdinalIgnoreCase),
            CameraStart = GetCameraOverrideFor(mapIdentifier)
        };

        mapCache[mapIdentifier] = entry;
        activeCacheEntry = entry;

        if (!string.IsNullOrEmpty(displayName))
        {
            SyncCameraOverrideKeys(displayName, mapIdentifier);
        }
    }

    private void ApplyCacheEntry(MapCacheEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        foreach (var kvp in entry.OverrideTiles)
        {
            overrideTiles[kvp.Key] = kvp.Value?.Clone();
        }

        foreach (var kvp in entry.TileCache)
        {
            tileCache[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in entry.ObjectCache)
        {
            objectCache[kvp.Key] = kvp.Value;
        }

        baseTilePath = entry.BaseTilePath;
        cachedBaseTile = entry.CachedBaseTile;
        planetSize = entry.PlanetSize;
        chunkSize = entry.ChunkSize > 0 ? entry.ChunkSize : DefaultChunkSize;
        mapLoaded = true;
        cameraPositioned = false;
        activeMapId = entry.MapId;
        activeCacheEntry = entry;
        activeCameraOverride = GetCameraOverrideFor(entry.MapId);
    }

    private void SyncCameraOverrideKeys(string aliasKey, string canonicalKey)
    {
        if (string.IsNullOrWhiteSpace(aliasKey) || string.IsNullOrWhiteSpace(canonicalKey))
        {
            return;
        }

        string alias = aliasKey.Trim();
        string canonical = canonicalKey.Trim();
        if (string.Equals(alias, canonical, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (cameraOverrides.TryGetValue(alias, out var overrideData))
        {
            cameraOverrides[canonical] = overrideData;
            if (mapCache.TryGetValue(canonical, out var cached))
            {
                cached.CameraStart = overrideData;
            }
        }
        else if (cameraOverrides.TryGetValue(canonical, out var existing))
        {
            cameraOverrides[alias] = existing;
        }
    }

    private class MapCacheEntry
    {
        public string MapId;
        public string MapName;
        public string BaseTilePath;
        public TileBase CachedBaseTile;
        public int PlanetSize;
        public int ChunkSize = DefaultChunkSize;
        public Dictionary<Vector2Int, TileVisualData> OverrideTiles = new Dictionary<Vector2Int, TileVisualData>();
        public Dictionary<string, TileBase> TileCache = new Dictionary<string, TileBase>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, GameObject> ObjectCache = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        public CameraOverride? CameraStart;
    }

    private class TileVisualData
    {
        public string SurfaceTilePath;
        public string OverlayTilePath;
        public string ObjectPath;
        public bool HasOverlayTransform;
        public Matrix4x4 OverlayTransform;

        public TileVisualData Clone()
        {
            return new TileVisualData
            {
                SurfaceTilePath = SurfaceTilePath,
                OverlayTilePath = OverlayTilePath,
                ObjectPath = ObjectPath,
                HasOverlayTransform = HasOverlayTransform,
                OverlayTransform = OverlayTransform
            };
        }
    }

    private class ActiveTileObject
    {
        public GameObject Instance;
        public string ResourcePath;
    }

    private static class HashSetPool<T>
    {
        private static readonly Stack<HashSet<T>> Pool = new Stack<HashSet<T>>();

        public static HashSet<T> Get()
        {
            return Pool.Count > 0 ? Pool.Pop() : new HashSet<T>();
        }

        public static void Release(HashSet<T> hashSet)
        {
            if (hashSet == null)
            {
                return;
            }
            hashSet.Clear();
            Pool.Push(hashSet);
        }
    }

    #region Firestore DTOs

    [FirestoreData]
    private class FirestoreMapDocument
    {
        [FirestoreProperty]
        public string PlanetSurface { get; set; }

        [FirestoreProperty]
        public int PlanetSize { get; set; }

        [FirestoreProperty]
        public string PlanetName { get; set; }
    }

    [FirestoreData]
    private class FirestoreMapDataDocument
    {
        [FirestoreProperty]
        public List<FirestoreTile> Tiles { get; set; }

        [FirestoreProperty]
        public List<string> ChunkIds { get; set; } = new List<string>();

        [FirestoreProperty]
        public int ChunkSize { get; set; } = DefaultChunkSize;
    }

    [FirestoreData]
    private class FirestoreMapChunk
    {
        [FirestoreProperty]
        public List<FirestoreTile> Tiles { get; set; } = new List<FirestoreTile>();
    }

    [FirestoreData]
    private class FirestoreTile
    {
        [FirestoreProperty]
        public string TileName { get; set; }

        [FirestoreProperty]
        public string TileObjectPath { get; set; }

        [FirestoreProperty]
        public string TileLayer { get; set; }

        [FirestoreProperty]
        public int x { get; set; }

        [FirestoreProperty]
        public int y { get; set; }

        [FirestoreProperty]
        public List<double> Transform { get; set; }
    }

    #endregion
}
