using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class RoomSealManager : MonoBehaviour
{
    [Tooltip("Optional explicit list. Leave empty to auto-find all DoorwayCap components in children.")]
    public DoorwayCap[] caps;

    private Room _room;

    void Awake()
    {
        _room = GetComponent<Room>();
        if (caps == null || caps.Length == 0)
            caps = GetComponentsInChildren<DoorwayCap>(true);
    }

    // ------------------------------------------------------------
    // Public API used by BossRoomController (and you manually)
    // ------------------------------------------------------------

    /// <summary>Seal (true) or unseal (false) every cap in this room.</summary>
    public void Seal(bool seal)
    {
        if (caps == null) return;
        foreach (var c in caps)
        {
            if (!c) continue;
            if (seal) c.Activate(); else c.Deactivate();
        }
    }

    public void SealAll() => Seal(true);
    public void UnsealAll() => Seal(false);

    // ------------------------------------------------------------
    // Compatibility API used by DungeonGenerator
    // ------------------------------------------------------------

    /// <summary>
    /// Seals only the doorways that ended up UNMATCHED (i.e., not connected to another room).
    /// Returns how many caps were turned on.
    /// </summary>
    public int SealUnmatched()
    {
        if (_room == null)
            _room = GetComponent<Room>();

        if (_room == null || caps == null || caps.Length == 0)
            return 0;

        int sealedCount = 0;

        // Map: Doorway -> Cap (first cap found for that doorway)
        var map = caps
            .Where(c => c != null && c.doorway != null)
            .GroupBy(c => c.doorway)
            .ToDictionary(g => g.Key, g => g.First());

        // For each doorway in the room, if it's not connected, make sure its cap is ON
        // If it IS connected, make sure any cap we have for it is OFF.
        foreach (var door in _room.Doorways)
        {
            if (door == null) continue;

            if (!door.isConnected)
            {
                if (map.TryGetValue(door, out var cap) && cap != null)
                {
                    cap.Activate();
                    sealedCount++;
                }
            }
            else
            {
                if (map.TryGetValue(door, out var capToHide) && capToHide != null)
                    capToHide.Deactivate();
            }
        }

        return sealedCount;
    }

    /// <summary>
    /// Overload kept for safety if some code calls with a bool (ignored; matches old signatures).
    /// </summary>
    public int SealUnmatched(bool _ignored) => SealUnmatched();
}
