using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class RoomContentSpawner : MonoBehaviour
{
    [Header("Placement")]
    public bool randomizePositions = false;
    public int numberOfRandomPoints = 3;
    public float roomHalfSize = 15f; // rooms 30x30 => half=15

    [Header("Spawn Rules")]
    public int minEnemies = 1;
    public GameObject[] enemyPrefabs; // DO NOT include ZeusBoss
    public GameObject trapPrefab;

    [Tooltip("Layer(s) considered as ground for raycast snapping.")]
    public string groundLayerName = "Ground";
    [Tooltip("Objects to avoid when placing enemies/traps (pillars, walls, props).")]
    public LayerMask obstacleMask;
    [Tooltip("Clear radius around spawn to avoid clipping pillars/walls.")]
    public float clearanceRadius = 0.6f;
    [Tooltip("Max attempts when searching for a valid random position.")]
    public int maxPlacementTries = 30;

    [Header("Offsets")]
    public float enemyYOffset = 0.05f;
    public float trapYOffset = 0.01f;

    [Header("NavMesh")]
    public bool snapToNavMesh = true;
    public float navMeshSampleMaxDistance = 2.0f;

    private readonly List<Transform> spawnPoints = new List<Transform>();
    private int groundMask;

    void Awake()
    {
        groundMask = string.IsNullOrEmpty(groundLayerName) ? ~0 : LayerMask.GetMask(groundLayerName);
    }

    void Start()
    {
        // Skip if boss room
        if (GetComponentInParent<BossRoomMarker>() != null) return;

        // Collect manual points WITHOUT requiring a tag (no more CompareTag)
        foreach (Transform child in transform)
        {
            // accept by name to avoid tag dependency
            string n = child.name.ToLowerInvariant();
            if (n.Contains("spawnpoint") || n.Contains("spawn_point") || n == "spawn")
                spawnPoints.Add(child);
        }

        if (randomizePositions)
            GenerateRandomSpawnPoints();

        ExecuteSpawns();
    }

    void GenerateRandomSpawnPoints()
    {
        for (int i = 0; i < numberOfRandomPoints; i++)
        {
            if (TryGetValidRandomPoint(out Vector3 pos))
            {
                var go = new GameObject("SpawnPoint_Random");
                go.transform.SetParent(transform, worldPositionStays: true);
                go.transform.position = pos;
                spawnPoints.Add(go.transform);
            }
        }
    }

    bool TryGetValidRandomPoint(out Vector3 validPos)
    {
        validPos = transform.position;

        for (int t = 0; t < maxPlacementTries; t++)
        {
            float rx = Random.Range(-roomHalfSize + 1f, roomHalfSize - 1f);
            float rz = Random.Range(-roomHalfSize + 1f, roomHalfSize - 1f);
            Vector3 candidateTop = transform.position + new Vector3(rx, 3f, rz); // cast from above

            if (!RaycastToGround(candidateTop, out Vector3 groundPos)) continue;
            if (IsBlocked(groundPos, clearanceRadius)) continue;

            if (snapToNavMesh && TrySnapToNavMesh(groundPos, out Vector3 navPos))
            {
                validPos = navPos;
                return true;
            }

            validPos = groundPos;
            return true;
        }
        return false;
    }

    enum SpawnType { None, Enemy, Trap }

    void ExecuteSpawns()
    {
        if (spawnPoints.Count == 0) return;

        SpawnType[] outcomes = new SpawnType[spawnPoints.Count];
        int enemyCount = 0;

        for (int i = 0; i < outcomes.Length; i++)
        {
            float r = Random.value;
            outcomes[i] = r < 0.33f ? SpawnType.Enemy : (r < 0.66f ? SpawnType.Trap : SpawnType.None);
            if (outcomes[i] == SpawnType.Enemy) enemyCount++;
        }

        // Ensure minimum enemies
        for (int i = 0; i < outcomes.Length && enemyCount < minEnemies; i++)
        {
            if (outcomes[i] != SpawnType.Enemy)
            {
                outcomes[i] = SpawnType.Enemy;
                enemyCount++;
            }
        }

        for (int i = 0; i < outcomes.Length; i++)
        {
            Transform p = spawnPoints[i];

            // find a ground-snapped, obstacle-free position
            Vector3 spawnPos = p.position;
            if (!RaycastToGround(spawnPos + Vector3.up * 3f, out spawnPos))
                RaycastToGround(transform.position + Vector3.up * 3f, out spawnPos);

            if (IsBlocked(spawnPos, clearanceRadius))
            {
                if (!TryGetValidRandomPoint(out spawnPos))
                    continue; // give up this spawn
            }

            // optionally snap to navmesh
            if (snapToNavMesh && TrySnapToNavMesh(spawnPos, out Vector3 nm))
                spawnPos = nm;

            switch (outcomes[i])
            {
                case SpawnType.Enemy:
                    if (enemyPrefabs.Length > 0)
                    {
                        int idx = Random.Range(0, enemyPrefabs.Length);
                        var enemy = Instantiate(enemyPrefabs[idx],
                            spawnPos + Vector3.up * enemyYOffset, Quaternion.identity);

                        // Defer enabling the agent until a navmesh position is available (prevents “no valid NavMesh” errors)
                        var agent = enemy.GetComponent<NavMeshAgent>();
                        if (agent)
                        {
                            agent.enabled = false;
                            StartCoroutine(EnableAgentWhenReady(agent, spawnPos + Vector3.up * enemyYOffset));
                        }
                    }
                    break;

                case SpawnType.Trap:
                    if (trapPrefab)
                    {
                        Instantiate(trapPrefab,
                            spawnPos + Vector3.up * trapYOffset, Quaternion.identity);
                    }
                    break;
            }
        }
    }

    IEnumerator EnableAgentWhenReady(NavMeshAgent agent, Vector3 desiredPos)
    {
        // wait a short moment for runtime bake to complete if it’s happening this frame
        float timeout = 2f;
        float t = 0f;

        while (t < timeout)
        {
            if (NavMesh.SamplePosition(desiredPos, out NavMeshHit hit, navMeshSampleMaxDistance, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(hit.position); // safe now
                yield break;
            }
            t += Time.deltaTime;
            yield return null;
        }

        // last attempt: try enabling anyway if a mesh exists underfoot soon
        agent.enabled = true;
        if (!agent.isOnNavMesh)
        {
            // disable again to prevent spam; AI scripts will early-out when not on a mesh
            agent.enabled = false;
            Debug.LogWarning($"[{name}] Could not place NavMeshAgent on a NavMesh near {desiredPos}. " +
                             $"Ensure NavMeshSurface is baked and Ground is included.");
        }
    }

    // ---------- helpers ----------
    bool RaycastToGround(Vector3 from, out Vector3 hitPos)
    {
        hitPos = from;
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 10f, groundMask, QueryTriggerInteraction.Ignore))
        {
            hitPos = hit.point;
            return true;
        }
        return false;
    }

    bool TrySnapToNavMesh(Vector3 pos, out Vector3 snapped)
    {
        snapped = pos;
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, navMeshSampleMaxDistance, NavMesh.AllAreas))
        {
            snapped = hit.position;
            return true;
        }
        return false;
    }

    bool IsBlocked(Vector3 pos, float radius)
    {
        return Physics.CheckSphere(pos + Vector3.up * 0.1f, radius, obstacleMask, QueryTriggerInteraction.Ignore);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(roomHalfSize * 2f, 0.1f, roomHalfSize * 2f));
    }
#endif
}
