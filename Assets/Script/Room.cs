using System.Linq;
using UnityEngine;

public class Room : MonoBehaviour
{
    [SerializeField] private Doorway[] doorways;

    [Tooltip("Optional: a BoxCollider (IsTrigger) that outlines the room footprint for generation overlap checks.")]
    public Collider generationBounds; // assign in Inspector, or auto-found

    void Awake()
    {
        RefreshDoorwaysIfNeeded();
        AutoFindBoundsIfNeeded();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        RefreshDoorwaysIfNeeded();
        AutoFindBoundsIfNeeded();
    }
#endif

    void RefreshDoorwaysIfNeeded()
    {
        if (doorways == null || doorways.Length == 0 || doorways.Any(d => d == null))
            doorways = GetComponentsInChildren<Doorway>(true);
    }

    void AutoFindBoundsIfNeeded()
    {
        if (generationBounds == null)
        {
            // prefer a collider named "Bounds" if present
            generationBounds = GetComponentsInChildren<Collider>(true)
                               .FirstOrDefault(c => c.gameObject.name.ToLower().Contains("bounds"));
        }
    }

    public Doorway[] Doorways
    {
        get { RefreshDoorwaysIfNeeded(); return doorways; }
    }

    public Doorway GetDoorway(Direction dir)
    {
        RefreshDoorwaysIfNeeded();
        for (int i = 0; i < doorways.Length; i++)
        {
            var d = doorways[i];
            if (d != null && d.direction == dir) return d;
        }
        return null;
    }

    public bool HasDoor(Direction dir)
    {
        RefreshDoorwaysIfNeeded();
        for (int i = 0; i < doorways.Length; i++)
        {
            var d = doorways[i];
            if (d != null && d.direction == dir) return true;
        }
        return false;
    }

    // Used by the generator to get a good footprint for overlap
    public Bounds GetFootprintBounds()
    {
        if (generationBounds != null) return generationBounds.bounds;

        // Fallback: combine all colliders + renderers
        var cols = GetComponentsInChildren<Collider>(true);
        var rends = GetComponentsInChildren<Renderer>(true);

        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool init = false;

        if (cols.Length > 0)
        {
            b = cols[0].bounds; init = true;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
        }
        if (rends.Length > 0)
        {
            if (!init) { b = rends[0].bounds; init = true; }
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        }

        if (!init) b = new Bounds(transform.position, Vector3.one); // last resort
        return b;
    }
}
