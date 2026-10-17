using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyPool : MonoBehaviour
{
    [SerializeField] GameObject[] enemyPrefabs;
    [SerializeField] int poolSize = 20;

    readonly Queue<GameObject> _available = new();
    readonly Dictionary<GameObject, GameObject> _sourcePrefab = new();
    Transform _spawnedRoot;
    int _activeCount;

    void Awake()
    {
        EnsureSpawnedRoot();
        KeepWerewolvesInLevel3();
    }

    void KeepWerewolvesInLevel3()
    {
        if (GraveyardLevels.LevelNumber(gameObject.scene.name) != 3)
            return;

        var werewolves = new List<GameObject>();
        if (enemyPrefabs != null)
        {
            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                GameObject prefab = enemyPrefabs[i];
                if (prefab != null && prefab.name.Contains("Werewolf"))
                    werewolves.Add(prefab);
            }
        }

        if (werewolves.Count == 0)
        {
            Vampire[] bodies = FindObjectsByType<Vampire>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null && bodies[i].name.Contains("Werewolf"))
                {
                    werewolves.Add(bodies[i].gameObject);
                    break;
                }
            }
        }

        if (werewolves.Count > 0)
            enemyPrefabs = werewolves.ToArray();

        Vampire[] placed = FindObjectsByType<Vampire>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < placed.Length; i++)
        {
            if (placed[i] != null && placed[i].name == "Vampire")
                Destroy(placed[i].gameObject);
        }
    }

    void EnsureSpawnedRoot()
    {
        if (_spawnedRoot != null)
            return;

        var container = new GameObject("Spawned Zombies");
        container.transform.SetParent(transform, false);
        _spawnedRoot = container.transform;
    }

    public int EnemyLength => _activeCount;
    public int MaxEnemies => poolSize;

    public bool TrySpawn(Vector3 position, Quaternion rotation, out GameObject enemy)
    {
        enemy = null;
        if (enemyPrefabs == null || enemyPrefabs.Length == 0 || _activeCount >= poolSize)
            return false;

        if (!TryFindNavPoint(position, out Vector3 navPoint))
        {
            Debug.LogWarning("Enemy spawn point is not close to the NavMesh.", this);
            return false;
        }

        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Quaternion facing = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        enemy = TakeOrCreate(prefab, navPoint, facing);
        EnsureSpawnedRoot();
        enemy.transform.SetParent(_spawnedRoot, true);
        enemy.transform.SetPositionAndRotation(navPoint, facing);

        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.enabled = false;

        enemy.SetActive(true);

        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(navPoint);
            agent.isStopped = false;
        }

        var brain = enemy.GetComponent<Vampire>();
        if (brain != null)
            brain.enabled = true;

        Physics.IgnoreLayerCollision(9, 9, true);
        if (enemy.GetComponent<EnemyNavUnstuck>() == null)
            enemy.AddComponent<EnemyNavUnstuck>();

        Animator animator = enemy.GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Update(0f);
        }

        EnemyAppearanceLight.Apply(enemy);

        _activeCount++;
        return true;
    }

    public void ReturnEnemy(GameObject enemy)
    {
        if (enemy == null || !enemy.activeSelf)
            return;

        enemy.SetActive(false);
        EnsureSpawnedRoot();
        enemy.transform.SetParent(_spawnedRoot, false);
        _available.Enqueue(enemy);
        _activeCount--;
    }

    GameObject TakeOrCreate(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        int waiting = _available.Count;
        for (int i = 0; i < waiting; i++)
        {
            GameObject candidate = _available.Dequeue();
            if (_sourcePrefab.TryGetValue(candidate, out GameObject source) && source == prefab)
                return candidate;

            _available.Enqueue(candidate);
        }

        GameObject created = Instantiate(prefab, position, rotation);
        created.SetActive(false);
        _sourcePrefab[created] = prefab;
        return created;
    }

    static bool TryFindNavPoint(Vector3 origin, out Vector3 point)
    {
        if (!NavMesh.SamplePosition(origin, out NavMeshHit center, 6f, NavMesh.AllAreas))
        {
            point = origin;
            return false;
        }

        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = Random.insideUnitCircle * 1.5f;
            Vector3 candidate = center.position + new Vector3(offset.x, 0f, offset.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }
        }

        point = center.position;
        return true;
    }
}

public class EnemyNavUnstuck : MonoBehaviour
{
    NavMeshAgent _agent;
    Transform _player;
    Vector3 _lastPosition;
    float _stuckTime;
    float _lostTime;
    float _pathCheckTime;
    bool _pathBlocked;

    void OnEnable()
    {
        _agent = GetComponent<NavMeshAgent>();
        _lastPosition = transform.position;
        _stuckTime = 0f;
        _lostTime = 0f;
        _pathCheckTime = 0f;

        if (_agent == null)
            return;

        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        _agent.avoidancePriority = Random.Range(20, 80);
        _agent.radius = Mathf.Min(_agent.radius, 0.32f);
    }

    void Update()
    {
        if (_agent == null || !_agent.enabled)
            return;

        UHFPS.Runtime.NPCHealth health = GetComponent<UHFPS.Runtime.NPCHealth>();
        if (health != null && health.IsDead)
            return;

        if (IsLost(out bool offMesh))
        {
            _lostTime += Time.deltaTime;
            float wait = offMesh ? 3f : 8f;
            if (_lostTime >= wait && RecallNearPlayer())
                _lostTime = 0f;
            return;
        }

        _lostTime = 0f;
        NudgeIfStuck();
    }

    bool IsLost(out bool offMesh)
    {
        offMesh = !_agent.isOnNavMesh
            || !NavMesh.SamplePosition(transform.position, out NavMeshHit ground, 3f, NavMesh.AllAreas)
            || transform.position.y < ground.position.y - 2f;

        if (offMesh)
            return true;

        Transform player = Player();
        if (player == null)
            return false;

        _pathCheckTime += Time.deltaTime;
        if (_pathCheckTime < 1f)
            return _pathBlocked;

        _pathCheckTime = 0f;
        NavMeshPath path = new NavMeshPath();
        _pathBlocked = !_agent.CalculatePath(player.position, path) || path.status != NavMeshPathStatus.PathComplete;
        return _pathBlocked;
    }

    void NudgeIfStuck()
    {
        if (!_agent.isOnNavMesh || _agent.isStopped || !_agent.hasPath || _agent.pathPending)
            return;

        if (_agent.remainingDistance <= _agent.stoppingDistance + 0.5f)
        {
            _stuckTime = 0f;
            _lastPosition = transform.position;
            return;
        }

        if ((transform.position - _lastPosition).sqrMagnitude > 0.05f)
        {
            _stuckTime = 0f;
            _lastPosition = transform.position;
            return;
        }

        _stuckTime += Time.deltaTime;
        if (_stuckTime < 1.5f)
            return;

        _stuckTime = 0f;
        Vector3 destination = _agent.destination;
        Vector3 forward = destination - transform.position;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
            forward = transform.forward;

        Vector3 side = Vector3.Cross(Vector3.up, forward.normalized);
        Vector3 nudge = transform.position + side * Random.Range(-1.6f, 1.6f);
        if (NavMesh.SamplePosition(nudge, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            _agent.Warp(hit.position);

        _agent.ResetPath();
        _agent.SetDestination(destination);
        _lastPosition = transform.position;
    }

    bool RecallNearPlayer()
    {
        Transform player = Player();
        if (player == null)
            return false;

        for (int i = 0; i < 8; i++)
        {
            Vector3 around = player.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * (Vector3.forward * Random.Range(8f, 14f));
            if (!NavMesh.SamplePosition(around, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                continue;

            if (!_agent.isOnNavMesh)
            {
                _agent.Warp(hit.position);
            }
            else
            {
                _agent.Warp(hit.position);
            }

            _agent.ResetPath();
            _agent.isStopped = false;
            _lastPosition = transform.position;
            return true;
        }

        return false;
    }

    Transform Player()
    {
        if (_player == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _player = player.transform;
        }

        return _player;
    }
}
