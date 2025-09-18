using UnityEngine;

public class MedusaAI : EnemyBase
{
    [Header("Ranges")]
    [Tooltip("How far Medusa will start chasing.")]
    public float chaseRange = 9f;

    [Tooltip("Nominal melee range measured from Medusa's pivot to the player's collider surface.")]
    public float meleeRange = 2.2f;

    [Header("Attack")]
    [Tooltip("Damage dealt on a successful melee.")]
    public int meleeDamage = 8;

    [Tooltip("How long the player is stunned on hit.")]
    public float stunDuration = 1.5f;

    [Tooltip("Cooldown between melee attempts (seconds).")]
    public float meleeCooldown = 0.7f;

    [Header("Hit Detection (forgiveness)")]
    [Tooltip("Extra forgiveness added to melee range for the animation event hit check.")]
    public float meleeForgiveness = 0.5f;

    [Tooltip("Radius of OverlapSphere used at swing time.")]
    public float meleeHitRadius = 0.9f;

    [Tooltip("Vertical offset of the OverlapSphere from Medusa's pivot (helps line up with chest height).")]
    public float meleeHitHeight = 0.9f;

    [Tooltip("Forward offset of the OverlapSphere from Medusa's pivot.")]
    public float meleeHitForward = 0.9f;

    [Tooltip("Layers considered hittable (put Player/Hurtbox here).")]
    public LayerMask meleeHitMask = ~0;

    // cache
    Collider playerCol;
    float nextMeleeAllowed;

    void Update()
    {
        if (!player) return;
        if (!TryEnsureOnNavMesh()) return;

        // Cache player collider once
        if (!playerCol) playerCol = player.GetComponentInParent<Collider>();

        // Keep agent close enough to actually hit
        if (AgentReady())
        {
            float desiredStop = Mathf.Max(0f, meleeRange - 0.1f);
            if (agent.stoppingDistance != desiredStop)
                agent.stoppingDistance = desiredStop;
        }

        float distSurface = DistanceToPlayerSurface();

        // Rotate toward player on the plane
        FaceFlat(player.position);

        // Decide state
        if (distSurface <= meleeRange && Time.time >= nextMeleeAllowed)
        {
            // In melee range: stop and attack
            SafeStopResume(true);
            if (anim && anim.runtimeAnimatorController)
                anim.SetTrigger("Melee");   // animation should call AE_MeleeHit
            else
                AE_MeleeHit();               // no anim? still apply damage

            nextMeleeAllowed = Time.time + meleeCooldown;
        }
        else if (distSurface <= chaseRange)
        {
            // Chase
            SafeStopResume(false);
            SafeSetDestination(player.position);

            if (anim && anim.runtimeAnimatorController)
                anim.SetFloat("Speed", AgentReady() ? agent.velocity.magnitude : 0f);
        }
        else
        {
            // Idle
            SafeStopResume(true);
            if (anim && anim.runtimeAnimatorController)
                anim.SetFloat("Speed", 0f);
        }
    }

    float DistanceToPlayerSurface()
    {
        Vector3 myPos = transform.position;
        if (playerCol)
        {
            Vector3 closest = playerCol.ClosestPoint(myPos);
            return Vector3.Distance(myPos, closest);
        }
        return Vector3.Distance(transform.position, player.position);
    }

    // -------- Animation Event (or called directly without anim) ----------
    public void AE_MeleeHit()
    {
        if (!player) return;

        // 1) Primary: OverlapSphere in front of Medusa at chest height (reliable)
        Vector3 origin = transform.position
                       + transform.forward * meleeHitForward
                       + Vector3.up * meleeHitHeight;

        var hits = Physics.OverlapSphere(origin, Mathf.Max(0.05f, meleeHitRadius), meleeHitMask, QueryTriggerInteraction.Ignore);

        // Try to apply to the best target found in the sphere
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];

            // Prefer a Hurtbox if present
            var hb = h.GetComponentInParent<Hurtbox>() ?? h.GetComponent<Hurtbox>();
            if (hb)
            {
                hb.TakeDamage(meleeDamage);

                var pc = hb.GetComponentInParent<PlayerController>();
                if (pc && !pc.IsStunned && !pc.IsStunImmune)
                    pc.Stun(stunDuration);

                return;
            }

            // Otherwise hit PlayerHealth via parent
            var ph = h.GetComponentInParent<PlayerHealth>() ?? h.GetComponent<PlayerHealth>();
            if (ph)
            {
                ph.TakeDamage(meleeDamage);

                var pc = ph.GetComponentInParent<PlayerController>();
                if (pc && !pc.IsStunned && !pc.IsStunImmune)
                    pc.Stun(stunDuration);

                return;
            }
        }

        // 2) Fallback: collider-surface distance check (for near-misses)
        float distSurface = DistanceToPlayerSurface();
        if (distSurface <= meleeRange + Mathf.Abs(meleeForgiveness))
        {
            var ph = player.GetComponentInParent<PlayerHealth>();
            if (ph) ph.TakeDamage(meleeDamage);

            var pc = player.GetComponentInParent<PlayerController>();
            if (pc && !pc.IsStunned && !pc.IsStunImmune)
                pc.Stun(stunDuration);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Vector3 origin = transform.position
                       + transform.forward * meleeHitForward
                       + Vector3.up * meleeHitHeight;
        Gizmos.DrawWireSphere(origin, Mathf.Max(0.05f, meleeHitRadius));
    }
#endif
}
