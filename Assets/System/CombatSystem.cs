using UnityEngine;

/// <summary>
/// Manages the player's offensive combat loop.
///
/// Features:
///   • 3-hit light attack combo (마우스 좌클릭)
///   • Heavy attack — 우클릭으로 즉시 발동 (추가 데미지) ← 신규
///   • Air attack variant
///   • Hitbox activate/deactivate tied to animation events or timings
///   • Knockback applied to enemies on hit
///   • Attack cancelling via input buffer
///
/// 강공격(우클릭) vs 기존 헤비(홀드):
///   우클릭 강공격 — 즉시 발동, 콤보 리셋, heavyDamage × rightClickDamageMultiplier
/// </summary>
public class CombatSystem : MonoBehaviour
{
    // ── Tuning ────────────────────────────────────────────────────────────────
    [Header("Combo (좌클릭)")]
    [SerializeField] private int   maxComboSteps     = 3;
    [SerializeField] private float comboWindow       = 0.45f;
    [SerializeField] private float[] attackDurations = { 0.30f, 0.28f, 0.40f };
    [SerializeField] private float[] attackDamages   = { 10f,   12f,   18f  };
    [SerializeField] private float[] attackKnockback = { 5f,    6f,    10f  };

    [Header("Heavy Attack (기존 홀드 헤비)")]
    [SerializeField] private float heavyDuration     = 0.55f;
    [SerializeField] private float heavyDamage       = 25f;
    [SerializeField] private float heavyKnockback    = 14f;
    [SerializeField] private float heavyHoldTime     = 0.3f;

    [Header("강공격 (우클릭 즉시 발동)")]
    [Tooltip("우클릭 강공격의 데미지 = heavyDamage × 이 배율")]
    [SerializeField] private float rightClickDamageMultiplier = 1.4f;
    [Tooltip("우클릭 강공격의 넉백 = heavyKnockback × 이 배율")]
    [SerializeField] private float rightClickKnockbackMultiplier = 1.3f;
    [Tooltip("우클릭 강공격 모션 지속 시간")]
    [SerializeField] private float rightClickDuration  = 0.50f;
    [Tooltip("우클릭 강공격 쿨타임 (초)")]
    [SerializeField] private float rightClickCooldown  = 1.2f;

    [Header("Hitbox")]
    [SerializeField] private HitboxController hitbox;
    [SerializeField] private Vector2 hitboxOffset    = new(0.8f, 0f);
    [SerializeField] private Vector2 hitboxSize      = new(1.2f, 0.9f);

    // ── Runtime state ─────────────────────────────────────────────────────────
    private PlayerController ctrl;
    private bool  isAttacking;
    private int   comboStep;
    private float comboTimer;
    private float attackTimer;
    private float attackHoldTimer;
    private bool  comboQueued;
    private bool  isHeavyAttack;

    // 우클릭 강공격
    private bool  isRightClickAttack;
    private float rightClickCooldownTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        ctrl = GetComponent<PlayerController>();
    }

    // ── Called by PlayerController.Update ────────────────────────────────────
    public void Tick()
    {
        rightClickCooldownTimer = Mathf.Max(0f, rightClickCooldownTimer - Time.deltaTime);

        // 홀드 헤비 감지 (좌클릭 유지)
        if (Input.GetButton("Fire1"))
            attackHoldTimer += Time.deltaTime;
        else
            attackHoldTimer = 0f;

        if (!isAttacking)
        {
            // 콤보 윈도우 소진
            if (comboTimer > 0f)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0f) ResetCombo();
            }

            // ① 우클릭 강공격 — 쿨타임이 돌아왔을 때 즉시 발동 (최우선)
            if (ctrl.InputBuf.ConsumeHeavyAttack() && rightClickCooldownTimer <= 0f)
            {
                StartRightClickAttack();
                return;
            }

            // ② 큐에 쌓인 콤보 연속타
            if (comboQueued)
            {
                comboQueued = false;
                ExecuteAttack();
                return;
            }

            // ③ 좌클릭 일반/홀드 헤비
            if (ctrl.InputBuf.ConsumeAttack())
            {
                isHeavyAttack = attackHoldTimer >= heavyHoldTime;
                ExecuteAttack();
            }
        }
        else
        {
            attackTimer -= Time.deltaTime;

            // 공격 중 좌클릭 입력 → 다음 콤보 예약
            if (ctrl.InputBuf.HasBufferedAttack && comboStep < maxComboSteps && !isRightClickAttack)
                comboQueued = true;

            if (attackTimer <= 0f)
                EndAttack();
        }
    }

    // ── Light / Hold-Heavy attack ─────────────────────────────────────────────
    private void ExecuteAttack()
    {
        if (isHeavyAttack)
        {
            StartHeavyAttack();
            return;
        }

        isAttacking = true;
        comboTimer  = 0f;

        bool isAir = !ctrl.IsGrounded;
        float dur  = attackDurations[Mathf.Clamp(comboStep, 0, attackDurations.Length - 1)];
        attackTimer = dur;

        ActivateHitbox(
            attackDamages[Mathf.Clamp(comboStep, 0, attackDamages.Length - 1)],
            attackKnockback[Mathf.Clamp(comboStep, 0, attackKnockback.Length - 1)],
            dur * 0.5f
        );

        string trigger = isAir ? "AirAttack" : $"Attack{comboStep + 1}";
        ctrl.Anim?.SetTrigger(trigger);
        ctrl.Anim?.SetInteger("ComboStep", comboStep + 1);
    }

    private void StartHeavyAttack()
    {
        isAttacking   = true;
        isHeavyAttack = true;
        attackTimer   = heavyDuration;

        ActivateHitbox(heavyDamage, heavyKnockback, heavyDuration * 0.6f);
        ctrl.Anim?.SetTrigger("HeavyAttack");
        comboStep = 0;
    }

    // ── 우클릭 강공격 ─────────────────────────────────────────────────────────
    /// <summary>
    /// 우클릭으로 즉시 발동하는 강공격.
    /// 현재 콤보를 초기화하고 독립 쿨타임을 적용한다.
    /// 데미지·넉백은 heavyDamage/Knockback에 각각의 배율을 곱한 값.
    /// </summary>
    private void StartRightClickAttack()
    {
        isAttacking          = true;
        isRightClickAttack   = true;
        attackTimer          = rightClickDuration;
        rightClickCooldownTimer = rightClickCooldown;

        float dmg  = heavyDamage     * rightClickDamageMultiplier;
        float kb   = heavyKnockback  * rightClickKnockbackMultiplier;

        ActivateHitbox(dmg, kb, rightClickDuration * 0.55f);
        ctrl.Anim?.SetTrigger("RightClickAttack");   // 애니메이터에 동일 이름 트리거 추가 필요

        ResetCombo();   // 콤보 초기화
    }

    // ── End attack ────────────────────────────────────────────────────────────
    private void EndAttack()
    {
        isAttacking = false;
        attackTimer = 0f;

        if (isRightClickAttack)
        {
            isRightClickAttack = false;
            // 강공격은 콤보를 열지 않음
            return;
        }

        if (!isHeavyAttack)
        {
            comboStep  = (comboStep + 1) % maxComboSteps;
            comboTimer = comboWindow;
        }
        else
        {
            isHeavyAttack = false;
            ResetCombo();
        }
    }

    private void ResetCombo()
    {
        comboStep   = 0;
        comboTimer  = 0f;
        comboQueued = false;
        ctrl.Anim?.SetInteger("ComboStep", 0);
    }

    // ── Hitbox helpers ────────────────────────────────────────────────────────
    private void ActivateHitbox(float damage, float knockbackForce, float duration)
    {
        if (!hitbox) return;

        Vector2 offset = new(
            ctrl.IsFacingRight ? hitboxOffset.x : -hitboxOffset.x,
            hitboxOffset.y
        );

        hitbox.Activate(damage, knockbackForce, ctrl.IsFacingRight, offset, hitboxSize, duration);
    }

    // ── Public helpers ────────────────────────────────────────────────────────
    public bool IsAttacking         => isAttacking;
    public bool IsRightClickAttack  => isRightClickAttack;

    /// <summary>우클릭 강공격 쿨타임 진행률 (0=준비, 1=쿨타임 풀)</summary>
    public float RightClickCooldownRatio =>
        rightClickCooldown > 0f ? rightClickCooldownTimer / rightClickCooldown : 0f;
}