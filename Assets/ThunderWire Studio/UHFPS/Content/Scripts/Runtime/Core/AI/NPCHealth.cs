using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UHFPS.Tools;
using Newtonsoft.Json.Linq;

namespace UHFPS.Runtime
{
    public class NPCHealth : BaseHealthEntity, ISaveable
    {
        [System.Serializable]
        public struct BodySegment
        {
            public Rigidbody Rigidbody;
            public Collider Collider;
            public NPCBodyPart BodyPart;

            public BodySegment(Rigidbody rigidbody, Collider collider, NPCBodyPart bodyPart)
            {
               Rigidbody = rigidbody;
               Collider = collider;
                BodyPart = bodyPart;
            }
        }

        public List<BodySegment> BodySegments = new();
        public List<Component> DisableComponents;

        public Transform Hips;
        public Collider Head;
        public Layer BodyPartLayer;

        public uint MaxHealth = 100;
        public uint StartHealth = 100;
        public float HeadshotMultiplier = 2f;
        public bool AllowHeadhsot = true;

        public bool RemoveCorpse;
        public bool DisableCorpse;
        public float CorpseRemoveTime = 10f;

        public AudioClip[] DamageSounds;
        [Range(0f, 1f)] public float DamageVolume = 1f;

        public SoundClip DeathSound;

        public UnityEvent<int> OnTakeDamage;
        public UnityEvent OnDeath;
        public UnityEvent OnCorpseRemove;

        private int lastDamageSound;
        private float corpseTime;
        private bool corpseRemoved;

        private void Awake()
        {
            if (!SaveGameManager.GameWillLoad)
                InitializeHealth((int)StartHealth, (int)MaxHealth);
        }

        private void Update()
        {
            if (!IsDead || corpseRemoved) 
                return;

            if(corpseTime > 0) corpseTime -= Time.deltaTime;
            else
            {
                if (DisableCorpse) gameObject.SetActive(false);
                OnCorpseRemove?.Invoke();
                corpseTime = 0;
                corpseRemoved = true;
            }
        }

        public override void ApplyDamage(int damage, Transform sender = null)
        {
            if (IsDead || corpseRemoved)
                return;

            BossTeleport boss = GetComponent<BossTeleport>();
            if (boss == null)
                boss = GetComponentInParent<BossTeleport>();

            if (boss != null)
            {
                if (!boss.AcceptsPlayerHit())
                    return;

                ApplyDamageMax(sender);
                OnTakeDamage?.Invoke(damage);
                PlayDamageSound();
                return;
            }

            base.ApplyDamage(damage, sender);
            OnTakeDamage?.Invoke(damage);
            PlayDamageSound();
        }

        public void PlayHitReaction()
        {
            if (IsDead || corpseRemoved)
                return;

            OnTakeDamage?.Invoke(0);
            PlayDamageSound();
        }

        void PlayDamageSound()
        {
            if (DamageSounds.Length == 0)
                return;

            int damageSound = GameTools.RandomUnique(0, DamageSounds.Length, lastDamageSound);
            GameTools.PlayOneShot3D(transform.position, DamageSounds[damageSound], DamageVolume, "ZombieDamageAudio");
            lastDamageSound = damageSound;
        }

        public override void OnHealthZero()
        {
            BossTeleport boss = GetComponent<BossTeleport>();
            if (boss == null)
                boss = GetComponentInParent<BossTeleport>();

            if (boss != null)
            {
                boss.PlayDeath();
                OnDeath?.Invoke();
                corpseRemoved = true;
                GameTools.PlayOneShot3D(transform.position, DeathSound, "ZombieDeathAudio");
                return;
            }

            foreach (var component in DisableComponents)
            {
                if(component is Behaviour behaviour)
                    behaviour.enabled = false;
                else if(component is Collider collider)
                    collider.enabled = false;
            }

            foreach (Animator animator in GetComponentsInChildren<Animator>(true))
                animator.enabled = false;

            AttachLooseJoints();
            EnableRagdoll(true);
            OnDeath?.Invoke();
            corpseTime = CorpseRemoveTime;

            GameTools.PlayOneShot3D(transform.position, DeathSound, "ZombieDeathAudio");
        }

        /// <summary>
        /// The head joint was aimed at the neck bone, which has no rigidbody, so the head fell away and the neck mesh stretched.
        /// </summary>
        void AttachLooseJoints()
        {
            foreach (CharacterJoint joint in GetComponentsInChildren<CharacterJoint>(true))
            {
                if (joint.connectedBody == null)
                    joint.connectedBody = FindParentBody(joint.transform);

                if (joint.connectedBody == null)
                    continue;

                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.zero;
                joint.connectedAnchor = joint.connectedBody.transform.InverseTransformPoint(joint.transform.position);
            }
        }

        static Rigidbody FindParentBody(Transform bone)
        {
            Transform parent = bone.parent;
            while (parent != null)
            {
                Rigidbody body = parent.GetComponent<Rigidbody>();
                if (body != null)
                    return body;

                parent = parent.parent;
            }

            return null;
        }

        private void EnableRagdoll(bool enabled)
        {
            foreach (BodySegment bodyPart in BodySegments)
            {
                if (enabled)
                {
                    bodyPart.Rigidbody.isKinematic = false;
                    bodyPart.Rigidbody.useGravity = true;
                    bodyPart.Collider.isTrigger = false;
                }
                else
                {
                    bodyPart.Rigidbody.isKinematic = true;
                    bodyPart.Rigidbody.useGravity = false;
                    bodyPart.Collider.isTrigger = true;
                }
            }
        }

        public StorableCollection OnSave()
        {
            return new StorableCollection()
            {
                { "position", transform.position.ToSaveable() },
                { "rotation", transform.eulerAngles.ToSaveable() },
                { "health", EntityHealth },
            };
        }

        public void OnLoad(JToken data)
        {
            transform.position = data["position"].ToObject<Vector3>();
            transform.eulerAngles = data["rotation"].ToObject<Vector3>();

            int health = (int)data["health"];
            if (health <= 0)
            {
                IsDead = true;
                EntityHealth = health;
                gameObject.SetActive(false);
            }
            else
            {
                InitializeHealth(health, (int)MaxHealth);
            }
        }
    }
}