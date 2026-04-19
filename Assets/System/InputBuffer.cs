using UnityEngine;

/// <summary>
/// Stores raw input and provides buffered "consume" methods.
/// Each action press is remembered for `bufferWindow` seconds so that
/// inputs made just before a state allows them are not silently dropped.
///
/// 버퍼 액션 목록:
///   Jump        — Space
///   Attack      — 마우스 좌클릭 (Fire1)
///   HeavyAttack — 마우스 우클릭 (Fire2) ← 신규
///   Dash        — Fire3 (Left Shift / 게임패드 RB)
/// </summary>
public class InputBuffer : MonoBehaviour
{
    // ── Settings ──────────────────────────────────────────────────────────────
    [Header("Buffer Windows (seconds)")]
    [Tooltip("How long a jump input stays valid before it expires.")]
    [SerializeField] private float jumpBufferTime        = 0.15f;
    [Tooltip("How long a light attack input stays valid before it expires.")]
    [SerializeField] private float attackBufferTime      = 0.12f;
    [Tooltip("How long a heavy attack input stays valid before it expires.")]
    [SerializeField] private float heavyAttackBufferTime = 0.12f;
    [Tooltip("How long a dash input stays valid before it expires.")]
    [SerializeField] private float dashBufferTime        = 0.10f;

    // ── Internal state ────────────────────────────────────────────────────────
    private float jumpBufferTimer;
    private float attackBufferTimer;
    private float heavyAttackBufferTimer;
    private float dashBufferTimer;

    // ── Raw axis (non-buffered, read every frame) ─────────────────────────────
    public float HorizontalInput { get; private set; }
    public float VerticalInput   { get; private set; }

    // ── Public queries ────────────────────────────────────────────────────────
    public bool HasBufferedJump        => jumpBufferTimer        > 0f;
    public bool HasBufferedAttack      => attackBufferTimer      > 0f;
    public bool HasBufferedHeavyAttack => heavyAttackBufferTimer > 0f;
    public bool HasBufferedDash        => dashBufferTimer        > 0f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Update()
    {
        jumpBufferTimer        = Mathf.Max(0f, jumpBufferTimer        - Time.deltaTime);
        attackBufferTimer      = Mathf.Max(0f, attackBufferTimer      - Time.deltaTime);
        heavyAttackBufferTimer = Mathf.Max(0f, heavyAttackBufferTimer - Time.deltaTime);
        dashBufferTimer        = Mathf.Max(0f, dashBufferTimer        - Time.deltaTime);
    }

    // ── Called by PlayerController each Update ────────────────────────────────
    public void CollectInput()
    {
        HorizontalInput = Input.GetAxisRaw("Horizontal");
        VerticalInput   = Input.GetAxisRaw("Vertical");

        if (Input.GetButtonDown("Jump"))
            jumpBufferTimer = jumpBufferTime;

        if (Input.GetButtonDown("Fire1"))           // 마우스 좌클릭 — 일반 공격
            attackBufferTimer = attackBufferTime;

        if (Input.GetButtonDown("Fire2"))           // 마우스 우클릭 — 강공격 ← 신규
            heavyAttackBufferTimer = heavyAttackBufferTime;

        if (Input.GetButtonDown("Fire3"))           // Left Shift / 게임패드 RB — 대시
            dashBufferTimer = dashBufferTime;
    }

    // ── Consume methods (call once; clears the buffer) ────────────────────────
    public bool ConsumeJump()
    {
        if (jumpBufferTimer <= 0f) return false;
        jumpBufferTimer = 0f;
        return true;
    }

    public bool ConsumeAttack()
    {
        if (attackBufferTimer <= 0f) return false;
        attackBufferTimer = 0f;
        return true;
    }

    /// <summary>우클릭 강공격 버퍼를 소비한다.</summary>
    public bool ConsumeHeavyAttack()
    {
        if (heavyAttackBufferTimer <= 0f) return false;
        heavyAttackBufferTimer = 0f;
        return true;
    }

    public bool ConsumeDash()
    {
        if (dashBufferTimer <= 0f) return false;
        dashBufferTimer = 0f;
        return true;
    }

    // ── Debug ─────────────────────────────────────────────────────────────────
    public override string ToString() =>
        $"Jump:{jumpBufferTimer:F2} Atk:{attackBufferTimer:F2} Heavy:{heavyAttackBufferTimer:F2} Dash:{dashBufferTimer:F2}";
}