using UnityEngine;

/// Simple trigger trap: damages, stuns, and/or knocks back the player on contact.
/// Put this on your trap prefab. Make sure it has a Collider with **Is Trigger** checked.
[RequireComponent(typeof(Collider))]
public class Trap : MonoBehaviour
{
    [Header("Effect")]
    public int damage = 20;                  // 0 = no damage
    public float stunSeconds = 0.25f;        // 0 = no stun
    public float knockbackForce = 8f;        // 0 = no knockback
    public bool killInstead = false;         // if true, ignores 'damage' and kills player

    [Header("Use / Cooldown")]
    public bool singleUse = true;            // destroy or disable after 1 trigger
    public float rearmDelay = 1.0f;          // for multi-use traps
    public bool destroyOnUse = false;        // if singleUse, destroy the GameObject instead of disabling collider

    [Header("Misc")]
    public Transform knockbackOrigin;        // if null, uses this.transform.position
    public LayerMask playerLayers = ~0;      // leave as Everything unless you use layers heavily

    Collider _col;
    bool _armed = true;

    void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true; // enforce trigger
        if (!knockbackOrigin) knockbackOrigin = transform;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!_armed) return;
        if (!IsPlayer(other)) return;

        // Apply effects
        var ph = other.GetComponentInParent<PlayerHealth>();
        var pc = other.GetComponentInParent<PlayerController>();
        var rb = other.attachedRigidbody ?? other.GetComponentInParent<Rigidbody>();

        if (ph)
        {
            if (killInstead) ph.Kill();
            else if (damage > 0) ph.TakeDamage(damage);
        }

        if (pc && stunSeconds > 0f)
            pc.Stun(stunSeconds);

        if (rb && knockbackForce > 0f)
        {
            Vector3 dir = (other.transform.position - knockbackOrigin.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = -knockbackOrigin.forward;
            rb.AddForce(dir.normalized * knockbackForce, ForceMode.VelocityChange);
        }

        // Arm/Disarm logic
        if (singleUse)
        {
            if (destroyOnUse) Destroy(gameObject);
            else
            {
                _col.enabled = false;
                _armed = false;
                gameObject.SetActive(false); // hide if you like
            }
        }
        else
        {
            _armed = false;
            _col.enabled = false;
            Invoke(nameof(Rearm), rearmDelay);
        }
    }

    bool IsPlayer(Collider c)
    {
        if (((1 << c.gameObject.layer) & playerLayers) == 0) return false;
        if (c.CompareTag("Player")) return true;
        var rb = c.attachedRigidbody;
        return rb && rb.gameObject.CompareTag("Player");
    }

    void Rearm()
    {
        _armed = true;
        if (_col) _col.enabled = true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }
#endif
}
