using UnityEngine;
using System.Collections.Generic;

public class BossRoomSelector : MonoBehaviour
{
    public int minDistanceFromStart = 5;
    public GameObject bossRoomTriggerPrefab; // contains BossRoomController & a trigger collider

    [ContextMenu("Pick Boss Room Now")]
    public void PickBossRoom()
    {
        Room[] rooms = Object.FindObjectsByType<Room>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (rooms.Length == 0) return;

        float size = 30f;
        Dictionary<Vector2Int, Room> map = new();
        foreach (var r in rooms)
        {
            int x = Mathf.RoundToInt(r.transform.position.x / size);
            int y = Mathf.RoundToInt(r.transform.position.z / size);
            map[new Vector2Int(x, y)] = r;
        }

        // start ~ closest to (0,0)
        Room start = null;
        float best = float.MaxValue;
        foreach (var r in rooms)
        {
            float d = r.transform.position.sqrMagnitude;
            if (d < best) { best = d; start = r; }
        }
        if (!start) return;

        // BFS
        Dictionary<Room, int> dist = new();
        Queue<Room> q = new();
        dist[start] = 0; q.Enqueue(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            var c = new Vector2Int(Mathf.RoundToInt(cur.transform.position.x / size),
                                   Mathf.RoundToInt(cur.transform.position.z / size));
            foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
            {
                Vector2Int n = c;
                switch (dir)
                {
                    case Direction.Top: n += Vector2Int.up; break;
                    case Direction.Bottom: n += Vector2Int.down; break;
                    case Direction.Left: n += Vector2Int.left; break;
                    case Direction.Right: n += Vector2Int.right; break;
                }
                if (!map.TryGetValue(n, out var nr)) continue;
                if (!dist.ContainsKey(nr))
                {
                    dist[nr] = dist[cur] + 1;
                    q.Enqueue(nr);
                }
            }
        }

        // farthest ? min
        Room chosen = null; int bestDist = -1;
        foreach (var kv in dist)
        {
            if (kv.Value >= minDistanceFromStart && kv.Value > bestDist)
            {
                bestDist = kv.Value; chosen = kv.Key;
            }
        }
        if (!chosen)
        {
            foreach (var kv in dist)
                if (kv.Value > bestDist) { bestDist = kv.Value; chosen = kv.Key; }
        }
        if (!chosen) return;

        if (!chosen.GetComponent<BossRoomMarker>()) chosen.gameObject.AddComponent<BossRoomMarker>();

        if (bossRoomTriggerPrefab != null)
        {
            var trigger = Instantiate(bossRoomTriggerPrefab, chosen.transform);
            trigger.transform.localPosition = Vector3.zero;
        }

        Debug.Log($"[BossRoomSelector] Boss room selected at {chosen.transform.position}, distance {bestDist}");
    }
}
