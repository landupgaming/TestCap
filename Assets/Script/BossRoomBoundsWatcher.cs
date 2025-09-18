using UnityEngine;

/// Put this on the *same object* as BossRoomController in the boss room.
/// It uses the room's Bounds/RoomBounds child to detect the player.
[DefaultExecutionOrder(50)]
public class BossRoomBoundsWatcher : MonoBehaviour
{
    public enum StartMode { ImmediateOnEnter, DwellTime, DelayAfterEnter }

    [Header("Links")]
    public BossRoomController controller;     // auto-find on Awake
    public Transform boundsChild;             // auto-find by name if null

    [Header("Mode")]
    public StartMode mode = StartMode.ImmediateOnEnter;
    public float seconds = 0.75f;             // used by dwell/delay
    public bool cancelDelayIfExit = true;

    [Header("Detection")]
    public float innerMargin = 0.35f;         // how far inside before triggering
    public bool checkY = false;

    // runtime
    Transform player;
    bool inside;
    float tInside, delayTimer;
    Bounds lastBounds;
    bool haveBounds;

    void Awake()
    {
        if (!controller) controller = GetComponent<BossRoomController>();
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc) player = pc.transform;
        if (!boundsChild) boundsChild = BossRoomController.FindBoundsChild(transform);
    }

    void Update()
    {
        if (!controller || controller.engaged || !player || !boundsChild) return;

        haveBounds = TryGetBounds(boundsChild, out lastBounds);
        if (!haveBounds) return;

        var p = player.position;
        if (!checkY) p.y = lastBounds.center.y;

        var shrink = ShrinkXZ(lastBounds, innerMargin);
        bool nowInside = shrink.Contains(p);

        switch (mode)
        {
            case StartMode.ImmediateOnEnter:
                if (!inside && nowInside) controller.EngageFight();
                break;

            case StartMode.DwellTime:
                if (nowInside) { tInside += Time.deltaTime; if (tInside >= seconds) controller.EngageFight(); }
                else tInside = 0f;
                break;

            case StartMode.DelayAfterEnter:
                if (!inside && nowInside) delayTimer = seconds;
                if (delayTimer > 0f)
                {
                    if (cancelDelayIfExit && !nowInside) delayTimer = 0f;
                    else { delayTimer -= Time.deltaTime; if (delayTimer <= 0f) controller.EngageFight(); }
                }
                break;
        }

        inside = nowInside;
    }

    static Bounds ShrinkXZ(Bounds b, float m)
    {
        if (m <= 0f) return b;
        var s = b.size;
        s = new Vector3(Mathf.Max(0.01f, s.x - 2f * m), s.y, Mathf.Max(0.01f, s.z - 2f * m));
        return new Bounds(b.center, s);
    }

    static bool TryGetBounds(Transform t, out Bounds b)
    {
        var bc = t.GetComponent<BoxCollider>();
        if (bc) { b = bc.bounds; return true; }

        bool has = false; b = new Bounds(t.position, Vector3.zero);
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        foreach (var c in t.GetComponentsInChildren<Collider>(true))
        { if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds); }
        if (!has) { b = new Bounds(t.position, new Vector3(8, 2, 8)); return false; }
        b.center = new Vector3(b.center.x, t.position.y, b.center.z);
        return true;
    }

    void OnDrawGizmosSelected()
    {
        if (!boundsChild) return;
        if (!haveBounds) TryGetBounds(boundsChild, out lastBounds);
        Gizmos.color = Color.cyan; Gizmos.DrawWireCube(lastBounds.center, lastBounds.size);
        var shrink = ShrinkXZ(lastBounds, innerMargin);
        Gizmos.color = Color.green; Gizmos.DrawWireCube(shrink.center, new Vector3(shrink.size.x, .02f, shrink.size.z));
    }
}
