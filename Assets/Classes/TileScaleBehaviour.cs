using UnityEngine;

/// <summary>
/// Applies a serialized scale to the tile's instantiated GameObject at runtime.
/// Attach this script to a prefab referenced by a RuleTile to control the tile's size.
/// </summary>
public class TileScaleBehaviour : MonoBehaviour
{
    [Tooltip("Scale applied to this tile instance when it spawns.")]
    [SerializeField]
    private Vector3 scale = Vector3.one;

    [Tooltip("Apply the scale change in Awake (before the first frame).")]
    [SerializeField]
    private bool applyOnAwake = true;

    private void Awake()
    {
        if (applyOnAwake)
        {
            ApplyScale();
        }
    }

    private void OnEnable()
    {
        if (!applyOnAwake)
        {
            ApplyScale();
        }
    }

    private void OnValidate()
    {
        // Keep scale components reasonable and avoid negative zero confusions in the inspector.
        scale.x = Mathf.Approximately(scale.x, 0f) ? 0f : scale.x;
        scale.y = Mathf.Approximately(scale.y, 0f) ? 0f : scale.y;
        scale.z = Mathf.Approximately(scale.z, 0f) ? 0f : scale.z;
    }

    /// <summary>
    /// Applies the configured scale immediately.
    /// </summary>
    public void ApplyScale()
    {
        transform.localScale = scale;
    }
}
