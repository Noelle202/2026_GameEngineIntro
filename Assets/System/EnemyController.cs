using UnityEngine;

/// <summary>
/// Simple enemy with two states: Patrol and Chase.
/// Has its own HealthSystem; can be hit by the player's HitboxController.
///
/// Requires: Rigidbody2D, Collider2D, HealthSystem
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(HealthSystem))]
public class EnemyController : MonoBehaviour
{
    // ── Config ────────────────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float patrolSpeed   = 2.5f;
    [SerializeField] private float chaseSpeed    = 5f;
    [SerializeField] private float patrolRange   = 4f;
    [SerializeField] private float detectionRange = 7f;
    [SerializeField] private float attackRange   = 1.2f;

    [Header("Attack")]
    [SerializeField] private float attackDamage  = 10f;
    [SerializeField] private float attackCooldown = 1.2f;

    [Header("Ground / Wall checks")]
    [SerializeField] private Transform groundAhead;
    [SerializeField] private LayerMask groundLayer;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private enum State { Patrol, Chase, Attack, Dead }
    private State state = State.Patrol;

    private Rigidbody2D rb;
    private HealthSystem health;
    private Animator anim;
    private Transform player;

    private Vector2 patrolOrigin;
    private float   patrolDir = 1f;
    private float   attackCooldownTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        rb     = GetComponent<Rigidbody2D>();
        health = GetComponent<HealthSystem>();
        anim   = GetComponentInChildren<Animator>();

        patrolOrigin = transform.position;

        health.OnDeath   += OnDeath;
        health.OnDamaged += (dmg, _) => anim?.SetTrigger("Hit");
    }

    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj) player = playerObj.transform;
    }

    private void Update()
    {
        if (state == State.Dead) return;

        attackCooldownTimer = Mathf.Max(0f, attackCooldownTimer - Time.deltaTime);
        UpdateState();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (state == State.Dead) return;
        ExecuteMovement();
    }

    // ── State machine ─────────────────────────────────────────────────────────
    private void UpdateState()
    {
        if (!player) return;

        float dist = Vector2.Distance(transform.position, player.position);

        switch (state)
        {
            case State.Patrol:
                if (dist < detectionRange) state = State.Chase;
                break;

            case State.Chase:
                if (dist > detectionRange * 1.2f) state = State.Patrol;
                else if (dist < attackRange)       state = State.Attack;
                break;

            case State.Attack:
                if (dist > attackRange * 1.5f)
                    state = State.Chase;
                else if (attackCooldownTimer <= 0f)
                    ExecuteAttack();
                break;
        }
    }

    private void ExecuteMovement()
    {
        switch (state)
        {
            case State.Patrol:
                DoPatrol();
                break;
            case State.Chase:
                DoChase();
                break;
            case State.Attack:
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);  // stand still while attacking
                break;
        }
    }

    // ── Patrol ────────────────────────────────────────────────────────────────
    private void DoPatrol()
    {
        // Reverse at patrol boundary or edge
        if (Mathf.Abs(transform.position.x - patrolOrigin.x) >= patrolRange)
            patrolDir *= -1f;

        if (groundAhead && !Physics2D.Raycast(groundAhead.position, Vector2.down, 0.5f, groundLayer))
            patrolDir *= -1f;

        rb.linearVelocity = new Vector2(patrolDir * patrolSpeed, rb.linearVelocity.y);
        FaceDirection(patrolDir);
    }

    // ── Chase ─────────────────────────────────────────────────────────────────
    private void DoChase()
    {
        if (!player) return;
        float dir = Mathf.Sign(player.position.x - transform.position.x);
        rb.linearVelocity = new Vector2(dir * chaseSpeed, rb.linearVelocity.y);
        FaceDirection(dir);
    }

    // ── Attack ────────────────────────────────────────────────────────────────
    private void ExecuteAttack()
    {
        if (!player) return;
        attackCooldownTimer = attackCooldown;
        anim?.SetTrigger("Attack");
    }

    public void DealSimpleMeleeAttack()
    {
        if (Vector2.Distance(transform.position, player.position) < attackRange * 1.2f)
        {
            if (player.TryGetComponent<HealthSystem>(out var playerHealth))
                playerHealth.TakeDamage(attackDamage);
        }
    }

    // ── Death ─────────────────────────────────────────────────────────────────
    private void OnDeath()
    {
        state = State.Dead;
        anim?.SetTrigger("Die");
        rb.simulated = false;
        Destroy(gameObject, 2f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void FaceDirection(float dir)
    {
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (dir >= 0 ? 1f : -1f);
        transform.localScale = s;
    }

    private void UpdateAnimator()
    {
        if (!anim) return;
        anim.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
        anim.SetBool("IsChasing", state == State.Chase || state == State.Attack);
    }
}
