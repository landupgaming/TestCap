using UnityEngine;

/// Put this on the BossRoomTrigger prefab (with BossRoomController + BoxCollider).
/// At runtime it resizes the BoxCollider to cover the parent room's bounds.
[RequireComponent(typeof(BoxCollider))]
public class AutoFitRoomTrigger : MonoBehaviour
{
    public Transform roomRootOverride;   // leave empty to use transform.parent
    public float height = 3f;            // trigger thickness in Y

    void Awake()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;

        Transform roomRoot = roomRootOverride ? roomRootOverride : transform.parent;
        if (!roomRoot)
        {
            Debug.LogWarning("[AutoFitRoomTrigger] No room root (needs to be parented under a room).");
            return;
        }

        Bounds b = ComputeBounds(roomRoot);
        // Set size/center in local space
        Vector3 localCenter = roomRoot.InverseTransformPoint(b.center);
        Vector3 localMin = roomRoot.InverseTransformPoint(b.min);
        Vector3 localMax = roomRoot.InverseTransformPoint(b.max);
        Vector3 sizeLS = localMax - localMin;

        // Move the trigger inside the room root and align to its center
        transform.localPosition = localCenter;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        col.center = Vector3.zero;
        col.size = new Vector3(Mathf.Abs(sizeLS.x), Mathf.Max(0.5f, height), Mathf.Abs(sizeLS.z));
    }

    static Bounds ComputeBounds(Transform root)
    {
        bool has = false;
        Bounds b = new Bounds(root.position, Vector3.zero);

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }

        foreach (var c in root.GetComponentsInChildren<Collider>(true))
        { if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds); }

        if (!has) b = new Bounds(root.position, new Vector3(12, 2, 12));
        b.center = new Vector3(b.center.x, root.position.y, b.center.z);
        return b;
    }
}
