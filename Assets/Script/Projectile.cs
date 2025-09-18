using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Motion")]
    public float speed = 28f;
    public float lifetime = 4f;
    public bool useRaycastSweep = true;

    [Header("Damage")]
    public float damage = 15f;                 // <-- ADDED: used when applying damage

    [Header("Damage Targets")]
    public bool damageEnemies = true;
    public bool damagePlayer = false;
    [Tooltip("Layers this projectile can detect for sweeping/triggers (keep broad; logic below filters actual hits).")]
    public LayerMask hitMask = ~0;

    [Header("Collision Behavior")]
    [Tooltip("If false (default), environment hits are ignored (projectile won't pop on walls).")]
    public bool collideWithEnvironment = false;
    [Tooltip("If true, the projectile only destroys when it actually dealt damage.")]
    public bool destroyOnlyOnDamage = true;
    [Tooltip("If true, destroy on any valid hit (after filters). Ignored if destroyOnlyOnDamage is true.")]
    public bool destroyOnHit = true;

    // cached
    Rigidbody rb;
    Collider col;
    Vector3 lastPos;
    float t;

    // owner data
    GameObject ownerGO;
    string ownerTag;

    // ======================
    //   PUBLIC INITIALIZE
    // ======================

    /// Preferred overload: pass owner GameObject so we can ignore its colliders.
    public void Initialize(Vector3 direction, GameObject owner)
    {
        ownerGO = owner;
        ownerTag = owner ? owner.tag : null;
        CommonInit(direction);
        IgnoreOwnerColliders();
    }

    /// Back-compat overload: older code passed only a tag.
    public void Initialize(Vector3 direction, string ownerTag)
    {
        ownerGO = null;
        this.ownerTag = ownerTag;
        CommonInit(direction);
        // can't ignore owner colliders without GO; tag checks still prevent damage
    }

    /// Back-compat overload: older code passed only a direction.
    public void Initialize(Vector3 direction)
    {
        ownerGO = null;
        ownerTag = null;
        CommonInit(direction);
    }

    // ======================
    //   LIFECYCLE
    // ======================

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    void OnEnable()
    {
        lastPos = transform.position;
        if (rb != null && rb.velocity.sqrMagnitude < 0.01f)
            rb.velocity = transform.forward * speed;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t >= lifetime) { Destroy(gameObject); return; }

        if (useRaycastSweep && rb != null && col != null)
        {
            Vector3 curPos = transform.position;
            Vector3 delta = curPos - lastPos;
            float dist = delta.magnitude;

            if (dist > 0.0001f)
            {
                float radius = 0.1f;
                if (col is SphereCollider sc) radius = sc.radius * MaxScale(transform);
                else if (col is CapsuleCollider cc) radius = Mathf.Max(cc.radius * MaxScale(transform), 0.1f);

                if (Physics.SphereCast(lastPos, radius, delta.normalized,
                                        out RaycastHit hit, dist, hitMask,
                                        QueryTriggerInteraction.Ignore))
                {
                    if (ShouldProcessCollider(hit.collider))
                    {
                        HandleHit(hit.collider, hit.point, hit.normal);
                    }
                }
            }

            lastPos = curPos;
        }
    }

    // ======================
    //   COLLISION HANDLERS
    // ======================

    void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & hitMask) == 0) return;
        if (!ShouldProcessCollider(other)) return;

        Vector3 point = other.ClosestPoint(transform.position);
        Vector3 normal = (transform.position - point).sqrMagnitude > 0.0001f
            ? (transform.position - point).normalized
            : -transform.forward;

        HandleHit(other, point, normal);
    }

    bool ShouldProcessCollider(Collider c)
    {
        if (!c) return false;

        // Skip owner via GO or tag
        if (ownerGO && (c.transform == ownerGO.transform || c.GetComponentInParent<Transform>() == ownerGO.transform))
            return false;
        if (!string.IsNullOrEmpty(ownerTag) && c.CompareTag(ownerTag))
            return false;

        // Filter by tag: only care about Player/Enemy unless collideWithEnvironment is true
        bool isPlayer = c.CompareTag("Player") || (c.attachedRigidbody && c.attachedRigidbody.gameObject.CompareTag("Player"));
        bool isEnemy = c.CompareTag("Enemy") || (c.attachedRigidbody && c.attachedRigidbody.gameObject.CompareTag("Enemy"));

        if (!isPlayer && !isEnemy && !collideWithEnvironment)
            return false; // ignore walls, room triggers, etc.

        return true;
    }

    void HandleHit(Collider other, Vector3 hitPoint, Vector3 hitNormal)
    {
        bool isPlayer = other.CompareTag("Player") || (other.attachedRigidbody && other.attachedRigidbody.gameObject.CompareTag("Player"));
        bool isEnemy = other.CompareTag("Enemy") || (other.attachedRigidbody && other.attachedRigidbody.gameObject.CompareTag("Enemy"));

        bool didDamage = false;

        if (isEnemy && damageEnemies)
        {
            var enemyHealth = other.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage((int)damage);
                didDamage = true;
            }
        }
        else if (isPlayer && damagePlayer)
        {
            var playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage((int)damage);
                didDamage = true;
            }
        }
        else
        {
            // Environment or other object (only gets here if collideWithEnvironment == true)
        }

        if (destroyOnlyOnDamage)
        {
            if (didDamage) Destroy(gameObject);
        }
        else if (destroyOnHit)
        {
            Destroy(gameObject);
        }
    }

    // ======================
    //   INTERNAL HELPERS
    // ======================

    void CommonInit(Vector3 direction)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (col == null) col = GetComponent<Collider>();

        col.isTrigger = true;

        rb.useGravity = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        rb.velocity = direction.normalized * speed;

        lastPos = transform.position;
        t = 0f;
    }

    void IgnoreOwnerColliders()
    {
        if (!ownerGO || !col) return;
        var ownerColliders = ownerGO.GetComponentsInChildren<Collider>(includeInactive: true);
        foreach (var oc in ownerColliders)
        {
            if (oc && oc != col) Physics.IgnoreCollision(col, oc, true);
        }
    }

    static float MaxScale(Transform t)
    {
        var s = t.lossyScale;
        return Mathf.Max(s.x, Mathf.Max(s.y, s.z));
    }
}
