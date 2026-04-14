using System.Collections;
using UnityEngine;

/// <summary>
/// Manages the player's offensive combat loop.
///
/// Features:
///   • 3-hit light attack combo with cancel windows
///   • Heavy attack (hold)
///   • Air attack variant
///   • Hitbox activate/deactivate tied to animation events or timings
///   • Knockback applied to enemies on hit
///   • Attack cancelling via input buffer
/// </summary>
public class CombatSystem : MonoBehaviour
{
    // ── Tuning ────────────────────────────────────────────────────────────────
    [Header("Combo")]
    [SerializeField] private int   maxComboSteps     = 3;
    [SerializeField] private float comboWindow       = 0.45f;   // input window between hits
    [SerializeField] private float[] attackDurations = { 0.30f, 0.28f, 0.40f };
    [SerializeField] private float[] attackDamages   = { 10f,   12f,   18f  };
    [SerializeField] private float[] attackKnockback = { 5f,    6f,    10f  };

    [Header("Heavy Attack")]
    [SerializeField] private float heavyDuration     = 0.55f;
    [SerializeField] private float heavyDamage       = 25f;
    [SerializeField] private float heavyKnockback    = 14f;
    [SerializeField] private float heavyHoldTime     = 0.3f;    // hold threshold to trigger heavy

    [Header("Hitbox")]
    [SerializeField] private HitboxController hitbox;           // assign in Inspector
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

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        ctrl = GetComponent<PlayerController>();
    }

    // ── Called by PlayerController.Update ────────────────────────────────────
    public void Tick()
    {
        // Heavy attack detection: track how long Fire1 is held
        if (Input.GetButton("Fire1"))
            attackHoldTimer += Time.deltaTime;
        else
            attackHoldTimer = 0f;

        if (!isAttacking)
        {
            // Combo window expired → reset
            if (comboTimer > 0f)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0f)
                    ResetCombo();
            }

            // Check for queued follow-up
            if (comboQueued)
            {
                comboQueued = false;
                ExecuteAttack();
                return;
            }

            // New attack via buffer
            if (ctrl.InputBuf.ConsumeAttack())
            {
                isHeavyAttack = attackHoldTimer >= heavyHoldTime;
                ExecuteAttack();
            }
        }
        else
        {
            // Currently attacking — tick the active attack
            attackTimer -= Time.deltaTime;

            // Queue next combo step if input pressed during active window
            if (ctrl.InputBuf.HasBufferedAttack && comboStep < maxComboSteps)
                comboQueued = true;

            if (attackTimer <= 0f)
                EndAttack();
        }
    }

    // ── Attack execution ──────────────────────────────────────────────────────
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

        // Position hitbox relative to facing direction
        ActivateHitbox(
            attackDamages[Mathf.Clamp(comboStep, 0, attackDamages.Length - 1)],
            attackKnockback[Mathf.Clamp(comboStep, 0, attackKnockback.Length - 1)],
            dur * 0.5f     // hitbox active for first half of the attack
        );

        // Animator
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

        // Heavy attack resets combo
        comboStep = 0;
    }

    private void EndAttack()
    {
        isAttacking = false;
        attackTimer = 0f;

        if (!isHeavyAttack)
        {
            comboStep  = (comboStep + 1) % maxComboSteps;
            comboTimer = comboWindow;           // open window for next combo hit
        }
        else
        {
            isHeavyAttack = false;
            ResetCombo();
        }
    }

    private void ResetCombo()
    {
        comboStep    = 0;
        comboTimer   = 0f;
        comboQueued  = false;
        ctrl.Anim?.SetInteger("ComboStep", 0);
    }

    // ── Hitbox helpers ────────────────────────────────────────────────────────
    private void ActivateHitbox(float damage, float knockbackForce, float duration)
    {
        if (!hitbox) return;

        Vector2 offset = new Vector2(
            ctrl.IsFacingRight ? hitboxOffset.x : -hitboxOffset.x,
            hitboxOffset.y
        );

        hitbox.Activate(damage, knockbackForce, ctrl.IsFacingRight, offset, hitboxSize, duration);
    }

    // ── Public helpers ────────────────────────────────────────────────────────
    public bool IsAttacking => isAttacking;
}
