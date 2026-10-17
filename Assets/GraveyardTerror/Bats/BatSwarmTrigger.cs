using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UHFPS.Runtime;

namespace GraveyardTerror
{
    /// <summary>
    /// Player walks through this volume, bats spawn and fly at the camera,
    /// and the first arrival plays the face scare once.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BatSwarmTrigger : MonoBehaviour
    {
        [Header("Spawn")]
        [Tooltip("Prefab with BatFlyer. Drop your bat mesh on this prefab.")]
        public GameObject flyingBatPrefab;
        [Tooltip("Where bats appear. If empty, they spawn on this object.")]
        public Transform[] spawnPoints;
        public int batCount = 5;
        public float spawnInterval = 0.12f;
        public Vector2 spawnSpread = new Vector2(1.5f, 0.8f);

        [Header("Face scare")]
        public JumpscareTrigger jumpscare;
        [Tooltip("Inactive bat parented onto the player camera when play starts.")]
        public GameObject faceBat;
        public string faceModelId = "scare_bat";
        public Vector3 faceLocalPosition = new Vector3(0f, -0.05f, 0.45f);
        public Vector3 faceLocalEuler = new Vector3(10f, 180f, 0f);
        public Vector3 faceLocalScale = Vector3.one;

        [Header("After the scare")]
        public float scatterSpeed = 10f;
        public float scatterTime = 3.5f;

        [Header("Trigger")]
        [Tooltip("Red box glued to this object. Move BatSwarm with the Move tool (W).")]
        public Vector3 triggerSize = new Vector3(20f, 40f, 20f);

        bool started;
        bool scared;
        Transform player;
        readonly List<BatFlyer> alive = new List<BatFlyer>();

        public Transform AimPoint
        {
            get
            {
                PlayerPresenceManager presence = PlayerPresenceManager.Instance;
                if (presence != null && presence.PlayerManager != null && presence.PlayerManager.CameraHolder != null)
                    return presence.PlayerManager.CameraHolder;

                return Camera.main != null ? Camera.main.transform : null;
            }
        }

        void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            if (jumpscare == null)
                jumpscare = GetComponent<JumpscareTrigger>();
        }

        void Awake()
        {
            if (triggerSize.x < 1f || triggerSize.y < 1f || triggerSize.z < 1f)
                triggerSize = new Vector3(20f, 40f, 20f);

            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                box.enabled = true;
                box.isTrigger = true;
                box.center = Vector3.zero;
                box.size = triggerSize;
            }

            PrepareJumpscare();
            PlaceFaceBat();
        }

        void Update()
        {
            if (PlayerInside())
                BeginSwarm();
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
                BeginSwarm();
        }

        void BeginSwarm()
        {
            if (started)
                return;

            if (flyingBatPrefab == null)
            {
                Debug.LogWarning("Bat swarm has no flying bat prefab assigned.", this);
                return;
            }

            started = true;
            StartCoroutine(SpawnRoutine());
        }

        bool PlayerInside()
        {
            Transform body = PlayerBody();
            if (body == null)
                return false;

            Vector3 delta = body.position - transform.position;
            float reach = Mathf.Max(triggerSize.x, triggerSize.z) * 0.5f;
            if (delta.x * delta.x + delta.z * delta.z > reach * reach)
                return false;

            return Mathf.Abs(delta.y) <= triggerSize.y * 0.5f;
        }

        Transform PlayerBody()
        {
            if (player != null)
                return player;

            PlayerPresenceManager presence = PlayerPresenceManager.Instance;
            if (presence != null && presence.Player != null)
                player = presence.Player.transform;

            if (player == null)
            {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null)
                    player = tagged.transform;
            }

            return player;
        }

        static bool IsPlayer(Collider other)
        {
            if (other.CompareTag("Player"))
                return true;

            if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"))
                return true;

            return other.transform.root.CompareTag("Player");
        }

        public void OnBatReachedPlayer(BatFlyer bat)
        {
            if (scared)
                return;

            scared = true;

            if (jumpscare != null)
                jumpscare.TriggerJumpscare();
            else
                Debug.LogWarning("Bat swarm has no JumpscareTrigger, so the face scare will not play.", this);

            for (int i = 0; i < alive.Count; i++)
            {
                if (alive[i] != null)
                    alive[i].BeginScatter(scatterSpeed);
            }

            StartCoroutine(RemoveBats());
        }

        IEnumerator SpawnRoutine()
        {
            for (int i = 0; i < batCount; i++)
            {
                Transform point = SpawnPoint(i);
                Vector3 spread = new Vector3(
                    Random.Range(-spawnSpread.x, spawnSpread.x),
                    Random.Range(0f, spawnSpread.y),
                    Random.Range(-spawnSpread.x, spawnSpread.x));

                Vector3 spawnPosition = KeepAwayFromPlayer(point.position + spread);
                GameObject bat = Instantiate(flyingBatPrefab, spawnPosition, point.rotation);
                BatFlyer flyer = bat.GetComponent<BatFlyer>();
                if (flyer == null)
                    flyer = bat.AddComponent<BatFlyer>();

                flyer.Launch(this, i);
                alive.Add(flyer);

                if (scared)
                    flyer.BeginScatter(scatterSpeed);

                if (spawnInterval > 0f && i < batCount - 1)
                    yield return new WaitForSeconds(spawnInterval);
            }
        }

        IEnumerator RemoveBats()
        {
            yield return new WaitForSeconds(scatterTime);

            for (int i = 0; i < alive.Count; i++)
            {
                if (alive[i] != null)
                    Destroy(alive[i].gameObject);
            }

            alive.Clear();
        }

        Vector3 KeepAwayFromPlayer(Vector3 spawnPosition)
        {
            if (AimPoint == null)
                return spawnPosition;

            Vector3 away = spawnPosition - AimPoint.position;
            away.y = 0f;
            if (away.magnitude >= 14f)
                return spawnPosition;

            if (away.sqrMagnitude < 0.01f)
                away = transform.forward;

            return AimPoint.position + away.normalized * 18f + Vector3.up * 3.5f;
        }

        Transform SpawnPoint(int index)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return transform;

            Transform point = spawnPoints[index % spawnPoints.Length];
            return point != null ? point : transform;
        }

        void PrepareJumpscare()
        {
            if (jumpscare == null)
                jumpscare = GetComponent<JumpscareTrigger>();

            if (jumpscare == null)
                return;

            jumpscare.TriggerType = JumpscareTrigger.TriggerTypeEnum.Event;
            jumpscare.JumpscareType = JumpscareTrigger.JumpscareTypeEnum.Direct;
            jumpscare.DirectType = JumpscareTrigger.DirectTypeEnum.Model;
            jumpscare.JumpscareModelID = faceModelId;
        }

        void PlaceFaceBat()
        {
            if (faceBat == null)
                return;

            JumpscareDirect direct = FindFirstObjectByType<JumpscareDirect>();
            if (direct == null)
            {
                Debug.LogWarning("Bat swarm could not find JumpscareDirect on the player.", this);
                return;
            }

            Transform holder = direct.transform;
            JumpscareDirect.DirectModel[] models = direct.JumpscareDirectModels;
            if (models != null && models.Length > 0 && models[0].ModelObject != null && models[0].ModelObject.transform.parent != null)
                holder = models[0].ModelObject.transform.parent;

            faceBat.transform.SetParent(holder, false);
            faceBat.transform.localPosition = faceLocalPosition;
            faceBat.transform.localRotation = Quaternion.Euler(faceLocalEuler);
            faceBat.transform.localScale = faceLocalScale;

            RegisterFaceModel(direct);
            faceBat.SetActive(false);
        }

        void RegisterFaceModel(JumpscareDirect direct)
        {
            JumpscareDirect.DirectModel[] models = direct.JumpscareDirectModels ?? new JumpscareDirect.DirectModel[0];

            for (int i = 0; i < models.Length; i++)
            {
                if (models[i].ModelID == faceModelId)
                {
                    models[i].ModelObject = faceBat;
                    direct.JumpscareDirectModels = models;
                    return;
                }
            }

            var list = new List<JumpscareDirect.DirectModel>(models);
            list.Add(new JumpscareDirect.DirectModel
            {
                ModelID = faceModelId,
                ModelObject = faceBat
            });
            direct.JumpscareDirectModels = list.ToArray();
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.6f, 0.15f, 0.2f, 0.25f);
            Gizmos.DrawCube(Vector3.zero, triggerSize);
            Gizmos.color = new Color(0.8f, 0.2f, 0.25f, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, triggerSize);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.15f, 0.2f, 0.9f);
            if (spawnPoints == null)
                return;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null)
                    continue;

                Gizmos.DrawWireSphere(spawnPoints[i].position, 0.35f);
                Gizmos.DrawLine(transform.position, spawnPoints[i].position);
            }
        }
    }
}
