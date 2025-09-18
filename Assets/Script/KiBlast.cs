using UnityEngine;

// Optional adapter so old prefabs still work.
// Prefer removing this and just using Projectile directly.
[DisallowMultipleComponent]
public class KiBlast : MonoBehaviour
{
    public Projectile projectile;

    void Reset()
    {
        if (projectile == null) projectile = GetComponent<Projectile>();
    }

    // Old API compatibility
    public void SetDirection(Vector3 dir)
    {
        if (projectile == null) projectile = GetComponent<Projectile>();
        if (projectile == null)
        {
            Debug.LogError("[KiBlast] No Projectile found on this GameObject.");
            return;
        }
        projectile.Initialize(dir, GetOwnerTagSafe());
    }

    string GetOwnerTagSafe()
    {
        // Try to find an owner tag up the hierarchy; fallback to none.
        var t = transform.parent;
        while (t != null)
        {
            if (!string.IsNullOrEmpty(t.tag) && t.tag != "Untagged") return t.tag;
            t = t.parent;
        }
        return null;
    }
}
