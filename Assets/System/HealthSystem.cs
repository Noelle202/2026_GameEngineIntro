using System;
using UnityEngine;

/// <summary>
/// Generic health component that works for both the player and enemies.
///
/// Features:
///   • Configurable max HP
///   • Invincibility frames (iframes) — set externally or on damage
///   • Hit-stun duration
///   • C# events for damage and death
///   • Flash feedback on hit
///   • ScaleMaxHP() — 레벨업 시 외부에서 최대 HP를 배율로 증가
/// </summary>
public class HealthSystem : MonoBehaviour
{
    // ── Config ────────────────────────────────────────────────────────────────
    [Header("Stats")]
    [SerializeField] private float maxHP           = 100f;
    [SerializeField] private float iframesOnHit    = 0.5f;
    [SerializeField] private float hitStunDuration = 0.15f;

    [Header("Feedback")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float flashInterval   = 0.08f;

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<float, float> OnDamaged;   // (damage, currentHP)
    public event Action               OnDeath;
    public event Action<float>        OnHealed;

    // ── Runtime state ─────────────────────────────────────────────────────────
    public float CurrentHP    { get; private set; }
    public float MaxHP        => maxHP;
    public bool  IsAlive      => CurrentHP > 0f;
    public bool  IsInvincible => invincibleTimer > 0f;
    public bool  IsInHitStun  => hitStunTimer    > 0f;

    private float invincibleTimer;
    private float hitStunTimer;
    private float flashTimer;
    private bool  isDead;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        CurrentHP = maxHP;
        if (!spriteRenderer)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (invincibleTimer > 0f)
        {
            invincibleTimer -= Time.deltaTime;
            TickFlash();
        }
        else if (spriteRenderer)
        {
            spriteRenderer.enabled = true;
        }

        if (hitStunTimer > 0f)
            hitStunTimer = Mathf.Max(0f, hitStunTimer - Time.deltaTime);
    }

    // ── Public API ────────────────────────────────────────────────────────────
    public void TakeDamage(float amount)
    {
        if (isDead || IsInvincible || amount <= 0f) return;

        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        SetInvincible(iframesOnHit);
        hitStunTimer = hitStunDuration;

        OnDamaged?.Invoke(amount, CurrentHP);
        GetComponentInChildren<Animator>()?.SetTrigger("Hit");

        if (CurrentHP <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        OnHealed?.Invoke(CurrentHP);
    }

    public void SetInvincible(float duration)
    {
        invincibleTimer = Mathf.Max(invincibleTimer, duration);
    }

    /// <summary>
    /// 레벨업 시 최대 HP를 배율만큼 늘리고, 현재 HP도 같은 비율로 회복한다.
    /// </summary>
    public void ScaleMaxHP(float multiplier)
    {
        float oldMax  = maxHP;
        maxHP        *= multiplier;
        float gained  = maxHP - oldMax;
        CurrentHP     = Mathf.Min(maxHP, CurrentHP + gained);   // 늘어난 만큼 채워줌
    }

    // ── Internal ──────────────────────────────────────────────────────────────
    private void Die()
    {
        if (isDead) return;
        isDead = true;

        GetComponentInChildren<Animator>()?.SetTrigger("Die");
        OnDeath?.Invoke();

        if (TryGetComponent<Rigidbody2D>(out var rb))
            rb.simulated = false;

        foreach (var col in GetComponents<Collider2D>())
            col.enabled = false;
    }

    private void TickFlash()
    {
        if (!spriteRenderer) return;
        flashTimer -= Time.deltaTime;
        if (flashTimer <= 0f)
        {
            spriteRenderer.enabled = !spriteRenderer.enabled;
            flashTimer = flashInterval;
        }
    }
}