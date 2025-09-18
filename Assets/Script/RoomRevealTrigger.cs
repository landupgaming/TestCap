using UnityEngine;

/// Put a trigger collider in each room (set IsTrigger = true).
/// When the Player enters, the room is revealed on the minimap.
[RequireComponent(typeof(Collider))]
public class RoomRevealTrigger : MonoBehaviour
{
    [Tooltip("Optional: explicit beacon for this room. If not set, we reveal by world position.")]
    public RoomMinimapBeacon beacon;

    [Tooltip("MinimapController reference. If null, we'll FindObjectOfType.")]
    public MinimapController minimap;

    void Awake()
    {
        // Ensure trigger
        var c = GetComponent<Collider>();
        if (c && !c.isTrigger) c.isTrigger = true;

        if (minimap == null)
            minimap = FindFirstObjectByType<MinimapController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (minimap == null)
        {
            Debug.LogWarning("[RoomRevealTrigger] No MinimapController found.");
            return;
        }

        if (beacon != null)
        {
            // Ensure beacon has grid computed/registered already
            if (beacon.grid == Vector2Int.zero)
                beacon.grid = minimap.WorldToGridPublic(beacon.transform.position);

            minimap.RevealRoom(beacon.grid);
        }
        else
        {
            // Fallback: reveal at this trigger's world position
            minimap.RevealAtWorldPosition(transform.position);
        }
    }
}
