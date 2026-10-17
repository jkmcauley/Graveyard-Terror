using UnityEngine;

namespace GraveyardTerror
{
    /// <summary>
    /// World bat. Flies toward the player camera, then scatters after the face scare.
    /// Movement is scripted. This does not use a NavMeshAgent.
    /// </summary>
    public class BatFlyer : MonoBehaviour
    {
        public float speed = 4.5f;
        public float turnSpeed = 8f;
        public float arrivalDistance = 0.85f;
        public float approachSpread = 1.35f;
        public float endSpread = 0.12f;
        public float centerRange = 8f;
        public float bobAmount = 0.35f;
        public float bobFrequency = 3.5f;
        public float modelYawOffset;
        public string flyStateName = "Fly";
        public AudioClip wingSound;
        public Material skin;

        BatSwarmTrigger swarm;
        Vector3 sideOffset;
        float bobSeed;
        bool arrived;
        bool scattering;
        Vector3 scatterDirection;
        Animator animator;
        Transform head;

        public void Launch(BatSwarmTrigger owner, int index)
        {
            swarm = owner;
            bobSeed = index * 1.37f;
            float angle = index * 137.5f * Mathf.Deg2Rad;
            sideOffset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.35f, 0f);
            if (sideOffset.sqrMagnitude < 0.001f)
                sideOffset = Vector3.right;
            sideOffset.Normalize();
            head = FindHead(transform);

            animator = GetComponentInChildren<Animator>();
            if (animator != null)
                animator.applyRootMotion = false;

            ApplySkin();
            PlayState(flyStateName);

            if (wingSound != null)
            {
                AudioSource wings = gameObject.AddComponent<AudioSource>();
                wings.clip = wingSound;
                wings.loop = true;
                wings.spatialBlend = 1f;
                wings.volume = 0.55f;
                wings.minDistance = 1.5f;
                wings.maxDistance = 22f;
                wings.playOnAwake = false;
                wings.Play();
            }
        }

        void Update()
        {
            if (scattering)
            {
                transform.position += scatterDirection * speed * Time.deltaTime;
                Face(scatterDirection);
                return;
            }

            if (arrived || swarm == null)
                return;

            Transform aim = swarm.AimPoint;
            if (aim == null)
                return;

            Vector3 headPos = head != null ? head.position : transform.position;
            float distance = Vector3.Distance(headPos, aim.position);
            float spreadScale = Mathf.Clamp01((distance - arrivalDistance) / Mathf.Max(0.01f, centerRange));
            float spread = Mathf.Lerp(endSpread, approachSpread, spreadScale);
            Vector3 lateral = aim.TransformDirection(sideOffset) * spread;
            Vector3 target = aim.position + aim.forward * arrivalDistance + lateral;
            Vector3 toFace = target - headPos;
            if (toFace.magnitude <= 0.18f)
            {
                arrived = true;
                swarm.OnBatReachedPlayer(this);
                return;
            }

            float bob = Mathf.Sin((Time.time + bobSeed) * bobFrequency) * bobAmount * spreadScale;
            Vector3 step = toFace.normalized * speed * Time.deltaTime;
            step.y += bob * Time.deltaTime;
            transform.position += step;

            Vector3 look = aim.position - (head != null ? head.position : transform.position);
            Face(look.sqrMagnitude > 0.0001f ? look : toFace);
        }

        public void BeginScatter(float scatterSpeed)
        {
            if (scattering)
                return;

            scattering = true;
            arrived = true;
            speed = scatterSpeed;

            Vector3 away = transform.position;
            if (swarm != null && swarm.AimPoint != null)
                away -= swarm.AimPoint.position;

            away.y = 0.75f;
            if (away.sqrMagnitude < 0.01f)
                away = Vector3.up + transform.right;

            scatterDirection = away.normalized;
        }

        void Face(Vector3 direction)
        {
            direction.y *= 0.35f;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);
            look *= Quaternion.Euler(0f, modelYawOffset, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        static Transform FindHead(Transform root)
        {
            Transform[] bones = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i].name == "batHead")
                    return bones[i];
            }

            return null;
        }

        void ApplySkin()
        {
            if (skin == null)
                return;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = skin;
        }

        void PlayState(string stateName)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(stateName))
                return;

            int hash = Animator.StringToHash(stateName);
            if (animator.HasState(0, hash))
                animator.Play(hash, 0, 0f);
            else
                animator.Play(0, 0, 0f);
        }
    }
}
