using UnityEngine;
using System;

[Serializable]
public class PlayerInput : MonoBehaviour
{
    public Grid PlanetGrid;
    [SerializeField, Range(10f, 200f)] private float mapTouchDampering = 80f;
    [SerializeField, Range(1f, 15f)] private float mapTouchSensitivity = 3f;
    [SerializeField, Range(0.01f, 1f)] private float mapTouchEasingDistance = .12f;
    [SerializeField, Range(0.01f, 1f)] private float mapTouchEasingSpeed = .14f;
    [SerializeField] private float zoomSpeed = 0.01f;
    [SerializeField] private float zoomInBound = 2.93f;
    [SerializeField] private float zoomOutBound = 4.72f;
#if UNITY_EDITOR
    [SerializeField] private float editorScrollMultiplier = 25f;
#endif

    private Vector3 inertialVelocity = Vector3.zero;
    private float lastPinchDistance = 0f;
    private bool isPanning = false;
    private Vector3 targetCameraPosition;
    private Vector3 smoothDampVelocity = Vector3.zero;
    private bool targetInitialized = false;

    void Start()
    {
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            targetCameraPosition = mainCamera.transform.position;
            targetInitialized = true;
        }
    }

    void OnValidate()
    {
        if (zoomOutBound < zoomInBound)
        {
            zoomOutBound = zoomInBound;
        }
#if UNITY_EDITOR
        if (editorScrollMultiplier <= 0f)
            editorScrollMultiplier = 1f;
#endif
    }

    void Update()
    {
        var mainCamera = Camera.main;
        if (mainCamera == null) return;
        if (!targetInitialized)
        {
            targetCameraPosition = mainCamera.transform.position;
            targetInitialized = true;
        }

#if UNITY_EDITOR
        HandleEditorScroll(mainCamera);
#endif

        if (Input.touchCount > 0)
        {
            if (Input.touchCount == 1)
            {
                HandleSingleTouch(mainCamera, Input.GetTouch(0));
            }
            else
            {
                HandlePinchZoom(mainCamera, Input.GetTouch(0), Input.GetTouch(1));
            }
        }
        else
        {
            lastPinchDistance = 0f;
            ApplyInertia(mainCamera);
        }

        ClampTargetPosition(mainCamera, ref targetCameraPosition);

        float smoothTime = isPanning
            ? Mathf.Max(0.008f, mapTouchEasingSpeed * 0.2f)
            : Mathf.Max(0.02f, mapTouchEasingSpeed * 0.5f);

        mainCamera.transform.position = Vector3.SmoothDamp(
            mainCamera.transform.position,
            targetCameraPosition,
            ref smoothDampVelocity,
            smoothTime);

        var actual = mainCamera.transform.position;
        ClampTargetPosition(mainCamera, ref actual);
        mainCamera.transform.position = actual;

        if (isPanning)
        {
            targetCameraPosition = mainCamera.transform.position;
        }
        else if ((mainCamera.transform.position - targetCameraPosition).sqrMagnitude < 0.00004f)
        {
            smoothDampVelocity = Vector3.zero;
            targetCameraPosition = mainCamera.transform.position;
        }
    }

    void HandleSingleTouch(Camera mainCamera, Touch touch)
    {
        switch (touch.phase)
        {
            case TouchPhase.Began:
                isPanning = true;
                inertialVelocity = Vector3.zero;
                smoothDampVelocity = Vector3.zero;
                targetCameraPosition = mainCamera.transform.position;
                break;

            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                PanCamera(mainCamera, touch);
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                isPanning = false;
                break;
        }
    }

    void PanCamera(Camera mainCamera, Touch touch)
    {
        float damper = Mathf.Max(1f, mapTouchDampering);
        float sensitivityScale = Mathf.Clamp(mapTouchSensitivity, 1f, 20f);
        float effectiveDamper = damper / sensitivityScale;
        Vector2 delta = touch.deltaPosition;
        Vector3 offset = new Vector3(-delta.x / effectiveDamper, -delta.y / effectiveDamper, 0f);
        targetCameraPosition += offset;
        ClampTargetPosition(mainCamera, ref targetCameraPosition);

        float dt = touch.deltaTime > Mathf.Epsilon ? touch.deltaTime : Time.deltaTime;
        if (dt > Mathf.Epsilon)
        {
            Vector3 velocity = offset / dt;
            inertialVelocity = Vector3.Lerp(inertialVelocity, velocity, 0.5f);
        }
    }

    void HandlePinchZoom(Camera mainCamera, Touch firstTouch, Touch secondTouch)
    {
        isPanning = false;
        inertialVelocity = Vector3.zero;

        if (firstTouch.phase == TouchPhase.Began || secondTouch.phase == TouchPhase.Began)
        {
            lastPinchDistance = Vector2.Distance(firstTouch.position, secondTouch.position);
            targetCameraPosition = mainCamera.transform.position;
            return;
        }

        float currentDistance = Vector2.Distance(firstTouch.position, secondTouch.position);
        if (Mathf.Approximately(lastPinchDistance, 0f))
        {
            lastPinchDistance = currentDistance;
            return;
        }

        float distanceDelta = currentDistance - lastPinchDistance;
        lastPinchDistance = currentDistance;

        float zoomDelta = distanceDelta * zoomSpeed;

        if (mainCamera.orthographic)
        {
            float newSize = Mathf.Clamp(mainCamera.orthographicSize + zoomDelta, zoomInBound, zoomOutBound);
            mainCamera.orthographicSize = newSize;
        }
        else
        {
            float newFov = Mathf.Clamp(mainCamera.fieldOfView + zoomDelta, zoomInBound, zoomOutBound);
            mainCamera.fieldOfView = newFov;
        }

        targetCameraPosition = mainCamera.transform.position;
        ClampTargetPosition(mainCamera, ref targetCameraPosition);
    }

#if UNITY_EDITOR
    void HandleEditorScroll(Camera mainCamera)
    {
        float scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scrollDelta, 0f))
        {
            scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        }
        if (Mathf.Approximately(scrollDelta, 0f)) return;

        float zoomDelta = -scrollDelta * zoomSpeed * editorScrollMultiplier;

        if (mainCamera.orthographic)
        {
            float newSize = Mathf.Clamp(mainCamera.orthographicSize + zoomDelta, zoomInBound, zoomOutBound);
            mainCamera.orthographicSize = newSize;
        }
        else
        {
            float newFov = Mathf.Clamp(mainCamera.fieldOfView + zoomDelta, zoomInBound, zoomOutBound);
            mainCamera.fieldOfView = newFov;
        }

        targetCameraPosition = mainCamera.transform.position;
        ClampTargetPosition(mainCamera, ref targetCameraPosition);
    }
#endif

    void ApplyInertia(Camera mainCamera)
    {
        if (isPanning)
        {
            return;
        }

        float threshold = mapTouchEasingDistance * 0.5f;
        if (inertialVelocity.sqrMagnitude <= threshold * threshold)
        {
            inertialVelocity = Vector3.zero;
            return;
        }

        targetCameraPosition += inertialVelocity * Time.deltaTime;

        float damping = Mathf.Clamp01(mapTouchEasingSpeed * Time.deltaTime * 120f);
        inertialVelocity = Vector3.Lerp(inertialVelocity, Vector3.zero, damping);
        ClampTargetPosition(mainCamera, ref targetCameraPosition);
    }

    void ClampTargetPosition(Camera mainCamera, ref Vector3 position)
    {
        if (mainCamera == null) return;
        if (!TryGetPlanetBounds(out var bounds)) return;

        if (!mainCamera.orthographic)
        {
            position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            position.y = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
            return;
        }

        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;

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

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);
    }

    bool TryGetPlanetBounds(out Bounds bounds)
    {
        bounds = default;
        var planetRoot = FindPlanetRoot();
        if (planetRoot == null) return false;

        var renderers = planetRoot.GetComponentsInChildren<Renderer>(includeInactive: false);
        if (renderers.Length == 0)
        {
            bounds = new Bounds(planetRoot.transform.position, Vector3.zero);
            return true;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
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

                bool bestIsRoot = bestMatch.transform.parent == null;
                bool currentIsRoot = obj.transform.parent == null;
                if (!bestIsRoot && currentIsRoot)
                {
                    bestMatch = obj;
                    continue;
                }

                if (bestIsRoot == currentIsRoot)
                {
                    var bestBounds = GetApproximateBounds(bestMatch);
                    var currentBounds = GetApproximateBounds(obj);
                    if (currentBounds.size.sqrMagnitude > bestBounds.size.sqrMagnitude)
                    {
                        bestMatch = obj;
                    }
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
}
