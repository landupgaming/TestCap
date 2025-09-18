using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Room))]
[RequireComponent(typeof(RoomMinimapBeacon))]
public class RoomAutoRevealBinder : MonoBehaviour
{
    [Header("How to find the bounds child")]
    [Tooltip("First tries exact name, then substring match, then tag then layer.")]
    public string preferredChildName = "Bounds";
    public string childNameContains = "bound";           // case-insensitive
    public string preferredTag = "RoomBounds";           // optional: create this tag if you like
    public string preferredLayer = "";                   // optional: e.g., "Bounds"

    [Header("If no bounds child is found, auto-create a trigger from room extents")]
    public bool createTriggerIfMissing = true;
    public string createdChildName = "MinimapBounds";
    public float triggerPadding = 0.25f;                 // shrink slightly so it sits inside walls
    public float triggerHeight = 1.0f;                   // thin trigger height
    public float yOffset = 0.1f;                         // lift above floor

    void Awake()
    {
        // Try to find an existing child to host the trigger
        var target = FindExistingBoundsChild();

        if (!target && createTriggerIfMissing)
            target = CreateTriggerChildFromRoomBounds();

        if (!target) return; // nothing to bind

        // Ensure it has a collider (trigger) + RoomRevealTrigger
        var go = target.gameObject;

        var col = go.GetComponent<Collider>();
        if (!col) col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;

        var rrt = go.GetComponent<RoomRevealTrigger>();
        if (!rrt) rrt = go.AddComponent<RoomRevealTrigger>();

        // Auto-wire beacon
        if (!rrt.beacon) rrt.beacon = GetComponent<RoomMinimapBeacon>();
    }

    Transform FindExistingBoundsChild()
    {
        // 1) exact name
        var exact = transform.Find(preferredChildName);
        if (exact) return exact;

        // 2) substring (case-insensitive)
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t == transform) continue;
            var n = t.name.ToLowerInvariant();
            if (!string.IsNullOrEmpty(childNameContains) && n.Contains(childNameContains.ToLowerInvariant()))
                return t;
        }

        // 3) tag
        if (!string.IsNullOrEmpty(preferredTag))
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.CompareTag(preferredTag)) return t;
        }

        // 4) layer
        if (!string.IsNullOrEmpty(preferredLayer))
        {
            int layer = LayerMask.NameToLayer(preferredLayer);
            if (layer >= 0)
            {
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                    if (t.gameObject.layer == layer) return t;
            }
        }

        return null;
    }

    Transform CreateTriggerChildFromRoomBounds()
    {
        // Try to get bounds from the Room script if it exposes them
        Bounds b = new Bounds(transform.position, Vector3.one * 10f);

        var room = GetComponent<Room>();
        bool haveRoomBounds = false;

        // If your Room has GetFootprintBounds(), use it (we added this earlier)
        try
        {
            var method = typeof(Room).GetMethod("GetFootprintBounds");
            if (method != null)
            {
                b = (Bounds)method.Invoke(room, null);
                haveRoomBounds = true;
            }
        }
        catch { /* ignore and fall back */ }

        if (!haveRoomBounds)
        {
            // Fallback: compute from colliders/renderers in children
            bool initialized = false;
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (!initialized) { b = c.bounds; initialized = true; }
                else b.Encapsulate(c.bounds);
            }
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!initialized) { b = r.bounds; initialized = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!initialized) b = new Bounds(transform.position, new Vector3(30f, 2f, 30f)); // worst-case fallback
        }

        var go = new GameObject(createdChildName);
        var t = go.transform;
        t.SetParent(transform, false);

        var bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true;

        // Shrink a tad so it sits inside walls
        Vector3 size = b.size;
        size.x = Mathf.Max(0.1f, size.x - triggerPadding * 2f);
        size.z = Mathf.Max(0.1f, size.z - triggerPadding * 2f);
        size.y = triggerHeight;

        t.position = new Vector3(b.center.x, b.min.y + yOffset + size.y * 0.5f, b.center.z);
        bc.size = size;

        return t;
    }
}
