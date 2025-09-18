using UnityEngine;

public class MedusaAI : EnemyBase
{
    [Header("Ranges")]
    public float chaseRange = 9f;
    public float meleeRange = 2.2f;

    [Header("Attack")]
    public int meleeDamage = 8;
    public float stunDuration = 1.5f;

    void Update()
    {
        if (!player) return;
        if (!TryEnsureOnNavMesh()) return;      // early out until we’re on a mesh

        float dist = Vector3.Distance(transform.position, player.position);

        // Chase
        if (dist <= chaseRange && dist > meleeRange)
        {
            SafeStopResume(false);
            SafeSetDestination(player.position);
            FaceFlat(player.position);
        }
        // Melee range — stop agent (don’t slide)
        else if (dist <= meleeRange)
        {
            SafeStopResume(true);
            FaceFlat(player.position);
            // trigger your attack anim here
        }
    }

    // Animation Event
    public void AE_MeleeHit()
    {
        if (!player) return;
        if (Vector3.Distance(transform.position, player.position) <= meleeRange + 0.5f)
        {
            var ph = player.GetComponent<PlayerHealth>();
            if (ph) ph.TakeDamage(meleeDamage);

            var pc = player.GetComponent<PlayerController>();
            if (pc) pc.Stun(stunDuration);   // requires PlayerController.Stun (below)
        }
    }
}
