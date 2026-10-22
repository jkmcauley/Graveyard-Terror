using System.Collections;
using System.Collections.Generic;
using TMPro;
using UHFPS.Runtime;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;

public class BossTeleport : MonoBehaviour
{
    [SerializeField] private Transform _player;
    [SerializeField] private Transform[] _waypoints;
    [SerializeField] private float _keepAwayDistance = 8f;
    [SerializeField] private float _fadeDuration = 1.5f;
    [SerializeField] private float _lookDistance = 7f;
    [SerializeField] private float _attackRange = 5f;
    [SerializeField] private int _emitsBeforeTeleport = 3;
    [SerializeField] private int _boltDamage = 10;
    [SerializeField] private Transform _lightning;
    [SerializeField] private GameObject _bloodBurst;
    [SerializeField] private GameObject _bloodStain;
    [SerializeField] private AudioClip _pinnedVoice;
    [SerializeField] private AudioClip _bossDeathSound;
    [SerializeField] private AudioClip _comeToMe;
    [SerializeField] private PlayableDirector _biteCutscene;
    [SerializeField] private float _biteDistance = 4f;

    static readonly int AttackRightId = Animator.StringToHash("AttackRight");
    static readonly int DeathId = Animator.StringToHash("Death");

    private readonly List<Material> _materials = new List<Material>();
    private readonly List<StuckBlood> _stuckBlood = new List<StuckBlood>();
    private Animator _animator;
    private int _currentWaypoint = -1;
    private bool _isTeleporting;
    private bool _begun;
    private bool _pinned;
    private bool _bitePlayed;
    private bool _dead;
    private bool _attacking;
    private int _emits;
    private float _attackEndsAt;
    private float _nextEmitAt;
    private float _heldUntil;
    private Coroutine _comeToMeRoutine;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_lightning != null)
            _lightning.gameObject.SetActive(false);

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (_lightning != null && renderer.transform.IsChildOf(_lightning))
                continue;

            _materials.AddRange(renderer.materials);
        }
    }

    private void OnEnable()
    {
        Begin();
    }

    public void Begin()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (!enabled)
        {
            enabled = true;
            return;
        }

        if (_begun)
            return;

        _begun = true;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (_lightning != null && renderer.transform.IsChildOf(_lightning))
                continue;

            renderer.enabled = true;
        }

        SetAlpha(1f);

        int next = ChooseWaypoint();
        if (next == -1 && _waypoints != null && _waypoints.Length > 0)
            next = 0;

        if (next != -1)
            Place(next);
    }

    public void Hold(float seconds)
    {
        _heldUntil = Mathf.Max(_heldUntil, Time.time + seconds);
    }

    public bool IsPinned => _pinned;

    public void Pin()
    {
        if (_pinned)
            return;

        _pinned = true;
        _attacking = false;
        _isTeleporting = false;
        StopAllCoroutines();
        StopLightning();
        SetAlpha(1f);
        HoldPosition();
        PlayPinnedVoice();
    }

    void PlayPinnedVoice()
    {
        if (_pinnedVoice == null)
            return;

        GameObject voice = new GameObject("PinnedVoice");
        AudioSource source = voice.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.clip = _pinnedVoice;
        source.volume = 1f;
        source.Play();
        Destroy(voice, _pinnedVoice.length + 0.1f);
    }

    void PlayBossDeathSound()
    {
        if (_bossDeathSound == null)
            return;

        GameObject voice = new GameObject("BossDeathSound");
        AudioSource source = voice.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.clip = _bossDeathSound;
        source.volume = 1f;
        source.Play();
        Destroy(voice, _bossDeathSound.length + 0.1f);
    }

    public void SprayBlood(Vector3 point, Vector3 outward)
    {
        Vector3 spray = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.up;
        if (_bloodBurst != null)
        {
            Camera view = Camera.main;
            Vector3 toCamera = view != null ? view.transform.position - point : -spray;
            if (toCamera.sqrMagnitude < 0.0001f)
                toCamera = Vector3.back;

            Quaternion face = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            Vector3 right = Vector3.ProjectOnPlane(spray, toCamera);
            if (right.sqrMagnitude < 0.0001f)
                right = face * Vector3.right;

            Quaternion rotation = Quaternion.FromToRotation(face * Vector3.right, right.normalized) * face;
            GameObject burst = Instantiate(_bloodBurst, point + spray * 0.06f, rotation);
            burst.transform.localScale = Vector3.one * 0.55f;
            Destroy(burst, 1.2f);
        }

        StickBloodStain(point, spray);
    }

    public bool HitChest(Vector3 point)
    {
        Animator animator = _animator != null ? _animator : GetComponentInChildren<Animator>(true);
        if (animator == null)
            return false;

        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Transform upper = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null && upper == null)
            return false;

        HumanBodyBones[] ids =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head
        };

        Transform closest = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < ids.Length; i++)
        {
            Transform bone = animator.GetBoneTransform(ids[i]);
            if (bone == null)
                continue;

            float distance = (bone.position - point).sqrMagnitude;
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = bone;
        }

        return closest != null && (closest == chest || closest == upper);
    }

    void StickBloodStain(Vector3 point, Vector3 spray)
    {
        GameObject prefab = _bloodStain != null ? _bloodStain : Resources.Load<GameObject>("BloodTattoo");
        if (prefab == null)
            return;

        Camera view = Camera.main;
        Vector3 toCamera = view != null ? view.transform.position - point : -spray;
        if (toCamera.sqrMagnitude < 0.0001f)
            toCamera = Vector3.back;

        Quaternion face = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        Vector3 right = Vector3.ProjectOnPlane(spray, toCamera);
        if (right.sqrMagnitude < 0.0001f)
            right = face * Vector3.right;
        Quaternion rotation = Quaternion.FromToRotation(face * Vector3.right, right.normalized) * face;

        Vector3 bossLeft = Vector3.ProjectOnPlane(-transform.right, toCamera);
        if (bossLeft.sqrMagnitude < 0.0001f)
            bossLeft = -transform.right;

        Transform bone = ClosestSkinnedBone(point);
        GameObject stain = Instantiate(prefab, point + toCamera.normalized * 0.04f + bossLeft.normalized * 0.05f, rotation);
        Animator stainAnimator = stain.GetComponent<Animator>();
        if (stainAnimator != null)
        {
            stainAnimator.applyRootMotion = false;
            stainAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        stain.transform.SetParent(bone, true);
        stain.transform.localScale = Vector3.one * 0.1f;
        _stuckBlood.Add(new StuckBlood
        {
            stain = stain.transform,
            bone = bone,
            localPoint = stain.transform.localPosition,
            localRotation = stain.transform.localRotation,
            localScale = stain.transform.localScale
        });
    }

    void LateUpdate()
    {
        for (int i = _stuckBlood.Count - 1; i >= 0; i--)
        {
            StuckBlood stuck = _stuckBlood[i];
            if (stuck.stain == null)
            {
                _stuckBlood.RemoveAt(i);
                continue;
            }

            if (stuck.bone == null)
                continue;

            if (stuck.stain.parent != stuck.bone)
                stuck.stain.SetParent(stuck.bone, false);

            stuck.stain.localPosition = stuck.localPoint;
            stuck.stain.localRotation = stuck.localRotation;
            stuck.stain.localScale = stuck.localScale;
        }
    }

    Transform ClosestSkinnedBone(Vector3 point)
    {
        Animator animator = _animator != null ? _animator : GetComponentInChildren<Animator>(true);
        Transform best = null;
        float bestDistance = float.PositiveInfinity;
        if (animator != null)
        {
            HumanBodyBones[] ids =
            {
                HumanBodyBones.UpperChest,
                HumanBodyBones.Chest,
                HumanBodyBones.Spine,
                HumanBodyBones.Neck,
                HumanBodyBones.Head,
                HumanBodyBones.Hips
            };

            for (int i = 0; i < ids.Length; i++)
            {
                Transform bone = animator.GetBoneTransform(ids[i]);
                if (bone == null)
                    continue;

                float distance = (bone.position - point).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = bone;
            }
        }

        return best != null ? best : ClosestBodyBone(point);
    }

    struct StuckBlood
    {
        public Transform stain;
        public Transform bone;
        public Vector3 localPoint;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    Transform ClosestBodyBone(Vector3 point)
    {
        Transform best = transform;
        float bestDistance = float.PositiveInfinity;
        Transform[] bones = GetComponentsInChildren<Transform>(false);
        for (int i = 0; i < bones.Length; i++)
        {
            string boneName = bones[i].name;
            if (boneName.IndexOf("Chest", System.StringComparison.OrdinalIgnoreCase) < 0
                && boneName.IndexOf("Spine", System.StringComparison.OrdinalIgnoreCase) < 0
                && boneName.IndexOf("Hips", System.StringComparison.OrdinalIgnoreCase) < 0
                && boneName.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) < 0
                && boneName.IndexOf("Neck", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            float distance = (bones[i].position - point).sqrMagnitude;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = bones[i];
        }

        return best;
    }

    public bool AcceptsPlayerHit()
    {
        if (_dead || !_pinned)
            return false;

        if (!PlayerPresenceManager.HasReference || PlayerPresenceManager.Instance.PlayerManager == null)
            return false;

        PlayerItemsManager items = PlayerPresenceManager.Instance.PlayerManager.PlayerItems;
        return items != null && items.CurrentItem is KnifeItem;
    }

    public void PlayDeath()
    {
        bool ossabiteKill = AcceptsPlayerHit();
        _dead = true;
        _pinned = true;
        _attacking = false;
        _isTeleporting = false;
        StopAllCoroutines();
        StopLightning();
        SetAlpha(1f);
        if (ossabiteKill)
        {
            PlayBossDeathSound();
            if (GameManager.Instance != null)
                GameManager.Instance.ShowHintMessage("Dracula Dead, Mission Complete", 8f);
        }

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
        if (_animator == null)
            return;

        _animator.enabled = true;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animator.applyRootMotion = true;
        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger)
                _animator.ResetTrigger(parameter.nameHash);
        }

        _animator.Play(DeathId, 0, 0f);
        StartCoroutine(HoldOnGround());
    }

    IEnumerator HoldOnGround()
    {
        SkinnedMeshRenderer body = null;
        float largest = 0f;
        foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (_lightning != null && skin.transform.IsChildOf(_lightning))
                continue;

            float size = skin.bounds.size.sqrMagnitude;
            if (size <= largest)
                continue;

            largest = size;
            body = skin;
        }

        float startY = body != null ? body.bounds.center.y : transform.position.y;
        float lowestY = startY;
        float lowestTime = 0.55f;
        bool dropped = false;
        float timeout = 6f;

        while (timeout > 0f)
        {
            timeout -= Time.deltaTime;
            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName("Death") || _animator.IsInTransition(0))
            {
                yield return null;
                continue;
            }

            float y = body != null ? body.bounds.center.y : transform.position.y;
            if (y < lowestY)
            {
                lowestY = y;
                lowestTime = info.normalizedTime;
            }

            if (!dropped && startY - lowestY > 0.35f)
                dropped = true;

            if (dropped && y > lowestY + 0.12f)
                break;

            if (info.normalizedTime >= 0.98f)
                break;

            yield return null;
        }

        _animator.Play(DeathId, 0, Mathf.Clamp(lowestTime, 0.05f, 0.92f));
        _animator.Update(0f);
        _animator.speed = 0f;
    }

    private void Update()
    {
        if (_dead || !_begun || _player == null || Time.time < _heldUntil)
            return;

        float distance = Vector3.Distance(transform.position, _player.position);

        if (_bitePlayed)
            return;

        if (!_pinned && distance <= _biteDistance)
        {
            StartCoroutine(BiteCutsceneRoutine());
            return;
        }

        if (distance <= _lookDistance)
        {
            Vector3 lookPosition = _player.position;
            lookPosition.y = transform.position.y;
            if ((lookPosition - transform.position).sqrMagnitude > 0.001f)
                transform.LookAt(lookPosition);
        }

        if (_isTeleporting)
            return;

        if (_attacking)
        {
            if (!AttackFinished())
                return;

            _attacking = false;
            StopLightning();
            _emits++;
            _nextEmitAt = Time.time + 0.45f;

            if (!_pinned && _emits >= _emitsBeforeTeleport)
            {
                _emits = 0;
                TryTeleport();
            }

            return;
        }

        if (_pinned)
            return;

        if (distance <= _attackRange && Time.time >= _nextEmitAt && !InAttackState())
            StartAttack();
    }

    private void StartAttack()
    {
        _attacking = true;
        _attackEndsAt = Time.time + 2.2f;

        if (_animator != null)
            _animator.SetTrigger(AttackRightId);

        if (_lightning == null)
            return;

        _lightning.gameObject.SetActive(true);
        ParticleSystem[] systems = _lightning.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            systems[i].Clear(true);
            systems[i].Play(true);
        }

        DamagePlayer();
    }

    IEnumerator BiteCutsceneRoutine()
    {
        _bitePlayed = true;
        _attacking = false;
        _isTeleporting = false;
        StopLightning();
        Hold(30f);

        if (_biteCutscene == null)
        {
            GameObject cutscene = GameObject.Find("BiteCutSceneBoss");
            if (cutscene != null)
                _biteCutscene = cutscene.GetComponent<PlayableDirector>();
        }

        GameManager game = GameManager.Instance;
        if (game != null)
            game.FreezePlayer(true, false);

        if (_biteCutscene != null)
        {
            _biteCutscene.time = 0d;
            _biteCutscene.Play();
            yield return null;

            double length = _biteCutscene.duration;
            float timeout = Time.time + (float)length + 1f;
            while (Time.time < timeout
                && _biteCutscene.state == PlayState.Playing
                && _biteCutscene.time < length - 0.05d)
                yield return null;

            if (_biteCutscene.state == PlayState.Playing)
                _biteCutscene.Stop();
        }

        ShowGameOver();

        PlayerHealth health = _player.GetComponentInChildren<PlayerHealth>();
        if (health != null && !health.IsDead)
            health.ApplyDamageMax(transform);
    }

    void ShowGameOver()
    {
        GameManager game = GameManager.Instance;
        if (game == null || game.DeadPanel == null)
            return;

        TMP_Text[] labels = game.DeadPanel.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i].text.IndexOf("died", System.StringComparison.OrdinalIgnoreCase) >= 0)
                labels[i].text = "Gameover";
        }
    }

    private void DamagePlayer()
    {
        Vector3 origin = _lightning != null ? _lightning.position : transform.position + Vector3.up * 1.4f;
        Vector3 target = _player.position + Vector3.up * 1.1f;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance < 0.05f)
            return;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction.normalized,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            PlayerHealth health = hits[i].collider.GetComponentInParent<PlayerHealth>();
            if (health != null && !health.IsDead)
                health.ApplyDamage(_boltDamage, transform);

            return;
        }

        PlayerHealth direct = _player.GetComponentInChildren<PlayerHealth>();
        if (direct != null && !direct.IsDead)
            direct.ApplyDamage(_boltDamage, transform);
    }

    private bool AttackFinished()
    {
        if (Time.time >= _attackEndsAt)
            return true;

        if (_animator == null || _animator.IsInTransition(0))
            return false;

        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
        return info.IsName("AttackRight") && info.normalizedTime >= 0.92f;
    }

    private bool InAttackState()
    {
        if (_animator == null)
            return false;

        if (_animator.IsInTransition(0))
        {
            AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(0);
            if (next.IsName("AttackRight"))
                return true;
        }

        return _animator.GetCurrentAnimatorStateInfo(0).IsName("AttackRight");
    }

    private void StopLightning()
    {
        if (_lightning == null)
            return;

        ParticleSystem[] systems = _lightning.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
            systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        _lightning.gameObject.SetActive(false);
    }

    private void TryTeleport()
    {
        if (_pinned)
            return;

        if (_comeToMeRoutine != null)
        {
            StopCoroutine(_comeToMeRoutine);
            _comeToMeRoutine = null;
        }

        int nextWaypoint = ChooseWaypoint();
        if (nextWaypoint != -1)
            StartCoroutine(TeleportRoutine(nextWaypoint));
    }

    private int ChooseWaypoint()
    {
        int chosen = -1;
        int validCount = 0;

        if (_waypoints == null)
            return -1;

        for (int i = 0; i < _waypoints.Length; i++)
        {
            if (_waypoints[i] == null || i == _currentWaypoint)
                continue;

            if (_player != null)
            {
                float distance = Vector3.Distance(_waypoints[i].position, _player.position);
                if (distance < _keepAwayDistance)
                    continue;
            }

            validCount++;
            if (Random.Range(0, validCount) == 0)
                chosen = i;
        }

        return chosen;
    }

    private IEnumerator TeleportRoutine(int nextWaypoint)
    {
        _isTeleporting = true;

        yield return FadeRoutine(1f, 0f);

        if (_waypoints != null
            && nextWaypoint >= 0
            && nextWaypoint < _waypoints.Length
            && _waypoints[nextWaypoint] != null
            && _player != null
            && Vector3.Distance(_waypoints[nextWaypoint].position, _player.position) < _keepAwayDistance)
        {
            nextWaypoint = ChooseWaypoint();
        }

        if (_bitePlayed)
        {
            _isTeleporting = false;
            yield break;
        }

        if (nextWaypoint != -1)
            Place(nextWaypoint);

        yield return FadeRoutine(0f, 1f);

        _isTeleporting = false;
        if (_comeToMeRoutine != null)
            StopCoroutine(_comeToMeRoutine);
        _comeToMeRoutine = StartCoroutine(ComeToMeAfterTransport());
    }

    IEnumerator ComeToMeAfterTransport()
    {
        yield return new WaitForSeconds(3f);
        _comeToMeRoutine = null;
        if (_pinned || _dead || _bitePlayed || _comeToMe == null)
            yield break;

        GameObject voice = new GameObject("ComeToMe");
        AudioSource source = voice.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.clip = _comeToMe;
        source.volume = 1f;
        source.Play();
        Destroy(voice, _comeToMe.length + 0.1f);
    }

    private void Place(int waypoint)
    {
        Transform point = _waypoints[waypoint];
        if (TryGetComponent(out EnemyFlameBurn burn))
            burn.Extinguish();

        transform.SetPositionAndRotation(point.position, point.rotation);
        _currentWaypoint = waypoint;
        _emits = 0;
        HoldPosition();
    }

    private void HoldPosition()
    {
        if (!TryGetComponent(out NavMeshAgent agent) || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.ResetPath();
        agent.isStopped = true;
        agent.Warp(transform.position);
    }

    private IEnumerator FadeRoutine(float from, float to)
    {
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, elapsed / _fadeDuration));
            yield return null;
        }

        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
    {
        foreach (Material material in _materials)
        {
            if (material == null)
                continue;

            if (material.HasProperty("_BaseColor"))
            {
                Color baseColor = material.GetColor("_BaseColor");
                baseColor.a = alpha;
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_Color"))
            {
                Color color = material.color;
                color.a = alpha;
                material.color = color;
            }
        }
    }
}
