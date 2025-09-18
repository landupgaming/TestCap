using UnityEngine;

/// Attach this to each room root. It registers (and optionally reveals) the room on the minimap.
/// If grid isn't specified, it will be computed from world position using the MinimapController.
[DisallowMultipleComponent]
public class RoomMinimapBeacon : MonoBehaviour
{
    [Tooltip("Optional explicit grid coord for this room. If left at (0,0), we'll compute it from world position.")]
    public Vector2Int grid;

    [Tooltip("Reveal this room on Start (useful for starting room).")]
    public bool revealOnStart = false;

    [Tooltip("Reference to the MinimapController. If not set, we'll FindObjectOfType at runtime.")]
    public MinimapController minimap;

    void Awake()
    {
        if (minimap == null)
            minimap = FindFirstObjectByType<MinimapController>();
        if (minimap == null)
        {
            Debug.LogWarning($"[RoomMinimapBeacon] No MinimapController found for {name}.", this);
            return;
        }

        if (grid == Vector2Int.zero)
            grid = minimap.WorldToGridPublic(transform.position);

        // Always register so the cell exists (revealed or hidden).
        minimap.RegisterRoom(grid, revealOnStart);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, new Vector3(1f, 0.01f, 1f));
        if (minimap != null)
        {
            var g = minimap.WorldToGridPublic(transform.position);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, $"Grid {g.x},{g.y}");
        }
    }
#endif
}
