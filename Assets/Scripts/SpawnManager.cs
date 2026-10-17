using System.Collections;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [SerializeField] Transform _spawnPoint;
    [SerializeField] float _spawnInterval = 10f;
    [Tooltip("How many zombies appear in this level. Dead ones are not replaced.")]
    [SerializeField] int _maxEnemies = 10;
    [Tooltip("Added on top of the zombies, the ghost girl, and the church ghosts. Use this when you place an enemy the counter does not see.")]
    [SerializeField] int _additionalEnemies;

    EnemyPool _enemyPool;
    int _spawned;
    int _nextPoint;
    float _failedSpawnTime;

    void Start()
    {
        _enemyPool = GetComponent<EnemyPool>();
        if (_enemyPool == null)
            _enemyPool = FindAnyObjectByType<EnemyPool>();
        if (_enemyPool == null)
        {
            Debug.LogError("SpawnManager could not find an EnemyPool in the scene.", this);
            return;
        }

        StartCoroutine(SpawnZombie());
    }

    bool TrySpawnNext()
    {
        if (_spawnPoint == null || _enemyPool == null)
            return false;

        int count = _spawnPoint.childCount;
        if (count == 0)
            return _enemyPool.TrySpawn(_spawnPoint.position, _spawnPoint.rotation, out _);

        for (int i = 0; i < count; i++)
        {
            Transform point = _spawnPoint.GetChild((_nextPoint + i) % count);
            if (!_enemyPool.TrySpawn(point.position, point.rotation, out _))
                continue;

            _nextPoint = (_nextPoint + i + 1) % count;
            return true;
        }

        return false;
    }

    IEnumerator SpawnZombie()
    {
        while (_spawned < _maxEnemies)
        {
            if (TrySpawnNext())
            {
                _spawned++;
                _failedSpawnTime = 0f;
                if (_spawned >= _maxEnemies)
                    yield break;

                yield return new WaitForSeconds(_spawnInterval);
            }
            else
            {
                _failedSpawnTime += 1f;
                if (_failedSpawnTime >= 20f)
                {
                    _spawned++;
                    _failedSpawnTime = 0f;
                    Debug.Log("Spawn point has no navmesh. Skipping that enemy so the level can finish.", this);
                }

                yield return new WaitForSeconds(1f);
            }
        }
    }

    public int RemainingToSpawn => Mathf.Max(0, _maxEnemies - _spawned);
    public int AdditionalEnemies => Mathf.Max(0, _additionalEnemies);

    public void ReturnZombie(GameObject enemy)
    {
        _enemyPool.ReturnEnemy(enemy);
    }
}
