using UnityEngine;
using UnityEngine.AI;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;

    [Header("Aiming (GROUND RAY)")]
    [SerializeField] private Camera aimCamera;
    public bool aimWithMouse = true;
    public LayerMask groundRayMask;
    public bool projectAimToNavMesh = true;
    public float navSampleRadius = 2f;
    public float rotateSpeed = 18f;
    public bool faceAimDirection = true;

    [Header("Shooting (RMB)")]
    public Transform firePoint;
    public GameObject kiBlastPrefab;
    public float kiBlastCooldown = 0.15f;
    public bool fireWithRightMouse = true;
    [Tooltip("Forward offset so the projectile doesn't start inside the player collider.")]
    public float spawnForwardOffset = 0.3f;

    [Tooltip("Vertical spawn offset in world units (+ is up).")]
    public float spawnHeightOffset = 0.6f;

    [Header("Melee (LMB)")]
    public float meleeCooldown = 0.35f;
    public float meleeRange = 2.2f;
    public float meleeHalfAngle = 50f;
    public float meleeHeightOffset = 0.6f;
    public int meleeDamage = 20;
    public float meleeKnockback = 7f;
    public LayerMask meleeHitMask = ~0;
    public GameObject meleeSwingVFX;
    public float meleeVFXLife = 0.35f;

    [Header("Dash (Space)")]
    public float dashDistance = 5f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 0.5f;
    public bool iFramesDuringDash = true;
    public float iFrameAfterDash = 0.05f;
    public Collider[] collidersToDisableForIFrames;

    [Header("I-Frames Handling")]
    [Tooltip("If ON, i-frames will temporarily disable NON-trigger colliders. Leave OFF to avoid losing damage hits.")]
    public bool disableCollidersForIFrames = false;

    [Header("Stun")]
    public bool debugStunLogs = false;
    public float stunImmunityAfterStun = 0.35f;

    // --- Public stun state (for enemies to query) ---
    public bool IsStunImmune => Time.time < stunRecoverImmunityUntil;
    public bool IsStunned => isStunned;

    // cached
    Rigidbody rb;
    PlayerHealth playerHealth;
    float lastShotTime, lastMeleeTime;

    // stun state
    bool isStunned;
    float stunTimer, stunRecoverImmunityUntil;

    // dash state
    bool isDashing;
    float dashTimer, nextDashAllowedTime, dashSpeed;
    Vector3 dashDir;
    bool iFramesActive;
    Collider[] cachedAllColliders;

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

        // Cache colliders if list not provided
        if (collidersToDisableForIFrames == null || collidersToDisableForIFrames.Length == 0)
            cachedAllColliders = GetComponentsInChildren<Collider>(includeInactive: true);

        // Safety: start with all NON-trigger colliders enabled
        EnableAllNonTriggerColliders(true);

        dashSpeed = (dashDuration > 0f) ? (dashDistance / dashDuration) : 0f;
    }

    void Update()
    {
        // Stun gate + recovery -> immunity window
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
                stunRecoverImmunityUntil = Time.time + stunImmunityAfterStun;
                if (debugStunLogs) Debug.Log("[Player] Recovered from stun → immunity started.");
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space)) TryStartDash();

        if (isDashing)
        {
            DashUpdate();
            Vector3 aimDir = GetAimDirectionFromGroundRay();
            if (faceAimDirection && aimDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(aimDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
            }
            HandleShootWhileAiming();
            HandleMeleeWhileAiming();
            return;
        }

        HandleMovement();

        Vector3 aimDir2 = GetAimDirectionFromGroundRay();
        if (faceAimDirection && aimDir2.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(aimDir2, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
        }

        HandleMelee(aimDir2);
        HandleShoot(aimDir2);
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

        Vector3 d = transform.forward;
        if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
        d.y = 0f;
        d.Normalize();

        isDashing = true;
        dashTimer = dashDuration;
        dashDir = d;
        dashSpeed = (dashDuration > 0f) ? (dashDistance / dashDuration) : 0f;

        if (iFramesDuringDash) SetIFrames(true);
        nextDashAllowedTime = Time.time + dashCooldown;
    }

    void DashUpdate()
    {
        dashTimer -= Time.deltaTime;

        if (rb)
            rb.velocity = new Vector3(dashDir.x * dashSpeed, rb.velocity.y, dashDir.z * dashSpeed);
        else
            transform.position += dashDir * dashSpeed * Time.deltaTime;

        if (dashTimer <= 0f)
        {
            isDashing = false;
            if (iFramesDuringDash)
            {
                if (iFrameAfterDash > 0f) Invoke(nameof(EndIFrames), iFrameAfterDash);
                else SetIFrames(false);
            }
        }
    }

    // === I-Frames / Collider helpers ===
    void EnableAllNonTriggerColliders(bool enabled)
    {
        var list = (collidersToDisableForIFrames != null && collidersToDisableForIFrames.Length > 0)
            ? collidersToDisableForIFrames
            : cachedAllColliders;

        if (list == null) return;

        for (int i = 0; i < list.Length; i++)
        {
            var c = list[i];
            if (!c || c.isTrigger) continue;
            c.enabled = enabled;
        }
    }

    void SetIFrames(bool enabled)
    {
        iFramesActive = enabled;
        // By default, do NOT disable colliders (prevents "no damage" bugs).
        if (disableCollidersForIFrames)
            EnableAllNonTriggerColliders(!enabled);
    }

    void EndIFrames()
    {
        if (iFramesActive) SetIFrames(false);
        EnableAllNonTriggerColliders(true); // safety
    }
    // ================================

    // --- Aiming: ground ray using chosen camera ---
    Vector3 GetAimDirectionFromGroundRay()
    {
        var cam = Cam;
        if (!aimWithMouse || cam == null) return transform.forward;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundRayMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 point = hit.point;
            if (projectAimToNavMesh && NavMesh.SamplePosition(point, out var navHit, navSampleRadius, NavMesh.AllAreas))
                point = navHit.position;

            Vector3 dir = point - firePoint.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
        }

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

        Vector3 spawnPos =
            firePoint.position
            + Vector3.up * spawnHeightOffset
            + dir * Mathf.Max(0f, spawnForwardOffset);

        GameObject go = Instantiate(kiBlastPrefab, spawnPos, Quaternion.LookRotation(dir, Vector3.up));

        var proj = go.GetComponent<Projectile>();
        if (!proj) proj = go.AddComponent<Projectile>();
        proj.Initialize(dir, this.gameObject);
    }

    // --- Melee helpers (LMB) ---
    void HandleMelee(Vector3 aimDir)
    {
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

        Vector3 origin = transform.position + Vector3.up * meleeHeightOffset;
        float radius = Mathf.Max(0.5f, meleeRange);
        var hits = Physics.OverlapSphere(origin + dir * (radius * 0.6f), radius, meleeHitMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.transform == transform || h.GetComponentInParent<PlayerController>() == this) continue;

            Vector3 to = (h.transform.position - transform.position); to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) continue;

            float ang = Vector3.Angle(dir, to);
            if (ang > meleeHalfAngle) continue;

            var eh = h.GetComponentInParent<EnemyHealth>();
            if (eh) eh.TakeDamage(meleeDamage);

            var rbTarget = h.attachedRigidbody ?? h.GetComponentInParent<Rigidbody>();
            if (rbTarget && meleeKnockback > 0f)
                rbTarget.AddForce(to.normalized * meleeKnockback, ForceMode.VelocityChange);
        }
    }

    // --- Public stun API (non-refreshing; respects immunity) ---
    public void Stun(float seconds)
    {
        if (seconds <= 0f) return;

        // If already stunned, ignore further requests (prevents refresh/extend).
        if (isStunned) return;

        // Respect post-stun immunity window.
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
