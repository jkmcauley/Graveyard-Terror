using System.Collections.Generic;
using UHFPS.Runtime;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// On every level, an enemy the player cannot reach is brought back.
/// If that keeps failing, the enemy is cleared so the level can finish.
/// </summary>
public class EnemyReachWatch : MonoBehaviour
{
    const float CheckInterval = 1f;
    const float RecallAfter = 8f;
    const int FailedRecalls = 4;
    const float GroundReach = 20f;

    class Reach
    {
        public float Unreachable;
        public int Failures;
    }

    readonly Dictionary<int, Reach> _reach = new();
    readonly List<NPCHealth> _living = new();
    readonly List<int> _drop = new();
    float _nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var go = new GameObject(nameof(EnemyReachWatch));
        DontDestroyOnLoad(go);
        go.AddComponent<EnemyReachWatch>();
    }

    void Update()
    {
        if (!GraveyardLevels.IsPlayableScene(SceneManager.GetActiveScene().name))
            return;

        if (Time.time < _nextCheck)
            return;

        _nextCheck = Time.time + CheckInterval;
        Check();
    }

    void Check()
    {
        Transform player = Player();
        _living.Clear();
        EnemyCounterHud.CollectLiving(_living);

        var seen = new HashSet<int>();
        for (int i = 0; i < _living.Count; i++)
        {
            NPCHealth health = _living[i];
            if (health == null)
                continue;

            int id = health.gameObject.GetInstanceID();
            seen.Add(id);
            if (player == null)
                continue;

            if (CanReach(health, player.position))
            {
                _reach.Remove(id);
                continue;
            }

            if (!_reach.TryGetValue(id, out Reach reach))
            {
                reach = new Reach();
                _reach[id] = reach;
            }

            reach.Unreachable += CheckInterval;
            if (reach.Unreachable < RecallAfter)
                continue;

            reach.Unreachable = 0f;
            bool broughtBack = health.gameObject.activeInHierarchy && TryBringNearPlayer(health, player.position);
            if (broughtBack && CanReach(health, player.position))
            {
                reach.Failures = 0;
                continue;
            }

            reach.Failures++;
            if (reach.Failures < FailedRecalls)
                continue;

            Debug.Log(health.name + " was out of reach. Clearing that enemy so the level can finish.", health);
            health.ApplyDamageMax(player);
            _reach.Remove(id);
        }

        _drop.Clear();
        foreach (int id in _reach.Keys)
        {
            if (!seen.Contains(id))
                _drop.Add(id);
        }

        for (int i = 0; i < _drop.Count; i++)
            _reach.Remove(_drop[i]);
    }

    static bool CanReach(NPCHealth health, Vector3 playerPos)
    {
        if (!health.gameObject.activeInHierarchy || health.name == "CathedralAmbush")
            return true;

        if (!NavMesh.SamplePosition(health.transform.position, out NavMeshHit enemyHit, GroundReach, NavMesh.AllAreas))
            return false;

        if (!NavMesh.SamplePosition(playerPos, out NavMeshHit playerHit, 6f, NavMesh.AllAreas))
            return true;

        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(enemyHit.position, playerHit.position, NavMesh.AllAreas, path))
            return false;

        if (path.status == NavMeshPathStatus.PathComplete)
            return true;

        if (path.status == NavMeshPathStatus.PathPartial && path.corners != null && path.corners.Length > 0)
            return (path.corners[path.corners.Length - 1] - playerHit.position).sqrMagnitude <= 36f;

        return false;
    }

    static bool TryBringNearPlayer(NPCHealth health, Vector3 playerPos)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 around = playerPos + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * (Vector3.forward * Random.Range(8f, 14f));
            if (!NavMesh.SamplePosition(around, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                continue;

            NavMeshAgent agent = health.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                if (!agent.enabled)
                    agent.enabled = true;

                agent.Warp(hit.position);
                agent.isStopped = false;
            }
            else
            {
                health.transform.position = hit.position;
            }

            return true;
        }

        return false;
    }

    static Transform Player()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }
}
