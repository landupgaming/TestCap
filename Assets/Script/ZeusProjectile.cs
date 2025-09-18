using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ZeusProjectile : MonoBehaviour
{
    public int damage = 10;
    public float speed = 16f;
    public float life = 6f;
    public GameObject owner;

    [Tooltip("Ignore collisions with world/static geometry until this much time has passed.")]
    public float minAliveTimeBeforeWorldHit = 0f;

    float born;

    void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnEnable()
    {
        born = Time.time;
        Invoke(nameof(SelfDestruct), life);
    }

    void Update()
    {
        transform.position += transform.forward * (speed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other) return;

        // Ignore owner
        if (owner && other.transform.IsChildOf(owner.transform)) return;

        // Player hit?
        var ph = other.GetComponentInParent<PlayerHealth>();
        if (ph)
        {
            ph.TakeDamage(damage);
            SelfDestruct();
            return;
        }

        // World/static hit (after grace period)
        if (Time.time - born >= minAliveTimeBeforeWorldHit)
        {
            if (other.gameObject.isStatic || other.attachedRigidbody == null)
                SelfDestruct();
        }
    }

    void SelfDestruct()
    {
        CancelInvoke();
        Destroy(gameObject);
    }
}
