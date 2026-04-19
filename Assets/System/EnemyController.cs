using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy AI — Patrol / Chase / Attack 상태머신.
///
/// 사망 후 시스템:
///   • 시체가 corpseDuration 동안 유지
///   • 플레이어가 carveRange 안에서 F 키를 carveDuration 초 동안 유지 → 갈무리 완료
///   • 갈무리 완료 시 아이템 1/2/3 중 랜덤 1개를 Inventory에 추가
///   • 확률적으로 열쇠 / 카드키 드랍 (프리팹 스폰)
///   • 사망 시 플레이어에게 EXP 지급
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(HealthSystem))]
public class EnemyController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float patrolSpeed    = 2.5f;
    [SerializeField] private float chaseSpeed     = 5f;
    [SerializeField] private float patrolRange    = 4f;
    [SerializeField] private float detectionRange = 7f;
    [SerializeField] private float attackRange    = 1.2f;

    [Header("Attack")]
    [SerializeField] private float attackDamage   = 10f;
    [SerializeField] private float attackCooldown = 1.2f;

    [Header("Ground / Wall checks")]
    [SerializeField] private Transform groundAhead;
    [SerializeField] private LayerMask groundLayer;

    [Header("EXP")]
    [SerializeField] private float expReward = 30f;

    [Header("갈무리 (Carve)")]
    [SerializeField] private float corpseDuration = 15f;
    [SerializeField] private float carveRange     = 1.8f;
    [SerializeField] private float carveDuration  = 1.5f;

    [Header("갈무리 소재 — 아이템 1 / 2 / 3")]
    [Tooltip("갈무리 시 랜덤으로 1개 지급. Inspector에서 ItemData 3종 할당.")]
    [SerializeField] private List<ItemData> carveItems;

    [Header("확률 아이템 드랍 (열쇠 / 카드키)")]
    [SerializeField] private GameObject keyPrefab;
    [SerializeField] private GameObject cardKeyPrefab;
    [SerializeField, Range(0f,1f)] private float keyDropChance     = 0.08f;
    [SerializeField, Range(0f,1f)] private float cardKeyDropChance = 0.04f;

    private enum State { Patrol, Chase, Attack, Dead }
    private State state = State.Patrol;

    private Rigidbody2D  rb;
    private HealthSystem health;
    private Animator     anim;
    private Transform    player;

    private Vector2 patrolOrigin;
    private float   patrolDir = 1f;
    private float   attackCooldownTimer;

    private bool  isDead;
    private bool  isCarving;
    private bool  isCarved;
    private float carveTimer;
    private float corpseTimer;

    private void Awake()
    {
        rb     = GetComponent<Rigidbody2D>();
        health = GetComponent<HealthSystem>();
        anim   = GetComponentInChildren<Animator>();
        patrolOrigin = transform.position;
        health.OnDeath   += OnDeath;
        health.OnDamaged += (_, __) => anim?.SetTrigger("Hit");
    }

    private void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;
    }

    private void Update()
    {
        if (isDead) { UpdateCorpse(); return; }
        attackCooldownTimer = Mathf.Max(0f, attackCooldownTimer - Time.deltaTime);
        UpdateState();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        ExecuteMovement();
    }

    // ── 시체 & 갈무리 ─────────────────────────────────────────────────────────
    private void UpdateCorpse()
    {
        if (isCarved) return;
        corpseTimer += Time.deltaTime;
        if (corpseTimer >= corpseDuration) { Destroy(gameObject); return; }
        if (!player) return;

        bool inRange = Vector2.Distance(transform.position, player.position) <= carveRange;

        if (inRange && Input.GetKey(KeyCode.F))
        {
            isCarving   = true;
            carveTimer += Time.deltaTime;
            anim?.SetBool("IsCarving", true);
            if (carveTimer >= carveDuration) CompleteCarve();
        }
        else
        {
            if (isCarving)
            {
                isCarving  = false;
                carveTimer = 0f;
                anim?.SetBool("IsCarving", false);
            }
        }
    }

    private void CompleteCarve()
    {
        isCarved  = true;
        isCarving = false;
        anim?.SetBool("IsCarving", false);

        // 아이템 1/2/3 중 랜덤 1개 → 인벤토리에 추가
        if (carveItems != null && carveItems.Count > 0 && Inventory.Instance != null)
        {
            ItemData picked = carveItems[Random.Range(0, carveItems.Count)];
            if (picked != null)
            {
                Inventory.Instance.AddItem(picked);
                Debug.Log($"[갈무리] {picked.itemName} 획득!");
            }
        }

        Vector2 dropPos = (Vector2)transform.position + Vector2.up * 0.3f;
        if (keyPrefab && Random.value < keyDropChance)
            Instantiate(keyPrefab, dropPos, Quaternion.identity);
        if (cardKeyPrefab && Random.value < cardKeyDropChance)
            Instantiate(cardKeyPrefab, dropPos + Vector2.right * 0.3f, Quaternion.identity);

        Destroy(gameObject, 0.15f);
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
                if (dist > attackRange * 1.5f) state = State.Chase;
                else if (attackCooldownTimer <= 0f) ExecuteAttack();
                break;
        }
    }

    private void ExecuteMovement()
    {
        switch (state)
        {
            case State.Patrol: DoPatrol(); break;
            case State.Chase:  DoChase();  break;
            case State.Attack: rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); break;
        }
    }

    private void DoPatrol()
    {
        if (Mathf.Abs(transform.position.x - patrolOrigin.x) >= patrolRange) patrolDir *= -1f;
        if (groundAhead && !Physics2D.Raycast(groundAhead.position, Vector2.down, 0.5f, groundLayer))
            patrolDir *= -1f;
        rb.linearVelocity = new Vector2(patrolDir * patrolSpeed, rb.linearVelocity.y);
        FaceDirection(patrolDir);
    }

    private void DoChase()
    {
        if (!player) return;
        float dir = Mathf.Sign(player.position.x - transform.position.x);
        rb.linearVelocity = new Vector2(dir * chaseSpeed, rb.linearVelocity.y);
        FaceDirection(dir);
    }

    private void ExecuteAttack()
    {
        if (!player) return;
        attackCooldownTimer = attackCooldown;
        anim?.SetTrigger("Attack");
    }

    public void DealSimpleMeleeAttack()
    {
        if (!player) return;
        if (Vector2.Distance(transform.position, player.position) < attackRange * 1.2f)
            if (player.TryGetComponent<HealthSystem>(out var ph))
                ph.TakeDamage(attackDamage);
    }

    private void OnDeath()
    {
        isDead = true;
        state  = State.Dead;
        anim?.SetTrigger("Die");
        rb.simulated = false;
        foreach (var col in GetComponents<Collider2D>()) col.enabled = false;
        if (player && player.TryGetComponent<PlayerController>(out var pc))
            pc.GainEXP(expReward);
    }

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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, carveRange);
    }
}