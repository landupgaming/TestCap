using UnityEngine;

[DisallowMultipleComponent]
public class DoorwayCap : MonoBehaviour
{
    [Tooltip("The Doorway this cap belongs to. If left empty, we'll auto-find on this GameObject.")]
    public Doorway doorway;

    [Tooltip("The plug GameObject (mesh + collider) to enable when sealing this doorway.")]
    public GameObject plug;

    [Header("Local Offsets (relative to the Doorway)")]
    public Vector3 localPositionOffset = Vector3.zero;
    public Vector3 localEulerOffset = Vector3.zero;
    public Vector3 localScale = Vector3.one;

    void Awake()
    {
        if (doorway == null) doorway = GetComponent<Doorway>();
        // Keep the plug fully inactive so it never participates in overlap during generation
        Deactivate();
    }

    /// <summary>Hide/disable the plug (safe to call anytime).</summary>
    public void Deactivate()
    {
        if (plug != null) plug.SetActive(false);
    }

    /// <summary>Enable the plug, align to the doorway, and make it collidable/visible.</summary>
    public void Activate()
    {
        if (doorway == null || plug == null) return;

        // Parent the plug to the Doorway so local alignment is trivial and robust
        plug.transform.SetParent(doorway.transform, worldPositionStays: false);

        // Reset then apply your per-room offsets
        plug.transform.localPosition = Vector3.zero + localPositionOffset;
        plug.transform.localEulerAngles = Vector3.zero + localEulerOffset;
        plug.transform.localScale = localScale;

        plug.SetActive(true);
    }
}
