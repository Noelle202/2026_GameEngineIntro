using UnityEngine;

/// <summary>
/// 2D 플랫포머 플레이어 컨트롤러
/// 필요 컴포넌트: Rigidbody2D, Collider2D
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    // ─────────────────────────────────────────
    // Inspector 설정값
    // ─────────────────────────────────────────

    [Header("이동")]
    [SerializeField] private float moveSpeed        = 8f;
    [SerializeField] private float acceleration     = 12f;   // 가속도
    [SerializeField] private float deceleration     = 16f;   // 감속도
    [SerializeField] private float airControlFactor = 0.7f;  // 공중 조작 배율

    [Header("점프")]
    [SerializeField] private float jumpForce        = 14f;
    [SerializeField] private int   maxJumpCount     = 2;     // 더블점프 포함
    [SerializeField] private float jumpCutMultiplier = 0.4f; // 짧게 누를 때 감쇄
    [SerializeField] private float fallGravityScale = 3f;    // 낙하 시 중력 배율
    [SerializeField] private float normalGravityScale = 1.5f;

    [Header("코요테 타임 & 점프 버퍼")]
    [SerializeField] private float coyoteTime       = 0.15f; // 절벽 끝 여유 시간
    [SerializeField] private float jumpBufferTime   = 0.15f; // 입력 선입력 시간

    [Header("벽 상호작용")]
    [SerializeField] private float wallSlideSpeed   = 2f;
    [SerializeField] private Vector2 wallJumpForce  = new Vector2(8f, 14f);
    [SerializeField] private float wallJumpLockTime = 0.2f;  // 벽 점프 후 입력 잠금

    [Header("지면/벽 감지")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheckLeft;
    [SerializeField] private Transform wallCheckRight;
    [SerializeField] private float     groundCheckRadius = 0.1f;
    [SerializeField] private float     wallCheckDistance = 0.05f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask wallLayer;

    // ─────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────

    private Rigidbody2D _rb;
    private Animator    _anim;          // (선택) 애니메이터
    private SpriteRenderer _sprite;    // (선택) 방향 전환용

    // 지면/벽
    private bool _isGrounded;
    private bool _isTouchingWallLeft;
    private bool _isTouchingWallRight;
    private bool _isWallSliding;
    private bool _isFacingRight = true;

    // 점프
    private int   _jumpsRemaining;
    private float _coyoteTimeCounter;
    private float _jumpBufferCounter;
    private bool  _isJumping;

    // 벽 점프
    private float _wallJumpLockCounter;

    // 입력
    private float _inputX;
    private bool  _jumpPressed;
    private bool  _jumpHeld;
    private bool  _jumpReleased;

    // ─────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────

    private void Awake()
    {
        _rb     = GetComponent<Rigidbody2D>();
        _anim   = GetComponent<Animator>();   // 없어도 동작함
        _sprite = GetComponent<SpriteRenderer>();

        _rb.gravityScale  = normalGravityScale;
        _rb.constraints   = RigidbodyConstraints2D.FreezeRotation;
        _jumpsRemaining   = maxJumpCount;
    }

    // ─────────────────────────────────────────
    // 매 프레임 입력 수집
    // ─────────────────────────────────────────

    private void Update()
    {
        GatherInput();
        UpdateTimers();
        HandleFlip();
        UpdateAnimator();
    }

    // ─────────────────────────────────────────
    // 물리 처리 (FixedUpdate)
    // ─────────────────────────────────────────

    private void FixedUpdate()
    {
        CheckCollisions();
        HandleMovement();
        HandleWallSlide();
        HandleJump();
        ApplyGravity();
    }

    // ═════════════════════════════════════════
    // 입력 수집
    // ═════════════════════════════════════════

    private void GatherInput()
    {
        _inputX       = Input.GetAxisRaw("Horizontal");
        _jumpPressed  = Input.GetButtonDown("Jump");
        _jumpHeld     = Input.GetButton("Jump");
        _jumpReleased = Input.GetButtonUp("Jump");

        // 점프 버퍼: 입력이 들어오면 카운터 충전
        if (_jumpPressed)
            _jumpBufferCounter = jumpBufferTime;
    }

    // ═════════════════════════════════════════
    // 타이머 업데이트
    // ═════════════════════════════════════════

    private void UpdateTimers()
    {
        // 코요테 타임: 지면에 있으면 충전, 없으면 감소
        if (_isGrounded)
            _coyoteTimeCounter = coyoteTime;
        else
            _coyoteTimeCounter -= Time.deltaTime;

        // 점프 버퍼 감소
        _jumpBufferCounter   -= Time.deltaTime;

        // 벽 점프 잠금 감소
        _wallJumpLockCounter -= Time.deltaTime;
    }

    // ═════════════════════════════════════════
    // 충돌 감지
    // ═════════════════════════════════════════

    private void CheckCollisions()
    {
        // 지면
        _isGrounded = Physics2D.OverlapCircle(
            groundCheck.position, groundCheckRadius, groundLayer);

        // 벽 (좌/우)
        _isTouchingWallLeft  = Physics2D.Raycast(
            wallCheckLeft.position,  Vector2.left,  wallCheckDistance, wallLayer);
        _isTouchingWallRight = Physics2D.Raycast(
            wallCheckRight.position, Vector2.right, wallCheckDistance, wallLayer);

        // 착지 시 점프 횟수 리셋
        if (_isGrounded)
        {
            _jumpsRemaining = maxJumpCount;
            _isJumping      = false;
        }
    }

    // ═════════════════════════════════════════
    // 수평 이동
    // ═════════════════════════════════════════

    private void HandleMovement()
    {
        // 벽 점프 직후 입력 잠금 중이면 처리 안 함
        if (_wallJumpLockCounter > 0f) return;

        float targetVelocityX = _inputX * moveSpeed;
        float currentVelocityX = _rb.linearVelocity.x;

        // 공중 vs 지상 가속도 결정
        float controlFactor = _isGrounded ? 1f : airControlFactor;

        float accel = (Mathf.Abs(targetVelocityX) > 0.01f)
            ? acceleration * controlFactor
            : deceleration * controlFactor;

        float newVelocityX = Mathf.MoveTowards(
            currentVelocityX, targetVelocityX, accel * Time.fixedDeltaTime);

        _rb.linearVelocity = new Vector2(newVelocityX, _rb.linearVelocity.y);
    }

    // ═════════════════════════════════════════
    // 점프 처리
    // ═════════════════════════════════════════

    private void HandleJump()
    {
        // ── 점프 실행 조건 ──
        bool canJump =
            (_coyoteTimeCounter > 0f || _jumpsRemaining > 0)
            && _jumpBufferCounter > 0f
            && !_isWallSliding;

        if (canJump)
        {
            // 코요테 타임 소진 후 점프하면 일반 점프 처리
            if (_coyoteTimeCounter <= 0f)
                _jumpsRemaining = Mathf.Max(0, _jumpsRemaining - 1);

            ExecuteJump(Vector2.up * jumpForce);
            _jumpBufferCounter  = 0f;
            _coyoteTimeCounter  = 0f;
        }

        // ── 벽 점프 ──
        if (_jumpBufferCounter > 0f && _isWallSliding)
        {
            float direction = _isTouchingWallLeft ? 1f : -1f;
            Vector2 force   = new Vector2(wallJumpForce.x * direction, wallJumpForce.y);
            ExecuteJump(force);
            _jumpBufferCounter   = 0f;
            _wallJumpLockCounter = wallJumpLockTime;
        }

        // ── 짧게 누를 때 점프 감쇄 (점프 컷) ──
        if (_jumpReleased && _rb.linearVelocity.y > 0f && _isJumping)
        {
            _rb.linearVelocity = new Vector2(
                _rb.linearVelocity.x,
                _rb.linearVelocity.y * jumpCutMultiplier);
        }
    }

    private void ExecuteJump(Vector2 force)
    {
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f); // 기존 Y 초기화
        _rb.AddForce(force, ForceMode2D.Impulse);
        _isJumping = true;
        _jumpsRemaining = Mathf.Max(0, _jumpsRemaining - 1);
    }

    // ═════════════════════════════════════════
    // 벽 슬라이드
    // ═════════════════════════════════════════

    private void HandleWallSlide()
    {
        bool touchingWall = _isTouchingWallLeft || _isTouchingWallRight;

        _isWallSliding = touchingWall
            && !_isGrounded
            && _rb.linearVelocity.y < 0f
            && _inputX != 0f;

        if (_isWallSliding)
        {
            _jumpsRemaining = maxJumpCount; // 벽에 붙으면 점프 횟수 리셋
            _rb.linearVelocity = new Vector2(
                _rb.linearVelocity.x,
                Mathf.Max(_rb.linearVelocity.y, -wallSlideSpeed));
        }
    }

    // ═════════════════════════════════════════
    // 중력 보정
    // ═════════════════════════════════════════

    private void ApplyGravity()
    {
        bool falling = _rb.linearVelocity.y < 0f && !_isGrounded;

        if (falling && !_isWallSliding)
            _rb.gravityScale = fallGravityScale;    // 낙하 시 중력 강화
        else
            _rb.gravityScale = normalGravityScale;
    }

    // ═════════════════════════════════════════
    // 스프라이트 방향 전환
    // ═════════════════════════════════════════

    private void HandleFlip()
    {
        if (_inputX > 0f && !_isFacingRight) Flip();
        else if (_inputX < 0f && _isFacingRight) Flip();
    }

    private void Flip()
    {
        _isFacingRight = !_isFacingRight;
        if (_sprite != null)
            _sprite.flipX = !_isFacingRight;
        // transform.localScale를 사용할 경우:
        // Vector3 s = transform.localScale;
        // s.x *= -1;
        // transform.localScale = s;
    }

    // ═════════════════════════════════════════
    // 애니메이터 연동 (선택)
    // ═════════════════════════════════════════

    private void UpdateAnimator()
    {
        if (_anim == null) return;

        _anim.SetFloat("SpeedX",    Mathf.Abs(_rb.linearVelocity.x));
        _anim.SetFloat("SpeedY",    _rb.linearVelocity.y);
        _anim.SetBool("IsGrounded", _isGrounded);
        _anim.SetBool("IsWallSlide",_isWallSliding);
    }

    // ═════════════════════════════════════════
    // 디버그 기즈모
    // ═════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (wallCheckLeft != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(wallCheckLeft.position,
                Vector2.left * wallCheckDistance);
        }

        if (wallCheckRight != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(wallCheckRight.position,
                Vector2.right * wallCheckDistance);
        }
    }
#endif
}
