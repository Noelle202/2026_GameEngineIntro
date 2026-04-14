using System;
using UnityEngine;

/// <summary>
/// Generic health component that works for both the player and enemies.
///
/// Features:
///   • Configurable max HP
///   • Invincibility frames (iframes) — set externally or on damage
///   • Hit-stun duration
///   • UnityEvents / C# events for damage and death
///   • Flash feedback on hit
/// </summary>
public class HealthSystem : MonoBehaviour
{
    // ── Config ────────────────────────────────────────────────────────────────
    [Header("Stats")]
    [SerializeField] private float maxHP               = 100f;
    [SerializeField] private float iframesOnHit        = 0.5f;    // seconds of invincibility after taking a hit
    [SerializeField] private float hitStunDuration     = 0.15f;

    [Header("Feedback")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float flashInterval       = 0.08f;

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<float, float> OnDamaged;   // (damage, currentHP)
    public event Action               OnDeath;
    public event Action<float>        OnHealed;    // (currentHP)

    // ── Runtime state ─────────────────────────────────────────────────────────
    public float CurrentHP  { get; private set; }
    public float MaxHP      => maxHP;
    public bool  IsAlive    => CurrentHP > 0f;
    public bool  IsInvincible => invincibleTimer > 0f;
    public bool  IsInHitStun  => hitStunTimer > 0f;

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
            spriteRenderer.enabled = true;   // ensure visible when not flashing
        }

        if (hitStunTimer > 0f)
            hitStunTimer = Mathf.Max(0f, hitStunTimer - Time.deltaTime);
    }

    // ── Public API ────────────────────────────────────────────────────────────
    /// <summary>Deal damage. Respects invincibility frames.</summary>
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

    /// <summary>Restore HP.</summary>
    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        OnHealed?.Invoke(CurrentHP);
    }

    /// <summary>Grant invincibility for `duration` seconds (e.g. during a dash).</summary>
    public void SetInvincible(float duration)
    {
        invincibleTimer = Mathf.Max(invincibleTimer, duration);
    }

    // ── Internal ──────────────────────────────────────────────────────────────
    private void Die()
    {
        if (isDead) return;
        isDead = true;

        GetComponentInChildren<Animator>()?.SetTrigger("Die");
        OnDeath?.Invoke();

        // Disable physics so the body stays in place for death animation
        if (TryGetComponent<Rigidbody2D>(out var rb))
            rb.simulated = false;

        // Disable hit detection
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
