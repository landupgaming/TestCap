using UnityEngine;

/// Attach this to your player (or weapon). Call Fire(dir) to shoot.
/// If you were previously calling Initialize(dir, "Player"), this script
/// fixes that by passing the actual owner GameObject.
public class KiBlast : MonoBehaviour
{
    [Header("Spawn")]
    public Transform firePoint;                 // where to spawn from
    public GameObject projectilePrefab;         // must have/allow a Projectile component
    [Tooltip("Spawn a little forward so it doesn't start inside the player collider.")]
    public float forwardOffset = 0.3f;

    [Header("Projectile Defaults")]
    public float speed = 20f;
    public int damage = 10;
    public float lifetime = 5f;
    [Tooltip("Ignore world/static hits for this long after spawn to avoid instant pops.")]
    public float graceTimeWorldHit = 0.05f;

    void Awake()
    {
        if (!firePoint) firePoint = transform;
    }

    /// Call this from your PlayerController with your aim direction.
    public void Fire(Vector3 dir)
    {
        if (!projectilePrefab)
        {
            Debug.LogError("[KiBlast] projectilePrefab not assigned.");
            return;
        }

        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        Vector3 spawnPos = firePoint.position + dir * Mathf.Max(0f, forwardOffset);

        // Spawn and face the aim direction
        GameObject go = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(dir, Vector3.up));

        // Ensure it has a Projectile
        var proj = go.GetComponent<Projectile>();
        if (!proj) proj = go.AddComponent<Projectile>();

        // Initialize the projectile with direction + OWNER GAMEOBJECT (not a string/tag)
        proj.Initialize(dir, this.gameObject);

        // Apply default projectile settings (optional but handy)
        proj.speed = speed;
        proj.damage = damage;
        proj.lifeTime = lifetime;
        proj.graceTimeWorldHit = graceTimeWorldHit;

        // Try to ignore owner collisions immediately (safety)
        var projCol = go.GetComponent<Collider>();
        if (projCol)
        {
            foreach (var c in GetComponentsInChildren<Collider>())
                if (c && c.enabled) Physics.IgnoreCollision(projCol, c, true);
        }
    }
}
