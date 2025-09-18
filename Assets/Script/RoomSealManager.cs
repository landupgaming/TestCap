using UnityEngine;
using System.Linq;

[DisallowMultipleComponent]
public class RoomSealManager : MonoBehaviour
{
    [Tooltip("Optional explicit list. Leave empty to auto-find all DoorwayCap components in children.")]
    public DoorwayCap[] caps;

    Room _room;

    void Awake()
    {
        _room = GetComponent<Room>();
        if (caps == null || caps.Length == 0)
            caps = GetComponentsInChildren<DoorwayCap>(true);
        // Make absolutely sure plugs are off at start
        foreach (var c in caps) if (c != null) c.Deactivate();
    }

    /// <summary>Seal every unconnected doorway in this room; returns how many got sealed.</summary>
    public int SealUnmatched()
    {
        if (_room == null)
            _room = GetComponent<Room>();

        if (_room == null || caps == null) return 0;

        int sealedCount = 0;

        // Build quick lookup: Doorway -> Cap
        // (Assumes one cap per doorway; if multiple, you can change to FirstOrDefault/where to pick by name)
        var map = caps
            .Where(c => c != null && c.doorway != null)
            .ToDictionary(c => c.doorway, c => c);

        foreach (var door in _room.Doorways)
        {
            if (door == null) continue;

            if (!door.isConnected && map.TryGetValue(door, out var cap))
            {
                cap.Activate();
                door.isConnected = true; // mark as sealed
                sealedCount++;
            }
            else if (map.TryGetValue(door, out var capToHide))
            {
                // This door is connected; ensure its plug stays off
                capToHide.Deactivate();
            }
        }

        return sealedCount;
    }
}
