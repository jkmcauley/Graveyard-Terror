using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UHFPS.Runtime;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class Vampire : MonoBehaviour
{
    enum VampireState
    {
        Travel,
        Chase,
        ClawLeft,
        ClawRight,
        TwoClaw,
        GrabBite
    }

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int ClawLeftId = Animator.StringToHash("ClawLeft");
    static readonly int ClawRightId = Animator.StringToHash("ClawRight");
    static readonly int GrabBiteId = Animator.StringToHash("GrabBite");
    static readonly int TwoClawId = Animator.StringToHash("TwoClaw");
    static readonly int TurnLeftId = Animator.StringToHash("TurnLeft");
    static readonly int TurnRightId = Animator.StringToHash("TurnRight");
    static readonly int DeathId = Animator.StringToHash("Death");
    static readonly int ZombieIdleId = Animator.StringToHash("Idle");
    static readonly int ZombieWalkId = Animator.StringToHash("Walk");
    static readonly int ZombieRunId = Animator.StringToHash("Run");
    static readonly int ZombiePatrolId = Animator.StringToHash("Patrol");
    static readonly int ZombieAttackId = Animator.StringToHash("Attack");
    static Vampire _engaged;

    Animator _animator;
    Rigidbody _rb;
    NavMeshAgent _agent;
    Transform _player;
    Vector3 _destination;
    VampireState _state;
    bool _hasDestination;
    bool _attackVisible;

    [SerializeField] int _walkSpeed = 2;
    [SerializeField] int _runSpeed = 3;
    [SerializeField] float _arriveDistance = 0.6f;
    [SerializeField] float _clawRange = 2f;
    [SerializeField] float _grabRange = 1f;
    [SerializeField] float _chaseRange = 8f;
    [SerializeField] float _separateRange = 12f;
    [SerializeField] float _frontAngle = 35f;
    [SerializeField] float _turnSpeed = 240f;
    [SerializeField] int _clawDamage = 4;
    [SerializeField] int _grabDamage = 6;

    int _speed;
    int _hitsLanded;
    float _stuckTime;
    float _deathWait;
    bool _dying;
    bool _zombieAnimator;
    NPCHealth _health;
    List<NPCHealth.BodySegment> _segments;
    List<HeldJoint> _heldJoints = new List<HeldJoint>();

    struct HeldJoint
    {
        public CharacterJoint Joint;
        public Rigidbody Connected;
    }
    List<AIWaypoint> _route = new List<AIWaypoint>();
    int _routeIndex = -1;
    bool _chasing;

    bool StillFighting()
    {
        return !_dying && (_health == null || !_health.IsDead);
    }

    bool WantsPlayer(float distance)
    {
        float reach = _chasing ? _separateRange : _chaseRange;
        return distance <= reach;
    }

    bool HoldClaim(float distance)
    {
        if (_engaged == this)
        {
            if (StillFighting() && WantsPlayer(distance))
                return true;

            _engaged = null;
            return false;
        }

        if (_engaged != null && _engaged.StillFighting())
        {
            Transform engagedPlayer = _engaged.PlayerTransform();
            if (engagedPlayer != null && _engaged.WantsPlayer(_engaged.HorizontalDistance(engagedPlayer)))
                return false;
        }

        if (!WantsPlayer(distance))
            return false;

        Vampire[] others = FindObjectsByType<Vampire>(FindObjectsSortMode.None);
        for (int i = 0; i < others.Length; i++)
        {
            Vampire other = others[i];
            if (other == null || other == this || !other.StillFighting())
                continue;

            Transform otherPlayer = other.PlayerTransform();
            if (otherPlayer == null)
                continue;

            float otherDistance = other.HorizontalDistance(otherPlayer);
            if (other.WantsPlayer(otherDistance) && otherDistance < distance)
                return false;
        }

        _engaged = this;
        return true;
    }

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody>();
        _agent = GetComponent<NavMeshAgent>();

        _animator.enabled = true;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animator.applyRootMotion = false;
        _zombieAnimator = HasParameter("Walk", AnimatorControllerParameterType.Bool)
            && HasParameter("Attack", AnimatorControllerParameterType.Trigger);
        if (_zombieAnimator)
            SetZombiePose(true, false, false);
        else
            _animator.Play("idleBreathe", 0, 0f);
        _rb.isKinematic = true;
        _rb.useGravity = false;
        _agent.radius = 0.3f;
        _agent.stoppingDistance = 0.75f;

        foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skin.updateWhenOffscreen = true;

        int playerLayer = LayerMask.NameToLayer("Player");
        int bodyLayer = LayerMask.NameToLayer("BodyPart");
        if (playerLayer >= 0 && bodyLayer >= 0)
            Physics.IgnoreLayerCollision(playerLayer, bodyLayer, true);

        CapsuleCollider body = GetComponent<CapsuleCollider>();
        if (body != null)
            body.radius = 0.3f;

        _health = GetComponent<NPCHealth>();
        if (_health != null)
        {
            _health.AllowHeadhsot = true;
            _health.HeadshotMultiplier = 10f;
            _health.RemoveCorpse = true;
            _health.DisableCorpse = true;
            CaptureSegments();
            HoldLimbsForAnimation();
            _health.OnDeath.AddListener(BeginDeath);
        }

        _animator.Update(0f);
        ExposeHeadCollider(body);

        if (GetComponent<EnemyNavUnstuck>() == null && !UnderBoss())
            gameObject.AddComponent<EnemyNavUnstuck>();
    }

    bool UnderBoss()
    {
        Transform point = transform;
        while (point != null)
        {
            if (point.name == "BossEnemy")
                return true;

            point = point.parent;
        }

        return false;
    }

    void ExposeHeadCollider(CapsuleCollider body)
    {
        if (body == null || body.direction != 1)
            return;

        SphereCollider head = null;
        foreach (NPCBodyPart part in GetComponentsInChildren<NPCBodyPart>(true))
        {
            if (!part.IsHeadDamage)
                continue;

            head = part.GetComponent<SphereCollider>();
            if (head != null)
                break;
        }

        if (head == null)
            return;

        int bodyLayer = LayerMask.NameToLayer("BodyPart");
        if (bodyLayer >= 0)
            head.gameObject.layer = bodyLayer;

        if (head.radius <= 0f || Mathf.Approximately(head.radius, 0.5f))
            head.radius = 0.22f;

        Vector3 headCenter = head.transform.TransformPoint(head.center);
        float scale = Mathf.Max(
            Mathf.Abs(head.transform.lossyScale.x),
            Mathf.Abs(head.transform.lossyScale.y),
            Mathf.Abs(head.transform.lossyScale.z));
        if (scale < 0.001f)
            scale = 1f;

        float scaleY = Mathf.Abs(transform.lossyScale.y);
        if (scaleY < 0.001f)
            scaleY = 1f;

        Vector3 capsuleCenter = transform.TransformPoint(body.center);
        float bottom = capsuleCenter.y - body.height * 0.5f * scaleY;
        float top = headCenter.y - head.radius * scale - 0.04f;
        if (top < bottom + 0.5f)
            return;

        body.height = (top - bottom) / scaleY;
        float midY = (top + bottom) * 0.5f;
        Vector3 mid = transform.InverseTransformPoint(new Vector3(capsuleCenter.x, midY, capsuleCenter.z));
        Vector3 center = body.center;
        center.y = mid.y;
        body.center = center;
    }

    void Start()
    {
        if (!PlaceOnNavMesh())
        {
            Debug.LogWarning("Vampire is not on the NavMesh.", this);
            Idle();
            return;
        }

        _route = BuildRoute();
        if (_route.Count == 0)
        {
            Debug.LogWarning("Vampire has no waypoint on the NavMesh.", this);
            Idle();
            return;
        }

        _routeIndex = ClosestRouteIndex();
        if (!GoToRouteIndex(_routeIndex))
            AdvanceRoute();
        else
            BeginTravel();
    }

    void Update()
    {
        if (_dying)
        {
            if (DeathFinished())
                DropRagdoll();
            return;
        }

        Transform player = PlayerTransform();
        if (player == null)
        {
            if (_state != VampireState.Travel)
                BeginTravel();
            UpdateTravel();
            return;
        }

        float distance = HorizontalDistance(player);
        float angle = SignedAngleTo(player);
        _chasing = HoldClaim(distance);

        if (_chasing)
        {
            if (IsAttacking())
            {
                Face(player);
                TryDamagePlayer(player, distance, angle);
                if (!AttackFinished())
                    return;

                if (distance <= _clawRange && Mathf.Abs(angle) <= 110f)
                    BeginAttack(distance, angle);
                else
                    UpdateChase(player);
                return;
            }

            if (distance <= _clawRange && Mathf.Abs(angle) <= 110f)
                BeginAttack(distance, angle);
            else if (distance <= _clawRange)
            {
                Face(player);
                Idle();
            }
            else
                UpdateChase(player);
            return;
        }

        if (IsAttacking())
        {
            if (AttackFinished())
                ResumeRoute();
            return;
        }

        UpdateTravel();
    }

    bool IsAttacking()
    {
        return _state == VampireState.ClawLeft
            || _state == VampireState.ClawRight
            || _state == VampireState.TwoClaw
            || _state == VampireState.GrabBite;
    }

    void UpdateChase(Transform player)
    {
        _state = VampireState.Chase;
        _attackVisible = false;
        _agent.updateRotation = true;
        _agent.isStopped = false;
        _agent.speed = 3.5f;
        _agent.stoppingDistance = _clawRange * 0.5f;

        if (NavMesh.SamplePosition(player.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);

        Vector3 velocity = _agent.velocity;
        velocity.y = 0f;
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        bool movingFast = velocity.sqrMagnitude > 6.25f;
        bool towardPlayer = toPlayer.sqrMagnitude < 0.01f
            || Vector3.Dot(velocity, toPlayer.normalized) > 0.25f;
        if (movingFast && towardPlayer)
            Run();
        else if (velocity.sqrMagnitude > 0.04f)
            Walk();
        else
            Idle();
    }

    void ResumeRoute()
    {
        _chasing = false;
        _agent.speed = 2f;
        _agent.stoppingDistance = 0.75f;
        _routeIndex = ClosestRouteIndex();
        if (!GoToRouteIndex(_routeIndex))
            AdvanceRoute();
        else
            BeginTravel();
    }

    void BeginTravel()
    {
        _state = VampireState.Travel;
        _attackVisible = false;
        _agent.updateRotation = true;
        _agent.isStopped = false;
        _agent.speed = 2f;
        _agent.stoppingDistance = 0.75f;
        if (_hasDestination)
            _agent.SetDestination(_destination);
        Walk();
    }

    void UpdateTravel()
    {
        if (!_agent.isOnNavMesh || _agent.pathPending || !_hasDestination)
            return;

        if (_agent.remainingDistance <= Mathf.Max(_agent.stoppingDistance, _arriveDistance))
        {
            _stuckTime = 0f;
            AdvanceRoute();
            return;
        }

        if (_agent.velocity.sqrMagnitude > 0.04f)
        {
            _stuckTime = 0f;
            Walk();
            return;
        }

        _stuckTime += Time.deltaTime;
        if (_stuckTime < 1f)
            return;

        _stuckTime = 0f;
        AdvanceRoute();
    }

    List<AIWaypoint> BuildRoute()
    {
        List<AIWaypoint> route = new List<AIWaypoint>();
        AIWaypointsGroup[] groups = FindObjectsByType<AIWaypointsGroup>(FindObjectsSortMode.None);
        AIWaypointsGroup closest = null;
        float best = Mathf.Infinity;

        foreach (AIWaypointsGroup group in groups)
        {
            foreach (AIWaypoint waypoint in group.Waypoints)
            {
                if (waypoint == null)
                    continue;

                float distance = (waypoint.transform.position - transform.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    closest = group;
                }
            }
        }

        if (closest == null)
            return route;

        foreach (AIWaypoint waypoint in closest.Waypoints)
        {
            if (waypoint != null)
                route.Add(waypoint);
        }

        return route;
    }

    int ClosestRouteIndex()
    {
        int index = 0;
        float best = Mathf.Infinity;
        for (int i = 0; i < _route.Count; i++)
        {
            float distance = (_route[i].transform.position - transform.position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                index = i;
            }
        }

        return index;
    }

    bool GoToRouteIndex(int index)
    {
        AIWaypoint waypoint = _route[index];
        if (!NavMesh.SamplePosition(waypoint.transform.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            return false;

        Vector3 offMesh = hit.position - waypoint.transform.position;
        offMesh.y = 0f;
        if (offMesh.sqrMagnitude > 0.25f)
            return false;

        NavMeshPath path = new NavMeshPath();
        if (!_agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
            return false;

        _routeIndex = index;
        _destination = hit.position;
        _hasDestination = true;
        _agent.SetPath(path);
        return true;
    }

    void AdvanceRoute()
    {
        if (_route.Count == 0)
            return;

        for (int step = 1; step <= _route.Count; step++)
        {
            int index = (_routeIndex + step) % _route.Count;
            if (GoToRouteIndex(index))
            {
                BeginTravel();
                return;
            }
        }

        Idle();
    }

    void BeginAttack(float distance, float angle)
    {
        _agent.isStopped = true;
        _agent.updateRotation = false;
        Idle();

        if (distance <= _grabRange)
            _state = VampireState.GrabBite;
        else if (Mathf.Abs(angle) <= _frontAngle)
            _state = VampireState.TwoClaw;
        else if (angle < 0f)
            _state = VampireState.ClawLeft;
        else
            _state = VampireState.ClawRight;

        _attackVisible = false;
        _hitsLanded = 0;
        switch (_state)
        {
            case VampireState.GrabBite:
                GrabBite();
                break;
            case VampireState.TwoClaw:
                TwoClaw();
                break;
            case VampireState.ClawLeft:
                ClawLeft();
                break;
            case VampireState.ClawRight:
                ClawRight();
                break;
        }
    }

    void TryDamagePlayer(Transform player, float distance, float angle)
    {
        if (_dying || _dropped || (_health != null && _health.IsDead))
            return;

        int layer = _zombieAnimator ? 1 : 0;
        if (_animator.IsInTransition(layer))
            return;

        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(layer);
        if (info.IsName("death") || !IsAttackState(info))
            return;

        int hits = HitsReached(_state, info.normalizedTime);
        if (hits <= _hitsLanded)
            return;

        int freshHits = hits - _hitsLanded;
        _hitsLanded = hits;
        float reach = _state == VampireState.GrabBite ? _grabRange + 0.35f : _clawRange;
        if (distance > reach || Mathf.Abs(angle) > 110f)
            return;

        PlayerManager manager = player.GetComponent<PlayerManager>();
        if (manager == null || manager.PlayerHealth == null || manager.PlayerHealth.IsDead)
            return;

        int damage = _state == VampireState.GrabBite ? _grabDamage : _clawDamage;
        for (int i = 0; i < freshHits; i++)
            manager.PlayerHealth.ApplyDamage(damage, transform);
    }

    static int HitsReached(VampireState state, float normalizedTime)
    {
        if (state == VampireState.TwoClaw)
        {
            if (normalizedTime >= 0.65f)
                return 2;
            if (normalizedTime >= 0.3f)
                return 1;
            return 0;
        }

        return normalizedTime >= 0.4f ? 1 : 0;
    }

    bool AttackFinished()
    {
        int layer = _zombieAnimator ? 1 : 0;
        if (_animator.IsInTransition(layer))
            return false;

        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(layer);
        if (!IsAttackState(info))
            return _attackVisible;

        _attackVisible = true;
        return info.normalizedTime >= 0.9f;
    }

    bool IsAttackState(AnimatorStateInfo info)
    {
        if (_zombieAnimator)
            return info.IsName("Attack 01");

        return info.IsName(AttackStateName(_state));
    }

    static string AttackStateName(VampireState state)
    {
        switch (state)
        {
            case VampireState.ClawLeft:
                return "clawsAttackLeft";
            case VampireState.ClawRight:
                return "clawsAttackRight";
            case VampireState.TwoClaw:
                return "clawsAttack2HitCombo";
            default:
                return "standGrabBite";
        }
    }

    Transform PlayerTransform()
    {
        if (_player != null)
            return _player;

        if (PlayerPresenceManager.HasReference && PlayerPresenceManager.Instance.Player != null)
            _player = PlayerPresenceManager.Instance.Player.transform;

        return _player;
    }

    float HorizontalDistance(Transform target)
    {
        Vector3 offset = target.position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    float SignedAngleTo(Transform target)
    {
        Vector3 offset = target.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude < 0.001f)
            return 0f;

        return Vector3.SignedAngle(transform.forward, offset, Vector3.up);
    }

    void Face(Transform target)
    {
        Vector3 offset = target.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude < 0.001f)
            return;

        Quaternion look = Quaternion.LookRotation(offset);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _turnSpeed * Time.deltaTime);
    }

    bool _dropped;

    void BeginDeath()
    {
        if (_dying)
            return;

        _dying = true;
        if (_engaged == this)
            _engaged = null;
        _deathWait = 0f;
        _hitsLanded = 0;
        if (_agent != null)
        {
            _agent.isStopped = true;
            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agent.enabled = false;
        }

        CapsuleCollider body = GetComponent<CapsuleCollider>();
        if (body != null)
            body.enabled = false;

        if (_rb != null)
            _rb.detectCollisions = false;

        if (_animator != null && !_zombieAnimator)
        {
            _animator.enabled = true;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.ResetTrigger(ClawLeftId);
            _animator.ResetTrigger(ClawRightId);
            _animator.ResetTrigger(GrabBiteId);
            _animator.ResetTrigger(TwoClawId);
            _animator.ResetTrigger(TurnLeftId);
            _animator.ResetTrigger(TurnRightId);
            _animator.ResetTrigger(DeathId);
            _animator.Play("death", 0, 0f);
            _animator.Update(0f);
        }
        else if (_zombieAnimator)
        {
            DropRagdoll();
        }
    }

    bool UsesControlRig()
    {
        return name.IndexOf("Boogey", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Boogie", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    bool DeathFinished()
    {
        if (_animator == null || !_animator.enabled)
            return true;

        if (_animator.IsInTransition(0))
            return false;

        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName("death"))
        {
            _deathWait += Time.deltaTime;
            return _deathWait >= 2.5f;
        }

        return info.normalizedTime >= 0.92f;
    }

    void DropRagdoll()
    {
        if (_dropped)
            return;

        _dropped = true;

        // This rig's joints are stacked on the control bones. Turning them into a ragdoll throws the body upward,
        // and the flamethrower fires are parented to those bones, so they leave the ground with him.
        if (UsesControlRig())
        {
            if (_animator != null)
                _animator.enabled = false;
            return;
        }

        if (_health != null && _segments != null)
        {
            _health.BodySegments = _segments;
            for (int i = 0; i < _heldJoints.Count; i++)
            {
                HeldJoint held = _heldJoints[i];
                if (held.Joint != null && held.Connected != null)
                    held.Joint.connectedBody = held.Connected;
            }

            foreach (NPCHealth.BodySegment part in _segments)
            {
                if (part.Rigidbody == null)
                    continue;

                part.Rigidbody.isKinematic = false;
                part.Rigidbody.useGravity = true;
                if (part.Collider != null)
                    part.Collider.isTrigger = false;
            }
        }

        if (_animator != null)
            _animator.enabled = false;

        CapsuleCollider body = GetComponent<CapsuleCollider>();
        if (body != null)
            body.enabled = false;

        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.detectCollisions = false;
        }
    }

    void CaptureSegments()
    {
        _segments = new List<NPCHealth.BodySegment>();
        if (_health.BodySegments != null)
        {
            foreach (NPCHealth.BodySegment part in _health.BodySegments)
            {
                if (part.Rigidbody != null)
                    _segments.Add(part);
            }
        }

        if (_segments.Count == 0)
        {
            foreach (NPCBodyPart part in GetComponentsInChildren<NPCBodyPart>(true))
            {
                Rigidbody body = part.GetComponent<Rigidbody>();
                Collider collider = part.GetComponent<Collider>();
                if (body == null)
                    continue;

                _segments.Add(new NPCHealth.BodySegment(body, collider, part));
            }
        }

        _health.BodySegments = new List<NPCHealth.BodySegment>();
    }

    void HoldLimbsForAnimation()
    {
        if (_segments == null)
            return;

        foreach (NPCHealth.BodySegment part in _segments)
        {
            if (part.Rigidbody == null)
                continue;

            part.Rigidbody.isKinematic = true;
            part.Rigidbody.useGravity = false;
            CharacterJoint joint = part.Rigidbody.GetComponent<CharacterJoint>();
            if (joint != null)
            {
                _heldJoints.Add(new HeldJoint { Joint = joint, Connected = joint.connectedBody });
                joint.connectedBody = null;
            }

            if (part.Collider != null)
                part.Collider.isTrigger = true;
        }
    }

    bool PlaceOnNavMesh()
    {
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            return false;

        _agent.Warp(hit.position);
        return _agent.isOnNavMesh;
    }

    public void Idle()
    {
        if (_zombieAnimator)
            SetZombiePose(true, false, false);
        else
            SetSpeed(0);
    }

    public void Walk()
    {
        if (_zombieAnimator)
            SetZombiePose(false, true, false);
        else
            SetSpeed(_walkSpeed);
    }

    public void Run()
    {
        if (_zombieAnimator)
            SetZombiePose(false, false, true);
        else
            SetSpeed(_runSpeed);
    }

    public void ClawLeft()
    {
        if (_zombieAnimator)
            _animator.SetTrigger(ZombieAttackId);
        else
            _animator.SetTrigger(ClawLeftId);
    }

    public void ClawRight()
    {
        if (_zombieAnimator)
            _animator.SetTrigger(ZombieAttackId);
        else
            _animator.SetTrigger(ClawRightId);
    }

    public void GrabBite()
    {
        if (_zombieAnimator)
            _animator.SetTrigger(ZombieAttackId);
        else
            _animator.SetTrigger(GrabBiteId);
    }

    public void TwoClaw()
    {
        if (_zombieAnimator)
            _animator.SetTrigger(ZombieAttackId);
        else
            _animator.SetTrigger(TwoClawId);
    }

    public void TurnLeft()
    {
        if (!_zombieAnimator)
            _animator.SetTrigger(TurnLeftId);
    }

    public void TurnRight()
    {
        if (!_zombieAnimator)
            _animator.SetTrigger(TurnRightId);
    }

    void SetZombiePose(bool idle, bool walk, bool run)
    {
        _animator.SetBool(ZombieIdleId, idle);
        _animator.SetBool(ZombieWalkId, walk);
        _animator.SetBool(ZombiePatrolId, false);
        _animator.SetBool(ZombieRunId, run);
    }

    bool HasParameter(string name, AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter[] parameters = _animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == name && parameters[i].type == type)
                return true;
        }

        return false;
    }

    void SetSpeed(int speed)
    {
        _speed = speed;
        _animator.SetInteger(SpeedId, _speed);
    }
}
