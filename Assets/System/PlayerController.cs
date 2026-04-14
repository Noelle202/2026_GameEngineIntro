using UnityEngine;

/// <summary>
/// Central orchestrator. Attach to the player root GameObject.
/// Requires: Rigidbody2D, Collider2D, MovementSystem, CombatSystem, HealthSystem, InputBuffer
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(MovementSystem))]
[RequireComponent(typeof(CombatSystem))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(InputBuffer))]
public class PlayerController : MonoBehaviour
{
    // ── Shared state ──────────────────────────────────────────────────────────
    [Header("Ground Detection")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.12f;
    [SerializeField] private LayerMask groundLayer;

    // ── Component references ──────────────────────────────────────────────────
    public Rigidbody2D Rb        { get; private set; }
    public MovementSystem Movement { get; private set; }
    public CombatSystem Combat    { get; private set; }
    public HealthSystem Health    { get; private set; }
    public InputBuffer InputBuf   { get; private set; }
    public Animator Anim          { get; private set; }

    // ── Ground state ──────────────────────────────────────────────────────────
    public bool IsGrounded        { get; private set; }
    public bool IsFacingRight     { get; private set; } = true;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        Rb        = GetComponent<Rigidbody2D>();
        Movement  = GetComponent<MovementSystem>();
        Combat    = GetComponent<CombatSystem>();
        Health    = GetComponent<HealthSystem>();
        InputBuf  = GetComponent<InputBuffer>();
        Anim      = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        IsGrounded = CheckGround();

        // Collect raw input every frame and push into buffer
        InputBuf.CollectInput();

        // Let subsystems process their buffered actions
        Movement.Tick();
        Combat.Tick();
    }

    private void FixedUpdate()
    {
        Movement.FixedTick();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private bool CheckGround()
    {
        return Physics2D.OverlapCircle(
            groundCheck ? groundCheck.position : (Vector2)transform.position + Vector2.down * 0.5f,
            groundCheckRadius,
            groundLayer
        );
    }

    /// <summary>Flip sprite to match movement direction.</summary>
    public void SetFacing(float horizontalInput)
    {
        if (horizontalInput > 0.01f && !IsFacingRight)       Flip();
        else if (horizontalInput < -0.01f && IsFacingRight)  Flip();
    }

    private void Flip()
    {
        IsFacingRight = !IsFacingRight;
        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
