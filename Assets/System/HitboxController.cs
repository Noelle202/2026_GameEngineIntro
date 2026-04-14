using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A hitbox that activates for a short window during an attack.
/// Uses Physics2D.OverlapBox each frame so the window timing is crisp.
///
/// Attach to a child GameObject of the player. Assign in CombatSystem Inspector slot.
/// </summary>
public class HitboxController : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private bool      showGizmos = true;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private bool   active;
    private float  activeTimer;
    private float  damage;
    private float  knockbackForce;
    private bool   facingRight;
    private Vector2 currentOffset;
    private Vector2 currentSize;

    // Prevent multi-hit in a single activation window
    private readonly HashSet<Collider2D> hitThisSwing = new();

    // ── Activate ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Called by CombatSystem to begin a hitbox window.
    /// </summary>
    public void Activate(float dmg, float knockback, bool isFacingRight,
                         Vector2 offset, Vector2 size, float duration)
    {
        active         = true;
        activeTimer    = duration;
        damage         = dmg;
        knockbackForce = knockback;
        facingRight    = isFacingRight;
        currentOffset  = offset;
        currentSize    = size;
        hitThisSwing.Clear();
    }

    public void Deactivate()
    {
        active = false;
        hitThisSwing.Clear();
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Update()
    {
        if (!active) return;

        activeTimer -= Time.deltaTime;
        if (activeTimer <= 0f)
        {
            Deactivate();
            return;
        }

        DetectHits();
    }

    // ── Detection ─────────────────────────────────────────────────────────────
    private void DetectHits()
    {
        Vector2 worldCenter = (Vector2)transform.position + currentOffset;
        Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, currentSize, 0f, enemyLayer);

        foreach (Collider2D col in hits)
        {
            if (hitThisSwing.Contains(col)) continue;   // already hit this enemy this swing
            hitThisSwing.Add(col);

            ApplyHit(col);
        }
    }

    private void ApplyHit(Collider2D col)
    {
        // Apply damage
        if (col.TryGetComponent<HealthSystem>(out var health))
            health.TakeDamage(damage);

        // Apply knockback
        if (col.TryGetComponent<Rigidbody2D>(out var rb))
        {
            Vector2 dir = new Vector2(facingRight ? 1f : -1f, 0.4f).normalized;
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
        }

        // Send hit event for VFX / SFX
        col.SendMessage("OnHitReceived", damage, SendMessageOptions.DontRequireReceiver);
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        if (!showGizmos || !active) return;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawCube((Vector2)transform.position + currentOffset, currentSize);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawWireCube((Vector2)transform.position + currentOffset, currentSize);
    }
}
