using UnityEngine;
using UnityEngine.AI;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;     // adjustable move speed

    [Header("Aiming (GROUND RAY)")]
    [Tooltip("Camera used for cursor raycasts. If left empty, falls back to Camera.main.")]
    [SerializeField] private Camera aimCamera;
    public bool aimWithMouse = true;
    [Tooltip("Set this to your Ground layer.")]
    public LayerMask groundRayMask;
    [Tooltip("Project the hit point onto the NavMesh (optional but stable).")]
    public bool projectAimToNavMesh = true;
    public float navSampleRadius = 2f;
    [Tooltip("How quickly the player rotates to face the cursor.")]
    public float rotateSpeed = 18f;
    public bool faceAimDirection = true;

    [Header("Shooting (RMB)")]
    public Transform firePoint;                  // assign in Inspector
    public GameObject kiBlastPrefab;             // needs Collider(Trigger)+Rigidbody (+Projectile preferred)
    public float kiBlastCooldown = 0.15f;
    [Tooltip("Right mouse to shoot (if false, uses Fire1).")]
    public bool fireWithRightMouse = true;
    [Tooltip("Forward offset so the projectile doesn't start inside the player collider.")]
    public float spawnForwardOffset = 0.3f;

    [Header("Melee (LMB)")]
    [Tooltip("Time between melee attacks.")]
    public float meleeCooldown = 0.35f;
    [Tooltip("How far the melee can reach in world units.")]
    public float meleeRange = 2.2f;
    [Tooltip("Half-angle of the attack cone (degrees).")]
    public float meleeHalfAngle = 50f;
    [Tooltip("Vertical offset of the check from player pivot.")]
    public float meleeHeightOffset = 0.6f;
    [Tooltip("Damage dealt to enemies hit by the melee.")]
    public int meleeDamage = 20;
    [Tooltip("Knockback applied to rigidbodies hit by the melee.")]
    public float meleeKnockback = 7f;
    [Tooltip("Layers the melee can hit (put enemies here).")]
    public LayerMask meleeHitMask = ~0;
    [Tooltip("Optional VFX prefab to spawn at swing start (destroyed automatically).")]
    public GameObject meleeSwingVFX;
    [Tooltip("How long to keep the swing VFX alive.")]
    public float meleeVFXLife = 0.35f;

    [Header("Dash (Space)")]
    [Tooltip("How far the dash should travel total (world units).")]
    public float dashDistance = 5f;
    [Tooltip("How long the dash lasts in seconds.")]
    public float dashDuration = 0.18f;
    [Tooltip("Cooldown between dashes in seconds.")]
    public float dashCooldown = 0.5f;
    [Tooltip("If true, player becomes invulnerable while dashing.")]
    public bool iFramesDuringDash = true;
    [Tooltip("Extra invulnerability AFTER the dash ends (seconds).")]
    public float iFrameAfterDash = 0.05f;
    [Tooltip("Leave empty to auto-collect ALL colliders under the player. Otherwise, specify only your hurtbox colliders.")]
    public Collider[] collidersToDisableForIFrames;

    [Header("Stun")]
    public bool debugStunLogs = false;
    public float stunImmunityAfterStun = 0.35f;

    // cached
    Rigidbody rb;
    PlayerHealth playerHealth; // to optionally poke invuln, if you want on dash, etc.
    float lastShotTime;
    float lastMeleeTime;

    // stun state
    bool isStunned;
    float stunTimer;
    float stunRecoverImmunityUntil;

    // dash state
    bool isDashing;
    float dashTimer;
    float nextDashAllowedTime;
    Vector3 dashDir;
    float dashSpeed;  // computed from distance/duration
    bool iFramesActive;
    Collider[] cachedAllColliders;  // auto-populated if user left list empty

    Camera Cam => aimCamera != null ? aimCamera : Camera.main;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerHealth = GetComponent<PlayerHealth>();

        if (!firePoint)
        {
            Debug.LogWarning("[PlayerController] FirePoint not assigned — using player transform.");
            firePoint = transform;
        }
        if (Cam == null && aimWithMouse)
            Debug.LogWarning("[PlayerController] No Aim Camera set and no Camera.main found. Assign one in the Inspector.");

        // Auto-collect colliders if user didn't specify
        if (collidersToDisableForIFrames == null || collidersToDisableForIFrames.Length == 0)
        {
            cachedAllColliders = GetComponentsInChildren<Collider>(includeInactive: true);
        }
        dashSpeed = (dashDuration > 0f) ? (dashDistance / dashDuration) : 0f;
    }

    void Update()
    {
        // Stun gate
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
                stunRecoverImmunityUntil = Time.time + stunImmunityAfterStun;
                if (debugStunLogs) Debug.Log("[Player] Recovered from stun.");
            }
            return;
        }

        // Handle dash input before movement so dash overrides velocity
        if (Input.GetKeyDown(KeyCode.Space))
            TryStartDash();

        if (isDashing)
        {
            DashUpdate();
            // Rotate toward cursor while dashing
            Vector3 aimDir = GetAimDirectionFromGroundRay();
            if (faceAimDirection && aimDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(aimDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
            }
            // allow attacks during dash if you want
            HandleShootWhileAiming();
            HandleMeleeWhileAiming();
            return; // skip normal movement while dashing
        }

        HandleMovement();

        // Aim every frame (ground ray)
        Vector3 aimDir2 = GetAimDirectionFromGroundRay();
        if (faceAimDirection && aimDir2.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(aimDir2, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
        }

        // LMB melee, RMB shoot
        HandleMelee(aimDir2);   // Left mouse
        HandleShoot(aimDir2);   // Right mouse (or Fire1 if you flip the toggle)
    }

    void HandleMovement()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 v = new Vector3(x, 0f, z).normalized * moveSpeed;

        if (rb) rb.velocity = new Vector3(v.x, rb.velocity.y, v.z);
        else transform.position += v * Time.deltaTime;
    }

    // --- Dash ---
    void TryStartDash()
    {
        if (Time.time < nextDashAllowedTime || isDashing) return;

        // Dash direction = where we’re facing; if you prefer input-based, replace with input vector
        Vector3 d = transform.forward;
        if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
        d.y = 0f;
        d.Normalize();

        isDashing = true;
        dashTimer = dashDuration;
        dashDir = d;
        dashSpeed = (dashDuration > 0f) ? (dashDistance / dashDuration) : 0f;

        if (iFramesDuringDash)
            SetIFrames(true);

        // Prevent immediate re-dash
        nextDashAllowedTime = Time.time + dashCooldown;
    }

    void DashUpdate()
    {
        dashTimer -= Time.deltaTime;

        // Override horizontal velocity with dash speed; preserve Y
        if (rb)
        {
            rb.velocity = new Vector3(dashDir.x * dashSpeed, rb.velocity.y, dashDir.z * dashSpeed);
        }
        else
        {
            transform.position += dashDir * dashSpeed * Time.deltaTime;
        }

        if (dashTimer <= 0f)
        {
            isDashing = false;

            // Small grace i-frame after dash end
            if (iFramesDuringDash)
            {
                if (iFrameAfterDash > 0f)
                    Invoke(nameof(EndIFrames), iFrameAfterDash);
                else
                    SetIFrames(false);
            }
        }
    }

    void SetIFrames(bool enabled)
    {
        iFramesActive = enabled;

        // If user provided a list, use that; else use auto-collected colliders
        var list = (collidersToDisableForIFrames != null && collidersToDisableForIFrames.Length > 0)
            ? collidersToDisableForIFrames
            : cachedAllColliders;

        if (list == null) return;

        for (int i = 0; i < list.Length; i++)
        {
            var c = list[i];
            if (!c || c.isTrigger) continue; // keep triggers if they drive UI or events
            c.enabled = !enabled;
        }
    }

    void EndIFrames()
    {
        if (iFramesActive) SetIFrames(false);
    }

    // --- Aiming: ground ray using chosen camera ---
    Vector3 GetAimDirectionFromGroundRay()
    {
        var cam = Cam;
        if (!aimWithMouse || cam == null)
            return transform.forward;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // First: physics hit on your Ground layer
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundRayMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 point = hit.point;

            // Optional: project to nearest NavMesh point for stability
            if (projectAimToNavMesh && NavMesh.SamplePosition(point, out var navHit, navSampleRadius, NavMesh.AllAreas))
                point = navHit.position;

            Vector3 dir = point - firePoint.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
        }

        // Fallback: flat plane at player Y if Ground ray missed
        Plane p = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (p.Raycast(ray, out float enter))
        {
            Vector3 pt = ray.GetPoint(enter);
            Vector3 d = pt - firePoint.position; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) return d.normalized;
        }

        return transform.forward;
    }

    // --- Shooting helpers (RMB) ---
    void HandleShoot(Vector3 dir)
    {
        bool shootPressed = fireWithRightMouse ? Input.GetMouseButton(1) : Input.GetButton("Fire1");
        if (shootPressed) TryFire(dir);
    }

    void HandleShootWhileAiming()
    {
        Vector3 dir = GetAimDirectionFromGroundRay();
        HandleShoot(dir);
    }

    void TryFire(Vector3 dir)
    {
        if (Time.time - lastShotTime < kiBlastCooldown) return;
        lastShotTime = Time.time;

        // Spawn slightly ahead to avoid self-collision at start
        Vector3 spawnPos = firePoint.position + dir * Mathf.Max(0f, spawnForwardOffset);

        GameObject go = Instantiate(kiBlastPrefab, spawnPos, Quaternion.LookRotation(dir, Vector3.up));

        // Ensure a Projectile exists (auto-add if missing)
        var proj = go.GetComponent<Projectile>();
        if (!proj) proj = go.AddComponent<Projectile>();

        // Pass owner so projectile ignores our colliders
        proj.Initialize(dir, this.gameObject);
    }

    // --- Melee helpers (LMB) ---
    void HandleMelee(Vector3 aimDir)
    {
        // Left mouse click starts melee
        if (Input.GetMouseButtonDown(0))
            TryMelee(aimDir);
    }

    void HandleMeleeWhileAiming()
    {
        if (Input.GetMouseButtonDown(0))
            TryMelee(GetAimDirectionFromGroundRay());
    }

    void TryMelee(Vector3 dir)
    {
        if (Time.time - lastMeleeTime < meleeCooldown) return;
        lastMeleeTime = Time.time;

        if (meleeSwingVFX)
        {
            var v = Instantiate(meleeSwingVFX, firePoint.position, Quaternion.LookRotation(dir, Vector3.up));
            if (meleeVFXLife > 0f) Destroy(v, meleeVFXLife);
        }

        // Overlap + arc filter
        Vector3 origin = transform.position + Vector3.up * meleeHeightOffset;
        float radius = Mathf.Max(0.5f, meleeRange);
        var hits = Physics.OverlapSphere(origin + dir * (radius * 0.6f), radius, meleeHitMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            // Exclude self
            if (h.transform == transform || h.GetComponentInParent<PlayerController>() == this) continue;

            Vector3 to = (h.transform.position - transform.position); to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) continue;

            float ang = Vector3.Angle(dir, to);
            if (ang > meleeHalfAngle) continue;

            // Damage enemy if present
            var eh = h.GetComponentInParent<EnemyHealth>();
            if (eh) eh.TakeDamage(meleeDamage);

            // Knockback
            var rbTarget = h.attachedRigidbody ?? h.GetComponentInParent<Rigidbody>();
            if (rbTarget && meleeKnockback > 0f)
                rbTarget.AddForce(to.normalized * meleeKnockback, ForceMode.VelocityChange);
        }
    }

    // --- Public stun API (used by enemies) ---
    public void Stun(float seconds)
    {
        if (seconds <= 0f) return;
        if (Time.time < stunRecoverImmunityUntil) return;

        isStunned = true;
        stunTimer = seconds;

        // Cancel dash if active (and i-frames)
        if (isDashing)
        {
            isDashing = false;
            CancelInvoke(nameof(EndIFrames));
            if (iFramesDuringDash) SetIFrames(false);
        }

        if (rb) rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
        if (debugStunLogs) Debug.Log($"[Player] Stunned for {seconds:0.00}s");
    }
}
