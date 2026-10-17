using UnityEngine;
using System.Collections;
using UHFPS.Runtime;

[RequireComponent(typeof(BoxCollider))]
public class GhostAngryTrigger : MonoBehaviour
{
    [SerializeField] GameObject ghost;
    [SerializeField] GameObject otherGhost;
    [SerializeField] AudioClip whisper;
    [SerializeField] float flySpeed = 4f;
    [SerializeField] int attackDamage = 15;
    [SerializeField] float resetDelay = 5f;
    [SerializeField] float keepApart = 1.75f;
    [SerializeField] float apartBlendDistance = 8f;

    public int LivingGhostCount => Living(ghost) + (otherGhost != ghost ? Living(otherGhost) : 0);
    public GameObject PrimaryBody => ghost;
    public GameObject SecondaryBody => otherGhost;

    public void CollectGhosts(System.Collections.Generic.HashSet<int> ids)
    {
        if (ghost != null)
            ids.Add(ghost.GetInstanceID());
        if (otherGhost != null)
            ids.Add(otherGhost.GetInstanceID());
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
    Flyer[] flyers;
    Coroutine resetRoutine;

    class Flyer
    {
        public GameObject ghost;
        public Animator animator;
        public Rigidbody body;
        public AudioSource whisper;
        public Coroutine routine;
        public Vector3 homePosition;
        public Quaternion homeRotation;
        public int homeState;
        public bool chasing;
        public bool attacked;
    }

    void Reset()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        GhostRagdoll.Prepare(ghost);
        GhostRagdoll.Prepare(otherGhost);
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
        flyers = new Flyer[] { MakeFlyer(ghost), MakeFlyer(otherGhost) };
        for (int i = 0; i < flyers.Length; i++)
        {
            if (flyers[i] != null)
                flyers[i].routine = StartCoroutine(Chase(flyers[i]));
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
        if (flyers != null)
        {
            for (int i = 0; i < flyers.Length; i++)
                StopFlyer(flyers[i]);
        }

        flyers = null;
        player = null;
        fired = false;
        resetRoutine = null;
    }

    void StopFlyer(Flyer flyer)
    {
        if (flyer == null)
            return;

        if (flyer.routine != null)
        {
            StopCoroutine(flyer.routine);
            flyer.routine = null;
        }

        flyer.chasing = false;
        flyer.attacked = false;
        SetWhisper(flyer, false);

        NPCHealth health = flyer.ghost != null ? flyer.ghost.GetComponent<NPCHealth>() : null;
        if (health != null && health.IsDead)
            return;

        if (flyer.animator != null)
        {
            flyer.animator.ResetTrigger("Angry");
            flyer.animator.ResetTrigger("Attack");
            flyer.animator.SetBool("Fly", false);
            flyer.animator.Play(flyer.homeState, 0, 0f);
            flyer.animator.Update(0f);
        }

        if (flyer.ghost == null)
            return;

        if (flyer.body != null)
        {
            flyer.body.position = flyer.homePosition;
            flyer.body.rotation = flyer.homeRotation;
            if (!flyer.body.isKinematic)
            {
                flyer.body.linearVelocity = Vector3.zero;
                flyer.body.angularVelocity = Vector3.zero;
            }
        }
        else
        {
            flyer.ghost.transform.SetPositionAndRotation(flyer.homePosition, flyer.homeRotation);
        }
    }

    Flyer MakeFlyer(GameObject target)
    {
        if (target == null)
            return null;

        NPCHealth health = target.GetComponent<NPCHealth>();
        if (health != null && health.IsDead)
            return null;

        Animator animator = target.GetComponent<Animator>();
        if (animator == null)
            return null;

        return new Flyer
        {
            ghost = target,
            animator = animator,
            body = target.GetComponent<Rigidbody>(),
            homePosition = target.transform.position,
            homeRotation = target.transform.rotation,
            homeState = animator.GetCurrentAnimatorStateInfo(0).fullPathHash
        };
    }

    IEnumerator Chase(Flyer flyer)
    {
        flyer.animator.SetTrigger("Angry");
        yield return new WaitForSeconds(2f);

        flyer.animator.SetBool("Fly", true);
        flyer.chasing = true;

        while (player != null && fired)
        {
            float distance = Vector3.Distance(flyer.ghost.transform.position, player.position);
            AnimatorStateInfo state = flyer.animator.GetCurrentAnimatorStateInfo(0);
            bool flying = state.IsName("Fly_1") || state.IsName("Fly_2");
            SetWhisper(flyer, IsFlyOrAttack(state));
            if (playerInside <= 0 || distance > AttackRange())
                flyer.attacked = false;
            else if (flying && !flyer.attacked)
            {
                flyer.animator.SetTrigger("Attack");
                flyer.attacked = true;
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null && !health.IsDead)
                    health.ApplyDamage(attackDamage, flyer.ghost.transform);
            }

            yield return null;
        }
    }

    void FixedUpdate()
    {
        if (playerInside <= 0 || flyers == null || player == null)
            return;

        for (int i = 0; i < flyers.Length; i++)
        {
            Flyer flyer = flyers[i];
            if (flyer == null || !flyer.chasing)
                continue;

            NPCHealth health = flyer.ghost.GetComponent<NPCHealth>();
            if (health != null && health.IsDead)
            {
                flyer.chasing = false;
                SetWhisper(flyer, false);
                continue;
            }

            Transform ghostTransform = flyer.ghost.transform;
            Vector3 toPlayer = player.position - ghostTransform.position;
            Vector3 flat = toPlayer;
            flat.y = 0f;
            Quaternion look = ghostTransform.rotation;
            if (flat.sqrMagnitude > 0.001f)
                look = Quaternion.LookRotation(flat.normalized);

            Vector3 goal = FlankGoal(i, ghostTransform.position);
            Vector3 next = ghostTransform.position;
            if ((goal - ghostTransform.position).sqrMagnitude > 0.04f)
                next = Vector3.MoveTowards(ghostTransform.position, goal, flySpeed * Time.fixedDeltaTime);
            next = ClampToBox(next);

            if (flyer.body != null)
            {
                flyer.body.position = next;
                flyer.body.rotation = look;
            }
            else
            {
                ghostTransform.SetPositionAndRotation(next, look);
            }
        }
    }

    float AttackRange()
    {
        return Mathf.Max(1f, keepApart * 0.5f + 0.35f);
    }

    Vector3 FlankGoal(int index, Vector3 from)
    {
        Vector3 goal = player.position;
        if (flyers == null || flyers.Length < 2 || flyers[0] == null || flyers[1] == null)
            return goal;

        float nearest = float.MaxValue;
        for (int i = 0; i < flyers.Length; i++)
        {
            if (flyers[i] == null || !flyers[i].chasing)
                continue;

            float distance = Vector3.Distance(flyers[i].ghost.transform.position, player.position);
            if (distance < nearest)
                nearest = distance;
        }

        if (nearest == float.MaxValue)
            nearest = Vector3.Distance(from, player.position);

        float close = 1f - Mathf.Clamp01(nearest / Mathf.Max(0.01f, apartBlendDistance));
        float spread = keepApart * 0.5f * close;
        Vector3 side = FlankAxis();
        float sign = index % 2 == 0 ? -1f : 1f;
        goal += side * (spread * sign);
        goal += Separation(from, index) * close;
        return goal;
    }

    Vector3 FlankAxis()
    {
        Vector3 mid = Vector3.zero;
        int count = 0;
        for (int i = 0; i < flyers.Length; i++)
        {
            if (flyers[i] == null || flyers[i].ghost == null)
                continue;

            mid += flyers[i].ghost.transform.position;
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
        for (int i = 0; i < flyers.Length; i++)
        {
            if (i == index || flyers[i] == null || flyers[i].ghost == null)
                continue;

            Vector3 away = position - flyers[i].ghost.transform.position;
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

    static bool IsFlyOrAttack(AnimatorStateInfo state)
    {
        return state.IsName("Fly_1") || state.IsName("Fly_2")
            || state.IsName("StartFly1") || state.IsName("StartFly2")
            || state.IsName("StartFly_1") || state.IsName("StartFly_2")
            || state.IsName("Attack_1") || state.IsName("Attack_2")
            || state.IsName("Fly1Attack_1");
    }

    void SetWhisper(Flyer flyer, bool play)
    {
        if (whisper == null)
            return;

        if (flyer.whisper == null)
        {
            flyer.whisper = flyer.ghost.AddComponent<AudioSource>();
            flyer.whisper.clip = whisper;
            flyer.whisper.loop = true;
            flyer.whisper.spatialBlend = 1f;
            flyer.whisper.playOnAwake = false;
            flyer.whisper.minDistance = 2f;
            flyer.whisper.maxDistance = 18f;
            flyer.whisper.volume = 0.7f;
        }

        if (play && !flyer.whisper.isPlaying)
            flyer.whisper.Play();
        else if (!play && flyer.whisper.isPlaying)
            flyer.whisper.Stop();
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
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.4f, 0.05f, 0.18f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(1f, 0.45f, 0.05f, 0.95f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
