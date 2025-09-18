using System.Collections;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// Drop this anywhere in your scene (or on the Player).
/// Hotkeys in Play Mode:
///   B = Teleport player to Boss Room
///   N = Ping/Highlight the Boss Room
public class DebugBossTools : MonoBehaviour
{
    [Header("Who to move")]
    [Tooltip("Player root. If null, will try PlayerController in the scene.")]
    public Transform player;

    [Tooltip("Optional camera rig to move with player (keeps framing). Leave null to ignore.")]
    public Transform cameraRig;

    [Header("Boss room detection")]
    [Tooltip("If you already have the boss marker, drag it here to skip auto-search.")]
    public Transform bossRoomRoot;

    [Tooltip("Extra height above the floor when teleporting the player.")]
    public float spawnYOffset = 0.5f;

    [Tooltip("How long the ping beacon stays visible (seconds).")]
    public float pingDuration = 3f;

    [Tooltip("Layers considered 'ground' when finding a safe Y.")]
    public LayerMask groundMask = ~0;

    [Header("Keys")]
    public KeyCode teleportKey = KeyCode.B;
    public KeyCode pingKey = KeyCode.N;

    // cached
    MinimapController minimap;

    void Awake()
    {
        if (!player)
        {
            var pc = FindFirstPlayerController();
            if (pc) player = pc.transform;
        }
        if (!cameraRig && Camera.main) cameraRig = Camera.main.transform;
        minimap = FindFirstMinimap();
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        if (Input.GetKeyDown(teleportKey)) TeleportToBoss();
        if (Input.GetKeyDown(pingKey)) PingBoss();
    }

#if UNITY_EDITOR
    [MenuItem("Debug/Teleport To Boss Room %#b")] // Ctrl/Cmd + Shift + B
    static void TeleportMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter Play Mode to use Teleport To Boss Room.");
            return;
        }
        var tool = FindFirstDebugBossTools() ?? new GameObject("DebugBossTools_Auto").AddComponent<DebugBossTools>();
        tool.TeleportToBoss();
    }
#endif

    // ——— Main actions ———

    public void TeleportToBoss()
    {
        if (!player)
        {
            Debug.LogWarning("[DebugBossTools] No player found.");
            return;
        }
        var boss = GetBossRoot();
        if (!boss)
        {
            Debug.LogWarning("[DebugBossTools] Could not find a boss room in the scene.");
            return;
        }

        var b = ComputeWorldBoundsXZ(boss);
        Vector3 center = b.center;

        // Try to find a safe Y on ground at the room center
        float targetY = player.position.y;
        Vector3 probe = center + Vector3.up * 10f;
        if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 50f, groundMask, QueryTriggerInteraction.Ignore))
            targetY = hit.point.y + spawnYOffset;

        Vector3 targetPos = new Vector3(center.x, targetY, center.z);

        // Move player
        player.position = targetPos;

        // Optionally move camera rig (only if it's not parented to the player)
        if (cameraRig && cameraRig.parent == null)
        {
            Vector3 cam = cameraRig.position;
            cameraRig.position = new Vector3(targetPos.x, cam.y, targetPos.z);
        }

        // Reveal on minimap if your controller is present
        if (minimap) minimap.RevealAtWorldPosition(center);

        Debug.Log("[DebugBossTools] Teleported to Boss Room at " + center);
    }

    public void PingBoss()
    {
        var boss = GetBossRoot();
        if (!boss)
        {
            Debug.LogWarning("[DebugBossTools] Could not find a boss room to ping.");
            return;
        }
        StartCoroutine(PingRoutine(boss));
    }

    // ——— Helpers ———

    Transform GetBossRoot()
    {
        if (bossRoomRoot && bossRoomRoot.gameObject.activeInHierarchy) return bossRoomRoot;

        // 1) Strong types
        var marker = FindFirst<BossRoomMarker>(includeInactive: true);
        if (marker) return marker.transform;

        var controller = FindFirst<BossRoomController>(includeInactive: true);
        if (controller) return controller.transform;

        // 2) Tag
        var tagged = GameObject.FindGameObjectWithTag("BossRoom");
        if (tagged) return tagged.transform;

        // 3) Name heuristic
        var all = FindAll<Transform>(includeInactive: true);
        var guess = all.FirstOrDefault(t =>
            t && t.name.ToLower().Contains("boss") && t.name.ToLower().Contains("room"));
        if (guess) return guess;

        return null;
    }

    static Bounds ComputeWorldBoundsXZ(Transform root)
    {
        bool has = false;
        Bounds b = new Bounds(root.position, Vector3.zero);

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        foreach (var c in root.GetComponentsInChildren<Collider>(true))
        {
            if (!has) { b = c.bounds; has = true; }
            else b.Encapsulate(c.bounds);
        }

        if (!has) return new Bounds(root.position, new Vector3(4, 0.1f, 4));
        // flatten Y
        b.center = new Vector3(b.center.x, 0f, b.center.z);
        b.extents = new Vector3(b.extents.x, 0.1f, b.extents.z);
        return b;
    }

    IEnumerator PingRoutine(Transform boss)
    {
        var b = ComputeWorldBoundsXZ(boss);
        Vector3 pos = b.center + Vector3.up * 2f;

        float t = 0f;
        while (t < pingDuration)
        {
            float s = Mathf.Lerp(0.5f, 2.5f, Mathf.PingPong(Time.time * 2f, 1f));
            Debug.DrawLine(pos + Vector3.left * s, pos + Vector3.right * s, Color.green, 0f, false);
            Debug.DrawLine(pos + Vector3.forward * s, pos + Vector3.back * s, Color.green, 0f, false);
            Debug.DrawRay(pos, Vector3.up * 1.5f, Color.yellow, 0f, false);
            t += Time.deltaTime;
            yield return null;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var boss = bossRoomRoot ? bossRoomRoot : GetBossRoot();
        if (!boss) return;

        var b = ComputeWorldBoundsXZ(boss);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(new Vector3(b.center.x, 0f, b.center.z),
                            new Vector3(b.size.x, 0.02f, b.size.z));
        Handles.color = Color.magenta;
        Handles.Label(new Vector3(b.center.x, 0f, b.center.z) + Vector3.up * 1f, "Boss Room");
    }
#endif

    // =========================
    // Cross-version find helpers
    // =========================

    static T FindFirst<T>(bool includeInactive) where T : Object
    {
        // Newer Unity (2023+) prefers enum overloads:
        //   FindFirstObjectByType<T>(FindObjectsInactive)
        // Older Unity has:
        //   FindObjectOfType<T>(bool includeInactive)
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
        return Object.FindObjectOfType<T>(includeInactive);
#endif
    }

    static T[] FindAll<T>(bool includeInactive) where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                                           FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>(includeInactive);
#endif
    }

    static PlayerController FindFirstPlayerController() => FindFirst<PlayerController>(includeInactive: false);
    static MinimapController FindFirstMinimap() => FindFirst<MinimapController>(includeInactive: true);

#if UNITY_EDITOR
    static DebugBossTools FindFirstDebugBossTools() => FindFirst<DebugBossTools>(includeInactive: true);
#endif
}
