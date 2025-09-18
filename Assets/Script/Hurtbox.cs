using UnityEngine;

public class Hurtbox : MonoBehaviour
{
    public PlayerHealth playerHealth; // assign in Inspector or auto-find

    void Awake()
    {
        if (!playerHealth)
            playerHealth = GetComponentInParent<PlayerHealth>();
    }

    // Optional helpers enemies can use if they rely on triggers.
    public void TakeDamage(int dmg)
    {
        if (!playerHealth) return;
        playerHealth.TakeDamage(dmg);
    }

    private void OnDrawGizmosSelected()
    {
        var sc = GetComponent<SphereCollider>();
        if (!sc) return;
        Gizmos.color = Color.red;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireSphere(sc.center, sc.radius);
    }
}
