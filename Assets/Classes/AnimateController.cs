using System.Collections;
using UnityEngine;

/// <summary>
/// Animates the attached object from an off-screen position into its stored position whenever it becomes active.
/// Works with both RectTransform (UI) and regular Transform components.
/// </summary>
[RequireComponent(typeof(Transform))]
public class AnimateController : MonoBehaviour
{
    [SerializeField]
    private float animationDuration = 0.4f;

    [SerializeField]
    private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Offset added to the stored position to determine the hidden position before the animation starts.")]
    [SerializeField]
    private Vector3 hiddenOffset = new Vector3(0f, 600f, 0f);

    [SerializeField]
    private bool playOnEnable = true;

    private RectTransform rectTransform;
    private Transform cachedTransform;
    private bool useRectTransform;

    private Vector3 shownPosition;
    private bool hasStoredPosition;
    private Coroutine activeAnimation;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        useRectTransform = rectTransform != null;
        cachedTransform = transform;

        CacheShownPosition();

        if (!Application.isPlaying)
        {
            ApplyPosition(shownPosition);
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            CacheShownPosition();
            ApplyPosition(shownPosition);
            return;
        }

        if (!playOnEnable)
        {
            CacheShownPosition();
            ApplyPosition(shownPosition);
            return;
        }

        CacheShownPosition();
        Vector3 hiddenPosition = shownPosition + hiddenOffset;
        ApplyPosition(hiddenPosition);
        StartAnimation(hiddenPosition, shownPosition);
    }

    private void OnDisable()
    {
        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
            activeAnimation = null;
        }

        if (hasStoredPosition)
        {
            ApplyPosition(shownPosition);
        }
    }

    public void PlayShowAnimation()
    {
        CacheShownPosition();
        Vector3 hiddenPosition = shownPosition + hiddenOffset;
        ApplyPosition(hiddenPosition);
        StartAnimation(hiddenPosition, shownPosition);
    }

    public void PlayHideAnimation()
    {
        CacheShownPosition();
        Vector3 hiddenPosition = shownPosition + hiddenOffset;
        StartAnimation(GetCurrentPosition(), hiddenPosition);
    }

    private void CacheShownPosition()
    {
        shownPosition = GetCurrentPosition();
        hasStoredPosition = true;
    }

    private Vector3 GetCurrentPosition()
    {
        if (useRectTransform)
        {
            return rectTransform.anchoredPosition3D;
        }

        return cachedTransform.localPosition;
    }

    private void ApplyPosition(Vector3 position)
    {
        if (useRectTransform)
        {
            rectTransform.anchoredPosition3D = position;
        }
        else
        {
            cachedTransform.localPosition = position;
        }
    }

    private void StartAnimation(Vector3 from, Vector3 to)
    {
        if (!gameObject.activeInHierarchy)
        {
            ApplyPosition(to);
            return;
        }

        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
        }

        activeAnimation = StartCoroutine(AnimatePosition(from, to));
    }

    private IEnumerator AnimatePosition(Vector3 from, Vector3 to)
    {
        ApplyPosition(from);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, animationDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = animationCurve.Evaluate(t);
            ApplyPosition(Vector3.LerpUnclamped(from, to, eased));
            yield return null;
        }

        ApplyPosition(to);
        activeAnimation = null;
    }
}
