using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 2.5D 플레이어 컨트롤러 (3D)
/// XZ 평면 이동 / 마우스 Ground Raycast 회전
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float walkSpeed     = 4f;
    [SerializeField] private float runSpeed      = 7f;
    [SerializeField] private float dashForce     = 12f;
    [SerializeField] private float dashCooldown  = 1.5f;
    [SerializeField] private float staminaPerRun = 5f;
    [SerializeField] private float staminaPerDash= 15f;

    [Header("마우스 조준")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float     rotationSpeed = 20f;

    [Header("상호작용")]
    [SerializeField] private float     interactRange = 1.8f;
    [SerializeField] private LayerMask interactableLayer;

    [Header("애니메이션")]
    [SerializeField] private Animator animator;

    private Rigidbody     _rb;
    private SurvivalStats _stats;
    private Camera        _mainCamera;

    private Vector3 _moveInput;
    private Vector2 _mouseScreenPos;
    private bool    _isRunning;
    private bool    _isDashing;
    private float   _dashTimer;

    private IInteractable _nearestInteractable;

    public static event Action<IInteractable> OnInteractableNear;
    public static event Action               OnInteractableLeft;

    private static readonly int AnimSpeed   = Animator.StringToHash("Speed");
    private static readonly int AnimMoveX   = Animator.StringToHash("MoveX");
    private static readonly int AnimMoveZ   = Animator.StringToHash("MoveZ");
    private static readonly int AnimDashing = Animator.StringToHash("IsDashing");

    public bool IsMoving  => _moveInput.sqrMagnitude > 0.01f;
    public bool IsRunning => _isRunning && IsMoving;
    public bool IsDashing => _isDashing;

    private void Awake()
    {
        _rb         = GetComponent<Rigidbody>();
        _stats      = GetComponent<SurvivalStats>();
        _mainCamera = Camera.main;

        _rb.constraints = RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ;
    }

    private void Update()
    {
        if (GameManager.Instance?.CurrentState != GameState.Playing) return;
        UpdateMouseAim();
        HandleRunInput();
        if (_dashTimer > 0) _dashTimer -= Time.deltaTime;
        DetectInteractable();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance?.CurrentState != GameState.Playing) return;
        Move();
    }

    // Input System 콜백
    public void OnMove(InputValue value)
    {
        Vector2 input = value.Get<Vector2>();
        _moveInput = new Vector3(input.x, 0f, input.y);
    }
    public void OnRun(InputValue value)    => _isRunning = value.isPressed;
    public void OnDash(InputValue value)   { if (value.isPressed) TryDash(); }
    public void OnInteract(InputValue value){ if (value.isPressed) TryInteract(); }
    public void OnInventory(InputValue value){ if (value.isPressed) UIManager.Instance?.ToggleInventory(); }
    public void OnPause(InputValue value)  { if (value.isPressed) GameManager.Instance?.PauseGame(); }
    public void OnAim(InputValue value)    => _mouseScreenPos = value.Get<Vector2>();

    // 마우스 Ground Raycast → 캐릭터 회전 (XZ 평면)
    private void UpdateMouseAim()
    {
        if (_mainCamera == null) return;
        Ray ray = _mainCamera.ScreenPointToRay(_mouseScreenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
        {
            Vector3 dir = hit.point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
        }
    }

    // XZ 평면 이동, Y는 중력 유지
    private void Move()
    {
        if (_isDashing) return;
        float speed = walkSpeed;
        if (IsRunning)
        {
            bool ok = _stats.ConsumeStamina(staminaPerRun * Time.fixedDeltaTime);
            speed = ok ? runSpeed : walkSpeed;
        }
        Vector3 move = _moveInput * speed;
        _rb.linearVelocity = new Vector3(move.x, _rb.linearVelocity.y, move.z);
    }

    private void TryDash()
    {
        if (_isDashing || _dashTimer > 0) return;
        if (!_stats.ConsumeStamina(staminaPerDash)) return;
        _isDashing = true;
        _dashTimer = dashCooldown;
        Vector3 dir = _moveInput.sqrMagnitude > 0.01f ? _moveInput.normalized : transform.forward;
        dir.y = 0f;
        _rb.AddForce(dir * dashForce, ForceMode.Impulse);
        Invoke(nameof(EndDash), 0.2f);
    }
    private void EndDash() => _isDashing = false;
    private void HandleRunInput() { if (_stats.Stamina <= 0) _isRunning = false; }

    // 상호작용 감지 (3D OverlapSphere)
    private void DetectInteractable()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange, interactableLayer);
        IInteractable found = null;
        float minDist = float.MaxValue;
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out IInteractable i))
            {
                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < minDist) { minDist = d; found = i; }
            }
        }
        if (found != _nearestInteractable)
        {
            _nearestInteractable = found;
            if (found != null) OnInteractableNear?.Invoke(found);
            else               OnInteractableLeft?.Invoke();
        }
    }
    private void TryInteract() => _nearestInteractable?.Interact(this);

    public static bool IsPointerOverUI()
        => UnityEngine.EventSystems.EventSystem.current != null
        && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

    private void UpdateAnimator()
    {
        if (animator == null) return;
        Vector3 lv = transform.InverseTransformDirection(_rb.linearVelocity);
        float speed = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z).magnitude;
        animator.SetFloat(AnimSpeed,  speed);
        animator.SetFloat(AnimMoveX,  lv.x);
        animator.SetFloat(AnimMoveZ,  lv.z);
        animator.SetBool(AnimDashing, _isDashing);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
#endif
}
