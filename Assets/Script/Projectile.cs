using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Kinetics")]
    public float speed = 20f;
    public float lifeTime = 5f;

    [Header("Damage")]
    public int damage = 10;

    [Header("AOE (0 = none)")]
    public float explosionRadius = 0f;
    public GameObject impactEffect;

    [Header("Ownership / Safety")]
    [Tooltip("Set by the shooter so we can ignore collisions with the owner.")]
    public GameObject owner;
    [Tooltip("Ignore world/static hits for this long after spawn to avoid instant pops.")]
    public float graceTimeWorldHit = 0.05f;

    Vector3 dir = Vector3.forward;
    float born;

    void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnEnable()
    {
        born = Time.time;
        CancelInvoke();
        Invoke(nameof(SelfDestruct), lifeTime);
    }

    public void Initialize(Vector3 direction, GameObject ownerObj = null)
    {
        dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        owner = ownerObj;

        // Ignore owner’s colliders if we know them
        if (owner)
        {
            var myCol = GetComponent<Collider>();
            if (myCol)
            {
                foreach (var c in owner.GetComponentsInChildren<Collider>())
                    if (c && c.enabled) Physics.IgnoreCollision(myCol, c, true);
            }
        }
    }

    void Update()
    {
        transform.position += dir * (speed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other) return;

        // Ignore collisions with the owner (self-fire)
        if (owner && other.transform.IsChildOf(owner.transform)) return;

        bool dealt = false;

        // Player?
        var ph = other.GetComponentInParent<PlayerHealth>();
        if (ph)
        {
            ph.TakeDamage(damage);
            dealt = true;
        }

        // Generic enemy?
        if (!dealt)
        {
            var eh = other.GetComponentInParent<EnemyHealth>();
            if (eh)
            {
                eh.TakeDamage(damage);
                dealt = true;
            }
        }

        // Zeus boss?
        if (!dealt)
        {
            var zeus = other.GetComponentInParent<ZeusBossController>();
            if (zeus)
            {
                zeus.TakeDamage(damage);
                dealt = true;
            }
        }

        // World/static geometry (after grace)
        if (!dealt)
        {
            bool isWorld = other.gameObject.isStatic || other.attachedRigidbody == null;
            if (isWorld && (Time.time - born) >= graceTimeWorldHit)
                dealt = true;
        }

        if (dealt)
        {
            if (explosionRadius > 0f)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, ~0, QueryTriggerInteraction.Ignore);
                foreach (var h in hits)
                {
                    if (owner && h.transform.IsChildOf(owner.transform)) continue;

                    var eh2 = h.GetComponentInParent<EnemyHealth>();
                    if (eh2) eh2.TakeDamage(damage);

                    var z2 = h.GetComponentInParent<ZeusBossController>();
                    if (z2) z2.TakeDamage(damage);

                    var ph2 = h.GetComponentInParent<PlayerHealth>();
                    if (ph2) ph2.TakeDamage(damage);
                }
            }

            if (impactEffect) Instantiate(impactEffect, transform.position, Quaternion.identity);
            SelfDestruct();
        }
    }

    void SelfDestruct()
    {
        CancelInvoke();
        Destroy(gameObject);
    }
}
