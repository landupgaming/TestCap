using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBase : MonoBehaviour
{
    protected Transform player;
    protected NavMeshAgent agent;
    protected Animator anim;

    [Header("NavMesh")]
    [Tooltip("How far we search for the nearest NavMesh position if spawned off-mesh.")]
    public float sampleRadius = 3f;

    protected virtual void Awake()
    {
        player = GameObject.FindWithTag("Player")?.transform;
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>(); // optional
    }

    protected virtual void OnEnable()
    {
        TryEnsureOnNavMesh();
    }

    // -------------------------
    // NavMesh safety primitives
    // -------------------------

    /// Ensure the NavMeshAgent is on a NavMesh. If not, sample and warp.
    protected bool TryEnsureOnNavMesh()
    {
        if (agent == null || !agent.enabled) return false;
        if (agent.isOnNavMesh) return true;

        if (NavMesh.SamplePosition(transform.position, out var hit, sampleRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }
        return false;
    }

    /// Safe SetDestination (no-op until agent is on the mesh).
    protected bool SafeSetDestination(Vector3 worldPos)
    {
        if (!TryEnsureOnNavMesh()) return false;
        return agent.SetDestination(worldPos);
    }

    /// Safe stop/resume toggle for agents.
    protected void SafeStopResume(bool stop)
    {
        if (!TryEnsureOnNavMesh()) return;
        agent.isStopped = stop;
    }

    /// Flat face toward a target (Y locked).
    protected void FaceFlat(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    // ------------------------------------------------------------
    // Compatibility shims so existing AI code compiles without edits
    // ------------------------------------------------------------

    // Some of your scripts call AgentReady() like a method:
    protected bool AgentReady()
    {
        return agent != null && agent.enabled && agent.isOnNavMesh;
    }

    // Keep property form too (if any script used it as a property).
    protected bool AgentReadyProp
    {
        get { return AgentReady(); }
    }

    // Some scripts call SafeStop() with no args:
    protected void SafeStop()
    {
        SafeStopResume(true);
    }

    // Some scripts call SafeStop(bool): true = stop, false = resume.
    protected void SafeStop(bool stop)
    {
        SafeStopResume(stop);
    }

    // If any script calls SafeResume(), map it here:
    protected void SafeResume()
    {
        SafeStopResume(false);
    }

    // If any script passes a duration (e.g., SafeStop(1.0f)), we stop then auto-resume:
    protected void SafeStop(float seconds)
    {
        SafeStopResume(true);
        if (seconds > 0f) Invoke(nameof(SafeResume), seconds);
    }
}
