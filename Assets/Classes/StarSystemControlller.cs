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

    private const string WarpVideoPlayerObjectName = "WarpTravelVideoPlayer";

    private static VideoPlayer warpVideoPlayer;
    private static AudioSource warpVideoAudio;
    private static GameObject warpVideoPlayerObject;
    private static StarSystemControlller warpVideoOwner;
    private static bool warpVideoPlaying;

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

        bool startedVideo = playWarpTravelVideo && PlayWarpTravelVideo();

        Debug.Log(string.IsNullOrEmpty(systemName)
            ? $"Star system clicked: {gameObject.name}"
            : $"Star system clicked: {systemName}");

        onClicked?.Invoke();

        if (!startedVideo)
        {
            WarpToDestinationIfConfigured();
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

        StopWarpTravelVideo();
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

    private void OnWarpVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"Warp travel video error on {gameObject.name}: {message}");
        StopWarpTravelVideo();
        WarpToDestinationIfConfigured();
    }

    private void OnWarpVideoFinished(VideoPlayer source)
    {
        StopWarpTravelVideo();
        WarpToDestinationIfConfigured();
    }

    private void WarpToDestinationIfConfigured()
    {
        if (string.IsNullOrWhiteSpace(warpToMapName))
        {
            return;
        }

        WarpToMapAsync(warpToMapName.Trim());
    }

    private async void WarpToMapAsync(string mapName)
    {
        try
        {
            var controller = FindObjectOfType<RenderMapTilesController>();
            if (controller == null)
            {
                Debug.LogWarning($"Warp target '{mapName}' requested but no {nameof(RenderMapTilesController)} is active in the scene.");
                return;
            }

            bool loaded = await controller.LoadMapByNameAsync(mapName);
            if (!loaded)
            {
                Debug.LogWarning($"Warp target '{mapName}' could not be loaded from Firestore.");
            }
            else
            {
                Debug.Log($"Warped to map '{mapName}'.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error warping to map '{mapName}': {ex}");
        }
    }
}
