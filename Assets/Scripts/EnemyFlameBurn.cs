using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UHFPS.Runtime;

/// <summary>
/// Body fire appears 2 seconds after the first flamethrower hit.
/// If that enemy dies, a ground fire circle appears 1 second later.
/// </summary>
public class EnemyFlameBurn : MonoBehaviour
{
    const float BodyFireDelay = 2f;
    const float GroundFireDelay = 1f;
    const float GroundFireLifetime = 8f;

    NPCHealth _health;
    GameObject _bodyFirePrefab;
    GameObject _groundFirePrefab;
    GameObject _bodyFire;
    bool _ignited;
    bool _deathStarted;
    Vector3 _deathPosition;
    bool _hasDeathPosition;
    Coroutine _bodyRoutine;
    Coroutine _groundRoutine;

    void Awake()
    {
        _health = GetComponent<NPCHealth>();
    }

    void OnEnable()
    {
        if (_health == null)
            _health = GetComponent<NPCHealth>();
        if (_health != null)
            _health.OnDeath.AddListener(OnDied);
    }

    void OnDisable()
    {
        if (_health != null)
            _health.OnDeath.RemoveListener(OnDied);

        if (_bodyRoutine != null)
            StopCoroutine(_bodyRoutine);
        if (_groundRoutine != null)
            StopCoroutine(_groundRoutine);

        _bodyRoutine = null;
        _groundRoutine = null;

        if (_bodyFire != null)
            Destroy(_bodyFire);

        _bodyFire = null;
        _ignited = false;
        _deathStarted = false;
    }

    public void Ignite(GameObject bodyFirePrefab, GameObject groundFirePrefab)
    {
        if (bodyFirePrefab != null)
            _bodyFirePrefab = bodyFirePrefab;
        if (groundFirePrefab != null)
            _groundFirePrefab = groundFirePrefab;

        bool dead = _health != null && _health.IsDead;

        if (!_ignited)
        {
            _ignited = true;
            _bodyRoutine = StartCoroutine(ShowBodyFire());
        }

        if (dead)
            BeginDeathFire();
    }

    void OnDied()
    {
        BeginDeathFire();
    }

    void BeginDeathFire()
    {
        if (!_ignited || _deathStarted)
            return;

        _deathStarted = true;
        _deathPosition = transform.position;
        _hasDeathPosition = true;
        _groundRoutine = StartCoroutine(ShowGroundFire());
    }

    Transform FireAnchor()
    {
        Transform anchor = _health != null && _health.Hips != null ? _health.Hips : transform;
        if (_hasDeathPosition && anchor.position.y > _deathPosition.y + 2f)
            return transform;

        return anchor;
    }

    IEnumerator ShowBodyFire()
    {
        yield return new WaitForSeconds(BodyFireDelay);
        _bodyRoutine = null;

        if (_bodyFirePrefab == null || _bodyFire != null)
            yield break;

        Transform anchor = FireAnchor();
        _bodyFire = Instantiate(_bodyFirePrefab, anchor);
        _bodyFire.transform.localPosition = Vector3.zero;
        _bodyFire.transform.localRotation = Quaternion.identity;
    }

    IEnumerator ShowGroundFire()
    {
        yield return new WaitForSeconds(GroundFireDelay);
        _groundRoutine = null;

        if (_groundFirePrefab == null)
            yield break;

        Vector3 position = GroundPoint(FireAnchor().position);
        Quaternion rotation = _groundFirePrefab.transform.rotation;
        GameObject circle = Instantiate(_groundFirePrefab, position, rotation);
        Destroy(circle, GroundFireLifetime);
    }

    Vector3 GroundPoint(Vector3 from)
    {
        Vector3 origin = from + Vector3.up * 0.5f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        Vector3 point = from;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform))
                continue;
            if (hits[i].distance >= nearest)
                continue;

            nearest = hits[i].distance;
            point = hits[i].point;
            found = true;
        }

        if (found)
            return point + Vector3.up * 0.02f;

        if (NavMesh.SamplePosition(from, out NavMeshHit nav, 3f, NavMesh.AllAreas))
            return nav.position;

        return from;
    }
}
