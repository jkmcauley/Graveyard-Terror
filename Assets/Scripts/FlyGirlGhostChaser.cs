using UnityEngine;
using UHFPS.Runtime;

public class FlyGirlGhostChaser : MonoBehaviour
{
    [SerializeField] Transform _player;
    [SerializeField] PlayerHealth _playerHealth;
    [SerializeField] NPCHealth _ghostHealth;

    [Header("Movement")]
    [SerializeField] float _detectRange = 12f;
    [SerializeField] float _attackRange = 2f;
    [SerializeField] float _moveSpeed = 3f;

    [Header("Hover")]
    [SerializeField] float _bobAmount = 0.25f;
    [SerializeField] float _bobSpeed = 2f;

    [Header("Attack")]
    [SerializeField] int _damage = 10;
    [SerializeField] float _attackCooldown = 2f;

    Vector3 _hoverPosition;
    float _nextAttackTime;

    public bool IsAlive
    {
        get
        {
            if (_ghostHealth == null)
                _ghostHealth = GetComponent<NPCHealth>();

            return _ghostHealth == null || !_ghostHealth.IsDead;
        }
    }

    public int BodyId
    {
        get
        {
            if (_ghostHealth == null)
                _ghostHealth = GetComponent<NPCHealth>();

            return (_ghostHealth != null ? _ghostHealth.gameObject : gameObject).GetInstanceID();
        }
    }
    bool _aggro;
    private Animator _animator;
    

    void Awake()
    {
        if (_ghostHealth == null)
            _ghostHealth = GetComponent<NPCHealth>();

        if (_ghostHealth != null)
        {
            _ghostHealth.OnTakeDamage.AddListener(OnGhostDamaged);
            _ghostHealth.OnDeath.AddListener(OnGhostDied);
        }

        _animator = GetComponent<Animator>();
    }

    void OnDestroy()
    {
        if (_ghostHealth == null)
            return;

        _ghostHealth.OnTakeDamage.RemoveListener(OnGhostDamaged);
        _ghostHealth.OnDeath.RemoveListener(OnGhostDied);
    }

    void Start()
    {
        _hoverPosition = transform.position;
        ResolvePlayer();
    }

    void Update()
    {
        if (_ghostHealth != null && _ghostHealth.IsDead)
            return;

        ResolvePlayer();
        if (_player == null || _playerHealth == null || _playerHealth.IsDead)
            return;

        Vector3 target = new Vector3(
            _player.position.x,
            _hoverPosition.y,
            _player.position.z
        );

        float horizontalDistance = Vector3.Distance(_hoverPosition, target);
        float playerDistance = Vector3.Distance(_hoverPosition, _player.position);
        bool inRange = _aggro || playerDistance <= _detectRange;

        if (inRange)
        {
            if (horizontalDistance > _attackRange)
            {
                float step = Mathf.Min(
                    _moveSpeed * Time.deltaTime,
                    horizontalDistance - _attackRange
                );

                _hoverPosition = Vector3.MoveTowards(_hoverPosition, target, step);
            }

            FacePlayer();
        }

        // Attack range is horizontal. The hover height would keep a 3D
        // distance above _attackRange even when she has stopped beside the player.
        bool attacking = inRange && horizontalDistance <= _attackRange;
        UpdateAnimator(inRange, attacking);

        if (attacking && Time.time >= _nextAttackTime)
        {
            _nextAttackTime = Time.time + _attackCooldown;
            _playerHealth.ApplyDamage(_damage, transform);
        }

        transform.position = _hoverPosition + Vector3.up *
            Mathf.Sin(Time.time * _bobSpeed) * _bobAmount;
    }

    void UpdateAnimator(bool chasing, bool attacking)
    {
        if (_animator == null)
            return;

        // Attack only leaves Fly_1. Fly stays on during the swing so the clip
        // returns to flight, then the trigger starts the next swing.
        _animator.SetBool("Fly", chasing);
        if (attacking)
            _animator.SetTrigger("Attack");
        else
            _animator.ResetTrigger("Attack");
    }

    void OnGhostDamaged(int damage)
    {
        _aggro = true;
        ResolvePlayer();
        if (_animator != null)
            _animator.SetTrigger("Hit");
    }

    void OnGhostDied()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
        if (_animator == null)
            return;

        // Health turns the animator off before this runs, which left her frozen in the hover pose.
        _animator.enabled = true;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animator.ResetTrigger("Attack");
        _animator.ResetTrigger("Hit");
        _animator.SetBool("Fly", false);
        _animator.Play("Fly1Death_1", 0, 0f);
        _animator.Update(0f);
    }

    void ResolvePlayer()
    {
        if (_playerHealth != null)
        {
            if (_player == null)
                _player = _playerHealth.transform;
            return;
        }

        if (_player != null)
        {
            _playerHealth = _player.GetComponent<PlayerHealth>();
            if (_playerHealth == null)
                _playerHealth = _player.GetComponentInChildren<PlayerHealth>();
            if (_playerHealth != null)
                return;
        }

        if (!PlayerPresenceManager.HasReference || PlayerPresenceManager.Instance.Player == null)
            return;

        GameObject playerObject = PlayerPresenceManager.Instance.Player;
        _player = playerObject.transform;
        PlayerManager manager = PlayerPresenceManager.Instance.PlayerManager;
        _playerHealth = manager != null
            ? manager.PlayerHealth
            : playerObject.GetComponentInChildren<PlayerHealth>();
    }

    void FacePlayer()
    {
        Vector3 direction = _player.position - _hoverPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }
}
