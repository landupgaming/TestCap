using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class MinotaurAI : EnemyBase
{
    [Header("Ranges")]
    public float chaseRange = 10f;
    public float meleeRange = 2f;

    [Header("Damage")]
    public int punchDamage = 12;
    public int headbuttDamage = 16;
    public int chargeDamage = 25;

    [Header("Charge")]
    public float chargeSpeed = 12f;
    public float chargeDuration = 1.2f;
    public float chargeCooldown = 3f;

    bool charging = false;
    float lastChargeTime = -999f;

    void Update()
    {
        if (!player) return;

        // If we’re not on a mesh yet, don’t touch the agent this frame.
        if (!TryEnsureOnNavMesh()) return;

        float dist = Vector3.Distance(transform.position, player.position);
        FaceFlat(player.position);

        if (charging) return;

        if (dist <= meleeRange)
        {
            SafeStop(true);
            if (anim && anim.runtimeAnimatorController)
                anim.SetTrigger(Random.value < 0.5f ? "Punch" : "Headbutt");
            else
                AE_PunchHit(); // no anim? still apply damage
        }
        else if (dist <= chaseRange)
        {
            bool canCharge = (Time.time - lastChargeTime) >= chargeCooldown && dist > meleeRange + 2f;
            if (canCharge && Random.value < 0.3f)
            {
                StartCoroutine(DoCharge());
            }
            else
            {
                SafeStop(false);
                SafeSetDestination(player.position);
                if (anim && anim.runtimeAnimatorController)
                    anim.SetFloat("Speed", AgentReady() ? agent.velocity.magnitude : 0f);
            }
        }
        else
        {
            SafeStop(true);
            if (anim && anim.runtimeAnimatorController)
                anim.SetFloat("Speed", 0f);
        }
    }

    IEnumerator DoCharge()
    {
        charging = true;
        if (anim && anim.runtimeAnimatorController) anim.SetTrigger("Charge");

        // small wind-up
        yield return new WaitForSeconds(0.35f);

        // If we lost the NavMesh during the wind-up, bail
        if (!TryEnsureOnNavMesh()) { charging = false; yield break; }

        float savedSpeed = agent.speed;
        agent.speed = chargeSpeed;

        SafeStop(false);
        SafeSetDestination(player.position);

        float t = 0f;
        while (t < chargeDuration)
        {
            // If for some reason we get knocked off the mesh mid-charge, stop safely.
            if (!AgentReady()) break;
            t += Time.deltaTime;
            yield return null;
        }

        if (AgentReady())
        {
            agent.speed = savedSpeed;
            agent.ResetPath();
            SafeStop(true);
        }

        lastChargeTime = Time.time;
        charging = false;
    }

    // Animation Events (or called directly without anim)
    public void AE_PunchHit() { TryMeleeDamage(punchDamage, meleeRange + 0.5f); }
    public void AE_HeadbuttHit() { TryMeleeDamage(headbuttDamage, meleeRange + 0.5f); }

    void TryMeleeDamage(int dmg, float range)
    {
        if (!player) return;
        if (Vector3.Distance(transform.position, player.position) <= range)
            player.GetComponent<PlayerHealth>()?.TakeDamage(dmg);
    }

    void OnCollisionEnter(Collision c)
    {
        if (!charging) return;
        if (c.collider.CompareTag("Player"))
            c.collider.GetComponent<PlayerHealth>()?.TakeDamage(chargeDamage);
    }
}
