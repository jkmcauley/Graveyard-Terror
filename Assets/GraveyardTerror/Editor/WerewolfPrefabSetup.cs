using System.Collections.Generic;
using System.IO;
using UHFPS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace GraveyardTerror.Editor
{
    [InitializeOnLoad]
    public static class WerewolfPrefabSetup
    {
        const string SourcePrefab = "Assets/Monsters and Undead - Horror Pack/Models/Prefabs/Werewolf.prefab";
        const string DestPrefab = "Assets/GraveyardTerror/Prefabs/Werewolf.prefab";
        const string ZombieControllerPath = "Assets/ThunderWire Studio/UHFPS/Content/Animation/Zombie/Zombie.controller";
        const string MarkerPath = "Assets/GraveyardTerror/Prefabs/Werewolf.built";
        const string SceneMarkerPath = "Assets/GraveyardTerror/Prefabs/Werewolf.scene-wired";
        const string ScenePath = "Assets/Scenes/Level3.unity";
        const int NpcLayer = 9;
        const int BodyPartLayer = 10;

        static WerewolfPrefabSetup()
        {
            EditorApplication.delayCall += TryBuildOnce;
        }

        static void TryBuildOnce()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryBuildOnce;
                return;
            }

            if (!File.Exists(MarkerPath) || !File.Exists(DestPrefab) || !PrefabUsesZombieController())
                Build();

            WireLevel3();
        }

        [MenuItem("Graveyard/Build Werewolf Prefab")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
            if (source == null)
            {
                Debug.LogError("Werewolf setup failed. Missing " + SourcePrefab);
                return;
            }

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ZombieControllerPath);
            if (controller == null)
            {
                Debug.LogError("Werewolf setup failed. Missing the zombie animator controller.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Werewolf";
            if (!ConfigureInstance(instance, controller))
            {
                Object.DestroyImmediate(instance);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(DestPrefab) ?? "Assets/GraveyardTerror/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(instance, DestPrefab);
            Object.DestroyImmediate(instance);
            File.WriteAllText(MarkerPath, "ok");
            AssetDatabase.SaveAssets();
            Debug.Log("Built " + DestPrefab);
        }

        static bool ConfigureInstance(GameObject instance, RuntimeAnimatorController controller)
        {
            SetLayer(instance.transform, NpcLayer);

            var animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null)
                animator = instance.AddComponent<Animator>();

            Avatar ownAvatar = animator.avatar;
            animator.runtimeAnimatorController = controller;
            animator.avatar = ownAvatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                Debug.LogError("Werewolf avatar is not a valid humanoid.");
                return false;
            }

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null || head == null)
            {
                Debug.LogError("Werewolf humanoid is missing hips or head.");
                return false;
            }

            FitBodyCollider(instance, out var capsule, out var agent, out var body);

            var health = instance.GetComponent<NPCHealth>();
            if (health == null)
                health = instance.AddComponent<NPCHealth>();

            health.Hips = hips;
            health.BodyPartLayer = BodyPartLayer;
            health.MaxHealth = 100;
            health.StartHealth = 100;
            health.HeadshotMultiplier = 10f;
            health.AllowHeadhsot = true;
            health.RemoveCorpse = true;
            health.DisableCorpse = true;
            health.CorpseRemoveTime = 10f;
            health.DamageVolume = 1f;
            health.DisableComponents = new List<Component> { animator, agent, capsule };

            var segments = new List<NPCHealth.BodySegment>();
            AddLimb(segments, health, hips, animator.GetBoneTransform(HumanBodyBones.Spine), null, 0.16f, 3.125f, false, 20f, 20f);
            AddLimb(segments, health, animator.GetBoneTransform(HumanBodyBones.Chest), animator.GetBoneTransform(HumanBodyBones.Neck), hips, 0.16f, 2.5f, false, 25f, 20f);
            SphereCollider headShot = AdoptHeadCircle(head);
            var headSegment = AddHead(segments, health, head, animator.GetBoneTransform(HumanBodyBones.Chest), headShot);
            health.Head = headSegment.Collider;

            AddLimb(segments, health, animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), animator.GetBoneTransform(HumanBodyBones.Chest), 0.07f, 1.2f, false, 70f, 40f);
            AddLimb(segments, health, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), animator.GetBoneTransform(HumanBodyBones.LeftHand), animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), 0.06f, 0.8f, false, 20f, 70f);
            AddLimb(segments, health, animator.GetBoneTransform(HumanBodyBones.RightUpperArm), animator.GetBoneTransform(HumanBodyBones.RightLowerArm), animator.GetBoneTransform(HumanBodyBones.Chest), 0.07f, 1.2f, false, 70f, 40f);
            AddLimb(segments, health, animator.GetBoneTransform(HumanBodyBones.RightLowerArm), animator.GetBoneTransform(HumanBodyBones.RightHand), animator.GetBoneTransform(HumanBodyBones.RightUpperArm), 0.06f, 0.8f, false, 20f, 70f);
            AddLimb(segments, health, animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), hips, 0.09f, 2f, false, 40f, 20f);
            AddLimb(segments, health, animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), 0.07f, 1.2f, false, 10f, 40f);
            AddLimb(segments, health, animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), hips, 0.09f, 2f, false, 40f, 20f);
            AddLimb(segments, health, animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightFoot), animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), 0.07f, 1.2f, false, 10f, 40f);

            health.BodySegments = segments;

            var brain = instance.GetComponent<Vampire>();
            if (brain == null)
                brain = instance.AddComponent<Vampire>();
            brain.enabled = true;

            body.isKinematic = true;
            body.useGravity = false;
            return true;
        }

        static SphereCollider AdoptHeadCircle(Transform head)
        {
            foreach (SphereCollider sphere in head.GetComponentsInChildren<SphereCollider>(true))
                return sphere;

            var circle = head.gameObject.AddComponent<SphereCollider>();
            circle.radius = 0.22f;
            circle.center = new Vector3(0.04f, 0.08f, 0f);
            circle.isTrigger = true;
            return circle;
        }

        static void FitBodyCollider(GameObject instance, out CapsuleCollider capsule, out NavMeshAgent agent, out Rigidbody body)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float height = Mathf.Max(1.6f, bounds.size.y);
            Vector3 center = instance.transform.InverseTransformPoint(bounds.center);
            center.x = 0f;
            center.z = 0f;

            capsule = instance.GetComponent<CapsuleCollider>();
            if (capsule == null)
                capsule = instance.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.height = height;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, center.y, 0f);

            body = instance.GetComponent<Rigidbody>();
            if (body == null)
                body = instance.AddComponent<Rigidbody>();

            agent = instance.GetComponent<NavMeshAgent>();
            if (agent == null)
                agent = instance.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = height;
            agent.baseOffset = 0f;
            agent.speed = 2f;
            agent.acceleration = 8f;
            agent.angularSpeed = 120f;
            agent.stoppingDistance = 0.75f;
            agent.autoBraking = true;
        }

        static NPCHealth.BodySegment AddHead(
            List<NPCHealth.BodySegment> segments,
            NPCHealth health,
            Transform bone,
            Transform connected,
            SphereCollider circle)
        {
            bone.gameObject.layer = BodyPartLayer;
            bone.gameObject.tag = "Flesh";
            circle.isTrigger = true;

            var body = bone.GetComponent<Rigidbody>();
            if (body == null)
                body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = 1.25f;
            body.isKinematic = true;
            body.useGravity = false;

            if (connected != null && bone.GetComponent<CharacterJoint>() == null)
            {
                var joint = bone.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = connected.GetComponent<Rigidbody>();
                joint.axis = Vector3.up;
                joint.swingAxis = Vector3.right;
                joint.enableProjection = true;
                joint.lowTwistLimit = Limit(-30f);
                joint.highTwistLimit = Limit(30f);
                joint.swing1Limit = Limit(40f);
                joint.swing2Limit = Limit(40f);
            }

            var part = bone.GetComponent<NPCBodyPart>();
            if (part == null)
                part = bone.gameObject.AddComponent<NPCBodyPart>();
            part.HealthScript = health;
            part.IsHeadDamage = true;

            var segment = new NPCHealth.BodySegment(body, circle, part);
            segments.Add(segment);
            return segment;
        }

        static NPCHealth.BodySegment AddLimb(
            List<NPCHealth.BodySegment> segments,
            NPCHealth health,
            Transform bone,
            Transform child,
            Transform connected,
            float radius,
            float mass,
            bool head,
            float swing,
            float twist)
        {
            if (bone == null)
                throw new System.InvalidOperationException("Missing a humanoid bone while building the werewolf.");

            bone.gameObject.layer = BodyPartLayer;
            bone.gameObject.tag = "Flesh";

            Collider collider = bone.GetComponent<Collider>();
            if (collider == null)
            {
                Vector3 localEnd = bone.InverseTransformPoint(child.position);
                Vector3 axis = localEnd.normalized;
                int direction = 0;
                float ax = Mathf.Abs(axis.x);
                float ay = Mathf.Abs(axis.y);
                float az = Mathf.Abs(axis.z);
                if (ay >= ax && ay >= az) direction = 1;
                else if (az >= ax && az >= ay) direction = 2;

                var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
                capsule.direction = direction;
                capsule.radius = radius;
                capsule.height = Mathf.Max(localEnd.magnitude, radius * 2f);
                capsule.center = localEnd * 0.5f;
                capsule.isTrigger = true;
                collider = capsule;
            }

            var body = bone.GetComponent<Rigidbody>();
            if (body == null)
                body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            body.useGravity = false;

            if (connected != null && bone.GetComponent<CharacterJoint>() == null)
            {
                var joint = bone.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = connected.GetComponent<Rigidbody>();
                Vector3 localDir = child != null
                    ? bone.InverseTransformPoint(child.position).normalized
                    : Vector3.up;
                if (localDir.sqrMagnitude < 0.001f)
                    localDir = Vector3.up;
                joint.axis = localDir;
                Vector3 swingAxis = Vector3.Cross(localDir, Vector3.up);
                if (swingAxis.sqrMagnitude < 0.001f)
                    swingAxis = Vector3.Cross(localDir, Vector3.right);
                joint.swingAxis = swingAxis.normalized;
                joint.enableProjection = true;
                joint.lowTwistLimit = Limit(-twist);
                joint.highTwistLimit = Limit(twist);
                joint.swing1Limit = Limit(swing);
                joint.swing2Limit = Limit(swing);
            }

            var part = bone.GetComponent<NPCBodyPart>();
            if (part == null)
                part = bone.gameObject.AddComponent<NPCBodyPart>();
            part.HealthScript = health;
            part.IsHeadDamage = head;

            var segment = new NPCHealth.BodySegment(body, collider, part);
            segments.Add(segment);
            return segment;
        }

        static SoftJointLimit Limit(float degrees)
        {
            return new SoftJointLimit { limit = degrees };
        }

        static void SetLayer(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            for (int i = 0; i < transform.childCount; i++)
                SetLayer(transform.GetChild(i), layer);
        }

        static bool PrefabUsesZombieController()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DestPrefab);
            if (prefab == null)
                return false;

            var animator = prefab.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null || animator.avatar == null)
                return false;

            return AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) == ZombieControllerPath;
        }

        static void WireLevel3()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DestPrefab);
            if (prefab == null || !File.Exists(ScenePath))
                return;

            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }

            bool changed = false;
            foreach (EnemyPool pool in Object.FindObjectsByType<EnemyPool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pool.gameObject.scene != scene)
                    continue;

                var serialized = new SerializedObject(pool);
                var prefabs = serialized.FindProperty("enemyPrefabs");
                bool already = prefabs.arraySize == 1 && prefabs.GetArrayElementAtIndex(0).objectReferenceValue == prefab;
                if (already)
                    continue;

                prefabs.arraySize = 1;
                prefabs.GetArrayElementAtIndex(0).objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            var pending = new List<GameObject>();
            var vampires = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                CollectRawWerewolves(root.transform, pending);
                if (root.name == "Vampire")
                    vampires.Add(root);
            }

            foreach (GameObject vampire in vampires)
            {
                Object.DestroyImmediate(vampire);
                changed = true;
            }

            foreach (GameObject raw in pending)
            {
                Transform parent = raw.transform.parent;
                Vector3 position = raw.transform.position;
                Quaternion rotation = raw.transform.rotation;
                Vector3 scale = raw.transform.localScale;
                int sibling = raw.transform.GetSiblingIndex();
                bool active = raw.activeSelf;
                SphereCollider placedCircle = raw.GetComponentInChildren<SphereCollider>(true);
                bool keepCircle = placedCircle != null;
                Vector3 circleCenter = keepCircle ? placedCircle.center : Vector3.zero;
                float circleRadius = keepCircle ? placedCircle.radius : 0f;

                Object.DestroyImmediate(raw);
                var spawned = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                spawned.name = "Werewolf";
                spawned.transform.SetParent(parent, false);
                spawned.transform.SetSiblingIndex(sibling);
                spawned.transform.SetPositionAndRotation(position, rotation);
                spawned.transform.localScale = scale;
                spawned.SetActive(active);
                if (keepCircle)
                    MatchHeadCircle(spawned, circleCenter, circleRadius);
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            File.WriteAllText(SceneMarkerPath, "ok");

            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }

        static void MatchHeadCircle(GameObject werewolf, Vector3 center, float radius)
        {
            var animator = werewolf.GetComponent<Animator>();
            Transform head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null)
                return;

            var circle = head.GetComponent<SphereCollider>();
            if (circle == null)
                return;

            circle.center = center;
            circle.radius = radius;
        }

        static void CollectRawWerewolves(Transform transform, List<GameObject> pending)
        {
            if (transform.name == "Werewolf" && transform.GetComponent<Vampire>() == null)
                pending.Add(transform.gameObject);

            for (int i = 0; i < transform.childCount; i++)
                CollectRawWerewolves(transform.GetChild(i), pending);
        }
    }
}
