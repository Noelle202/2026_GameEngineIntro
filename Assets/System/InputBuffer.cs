using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stores raw input and provides buffered "consume" methods.
/// Each action press is remembered for `bufferWindow` seconds so that
/// inputs made just before a state allows them are not silently dropped.
///
/// Usage:
///   if (InputBuf.ConsumeJump())  → do jump
///   if (InputBuf.ConsumeAttack()) → do attack
/// </summary>
public class InputBuffer : MonoBehaviour
{
    // ── Settings ──────────────────────────────────────────────────────────────
    [Header("Buffer Windows (seconds)")]
    [Tooltip("How long a jump input stays valid before it expires.")]
    [SerializeField] private float jumpBufferTime    = 0.15f;
    [Tooltip("How long an attack input stays valid before it expires.")]
    [SerializeField] private float attackBufferTime  = 0.12f;
    [Tooltip("How long a dash input stays valid before it expires.")]
    [SerializeField] private float dashBufferTime    = 0.10f;

    // ── Internal state ────────────────────────────────────────────────────────
    private float jumpBufferTimer;
    private float attackBufferTimer;
    private float dashBufferTimer;

    // ── Raw axis (non-buffered, read every frame) ─────────────────────────────
    public float HorizontalInput { get; private set; }
    public float VerticalInput   { get; private set; }

    // ── Public queries ────────────────────────────────────────────────────────
    public bool HasBufferedJump    => jumpBufferTimer    > 0f;
    public bool HasBufferedAttack  => attackBufferTimer  > 0f;
    public bool HasBufferedDash    => dashBufferTimer    > 0f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Update()
    {
        // Tick down all timers (nothing to do here; CollectInput() called by Controller)
        jumpBufferTimer   = Mathf.Max(0f, jumpBufferTimer   - Time.deltaTime);
        attackBufferTimer = Mathf.Max(0f, attackBufferTimer - Time.deltaTime);
        dashBufferTimer   = Mathf.Max(0f, dashBufferTimer   - Time.deltaTime);
    }

    // ── Called by PlayerController each Update ────────────────────────────────
    public void CollectInput()
    {
        // Axis inputs (uses Unity's legacy Input for simplicity;
        // swap with InputSystem.GetAxis if using the new Input System)
        HorizontalInput = Input.GetAxisRaw("Horizontal");
        VerticalInput   = Input.GetAxisRaw("Vertical");

        // Press events → reset the buffer timer
        if (Input.GetButtonDown("Jump"))
            jumpBufferTimer = jumpBufferTime;

        if (Input.GetButtonDown("Fire1"))          // left-click / Z / gamepad X
            attackBufferTimer = attackBufferTime;

        if (Input.GetButtonDown("Fire3"))          // left-shift / gamepad RB
            dashBufferTimer = dashBufferTime;
    }

    // ── Consume methods (call once; clears the buffer) ────────────────────────
    /// <summary>Returns true and clears the jump buffer if a buffered jump exists.</summary>
    public bool ConsumeJump()
    {
        if (jumpBufferTimer <= 0f) return false;
        jumpBufferTimer = 0f;
        return true;
    }

    /// <summary>Returns true and clears the attack buffer if a buffered attack exists.</summary>
    public bool ConsumeAttack()
    {
        if (attackBufferTimer <= 0f) return false;
        attackBufferTimer = 0f;
        return true;
    }

    /// <summary>Returns true and clears the dash buffer if a buffered dash exists.</summary>
    public bool ConsumeDash()
    {
        if (dashBufferTimer <= 0f) return false;
        dashBufferTimer = 0f;
        return true;
    }

    // ── Debug ─────────────────────────────────────────────────────────────────
    public override string ToString() =>
        $"Jump:{jumpBufferTimer:F2} Atk:{attackBufferTimer:F2} Dash:{dashBufferTimer:F2}";
}
