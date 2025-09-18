using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject[] roomPrefabs;
    public GameObject startRoomPrefab;
    public GameObject closedDoorPrefab; // optional

    [Header("Generation")]
    public int minRooms = 20;
    public int maxRooms = 40;
    public int branchLength = 5;
    [Tooltip("How many different prefabs to try for each door before giving up.")]
    public int triesPerDoor = 6;

    [Header("Placement Grid (XZ)")]
    [Tooltip("World XZ cell size; set to your room outer size (e.g., 30x30).")]
    public Vector2 cellSize = new Vector2(30f, 30f);
    [Range(0.90f, 1f)] public float boundsShrink = 0.98f;

    [Header("Player Teleport")]
    [Tooltip("Which tag to teleport to the start room after generation.")]
    public string playerTag = "Player";
    [Tooltip("Layers considered 'ground' when finding a safe Y.")]
    public LayerMask groundMask = ~0;
    [Tooltip("Vertical offset so the player doesn't clip into the floor.")]
    public float playerYOffset = 0.5f;

    [Header("Debug")]
    public bool debugLogs = false;
    public bool debugDrawOverlap = false;

    private readonly List<Doorway> openDoors = new List<Doorway>();
    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    private Transform _generatedRoot;

    void Start() => GenerateDungeon();

    public void GenerateDungeon()
    {
        // Cleanup
        foreach (Transform child in transform) Destroy(child.gameObject);

        _generatedRoot = new GameObject("GeneratedDungeon").transform;
        _generatedRoot.parent = transform;

        openDoors.Clear();
        occupiedCells.Clear();

        // Start room
        if (!startRoomPrefab)
        {
            Debug.LogError("[DG] startRoomPrefab not assigned.");
            return;
        }

        GameObject startRoom = Instantiate(startRoomPrefab, Vector3.zero, Quaternion.identity, _generatedRoot);
        startRoom.tag = "Room";
        Room start = startRoom.GetComponent<Room>();
        if (!start)
        {
            Debug.LogError("[DG] Start room missing Room component.");
            return;
        }

        foreach (var d in start.Doorways) d.isConnected = false;

        openDoors.AddRange(start.Doorways);
        occupiedCells.Add(ToCell(Vector3.zero));
        int totalRooms = 1;

        // Branches
        foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
        {
            var startDoor = start.GetDoorway(dir);
            if (startDoor == null) continue;
            GenerateBranch(startDoor, dir, branchLength, _generatedRoot, ref totalRooms);
        }

        // Fill until max
        while (totalRooms < maxRooms && openDoors.Count > 0)
        {
            var current = openDoors[Random.Range(0, openDoors.Count)];
            if (!TryExpandAtDoor(current, _generatedRoot, ref totalRooms, out _))
            {
                openDoors.Remove(current);
            }
        }

        // Final: per-room plug sealers
        int sealedTotal = SealUnmatchedDoorsInAllRooms(_generatedRoot);
        if (debugLogs) Debug.Log($"[DG] Rooms={occupiedCells.Count}, sealedWithPlugs={sealedTotal}");

        // Bake navmesh then choose boss room (new API calls)
        var baker = Object.FindFirstObjectByType<NavMeshBakerAtRuntime>(FindObjectsInactive.Include);
        baker?.BakeNow();

        var bossSel = Object.FindFirstObjectByType<BossRoomSelector>(FindObjectsInactive.Include);
        bossSel?.PickBossRoom();

        // Teleport anything with the player tag to the start room center (safe floor Y + offset)
        TeleportPlayersToStart(start);
    }

    void GenerateBranch(Doorway origin, Direction branchDir, int length, Transform parent, ref int totalRooms)
    {
        Doorway current = origin;

        for (int i = 0; i < length && totalRooms < maxRooms; i++)
        {
            if (current == null) return;

            if (!TryExpandAtDoor(current, parent, ref totalRooms, out Room placed))
                return;

            Doorway next = ChooseNextForBranch(placed, branchDir, current.GetOppositeDirection());
            if (next == null) return;

            current = next;
        }
    }

    bool TryExpandAtDoor(Doorway currentDoor, Transform parent, ref int roomsPlaced, out Room newRoomScript)
    {
        newRoomScript = null;

        Direction need = currentDoor.GetOppositeDirection();
        var candidates = roomPrefabs
            .Select(p => p.GetComponent<Room>())
            .Where(r => r != null && r.HasDoor(need))
            .Select(r => r.gameObject)
            .ToList();

        if (candidates.Count == 0) return false;

        int attempts = Mathf.Min(triesPerDoor, candidates.Count);
        for (int t = 0; t < attempts; t++)
        {
            var prefab = candidates[Random.Range(0, candidates.Count)];

            if (!ComputeSnapPosition(prefab, need, currentDoor.transform.position, out var spawnPos))
                continue;

            var cell = ToCell(spawnPos);
            if (occupiedCells.Contains(cell)) continue;

            GameObject newRoom = Instantiate(prefab, spawnPos, Quaternion.identity, parent);
            newRoom.tag = "Room";
            var rs = newRoom.GetComponent<Room>();
            foreach (var d in rs.Doorways) d.isConnected = false;

            if (IsOverlapping(rs))
            {
                Destroy(newRoom);
                continue;
            }

            // Connect
            currentDoor.isConnected = true;
            var opposite = rs.GetDoorway(need);
            if (opposite != null) opposite.isConnected = true;

            roomsPlaced++;
            occupiedCells.Add(cell);

            openDoors.Remove(currentDoor);
            foreach (var d in rs.Doorways)
                if (!d.isConnected && d != opposite && !openDoors.Contains(d))
                    openDoors.Add(d);

            newRoomScript = rs;
            return true;
        }

        openDoors.Remove(currentDoor);
        return false;
    }

    Doorway ChooseNextForBranch(Room placed, Direction branchDir, Direction connectedTo)
    {
        var straight = placed.GetDoorway(branchDir);
        if (straight != null && !straight.isConnected) return straight;

        foreach (var d in placed.Doorways)
            if (!d.isConnected && d.direction != connectedTo)
                return d;

        return null;
    }

    int SealUnmatchedDoorsInAllRooms(Transform root)
    {
        int total = 0;
        var managers = root.GetComponentsInChildren<RoomSealManager>(true);
        foreach (var m in managers)
            if (m != null) total += m.SealUnmatched();
        return total;
    }

    bool IsOverlapping(Room room)
    {
        Bounds b = room.GetFootprintBounds();
        Vector3 half = b.extents * boundsShrink;

        if (debugDrawOverlap) DrawWireBox(b.center, half * 2f, Color.red, 0.25f);

        var hits = Physics.OverlapBox(
            b.center,
            half,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Collide
        );

        foreach (var h in hits)
        {
            if (h == null) continue;
            if (h.transform.root == room.transform) continue;
            var root = h.transform.root;
            if (root != null && root.CompareTag("Room")) return true;
        }
        return false;
    }

    Vector2Int ToCell(Vector3 p)
    {
        int cx = Mathf.RoundToInt(p.x / Mathf.Max(0.0001f, cellSize.x));
        int cz = Mathf.RoundToInt(p.z / Mathf.Max(0.0001f, cellSize.y));
        return new Vector2Int(cx, cz);
    }

    bool ComputeSnapPosition(GameObject roomPrefab, Direction doorwayNeeded, Vector3 targetDoorWorldPos, out Vector3 spawnPos)
    {
        spawnPos = Vector3.zero;

        GameObject temp = Instantiate(roomPrefab);
        var tempRoom = temp.GetComponent<Room>();
        var match = tempRoom != null ? tempRoom.GetDoorway(doorwayNeeded) : null;

        if (tempRoom == null || match == null)
        {
            Destroy(temp);
            return false;
        }

        Vector3 offset = temp.transform.position - match.transform.position;
        spawnPos = targetDoorWorldPos + offset;

        Destroy(temp);
        return true;
    }

    void DrawWireBox(Vector3 center, Vector3 size, Color color, float duration)
    {
        Vector3 half = size * 0.5f;
        Vector3[] p = new Vector3[8]
        {
            center + new Vector3(-half.x, -half.y, -half.z),
            center + new Vector3( half.x, -half.y, -half.z),
            center + new Vector3( half.x, -half.y,  half.z),
            center + new Vector3(-half.x, -half.y,  half.z),
            center + new Vector3(-half.x,  half.y, -half.z),
            center + new Vector3( half.x,  half.y, -half.z),
            center + new Vector3( half.x,  half.y,  half.z),
            center + new Vector3(-half.x,  half.y,  half.z),
        };
        Debug.DrawLine(p[0], p[1], color, duration);
        Debug.DrawLine(p[1], p[2], color, duration);
        Debug.DrawLine(p[2], p[3], color, duration);
        Debug.DrawLine(p[3], p[0], color, duration);
        Debug.DrawLine(p[4], p[5], color, duration);
        Debug.DrawLine(p[5], p[6], color, duration);
        Debug.DrawLine(p[6], p[7], color, duration);
        Debug.DrawLine(p[7], p[4], color, duration);
        Debug.DrawLine(p[0], p[4], color, duration);
        Debug.DrawLine(p[1], p[5], color, duration);
        Debug.DrawLine(p[2], p[6], color, duration);
        Debug.DrawLine(p[3], p[7], color, duration);
    }

    // ----------------------------------------------------------------------
    // Player teleport to start room
    // ----------------------------------------------------------------------
    void TeleportPlayersToStart(Room startRoom)
    {
        // Center of the room's footprint (XZ)
        Bounds b = startRoom.GetFootprintBounds();
        Vector3 pos = b.center;

        // Find floor Y by raycasting down from above
        Vector3 probe = pos + Vector3.up * 10f;
        if (Physics.Raycast(probe, Vector3.down, out var hit, 50f, groundMask, QueryTriggerInteraction.Ignore))
            pos = hit.point;

        pos += Vector3.up * playerYOffset;

        // Teleport all objects with the tag
        GameObject[] players;
        try
        {
            players = GameObject.FindGameObjectsWithTag(playerTag);
        }
        catch
        {
            // Tag not defined — nothing to do
            if (debugLogs) Debug.LogWarning($"[DG] Tag '{playerTag}' not defined; no players teleported.");
            return;
        }

        foreach (var go in players)
        {
            if (!go) continue;

            // If they have a NavMeshAgent and you want to avoid "not on navmesh" spam,
            // you could Warp() here. For your project you're using Rigidbody movement,
            // so a simple transform move is fine.
            go.transform.position = pos;
        }

        if (debugLogs) Debug.Log($"[DG] Teleported {players.Length} object(s) tagged '{playerTag}' to start room at {pos}.");
    }
}
