using UnityEngine;

/// <summary>
/// Handles all horizontal movement, jumping, dashing, wall-sliding,
/// sprinting, rolling (dodge), and crouching.
///
/// Key features:
///   • Coyote time      — jump window after walking off a ledge
///   • Jump buffer      — jump pressed slightly before landing still fires
///   • Variable jump    — short-press = small hop, hold = full jump
///   • Wall-slide       — slow fall on walls; wall-jump pushes away
///   • Dash             — short directional dash with invincibility frames
///   • Sprint           — hold Shift to run faster (키: Left Shift)
///   • Roll / Dodge     — press Ctrl to roll with i-frames (키: Left Ctrl)
///                        롤 중 적 레이어와 물리 충돌 비활성화 → 몸통박치기 데미지 없음
///   • Crouch           — press/hold C to crouch; reduces speed & hitbox (키: C)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MovementSystem : MonoBehaviour
{
    // ── Tuning ────────────────────────────────────────────────────────────────
    [Header("Walk / Run")]
    [SerializeField] private float moveSpeed         = 8f;
    [SerializeField] private float airControlFactor  = 0.6f;

    [Header("Sprint")]
    [SerializeField] private float sprintMultiplier  = 1.6f;

    [Header("Roll / Dodge")]
    [SerializeField] private float rollForce         = 18f;
    [SerializeField] private float rollDuration      = 0.25f;
    [SerializeField] private float rollCooldown      = 0.6f;
    [Tooltip("롤 중 물리 충돌을 무시할 레이어 (Enemy 레이어 지정)")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Crouch")]
    [SerializeField] private float crouchSpeedMult        = 0.45f;
    [SerializeField] private Vector2 crouchColliderSize   = new(0.8f, 0.9f);
    [SerializeField] private Vector2 standColliderSize    = new(0.8f, 1.8f);
    [SerializeField] private Vector2 crouchColliderOffset = new(0f, -0.45f);
    [SerializeField] private Vector2 standColliderOffset  = new(0f, 0f);
    [SerializeField] private LayerMask ceilingLayer;
    [SerializeField] private float ceilingCheckDist  = 0.15f;

    [Header("Jump")]
    [SerializeField] private float jumpForce         = 16f;
    [SerializeField] private float coyoteTime        = 0.15f;
    [SerializeField] private float jumpCutMultiplier = 0.4f;
    [SerializeField] private float fallMultiplier    = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 2f;

    [Header("Wall")]
    [SerializeField] private float wallSlideSpeed    = 2f;
    [SerializeField] private Vector2 wallJumpForce   = new(10f, 14f);
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float wallCheckDist     = 0.3f;
    [SerializeField] private LayerMask wallLayer;

    [Header("Dash")]
    [SerializeField] private float dashForce         = 22f;
    [SerializeField] private float dashDuration      = 0.18f;
    [SerializeField] private float dashCooldown      = 0.8f;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private PlayerController  ctrl;
    private Rigidbody2D       rb;
    private CapsuleCollider2D col;

    private float coyoteTimer;
    private bool  isWallSliding;

    private bool    isDashing;
    private float   dashTimer;
    private float   dashCooldownTimer;
    private Vector2 dashDirection;

    private bool  isSprinting;

    private bool  isRolling;
    private float rollTimer;
    private float rollCooldownTimer;
    private float rollDirection;

    private bool isCrouching;
    private bool isForcedCrouch;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        ctrl = GetComponent<PlayerController>();
        rb   = GetComponent<Rigidbody2D>();
        col  = GetComponent<CapsuleCollider2D>();
    }

    public void Tick()
    {
        UpdateCoyoteTimer();
        UpdateWallSlide();
        HandleCrouch();
        HandleJump();
        HandleDash();
        HandleRoll();
        UpdateSprint();

        ctrl.SetFacing(ctrl.InputBuf.HorizontalInput);

        if (ctrl.Anim)
        {
            ctrl.Anim.SetFloat("Speed",        Mathf.Abs(rb.linearVelocity.x));
            ctrl.Anim.SetFloat("VelocityY",    rb.linearVelocity.y);
            ctrl.Anim.SetBool("IsGrounded",    ctrl.IsGrounded);
            ctrl.Anim.SetBool("IsWallSliding", isWallSliding);
            ctrl.Anim.SetBool("IsDashing",     isDashing);
            ctrl.Anim.SetBool("IsSprinting",   isSprinting);
            ctrl.Anim.SetBool("IsRolling",     isRolling);
            ctrl.Anim.SetBool("IsCrouching",   isCrouching);
        }
    }

    public void FixedTick()
    {
        if (isDashing || isRolling) return;
        ApplyHorizontalMovement();
        ApplyGravityModifiers();
    }

    // ── Coyote time ───────────────────────────────────────────────────────────
    private void UpdateCoyoteTimer()
    {
        if (ctrl.IsGrounded) coyoteTimer = coyoteTime;
        else coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.deltaTime);
    }

    private bool CanCoyoteJump() => coyoteTimer > 0f && !ctrl.IsGrounded;

    // ── Sprint ────────────────────────────────────────────────────────────────
    private void UpdateSprint()
    {
        isSprinting = Input.GetKey(KeyCode.LeftShift)
                      && Mathf.Abs(ctrl.InputBuf.HorizontalInput) > 0.01f
                      && ctrl.IsGrounded
                      && !isCrouching
                      && !isRolling
                      && !isDashing;
    }

    // ── Horizontal ────────────────────────────────────────────────────────────
    private void ApplyHorizontalMovement()
    {
        float speed = moveSpeed;
        if (isSprinting)      speed *= sprintMultiplier;
        else if (isCrouching) speed *= crouchSpeedMult;

        float targetSpeed = ctrl.InputBuf.HorizontalInput * speed;
        float newVx = ctrl.IsGrounded
            ? targetSpeed
            : Mathf.Lerp(rb.linearVelocity.x, targetSpeed, airControlFactor);

        rb.linearVelocity = new Vector2(newVx, rb.linearVelocity.y);
    }

    // ── Gravity modifiers ─────────────────────────────────────────────────────
    private void ApplyGravityModifiers()
    {
        if (isWallSliding)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x,
                Mathf.Max(rb.linearVelocity.y, -wallSlideSpeed));
            return;
        }

        if (rb.linearVelocity.y < 0f)
            rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime);
        else if (rb.linearVelocity.y > 0f && !Input.GetButton("Jump"))
            rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime);
    }

    // ── Jump ──────────────────────────────────────────────────────────────────
    private void HandleJump()
    {
        if (isCrouching || isRolling) return;

        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);

        if (!ctrl.InputBuf.ConsumeJump()) return;

        if (isWallSliding) WallJump();
        else if (ctrl.IsGrounded || CanCoyoteJump())
        {
            NormalJump();
            coyoteTimer = 0f;
        }
    }

    private void NormalJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        ctrl.Anim?.SetTrigger("Jump");
    }

    private void WallJump()
    {
        float dir = ctrl.IsFacingRight ? -1f : 1f;
        rb.linearVelocity = new Vector2(wallJumpForce.x * dir, wallJumpForce.y);
        ctrl.Anim?.SetTrigger("Jump");
    }

    // ── Wall slide detection ──────────────────────────────────────────────────
    private void UpdateWallSlide()
    {
        bool touchingWall = wallCheck &&
            Physics2D.Raycast(wallCheck.position,
                ctrl.IsFacingRight ? Vector2.right : Vector2.left,
                wallCheckDist, wallLayer);

        isWallSliding = touchingWall && !ctrl.IsGrounded && rb.linearVelocity.y < 0f;
    }

    // ── Dash ──────────────────────────────────────────────────────────────────
    private void HandleDash()
    {
        dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - Time.deltaTime);

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f) EndDash();
            return;
        }

        if (ctrl.InputBuf.ConsumeDash() && dashCooldownTimer <= 0f)
            StartDash();
    }

    private void StartDash()
    {
        isDashing         = true;
        dashTimer         = dashDuration;
        dashCooldownTimer = dashCooldown;

        dashDirection     = new Vector2(ctrl.IsFacingRight ? 1f : -1f, 0f);
        rb.linearVelocity = dashDirection * dashForce;
        rb.gravityScale   = 0f;

        ctrl.Anim?.SetTrigger("Dash");
        ctrl.Health.SetInvincible(dashDuration);
    }

    private void EndDash()
    {
        isDashing         = false;
        rb.gravityScale   = 1f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.4f, 0f);
    }

    // ── Roll / Dodge ──────────────────────────────────────────────────────────
    private void HandleRoll()
    {
        rollCooldownTimer = Mathf.Max(0f, rollCooldownTimer - Time.deltaTime);

        if (isRolling)
        {
            rollTimer -= Time.deltaTime;
            if (rollTimer <= 0f) EndRoll();
            return;
        }

        if (!ctrl.IsGrounded || isDashing) return;

        if (Input.GetKeyDown(KeyCode.LeftControl) && rollCooldownTimer <= 0f)
            StartRoll();
    }

    private void StartRoll()
    {
        isRolling         = true;
        rollTimer         = rollDuration;
        rollCooldownTimer = rollCooldown;

        float inputDir = ctrl.InputBuf.HorizontalInput;
        rollDirection = Mathf.Abs(inputDir) > 0.01f
            ? Mathf.Sign(inputDir)
            : (ctrl.IsFacingRight ? 1f : -1f);

        rb.linearVelocity = new Vector2(rollDirection * rollForce, 0f);
        rb.gravityScale   = 0f;

        ctrl.Anim?.SetTrigger("Roll");
        ctrl.Health.SetInvincible(rollDuration);
        SetColliderCrouch(true);

        // 롤 중 적 레이어와 물리 충돌 비활성화 → 몸통박치기 데미지 없음
        SetEnemyCollision(false);
    }

    private void EndRoll()
    {
        isRolling       = false;
        rb.gravityScale = 1f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.3f, rb.linearVelocity.y);

        if (!isCrouching)
            SetColliderCrouch(false);

        // 롤 종료 후 적 충돌 복원
        SetEnemyCollision(true);
    }

    /// <summary>
    /// 플레이어 레이어와 enemyLayer 사이의 물리 충돌을 토글한다.
    /// enable=false → 충돌 무시 / enable=true → 충돌 복원
    /// </summary>
    private void SetEnemyCollision(bool enable)
    {
        int playerLayer = gameObject.layer;
        for (int i = 0; i < 32; i++)
        {
            if ((enemyLayer.value & (1 << i)) != 0)
                Physics2D.IgnoreLayerCollision(playerLayer, i, !enable);
        }
    }

    // ── Crouch ────────────────────────────────────────────────────────────────
    private void HandleCrouch()
    {
        bool wantsCrouch = Input.GetKey(KeyCode.C);
        isForcedCrouch   = isCrouching && CeilingAbove();

        bool shouldCrouch = (wantsCrouch || isForcedCrouch) && ctrl.IsGrounded;

        if (shouldCrouch && !isCrouching)      EnterCrouch();
        else if (!shouldCrouch && isCrouching) ExitCrouch();
    }

    private void EnterCrouch()
    {
        isCrouching = true;
        SetColliderCrouch(true);
        ctrl.Anim?.SetTrigger("CrouchEnter");
    }

    private void ExitCrouch()
    {
        isCrouching = false;
        SetColliderCrouch(false);
        ctrl.Anim?.SetTrigger("CrouchExit");
    }

    private void SetColliderCrouch(bool crouched)
    {
        if (col == null) return;
        col.size   = crouched ? crouchColliderSize   : standColliderSize;
        col.offset = crouched ? crouchColliderOffset : standColliderOffset;
    }

    private bool CeilingAbove()
    {
        if (col == null) return false;
        Vector2 topCenter = (Vector2)transform.position
                            + standColliderOffset
                            + Vector2.up * (standColliderSize.y / 2f);
        return Physics2D.Raycast(topCenter, Vector2.up, ceilingCheckDist, ceilingLayer);
    }

    // ── Public state queries ──────────────────────────────────────────────────
    public bool IsRolling   => isRolling;
    public bool IsDashing   => isDashing;
    public bool IsCrouching => isCrouching;

    // ── Gizmos ────────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (wallCheck)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(wallCheck.position,
                wallCheck.position + (transform.localScale.x > 0 ? Vector3.right : Vector3.left) * wallCheckDist);
        }

        Gizmos.color = Color.yellow;
        Vector2 top = (Vector2)transform.position + standColliderOffset + Vector2.up * (standColliderSize.y / 2f);
        Gizmos.DrawLine(top, top + Vector2.up * ceilingCheckDist);
    }
}