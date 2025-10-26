using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Video;
using System;
using System.Threading.Tasks;

[RequireComponent(typeof(Collider))]
public class StarSystemControlller : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Invoked when this star system is clicked.")]
    [SerializeField]
    private UnityEvent onClicked;

    [Tooltip("Optional name of the system for debugging.")]
    [SerializeField]
    private string systemName;

    [Header("Warp Travel Video")]
    [Tooltip("Play the warp travel video when this star system is clicked.")]
    [SerializeField]
    private bool playWarpTravelVideo = true;

    [Tooltip("Video clip to play when this star system is clicked.")]
    [SerializeField]
    private VideoClip warpTravelClip;

    [Tooltip("Optional override for the camera used to render the video. Defaults to Camera.main.")]
    [SerializeField]
    private GameObject warpVideoCameraObject;

    [Header("Warp Destination")]
    [Tooltip("Map name to load after the warp video finishes playing.")]
    [SerializeField]
    private string warpToMapName;

    [Header("Camera Override")]
    [Tooltip("If enabled, sets the camera position and size when the warp finishes.")]
    [SerializeField]
    private bool overrideCameraStart;

    [SerializeField]
    private Vector3 cameraStartPosition = new Vector3(0f, 0f, 0f);

    [SerializeField]
    [Min(0.01f)]
    private float cameraStartSize = 4.72f;

    private const string WarpVideoPlayerObjectName = "WarpTravelVideoPlayer";

    private static VideoPlayer warpVideoPlayer;
    private static AudioSource warpVideoAudio;
    private static GameObject warpVideoPlayerObject;
    private static StarSystemControlller warpVideoOwner;
    private static bool warpVideoPlaying;
    private static RenderMapTilesController cachedWarpController;
    private static Task<bool> pendingWarpLoadTask;
    private static string pendingWarpMapName;
    private static bool mapHiddenForVideo;

    private static RenderMapTilesController GetMapController()
    {
        if (cachedWarpController == null)
        {
            cachedWarpController = UnityEngine.Object.FindObjectOfType<RenderMapTilesController>();
        }
        return cachedWarpController;
    }

    public UnityEvent OnClicked => onClicked;

    private void Reset()
    {
        // Ensure collider is configured for clicks.
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false;
        }
    }

    private void Awake()
    {
        if (onClicked == null)
        {
            onClicked = new UnityEvent();
        }

        if (playWarpTravelVideo)
        {
            LoadWarpTravelClip();
        }
    }

    private void OnMouseDown()
    {
        HandleClick();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!enabled)
        {
            return;
        }

        // Only react to left-click / primary touch to match original behavior.
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        HandleClick();
    }

    private void HandleClick()
    {
        if (!enabled)
        {
            return;
        }

        cachedWarpController = GetMapController();

        bool startedVideo = playWarpTravelVideo && PlayWarpTravelVideo();

        BeginWarpLoadIfNeeded(warpToMapName);

        var controllerForHide = GetMapController();
        if (startedVideo && controllerForHide != null)
        {
            controllerForHide.SetMapVisibility(false);
            mapHiddenForVideo = true;
        }

        Debug.Log(string.IsNullOrEmpty(systemName)
            ? $"Star system clicked: {gameObject.name}"
            : $"Star system clicked: {systemName}");

        onClicked?.Invoke();

        if (!startedVideo)
        {
            _ = WarpToDestinationIfConfiguredAsync();
        }
    }

    public void RegisterClickListener(UnityAction action)
    {
        if (onClicked == null)
        {
            onClicked = new UnityEvent();
        }
        onClicked.AddListener(action);
    }

    public void UnregisterClickListener(UnityAction action)
    {
        onClicked?.RemoveListener(action);
    }

    private void OnDisable()
    {
        StopWarpTravelVideo();
    }

    private void OnDestroy()
    {
        StopWarpTravelVideo();
        UnhookVideoPlayerEvents();
    }

    private bool PlayWarpTravelVideo()
    {
        EnsureVideoPlayer();
        LoadWarpTravelClip();
        if (warpTravelClip == null || warpVideoPlaying)
        {
            return false;
        }

        StartCoroutine(PlayWarpTravelVideoRoutine());
        return true;
    }

    private IEnumerator PlayWarpTravelVideoRoutine()
    {
        warpVideoPlaying = true;
        EnsureVideoPlayer();

        Camera targetCamera = null;
        if (warpVideoCameraObject != null)
        {
            targetCamera = warpVideoCameraObject.GetComponent<Camera>();
            if (targetCamera == null)
            {
                Debug.LogWarning($"Warp video camera override on {gameObject.name} does not have a Camera component. Falling back to Camera.main.");
            }
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            Debug.LogWarning($"Star system clicked but no camera available for video playback on {gameObject.name}.");
            warpVideoPlaying = false;
            yield break;
        }

        warpVideoPlayer.targetCamera = targetCamera;
        warpVideoPlayer.clip = warpTravelClip;
        warpVideoPlayer.targetCameraAlpha = 1f;
        warpVideoPlayer.isLooping = false;
        warpVideoPlayer.time = 0d;

        warpVideoPlayer.Prepare();
        while (!warpVideoPlayer.isPrepared)
        {
            yield return null;
        }

        warpVideoPlayer.Play();
        if (warpVideoPlayer.audioOutputMode == VideoAudioOutputMode.AudioSource && warpVideoAudio != null)
        {
            warpVideoAudio.Play();
        }

        while (warpVideoPlayer.isPlaying)
        {
            yield return null;
        }
    }

    private void StopWarpTravelVideo()
    {
        if (warpVideoOwner != this)
        {
            return;
        }

        if (warpVideoPlayer == null)
        {
            warpVideoPlaying = false;
            return;
        }

        if (warpVideoPlayer.isPlaying)
        {
            warpVideoPlayer.Stop();
        }
        if (warpVideoAudio != null && warpVideoAudio.isPlaying)
        {
            warpVideoAudio.Stop();
        }

        warpVideoPlayer.targetCameraAlpha = 0f;
        warpVideoPlayer.time = 0d;
        warpVideoPlaying = false;
        UnhookVideoPlayerEvents();

        if (mapHiddenForVideo)
        {
            GetMapController()?.SetMapVisibility(true);
            mapHiddenForVideo = false;
        }
    }

    private void EnsureVideoPlayer()
    {
        if (warpVideoPlayer != null)
        {
            HookVideoPlayerEvents();
            return;
        }

        if (warpVideoPlayerObject == null)
        {
            warpVideoPlayerObject = GameObject.Find(WarpVideoPlayerObjectName);
        }

        if (warpVideoPlayerObject == null)
        {
            warpVideoPlayerObject = new GameObject(WarpVideoPlayerObjectName);
            DontDestroyOnLoad(warpVideoPlayerObject);
        }

        warpVideoPlayer = warpVideoPlayerObject.GetComponent<VideoPlayer>();
        if (warpVideoPlayer == null)
        {
            warpVideoPlayer = warpVideoPlayerObject.AddComponent<VideoPlayer>();
        }

        warpVideoAudio = warpVideoPlayerObject.GetComponent<AudioSource>();
        if (warpVideoAudio == null)
        {
            warpVideoAudio = warpVideoPlayerObject.AddComponent<AudioSource>();
        }

        warpVideoPlayer.playOnAwake = false;
        warpVideoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
        warpVideoPlayer.aspectRatio = VideoAspectRatio.FitVertically;
        warpVideoPlayer.targetCameraAlpha = 0f;
        warpVideoPlayer.waitForFirstFrame = true;
        warpVideoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        warpVideoPlayer.EnableAudioTrack(0, true);
        warpVideoPlayer.SetTargetAudioSource(0, warpVideoAudio);

        ConfigureVideoPlayerDefaults(true);
        HookVideoPlayerEvents();
    }

    private void ConfigureVideoPlayerDefaults(bool hideAlpha)
    {
        if (warpVideoPlayer == null)
        {
            return;
        }

        warpVideoPlayer.playOnAwake = false;
        warpVideoPlayer.isLooping = false;
        warpVideoPlayer.waitForFirstFrame = true;
        if (hideAlpha)
        {
            warpVideoPlayer.targetCameraAlpha = 0f;
        }
        if (warpVideoAudio != null)
        {
            warpVideoAudio.playOnAwake = false;
            warpVideoAudio.loop = false;
        }
    }

    private void LoadWarpTravelClip()
    {
        if (warpTravelClip != null)
        {
            return;
        }

        Debug.LogWarning($"No warp travel video clip assigned on {gameObject.name}. Disabling warp video playback.");
        playWarpTravelVideo = false;
    }

    private void HookVideoPlayerEvents()
    {
        if (warpVideoPlayer == null)
        {
            return;
        }

        if (warpVideoOwner == this)
        {
            return;
        }

        warpVideoOwner?.UnhookVideoPlayerEvents();

        warpVideoPlayer.errorReceived += OnWarpVideoError;
        warpVideoPlayer.loopPointReached += OnWarpVideoFinished;
        warpVideoOwner = this;
    }

    private void UnhookVideoPlayerEvents()
    {
        if (warpVideoPlayer == null || warpVideoOwner != this)
        {
            return;
        }

        warpVideoPlayer.errorReceived -= OnWarpVideoError;
        warpVideoPlayer.loopPointReached -= OnWarpVideoFinished;
        warpVideoOwner = null;
    }

    private async void OnWarpVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"Warp travel video error on {gameObject.name}: {message}");
        StopWarpTravelVideo();
        await WarpToDestinationIfConfiguredAsync();
    }

    private async void OnWarpVideoFinished(VideoPlayer source)
    {
        await WarpToDestinationIfConfiguredAsync();
        StopWarpTravelVideo();
    }

    private Task<bool> BeginWarpLoadIfNeeded(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return null;
        }

        string trimmedName = mapName.Trim();

        if (pendingWarpLoadTask != null)
        {
            bool sameMap = string.Equals(pendingWarpMapName, trimmedName, StringComparison.OrdinalIgnoreCase);
            if (!pendingWarpLoadTask.IsCompleted)
            {
                if (sameMap)
                {
                    return pendingWarpLoadTask;
                }
            }
            else if (sameMap)
            {
                return pendingWarpLoadTask;
            }
        }

        var controller = GetMapController();
        if (controller == null)
        {
            Debug.LogWarning($"Warp target '{trimmedName}' requested but no {nameof(RenderMapTilesController)} is active in the scene.");
            pendingWarpLoadTask = null;
            pendingWarpMapName = null;
            return null;
        }

        if (overrideCameraStart)
        {
            Vector3 desiredPosition = cameraStartPosition;
            controller.SetMapCameraOverride(trimmedName, desiredPosition, cameraStartSize);
        }
        else
        {
            controller.ClearMapCameraOverride(trimmedName);
        }

        pendingWarpMapName = trimmedName;
        cachedWarpController = controller;
        pendingWarpLoadTask = controller.LoadMapByNameAsync(trimmedName);
        return pendingWarpLoadTask;
    }

    private async Task<bool> EnsureWarpMapReadyAsync(string mapName)
    {
        Task<bool> loadTask = BeginWarpLoadIfNeeded(mapName);
        if (loadTask == null)
        {
            if (mapHiddenForVideo)
            {
                GetMapController()?.SetMapVisibility(true);
                mapHiddenForVideo = false;
            }
            return false;
        }

        try
        {
            bool loaded = await loadTask;
            if (!loaded && mapHiddenForVideo)
            {
                GetMapController()?.SetMapVisibility(true);
                mapHiddenForVideo = false;
            }
            return loaded;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error while loading warp destination '{mapName}': {ex}");
            if (mapHiddenForVideo)
            {
                GetMapController()?.SetMapVisibility(true);
                mapHiddenForVideo = false;
            }
            return false;
        }
        finally
        {
            if (ReferenceEquals(loadTask, pendingWarpLoadTask))
            {
                pendingWarpLoadTask = null;
                pendingWarpMapName = null;
            }
        }
    }

    private async Task WarpToDestinationIfConfiguredAsync()
    {
        if (string.IsNullOrWhiteSpace(warpToMapName))
        {
            return;
        }

        string mapName = warpToMapName.Trim();
        bool loaded = await EnsureWarpMapReadyAsync(mapName);
        if (!loaded)
        {
            Debug.LogWarning($"Warp target '{mapName}' could not be loaded from Firestore.");
            return;
        }

        if (mapHiddenForVideo)
        {
            GetMapController()?.SetMapVisibility(true);
            mapHiddenForVideo = false;
        }

        Debug.Log($"Warped to map '{mapName}'.");
    }
}
