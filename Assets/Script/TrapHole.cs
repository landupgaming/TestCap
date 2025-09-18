using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TrapHole : MonoBehaviour
{
    [Tooltip("This object's collider should be set to IsTrigger.")]
    public bool requireTriggerCollider = true;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (requireTriggerCollider)
        {
            var col = GetComponent<Collider>();
            if (col && !col.isTrigger)
                Debug.LogWarning("[TrapHole] Collider should be set to IsTrigger.", this);
        }

        if (!other.CompareTag("Player")) return;

        var health = other.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.Kill(); // instant kill via public API
        }
    }
}
