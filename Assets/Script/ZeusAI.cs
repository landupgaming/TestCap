using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class ZeusAI : MonoBehaviour
{
    public Transform hand;          // where punch lightning spawns
    public Transform throwOrigin;   // projectile spawn point
    public ParticleSystem fistLightningEffect; // optional

    [Header("Ranges")]
    public float punchRange = 2.5f;
    public float throwRange = 14f;
    public float aoeMinRange = 6f;

    [Header("Damage")]
    public int punchDamage = 15;
    public int aoeDamage = 12;

    [Header("Prefabs")]
    public GameObject lightningBallPrefab;  // Projectile
    public GameObject lightningWallPrefab;  // has SpinningAttack

    Transform player;
    NavMeshAgent agent;
    Animator anim;

    void Awake()
    {
        player = GameObject.FindWithTag("Player")?.transform;
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (!player) return;

        float dist = Vector3.Distance(transform.position, player.position);
        FacePlayer();

        if (dist <= punchRange)
        {
            agent.isStopped = true;
            anim.SetTrigger("Punch");
        }
        else if (dist <= throwRange && Random.value < 0.6f)
        {
            agent.isStopped = true;
            anim.SetTrigger("Throw");
        }
        else if (dist >= aoeMinRange && Random.value < 0.3f)
        {
            agent.isStopped = true;
            anim.SetTrigger("AOE");
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
            anim.SetFloat("Speed", agent.velocity.magnitude);
        }
    }

    void FacePlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    // Animation Events:
    public void AE_PunchHit()
    {
        if (fistLightningEffect) fistLightningEffect.Play();
        if (Vector3.Distance(transform.position, player.position) <= punchRange + 0.5f)
        {
            player.GetComponent<PlayerHealth>()?.TakeDamage(punchDamage);
        }
    }

    public void AE_Throw()
    {
        if (!lightningBallPrefab || !throwOrigin) return;
        GameObject proj = Instantiate(lightningBallPrefab, throwOrigin.position, throwOrigin.rotation);
        Vector3 dir = (player.position - throwOrigin.position).normalized;
        proj.GetComponent<Projectile>()?.Initialize(dir);
    }

    public void AE_AOE()
    {
        if (!lightningWallPrefab) return;
        var wall = Instantiate(lightningWallPrefab, transform.position, Quaternion.identity);
        var spin = wall.GetComponent<SpinningAttack>();
        if (spin) spin.damageAmount = aoeDamage;
    }
}
