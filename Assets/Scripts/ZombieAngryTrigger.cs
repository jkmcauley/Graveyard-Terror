using System.Collections;
using System.Collections.Generic;
using UHFPS.Runtime;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BoxCollider))]
public class ZombieAngryTrigger : MonoBehaviour
{
    [SerializeField] GameObject zombie;
    [SerializeField] GameObject otherZombie;
    [SerializeField] AudioClip growl;
    [SerializeField] float chaseSpeed = 2.5f;
    [SerializeField] int attackDamage = 15;
    [SerializeField] float attackDistance = 1.4f;
    [SerializeField] float attackCooldown = 1.9f;
    [SerializeField] float windup = 0.4f;
    [SerializeField] float resetDelay = 5f;
    [SerializeField] float keepApart = 2.25f;
    [SerializeField] float apartBlendDistance = 8f;

    public int LivingZombieCount => Living(zombie) + (otherZombie != zombie ? Living(otherZombie) : 0);
    public GameObject PrimaryBody => zombie;
    public GameObject SecondaryBody => otherZombie;

    public void CollectZombies(HashSet<int> ids)
    {
        if (zombie != null)
            ids.Add(zombie.GetInstanceID());
        if (otherZombie != null)
            ids.Add(otherZombie.GetInstanceID());
    }

    static int Living(GameObject target)
    {
        if (target == null)
            return 0;

        NPCHealth health = target.GetComponent<NPCHealth>();
        return health != null && health.IsDead ? 0 : 1;
    }

    bool fired;
    int playerInside;
    Transform player;
    BoxCollider box;
    Chaser[] chasers;
    Coroutine resetRoutine;

    class Chaser
    {
        public GameObject zombie;
        public Animator animator;
        public Rigidbody body;
        public AudioSource growl;
        public Coroutine routine;
        public Vector3 homePosition;
        public Quaternion homeRotation;
        public int homeState;
        public bool chasing;
        public bool attacking;
        public float nextAttackTime;
    }

    void Reset()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
    }

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        HideStandIns();

        CathedralZombieSet set = Resources.Load<CathedralZombieSet>("CathedralZombies");
        zombie = Spawn(set != null ? set.first : null, -0.9f);
        otherZombie = Spawn(set != null ? set.second : null, 0.9f);
    }

    void HideStandIns()
    {
        HideNamed("CathedralZombie");
        HideNamed("CathedralZombie2");
        if (zombie != null)
            zombie.SetActive(false);
        if (otherZombie != null && otherZombie != zombie)
            otherZombie.SetActive(false);
    }

    static void HideNamed(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        while (found != null)
        {
            found.SetActive(false);
            found = GameObject.Find(objectName);
        }
    }

    GameObject Spawn(GameObject prefab, float side)
    {
        if (prefab == null)
            return null;

        Vector3 pos = transform.TransformPoint(new Vector3(side, -0.4f, 0.6f));
        Quaternion look = transform.rotation;
        GameObject body = Instantiate(prefab, pos, look);
        body.name = "CathedralAmbush";

        Vampire brain = body.GetComponent<Vampire>();
        if (brain != null)
            brain.enabled = false;

        EnemyNavUnstuck unstuck = body.GetComponent<EnemyNavUnstuck>();
        if (unstuck != null)
            unstuck.enabled = false;

        NavMeshAgent agent = body.GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.enabled = false;

        NPCStateMachine machine = body.GetComponent<NPCStateMachine>();
        if (machine != null)
            machine.enabled = false;

        body.transform.SetParent(transform, true);
        Place(body, pos, look);
        return body;
    }

    void Place(GameObject target, Vector3 pos, Quaternion look)
    {
        Prepare(target);
        if (target == null)
            return;

        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.enabled = false;

        NPCStateMachine machine = target.GetComponent<NPCStateMachine>();
        if (machine != null)
            machine.enabled = false;

        target.transform.SetPositionAndRotation(pos, look);
        Rigidbody body = target.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = pos;
            body.rotation = look;
        }
    }

    static void Prepare(GameObject target)
    {
        if (target == null)
            return;

        Animator animator = target.GetComponent<Animator>();
        if (animator != null)
            animator.applyRootMotion = false;

        foreach (SkinnedMeshRenderer skin in target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skin.updateWhenOffscreen = true;

        Rigidbody body = target.GetComponent<Rigidbody>();
        if (body == null)
            body = target.AddComponent<Rigidbody>();

        body.useGravity = false;
        body.isKinematic = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        playerInside++;
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }

        if (fired)
            return;

        fired = true;
        player = other.transform.root;
        chasers = new Chaser[] { MakeChaser(zombie), MakeChaser(otherZombie) };
        for (int i = 0; i < chasers.Length; i++)
        {
            if (chasers[i] != null)
                chasers[i].routine = StartCoroutine(Chase(chasers[i]));
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        playerInside = Mathf.Max(0, playerInside - 1);
        if (playerInside > 0 || resetRoutine != null)
            return;

        resetRoutine = StartCoroutine(ResetAfterDelay());
    }

    IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(resetDelay);
        resetRoutine = null;
        if (playerInside > 0)
            yield break;

        ResetEncounter();
    }

    void ResetEncounter()
    {
        if (chasers != null)
        {
            for (int i = 0; i < chasers.Length; i++)
                StopChaser(chasers[i]);
        }

        chasers = null;
        player = null;
        fired = false;
        resetRoutine = null;
    }

    void StopChaser(Chaser chaser)
    {
        if (chaser == null)
            return;

        if (chaser.routine != null)
        {
            StopCoroutine(chaser.routine);
            chaser.routine = null;
        }

        chaser.chasing = false;
        chaser.attacking = false;
        SetGrowl(chaser, false);

        NPCHealth health = chaser.zombie != null ? chaser.zombie.GetComponent<NPCHealth>() : null;
        if (health != null && health.IsDead)
            return;

        if (chaser.animator != null)
        {
            chaser.animator.ResetTrigger("Attack");
            SetIdle(chaser.animator);
            chaser.animator.Play(chaser.homeState, 0, 0f);
            chaser.animator.Update(0f);
        }

        if (chaser.zombie == null)
            return;

        if (chaser.body != null)
        {
            chaser.body.position = chaser.homePosition;
            chaser.body.rotation = chaser.homeRotation;
        }
        else
        {
            chaser.zombie.transform.SetPositionAndRotation(chaser.homePosition, chaser.homeRotation);
        }
    }

    Chaser MakeChaser(GameObject target)
    {
        if (target == null)
            return null;

        NPCHealth health = target.GetComponent<NPCHealth>();
        if (health != null && health.IsDead)
            return null;

        Animator animator = target.GetComponent<Animator>();
        if (animator == null)
            return null;

        Prepare(target);
        return new Chaser
        {
            zombie = target,
            animator = animator,
            body = target.GetComponent<Rigidbody>(),
            homePosition = target.transform.position,
            homeRotation = target.transform.rotation,
            homeState = animator.GetCurrentAnimatorStateInfo(0).fullPathHash
        };
    }

    IEnumerator Chase(Chaser chaser)
    {
        SetIdle(chaser.animator);
        if (windup > 0f)
            yield return new WaitForSeconds(windup);

        if (!fired || player == null)
            yield break;

        chaser.chasing = true;
        SetRun(chaser.animator);

        while (player != null && fired)
        {
            NPCHealth health = chaser.zombie.GetComponent<NPCHealth>();
            if (health != null && health.IsDead)
            {
                chaser.chasing = false;
                chaser.attacking = false;
                SetGrowl(chaser, false);
                yield break;
            }

            float distance = FlatDistance(chaser.zombie.transform.position, player.position);
            bool inRange = playerInside > 0 && distance <= attackDistance;
            bool swinging = IsSwinging(chaser.animator);
            if (inRange || swinging)
            {
                SetIdle(chaser.animator);
                chaser.attacking = true;
                if (inRange && !swinging && Time.time >= chaser.nextAttackTime)
                {
                    if (HasParameter(chaser.animator, "Attack", AnimatorControllerParameterType.Trigger))
                        chaser.animator.SetTrigger("Attack");
                    chaser.nextAttackTime = Time.time + attackCooldown;
                    PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                    if (playerHealth != null && !playerHealth.IsDead)
                        playerHealth.ApplyDamage(attackDamage, chaser.zombie.transform);
                }
            }
            else
            {
                chaser.attacking = false;
                chaser.animator.ResetTrigger("Attack");
                SetRun(chaser.animator);
            }

            SetGrowl(chaser, chaser.chasing);
            yield return null;
        }
    }

    void FixedUpdate()
    {
        if (playerInside <= 0 || chasers == null || player == null)
            return;

        for (int i = 0; i < chasers.Length; i++)
        {
            Chaser chaser = chasers[i];
            if (chaser == null || !chaser.chasing)
                continue;

            NPCHealth health = chaser.zombie.GetComponent<NPCHealth>();
            if (health != null && health.IsDead)
            {
                chaser.chasing = false;
                SetGrowl(chaser, false);
                continue;
            }

            Transform zombieTransform = chaser.zombie.transform;
            Vector3 flat = player.position - zombieTransform.position;
            flat.y = 0f;
            Quaternion look = zombieTransform.rotation;
            if (flat.sqrMagnitude > 0.001f)
                look = Quaternion.LookRotation(flat.normalized);

            Vector3 next = zombieTransform.position;
            if (!chaser.attacking)
            {
                Vector3 goal = FlankGoal(i, zombieTransform.position);
                goal.y = chaser.homePosition.y;
                if ((goal - zombieTransform.position).sqrMagnitude > 0.04f)
                    next = Vector3.MoveTowards(zombieTransform.position, goal, chaseSpeed * Time.fixedDeltaTime);
                next.y = chaser.homePosition.y;
                next = ClampToBox(next);
                next.y = chaser.homePosition.y;
            }

            if (chaser.body != null)
            {
                chaser.body.position = next;
                chaser.body.rotation = look;
            }
            else
            {
                zombieTransform.SetPositionAndRotation(next, look);
            }
        }
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static void SetIdle(Animator animator)
    {
        SetBool(animator, "Walk", false);
        SetBool(animator, "Patrol", false);
        SetBool(animator, "Run", false);
        SetBool(animator, "Idle", true);
    }

    static void SetRun(Animator animator)
    {
        SetBool(animator, "Idle", false);
        SetBool(animator, "Walk", false);
        SetBool(animator, "Patrol", false);
        SetBool(animator, "Run", true);
    }

    static void SetBool(Animator animator, string parameter, bool value)
    {
        if (!HasParameter(animator, parameter, AnimatorControllerParameterType.Bool))
            return;

        animator.SetBool(parameter, value);
    }

    static bool HasParameter(Animator animator, string parameter, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        foreach (AnimatorControllerParameter item in animator.parameters)
        {
            if (item.name == parameter && item.type == type)
                return true;
        }

        return false;
    }

    static bool IsSwinging(Animator animator)
    {
        if (animator.IsInTransition(1))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(1);
            if (next.IsName("Attack 01"))
                return true;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(1);
        return state.IsName("Attack 01") && state.normalizedTime < 0.95f;
    }

    Vector3 FlankGoal(int index, Vector3 from)
    {
        Vector3 goal = player.position;
        goal.y = from.y;
        if (chasers == null || chasers.Length < 2 || chasers[0] == null || chasers[1] == null)
            return goal;

        float nearest = float.MaxValue;
        for (int i = 0; i < chasers.Length; i++)
        {
            if (chasers[i] == null || !chasers[i].chasing)
                continue;

            float distance = FlatDistance(chasers[i].zombie.transform.position, player.position);
            if (distance < nearest)
                nearest = distance;
        }

        if (nearest == float.MaxValue)
            nearest = FlatDistance(from, player.position);

        float close = 1f - Mathf.Clamp01(nearest / Mathf.Max(0.01f, apartBlendDistance));
        float spread = keepApart * 0.5f * close;
        Vector3 side = FlankAxis();
        float sign = index % 2 == 0 ? -1f : 1f;
        goal += side * (spread * sign);
        goal += Separation(from, index) * close;
        goal.y = from.y;
        return goal;
    }

    Vector3 FlankAxis()
    {
        Vector3 mid = Vector3.zero;
        int count = 0;
        for (int i = 0; i < chasers.Length; i++)
        {
            if (chasers[i] == null || chasers[i].zombie == null)
                continue;

            mid += chasers[i].zombie.transform.position;
            count++;
        }

        if (count > 0)
            mid /= count;

        Vector3 toPlayer = player.position - mid;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.04f)
            return Vector3.Cross(Vector3.up, toPlayer.normalized);

        Vector3 right = player.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.001f)
            return Vector3.right;

        return right.normalized;
    }

    Vector3 Separation(Vector3 position, int index)
    {
        Vector3 push = Vector3.zero;
        for (int i = 0; i < chasers.Length; i++)
        {
            if (i == index || chasers[i] == null || chasers[i].zombie == null)
                continue;

            Vector3 away = position - chasers[i].zombie.transform.position;
            away.y = 0f;
            float distance = away.magnitude;
            if (distance >= keepApart)
                continue;

            if (distance < 0.001f)
                away = index % 2 == 0 ? Vector3.left : Vector3.right;
            else
                away /= distance;

            push += away * (keepApart - distance);
        }

        return push;
    }

    void SetGrowl(Chaser chaser, bool play)
    {
        if (growl == null)
            return;

        if (chaser.growl == null)
        {
            chaser.growl = chaser.zombie.AddComponent<AudioSource>();
            chaser.growl.clip = growl;
            chaser.growl.loop = true;
            chaser.growl.spatialBlend = 1f;
            chaser.growl.playOnAwake = false;
            chaser.growl.minDistance = 2f;
            chaser.growl.maxDistance = 18f;
            chaser.growl.volume = 0.7f;
        }

        if (play && !chaser.growl.isPlaying)
            chaser.growl.Play();
        else if (!play && chaser.growl.isPlaying)
            chaser.growl.Stop();
    }

    Vector3 ClampToBox(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        Vector3 half = box.size * 0.5f;
        Vector3 min = box.center - half;
        Vector3 max = box.center + half;
        local.x = Mathf.Clamp(local.x, min.x, max.x);
        local.y = Mathf.Clamp(local.y, min.y, max.y);
        local.z = Mathf.Clamp(local.z, min.z, max.z);
        return transform.TransformPoint(local);
    }

    static bool IsPlayer(Collider other)
    {
        if (other.CompareTag("Player"))
            return true;

        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"))
            return true;

        return other.transform.root.CompareTag("Player");
    }

    void OnDrawGizmos()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        if (trigger == null)
            return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.35f, 0.7f, 0.2f, 0.18f);
        Gizmos.DrawCube(trigger.center, trigger.size);
        Gizmos.color = new Color(0.4f, 0.85f, 0.2f, 0.95f);
        Gizmos.DrawWireCube(trigger.center, trigger.size);
    }
}
