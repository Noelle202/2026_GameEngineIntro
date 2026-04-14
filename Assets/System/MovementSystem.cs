using UnityEngine;

/// <summary>
/// Handles all horizontal movement, jumping, dashing, and wall-sliding.
///
/// Key features:
///   • Coyote time   — jump window after walking off a ledge
///   • Jump buffer   — jump pressed slightly before landing still fires
///   • Variable jump — short-press = small hop, hold = full jump
///   • Wall-slide    — slow fall on walls; wall-jump pushes away
///   • Dash          — short directional dash with invincibility frames
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MovementSystem : MonoBehaviour
{
    // ── Tuning ────────────────────────────────────────────────────────────────
    [Header("Walk / Run")]
    [SerializeField] private float moveSpeed         = 8f;
    [SerializeField] private float acceleration      = 16f;
    [SerializeField] private float deceleration      = 20f;
    [SerializeField] private float airControlFactor  = 0.6f;

    [Header("Jump")]
    [SerializeField] private float jumpForce         = 16f;
    [SerializeField] private float coyoteTime        = 0.15f;   // ledge grace window
    [SerializeField] private float jumpCutMultiplier = 0.4f;    // variable height: early release
    [SerializeField] private float fallMultiplier    = 2.5f;    // faster falling
    [SerializeField] private float lowJumpMultiplier = 2f;      // faster ascent when holding

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
    private PlayerController ctrl;
    private Rigidbody2D rb;

    private float coyoteTimer;
    private bool  isWallSliding;
    private bool  isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector2 dashDirection;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        ctrl = GetComponent<PlayerController>();
        rb   = GetComponent<Rigidbody2D>();
    }

    // ── Called by PlayerController.Update ────────────────────────────────────
    public void Tick()
    {
        UpdateCoyoteTimer();
        UpdateWallSlide();
        HandleJump();
        HandleDash();

        // Inform PlayerController about facing direction
        ctrl.SetFacing(ctrl.InputBuf.HorizontalInput);

        // Animator params
        if (ctrl.Anim)
        {
            ctrl.Anim.SetFloat("Speed",    Mathf.Abs(rb.linearVelocity.x));
            ctrl.Anim.SetFloat("VelocityY", rb.linearVelocity.y);
            ctrl.Anim.SetBool("IsGrounded", ctrl.IsGrounded);
            ctrl.Anim.SetBool("IsWallSliding", isWallSliding);
            ctrl.Anim.SetBool("IsDashing",  isDashing);
        }
    }

    // ── Called by PlayerController.FixedUpdate ────────────────────────────────
    public void FixedTick()
    {
        if (isDashing) return;            // physics locked during dash

        ApplyHorizontalMovement();
        ApplyGravityModifiers();
    }

    // ── Coyote time ───────────────────────────────────────────────────────────
    private void UpdateCoyoteTimer()
    {
        if (ctrl.IsGrounded)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.deltaTime);
    }

    private bool CanCoyoteJump() => coyoteTimer > 0f && !ctrl.IsGrounded;

    // ── Horizontal ────────────────────────────────────────────────────────────
    private void ApplyHorizontalMovement()
    {
        float targetSpeed = ctrl.InputBuf.HorizontalInput * moveSpeed;
        float accel       = ctrl.IsGrounded ? acceleration : acceleration * airControlFactor;
        float decel       = ctrl.IsGrounded ? deceleration : deceleration * airControlFactor;

        float rate = Mathf.Abs(ctrl.InputBuf.HorizontalInput) > 0.01f ? accel : decel;
        float newVx = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, rate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newVx, rb.linearVelocity.y);
    }

    // ── Gravity modifiers ─────────────────────────────────────────────────────
    private void ApplyGravityModifiers()
    {
        if (isWallSliding)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -wallSlideSpeed));
            return;
        }

        if (rb.linearVelocity.y < 0f)
        {
            // Falling — extra gravity for snappy arcs
            rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime);
        }
        else if (rb.linearVelocity.y > 0f && !Input.GetButton("Jump"))
        {
            // Rising but jump released — short hop
            rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime);
        }
    }

    // ── Jump ──────────────────────────────────────────────────────────────────
    private void HandleJump()
    {
        // Variable jump height: cut velocity on early release
        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);

        if (!ctrl.InputBuf.ConsumeJump()) return;

        if (isWallSliding)
        {
            WallJump();
        }
        else if (ctrl.IsGrounded || CanCoyoteJump())
        {
            NormalJump();
            coyoteTimer = 0f;   // consume coyote window
        }
    }

    private void NormalJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        ctrl.Anim?.SetTrigger("Jump");
    }

    private void WallJump()
    {
        float dir = ctrl.IsFacingRight ? -1f : 1f;   // push away from wall
        rb.linearVelocity = new Vector2(wallJumpForce.x * dir, wallJumpForce.y);
        ctrl.Anim?.SetTrigger("Jump");
    }

    // ── Wall slide detection ──────────────────────────────────────────────────
    private void UpdateWallSlide()
    {
        bool touchingWall = wallCheck &&
            Physics2D.Raycast(wallCheck.position, ctrl.IsFacingRight ? Vector2.right : Vector2.left,
                              wallCheckDist, wallLayer);

        isWallSliding = touchingWall && !ctrl.IsGrounded && rb.linearVelocity.y < 0f;
    }

    // ── Dash ─────────────────────────────────────────────────────────────────
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

        dashDirection = new Vector2(ctrl.IsFacingRight ? 1f : -1f, 0f);
        rb.linearVelocity  = dashDirection * dashForce;
        rb.gravityScale   = 0f;                     // ignore gravity during dash

        ctrl.Anim?.SetTrigger("Dash");

        // Grant invincibility frames during dash
        ctrl.Health.SetInvincible(dashDuration);
    }

    private void EndDash()
    {
        isDashing       = false;
        rb.gravityScale = 1f;
        rb.linearVelocity  = new Vector2(rb.linearVelocity.x * 0.4f, 0f);  // bleed momentum
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (!wallCheck) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(wallCheck.position,
            wallCheck.position + (transform.localScale.x > 0 ? Vector3.right : Vector3.left) * wallCheckDist);
    }
}
