using UnityEngine;
using System;

/// <summary>
/// Central orchestrator. Attach to the player root GameObject.
/// Requires: Rigidbody2D, Collider2D, MovementSystem, CombatSystem, HealthSystem, InputBuffer
///
/// 추가 기능:
///   • 레벨업 시스템 — EXP 적립, 레벨 업 시 HP·데미지 배율 상승
///     레벨은 10단계 단위로 스탯이 크게 오르는 기획서 구조를 반영
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(MovementSystem))]
[RequireComponent(typeof(CombatSystem))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(InputBuffer))]
public class PlayerController : MonoBehaviour
{
    // ── Ground Detection ──────────────────────────────────────────────────────
    [Header("Ground Detection")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.12f;
    [SerializeField] private LayerMask groundLayer;

    // ── Level & EXP ───────────────────────────────────────────────────────────
    [Header("레벨 & EXP")]
    [Tooltip("각 레벨에 필요한 누적 EXP. 인덱스 0 = Lv1→Lv2 에 필요한 EXP")]
    [SerializeField] private float[] expTable = GenerateDefaultExpTable();

    [Tooltip("레벨 10 단위마다 적용되는 주요 스탯 배율 (HP 최대치, 데미지)")]
    [SerializeField] private float majorLevelStatMultiplier = 1.25f;

    [Tooltip("일반 레벨업 시 스탯 배율")]
    [SerializeField] private float minorLevelStatMultiplier = 1.05f;

    // ── Component references ──────────────────────────────────────────────────
    public Rigidbody2D    Rb       { get; private set; }
    public MovementSystem Movement { get; private set; }
    public CombatSystem   Combat   { get; private set; }
    public HealthSystem   Health   { get; private set; }
    public InputBuffer    InputBuf { get; private set; }
    public Animator       Anim     { get; private set; }

    // ── Ground / Facing state ─────────────────────────────────────────────────
    public bool IsGrounded    { get; private set; }
    public bool IsFacingRight { get; private set; } = true;

    // ── Level state (읽기 전용 공개) ──────────────────────────────────────────
    public int   Level         { get; private set; } = 1;
    public float CurrentEXP   { get; private set; } = 0f;
    public float DamageMultiplier { get; private set; } = 1f;   // CombatSystem이 참조

    // 레벨업 이벤트 — UI 등 외부 구독용
    public event Action<int> OnLevelUp;   // (new level)

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        Rb       = GetComponent<Rigidbody2D>();
        Movement = GetComponent<MovementSystem>();
        Combat   = GetComponent<CombatSystem>();
        Health   = GetComponent<HealthSystem>();
        InputBuf = GetComponent<InputBuffer>();
        Anim     = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        IsGrounded = CheckGround();
        InputBuf.CollectInput();
        Movement.Tick();
        Combat.Tick();
    }

    private void FixedUpdate()
    {
        Movement.FixedTick();
    }

    // ── Ground check ──────────────────────────────────────────────────────────
    private bool CheckGround() =>
        Physics2D.OverlapCircle(
            groundCheck ? groundCheck.position : (Vector2)transform.position + Vector2.down * 0.5f,
            groundCheckRadius,
            groundLayer
        );

    // ── Facing ────────────────────────────────────────────────────────────────
    public void SetFacing(float horizontalInput)
    {
        if      (horizontalInput >  0.01f && !IsFacingRight) Flip();
        else if (horizontalInput < -0.01f &&  IsFacingRight) Flip();
    }

    private void Flip()
    {
        IsFacingRight = !IsFacingRight;
        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }

    // ── EXP / Level-up API ────────────────────────────────────────────────────
    /// <summary>
    /// EXP를 획득한다. 몬스터 사망 시 EnemyController가 호출한다.
    /// </summary>
    public void GainEXP(float amount)
    {
        if (amount <= 0f) return;
        CurrentEXP += amount;
        CheckLevelUp();
    }

    private void CheckLevelUp()
    {
        // expTable의 마지막 인덱스까지만 레벨업 가능
        while (Level - 1 < expTable.Length && CurrentEXP >= expTable[Level - 1])
        {
            CurrentEXP -= expTable[Level - 1];
            Level++;
            ApplyLevelUpStats();
            OnLevelUp?.Invoke(Level);
            Anim?.SetTrigger("LevelUp");
        }
    }

    /// <summary>
    /// 레벨업 시 스탯을 올린다.
    /// 기획서 기준: 10레벨 단위로 대폭 상승, 그 외엔 소폭 상승.
    /// </summary>
    private void ApplyLevelUpStats()
    {
        bool isMajor = (Level % 10 == 0);
        float mult   = isMajor ? majorLevelStatMultiplier : minorLevelStatMultiplier;

        // 데미지 배율 누적 (CombatSystem에서 참조)
        DamageMultiplier *= mult;

        // HP 최대치 및 현재 HP 증가
        Health.ScaleMaxHP(mult);
    }

    // ── EXP table helper ──────────────────────────────────────────────────────
    /// <summary>기본 EXP 테이블 생성 (Lv1→2: 100, 이후 1.2배씩 증가)</summary>
    private static float[] GenerateDefaultExpTable()
    {
        const int maxLevel = 50;
        float[]   table    = new float[maxLevel];
        float     required = 100f;
        for (int i = 0; i < maxLevel; i++)
        {
            table[i]  = Mathf.Round(required);
            required *= 1.2f;
        }
        return table;
    }

    // ── Public queries ────────────────────────────────────────────────────────
    /// <summary>다음 레벨까지 필요한 EXP (UI 표시용)</summary>
    public float ExpToNextLevel =>
        Level - 1 < expTable.Length ? expTable[Level - 1] : float.MaxValue;

    // ── Gizmos ────────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}