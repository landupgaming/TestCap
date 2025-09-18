using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ZeusBossController : MonoBehaviour
{
    [Header("General")]
    public int maxHealth = 500;
    public float attackPause = 1.0f;
    public bool facePlayer = true;
    public float turnSpeed = 6f;

    [Header("References")]
    public Transform shotOrigin;                 // defaults to transform
    public GameObject smallBoltPrefab;
    public GameObject bigBoltPrefab;
    public GameObject rotatingWallPrefab;        // optional; will auto-create if null

    [Header("Attack Weights (relative)")]
    [Min(0)] public float weightBasic = 1f;
    [Min(0)] public float weightThrow = 1f;
    [Min(0)] public float weightWall = 1f;

    [Header("Basic Punch Shot")]
    public float basicWindup = 0.15f;
    public float smallBoltSpeed = 18f;
    public int smallBoltDamage = 12;
    public float smallForward = 0.7f;
    public float smallUp = 0.2f;

    [Header("Giant Throw")]
    public float throwWindup = 0.30f;
    public float bigBoltSpeed = 12f;
    public int bigBoltDamage = 28;
    public float bigScale = 1.5f;
    public float bigForward = 1.0f;
    public float bigUp = 0.35f;

    [Header("Rotating Wall")]
    public float slamWindup = 0.35f;
    public float wallDuration = 5f;
    public float wallAngularSpeed = 70f;
    public int wallTouchDamage = 18;

    [Header("Environment")]
    public LayerMask groundMask = ~0;            // to nudge spawn above floor if needed

    // ---- Health / events -----------------------------------------------------
    public event Action onBossDied;
    public event Action<int, int> onHealthChanged; // (current, max)
    public int CurrentHP => hp;
    public int MaxHP => maxHealth;

    // ---- internals -----------------------------------------------------------
    Transform player;
    int hp;
    Bounds roomBounds;
    Rigidbody rb;
    Collider col;

    public void Initialize(BossRoomController room, Bounds bounds) => roomBounds = bounds;

    void Awake()
    {
        hp = maxHealth;
        if (!shotOrigin) shotOrigin = transform;

        var pc = FindFirstObjectByType<PlayerController>();
        if (pc) player = pc.transform;

        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        if (rb) { rb.isKinematic = true; rb.useGravity = false; }
        if (col) col.isTrigger = false;

        onHealthChanged?.Invoke(hp, maxHealth);
    }

    void OnEnable() => StartCoroutine(Brain());

    IEnumerator Brain()
    {
        yield return new WaitForSeconds(0.5f);

        while (hp > 0)
        {
            if (facePlayer && player)
            {
                Vector3 dir = player.position - transform.position; dir.y = 0;
                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(dir),
                        Time.deltaTime * turnSpeed
                    );
            }

            switch (PickAttackIndex())
            {
                case 0: yield return StartCoroutine(Attack_Basic()); break;
                case 1: yield return StartCoroutine(Attack_Throw()); break;
                case 2: yield return StartCoroutine(Attack_Wall()); break;
            }

            yield return new WaitForSeconds(attackPause);
        }
    }

    int PickAttackIndex()
    {
        float w0 = Mathf.Max(0, weightBasic);
        float w1 = Mathf.Max(0, weightThrow);
        float w2 = Mathf.Max(0, weightWall);
        float sum = w0 + w1 + w2; if (sum <= 0) { w0 = w1 = w2 = 1; sum = 3; }

        float r = UnityEngine.Random.value * sum;
        if (r < w0) return 0; r -= w0;
        if (r < w1) return 1;
        return 2;
    }

    // -------------------- Attacks --------------------

    IEnumerator Attack_Basic()
    {
        yield return new WaitForSeconds(basicWindup);
        if (!smallBoltPrefab) yield break;

        Vector3 dir = AimAtPlayerOrForward();
        Vector3 spawn = shotOrigin.position + transform.forward * smallForward + transform.up * smallUp;
        spawn = NudgeAboveGround(spawn);

        var go = Instantiate(smallBoltPrefab, spawn, Quaternion.LookRotation(dir, Vector3.up));
        SetupProjectile(go, smallBoltDamage, smallBoltSpeed, 1f, dir);
    }

    IEnumerator Attack_Throw()
    {
        yield return new WaitForSeconds(throwWindup);
        if (!bigBoltPrefab) yield break;

        Vector3 dir = AimAtPlayerOrForward();
        Vector3 spawn = shotOrigin.position + transform.forward * bigForward + transform.up * bigUp;
        spawn = NudgeAboveGround(spawn);

        var go = Instantiate(bigBoltPrefab, spawn, Quaternion.LookRotation(dir, Vector3.up));
        SetupProjectile(go, bigBoltDamage, bigBoltSpeed, bigScale, dir);
    }

    IEnumerator Attack_Wall()
    {
        yield return new WaitForSeconds(slamWindup);

        GameObject template = rotatingWallPrefab;
        if (!template)
        {
            template = new GameObject("RotatingWall");
            var box = template.AddComponent<BoxCollider>(); box.isTrigger = true;
            template.AddComponent<LightningWall>(); // your damage/rotate behaviour
        }

        float length = Mathf.Max(roomBounds.size.x, roomBounds.size.z) + 1f;
        float thickness = 0.8f;

        var wallGO = Instantiate(template, transform.position, Quaternion.identity);
        wallGO.transform.localScale = new Vector3(length, 2f, thickness);

        var lw = wallGO.GetComponent<LightningWall>();
        if (!lw) lw = wallGO.AddComponent<LightningWall>();
        lw.damage = wallTouchDamage;
        lw.angularSpeed = wallAngularSpeed;
        lw.duration = wallDuration;
        lw.pivot = transform;

        yield return new WaitForSeconds(wallDuration);
    }

    // -------------------- Helpers --------------------

    Vector3 AimAtPlayerOrForward()
    {
        if (player)
        {
            Vector3 d = player.position - shotOrigin.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.001f) return d.normalized;
        }
        return transform.forward;
    }

    Vector3 NudgeAboveGround(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, out var hit, 0.2f, groundMask, QueryTriggerInteraction.Ignore))
            p.y = hit.point.y + 0.05f;
        return p;
    }

    void SetupProjectile(GameObject go, int dmg, float spd, float scale, Vector3 dir)
    {
        if (scale != 1f) go.transform.localScale *= Mathf.Max(0.01f, scale);

        // Ensure generic Projectile
        var proj = go.GetComponent<Projectile>();
        if (!proj) proj = go.AddComponent<Projectile>();
        proj.damage = dmg;
        proj.speed = spd;
        proj.graceTimeWorldHit = 0.07f;

        // *** THE IMPORTANT LINE: pass the aim direction AND owner ***
        proj.Initialize(dir, this.gameObject);

        // Ignore collisions with the boss’ own colliders
        var myCols = GetComponentsInChildren<Collider>();
        var projCol = go.GetComponent<Collider>();
        if (projCol)
            foreach (var c in myCols)
                if (c && c.enabled) Physics.IgnoreCollision(projCol, c, true);
    }

    // -------------------- Health --------------------

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || hp <= 0) return;
        hp = Mathf.Max(0, hp - amount);
        onHealthChanged?.Invoke(hp, maxHealth);

        if (hp <= 0)
        {
            StopAllCoroutines();
            onBossDied?.Invoke();
            Destroy(gameObject, 2f); // hook death VFX here if needed
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        weightBasic = Mathf.Max(0f, weightBasic);
        weightThrow = Mathf.Max(0f, weightThrow);
        weightWall  = Mathf.Max(0f, weightWall);
        maxHealth   = Mathf.Max(1, maxHealth);
    }
#endif
}
