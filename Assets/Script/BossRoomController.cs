using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI; // <-- for Slider

public class BossRoomController : MonoBehaviour
{
    [Header("Room")]
    public Transform roomRoot;

    [Header("Boss")]
    public GameObject zeusPrefab;
    public Transform spawnPoint;                // optional (else: center of room bounds)

    [Header("Spawn Height")]
    [Tooltip("Layers considered 'ground' for the spawn raycast.")]
    public LayerMask groundMask = ~0;
    [Tooltip("Extra clearance above the floor after accounting for Zeus's collider height.")]
    public float floorClearance = 0.05f;
    [Tooltip("If ON, XZ is snapped on NavMesh after ground ray.")]
    public bool projectToNavMesh = true;
    public float navSampleRadius = 1.0f;

    [Header("Flow")]
    public bool sealDoorsOnStart = true;
    public bool unsealOnBossDeath = true;
    public bool onlyOnce = true;

    [Header("UI")]
    [Tooltip("Boss HP bar Slider. If left empty, will try to find one named with 'boss' or tagged 'BossBar'.")]
    public Slider bossBar;

    [HideInInspector] public bool engaged;

    Bounds roomBounds;
    Component sealMgr;

    void Awake()
    {
        if (!roomRoot) roomRoot = transform;
        sealMgr = roomRoot.GetComponent(GetTypeByName("RoomSealManager"));
        roomBounds = ComputeRoomBounds(roomRoot);
    }

    public void EngageFight()
    {
        if (onlyOnce && engaged) return;
        engaged = true;

        if (sealDoorsOnStart) TrySeal(true);

        if (!zeusPrefab)
        {
            Debug.LogWarning("[BossRoomController] No Zeus prefab assigned.");
            return;
        }

        // base spawn (XZ at room center unless a specific spawn point is set)
        Vector3 pos = spawnPoint ? spawnPoint.position : roomBounds.center;

        // Optional: XZ → NavMesh
        if (projectToNavMesh && NavMesh.SamplePosition(pos, out var nHit, navSampleRadius, NavMesh.AllAreas))
            pos = nHit.position;

        // Raycast straight down to get the floor Y
        if (Physics.Raycast(pos + Vector3.up * 10f, Vector3.down, out var hit, 50f, groundMask, QueryTriggerInteraction.Ignore))
            pos.y = hit.point.y;

        // Instantiate a bit above floor, then precisely seat on floor using collider height
        Vector3 tempPos = pos + Vector3.up * 2f;
        var bossGO = Instantiate(zeusPrefab, tempPos, Quaternion.identity, roomRoot);

        // Measure Zeus's height and seat him correctly
        float halfHeight = GetHalfHeightFromColliders(bossGO);
        pos.y += halfHeight + Mathf.Max(0f, floorClearance);
        bossGO.transform.position = pos;

        var boss = bossGO.GetComponent<ZeusBossController>();
        if (!boss) boss = bossGO.AddComponent<ZeusBossController>();
        boss.Initialize(this, roomBounds);

        // --- Wire Boss HP bar ---
        EnsureBossBarReference();
        if (bossBar)
        {
            bossBar.gameObject.SetActive(true);
            bossBar.minValue = 0f;
            bossBar.maxValue = 1f;
            bossBar.value = 1f;

            boss.onHealthChanged += (cur, max) =>
            {
                bossBar.value = max > 0 ? (float)cur / max : 0f;
            };

            boss.onBossDied += () =>
            {
                if (bossBar) bossBar.gameObject.SetActive(false);
            };
        }

        if (unsealOnBossDeath) boss.onBossDied += OnBossDied;
    }

    void EnsureBossBarReference()
    {
        if (bossBar) return;

        // Try to find any slider whose name contains "boss" (safe; no tag required)
        var all = GameObject.FindObjectsOfType<Slider>(true);
        foreach (var s in all)
        {
            if (s && s.name.ToLower().Contains("boss")) { bossBar = s; return; }
        }

        // Optional: try tag if it exists. Wrap in try/catch so missing tag never throws.
        try
        {
            var tagged = GameObject.FindGameObjectWithTag("BossBar");
            if (tagged) bossBar = tagged.GetComponent<Slider>();
        }
        catch { /* tag not defined; ignore */ }
    }

    void OnBossDied() => TrySeal(false);

    // ---------- sealing (works with many manager shapes) ----------
    void TrySeal(bool seal)
    {
        if (sealMgr && TryCallSealManager(sealMgr, seal)) return;
        if (sealMgr && TryToggleCapsField(sealMgr, seal)) return;
        ToggleDoorCapsByName(roomRoot, seal);
    }

    static bool TryCallSealManager(Component mgr, bool seal)
    {
        var t = mgr.GetType();
        string[] noArgSeal = seal ? new[] { "Seal", "Close", "Lock", "SealRoom", "CloseDoors" }
                                   : new[] { "Unseal", "Open", "Unlock", "UnsealRoom", "OpenDoors" };
        foreach (var name in noArgSeal)
        {
            var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (m != null) { m.Invoke(mgr, null); return true; }
        }
        string[] boolNames = new[] { "SetSealed", "SetLocked", "Seal", "SetSeal", "SetClosed", "SetDoorsClosed" };
        foreach (var name in boolNames)
        {
            var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
            if (m != null) { m.Invoke(mgr, new object[] { seal }); return true; }
        }
        return false;
    }

    static bool TryToggleCapsField(Component mgr, bool seal)
    {
        var t = mgr.GetType();
        string[] names = new[] { "Caps", "caps", "DoorCaps", "doorCaps", "capList" };

        foreach (var n in names)
        {
            var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && ToggleListObject(f.GetValue(mgr), seal)) return true;

            var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && ToggleListObject(p.GetValue(mgr, null), seal)) return true;
        }
        return false;
    }

    static bool ToggleListObject(object listObj, bool seal)
    {
        if (listObj == null) return false;

        if (listObj is GameObject[] goArr) { foreach (var go in goArr) if (go) go.SetActive(seal); return true; }
        if (listObj is Transform[] trArr) { foreach (var tr in trArr) if (tr) tr.gameObject.SetActive(seal); return true; }

        var type = listObj.GetType();
        if (type.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(type))
        {
            var ilist = (System.Collections.IList)listObj;
            var elem = type.GetGenericArguments()[0];
            if (elem == typeof(GameObject)) { foreach (var e in ilist) if (e is GameObject go) go.SetActive(seal); return true; }
            if (elem == typeof(Transform)) { foreach (var e in ilist) if (e is Transform tr) tr.gameObject.SetActive(seal); return true; }
        }
        return false;
    }

    static void ToggleDoorCapsByName(Transform root, bool seal)
    {
        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
        {
            if (tr == root) continue;
            var n = tr.name.ToLower();
            if ((n.Contains("door") || n.Contains("doorway")) && (n.Contains("cap") || n.Contains("block")))
                tr.gameObject.SetActive(seal);
        }
    }

    // ---------- bounds & collider height helpers ----------
    static Bounds ComputeRoomBounds(Transform room)
    {
        var boundsChild = FindBoundsChild(room);
        if (boundsChild)
        {
            var bc = boundsChild.GetComponent<BoxCollider>();
            if (bc) return bc.bounds;
        }
        bool has = false;
        Bounds b = new Bounds(room.position, Vector3.zero);
        foreach (var r in room.GetComponentsInChildren<Renderer>(true)) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        foreach (var c in room.GetComponentsInChildren<Collider>(true)) { if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds); }
        if (!has) b = new Bounds(room.position, new Vector3(12, 2, 12));
        b.center = new Vector3(b.center.x, room.position.y, b.center.z);
        return b;
    }

    public static Transform FindBoundsChild(Transform room)
    {
        foreach (Transform t in room.GetComponentsInChildren<Transform>(true))
            if (t.name.ToLower().Contains("bounds")) return t;
        return null;
    }

    static float GetHalfHeightFromColliders(GameObject go)
    {
        float maxY = 0.5f;
        var cols = go.GetComponentsInChildren<Collider>();
        if (cols != null && cols.Length > 0)
        {
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (var c in cols)
            {
                var b = c.bounds;
                if (b.min.y < min) min = b.min.y;
                if (b.max.y > max) max = b.max.y;
            }
            maxY = Mathf.Max(0.01f, (max - min) * 0.5f);
        }
        else
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                foreach (var r in rends)
                {
                    var b = r.bounds;
                    if (b.min.y < min) min = b.min.y;
                    if (b.max.y > max) max = b.max.y;
                }
                maxY = Mathf.Max(0.01f, (max - min) * 0.5f);
            }
        }
        return maxY;
    }

    static Type GetTypeByName(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var tp = asm.GetType(typeName, false);
            if (tp != null) return tp;
        }
        return null;
    }
}
