using System.Collections.Generic;
using System.IO;
using System.Text;
using UHFPS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace GraveyardTerror.Editor
{
    [InitializeOnLoad]
    public static class BoogeymanPrefabSetup
    {
        const string SourcePrefab = "Assets/SayanPaul/TheBoogeyman_HorrorCreatureCharacter/Prefabs/URP/Boogeyman V1.prefab";
        const string DestPrefab = "Assets/GraveyardTerror/Prefabs/Boogeyman.prefab";
        const string AvatarPath = "Assets/GraveyardTerror/Prefabs/BoogeymanAvatar.asset";
        const string ZombieControllerPath = "Assets/ThunderWire Studio/UHFPS/Content/Animation/Zombie/Zombie.controller";
        const string MarkerPath = "Assets/GraveyardTerror/Prefabs/Boogeyman.built";
        const string SceneMarkerPath = "Assets/GraveyardTerror/Prefabs/Boogeyman.scene-wired";
        const string LogPath = "Assets/GraveyardTerror/Prefabs/Boogeyman.setup.txt";
        const string ScenePath = "Assets/Scenes/Level_4.unity";
        const int NpcLayer = 9;
        const int BodyPartLayer = 10;

        static readonly string[][] ReparentPairs =
        {
            new[] { "c_spine_01.x", "root.x" },
            new[] { "head.x", "neck.x" },
            new[] { "foot.l", "leg.l" },
            new[] { "foot.r", "leg.r" },
            new[] { "thigh_stretch.l", "thigh.l" },
            new[] { "thigh_stretch.r", "thigh.r" },
            new[] { "leg_stretch.l", "leg.l" },
            new[] { "leg_stretch.r", "leg.r" },
            new[] { "arm_stretch.l", "arm.l" },
            new[] { "arm_stretch.r", "arm.r" },
            new[] { "forearm_stretch.l", "forearm.l" },
            new[] { "forearm_stretch.r", "forearm.r" },
            new[] { "thigh_twist.l", "thigh.l" },
            new[] { "thigh_twist.r", "thigh.r" },
            new[] { "arm_twist.l", "arm.l" },
            new[] { "arm_twist.r", "arm.r" },
        };

        static readonly string[][] HumanPairs =
        {
            new[] { "root.x", "Hips" },
            new[] { "c_spine_01.x", "Spine" },
            new[] { "c_spine_02.x", "Chest" },
            new[] { "c_spine_03.x", "UpperChest" },
            new[] { "neck.x", "Neck" },
            new[] { "head.x", "Head" },
            new[] { "arm.l", "LeftUpperArm" },
            new[] { "forearm.l", "LeftLowerArm" },
            new[] { "hand.l", "LeftHand" },
            new[] { "arm.r", "RightUpperArm" },
            new[] { "forearm.r", "RightLowerArm" },
            new[] { "hand.r", "RightHand" },
            new[] { "thigh.l", "LeftUpperLeg" },
            new[] { "leg.l", "LeftLowerLeg" },
            new[] { "foot.l", "LeftFoot" },
            new[] { "toes_01.l", "LeftToes" },
            new[] { "thigh.r", "RightUpperLeg" },
            new[] { "leg.r", "RightLowerLeg" },
            new[] { "foot.r", "RightFoot" },
            new[] { "toes_01.r", "RightToes" },
        };

        static bool _attempted;

        static BoogeymanPrefabSetup()
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

            if (_attempted)
                return;

            _attempted = true;
            if (!File.Exists(MarkerPath) || !File.Exists(DestPrefab) || !PrefabReady())
                Build();

            WireLevel4();
        }

        [MenuItem("Graveyard/Build Boogeyman Prefab")]
        public static void Build()
        {
            var report = new StringBuilder();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ZombieControllerPath);
            if (source == null || controller == null)
            {
                report.AppendLine("missing source prefab or zombie controller");
                WriteReport(report);
                Debug.LogError(report.ToString());
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "Boogeyman";
                instance.SetActive(true);
                if (!ConfigureInstance(instance, controller, report))
                {
                    WriteReport(report);
                    Debug.LogError(report.ToString());
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(DestPrefab) ?? "Assets/GraveyardTerror/Prefabs");
                PrefabUtility.SaveAsPrefabAsset(instance, DestPrefab);
                File.WriteAllText(MarkerPath, "ok");
                AssetDatabase.SaveAssets();
                report.AppendLine("built " + DestPrefab);
                WriteReport(report);
                Debug.Log(report.ToString());
            }
            finally
            {
                if (instance != null)
                    Object.DestroyImmediate(instance);
            }
        }

        static bool ConfigureInstance(GameObject instance, RuntimeAnimatorController controller, StringBuilder report)
        {
            SetLayer(instance.transform, NpcLayer);
            ParentRig(instance.transform, report);

            var animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();

            foreach (Animator extra in instance.GetComponentsInChildren<Animator>(true))
            {
                if (extra != animator)
                    Object.DestroyImmediate(extra);
            }

            Avatar avatar = BuildAvatar(instance, report);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                report.AppendLine("avatar is not a valid humanoid");
                if (avatar != null)
                    Object.DestroyImmediate(avatar);
                return false;
            }

            if (File.Exists(AvatarPath))
                AssetDatabase.DeleteAsset(AvatarPath);
            AssetDatabase.CreateAsset(avatar, AvatarPath);

            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null || head == null)
            {
                report.AppendLine("humanoid is missing hips or head");
                return false;
            }

            report.AppendLine("hips " + hips.name);
            report.AppendLine("head " + head.name);

            foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;

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

        static void ParentRig(Transform root, StringBuilder report)
        {
            foreach (string[] pair in ReparentPairs)
            {
                Transform child = FindNamed(root, pair[0]);
                Transform parent = FindNamed(root, pair[1]);
                if (child == null || parent == null)
                {
                    report.AppendLine("skip reparent " + pair[0] + " -> " + pair[1]);
                    continue;
                }

                if (child == parent || parent.IsChildOf(child))
                {
                    report.AppendLine("cycle " + pair[0] + " -> " + pair[1]);
                    continue;
                }

                child.SetParent(parent, true);
            }
        }

        static Avatar BuildAvatar(GameObject instance, StringBuilder report)
        {
            var human = new List<HumanBone>();
            foreach (string[] pair in HumanPairs)
            {
                Transform bone = FindNamed(instance.transform, pair[0]);
                if (bone == null)
                {
                    report.AppendLine("missing bone " + pair[0] + " for " + pair[1]);
                    continue;
                }

                var entry = new HumanBone
                {
                    boneName = pair[0],
                    humanName = pair[1],
                };
                entry.limit.useDefaultValues = true;
                human.Add(entry);
            }

            Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
            var skeleton = new SkeletonBone[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform bone = transforms[i];
                skeleton[i] = new SkeletonBone
                {
                    name = bone.name,
                    position = bone.localPosition,
                    rotation = bone.localRotation,
                    scale = bone.localScale,
                };
            }

            var description = new HumanDescription
            {
                human = human.ToArray(),
                skeleton = skeleton,
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false,
            };

            Avatar avatar = AvatarBuilder.BuildHumanAvatar(instance, description);
            if (avatar != null)
                avatar.name = "BoogeymanAvatar";
            report.AppendLine(avatar != null && avatar.isValid && avatar.isHuman
                ? "avatar created"
                : "avatar build failed");
            return avatar;
        }

        static bool PrefabReady()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DestPrefab);
            if (prefab == null)
                return false;

            var animator = prefab.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null || animator.avatar == null)
                return false;

            return animator.avatar.isValid
                && animator.avatar.isHuman
                && AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) == ZombieControllerPath;
        }

        static void WireLevel4()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DestPrefab);
            if (prefab == null || !File.Exists(ScenePath) || File.Exists(SceneMarkerPath))
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
            foreach (GameObject root in scene.GetRootGameObjects())
                CollectBoogeymen(root.transform, prefab, pending);

            foreach (GameObject raw in pending)
            {
                Transform parent = raw.transform.parent;
                Vector3 position = raw.transform.position;
                Quaternion rotation = raw.transform.rotation;
                Vector3 scale = raw.transform.localScale;
                int sibling = raw.transform.GetSiblingIndex();
                bool active = raw.activeSelf;

                Object.DestroyImmediate(raw);
                var spawned = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                spawned.name = "Boogeyman";
                spawned.transform.SetParent(parent, false);
                spawned.transform.SetSiblingIndex(sibling);
                spawned.transform.SetPositionAndRotation(position, rotation);
                spawned.transform.localScale = scale;
                spawned.SetActive(active);
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

        static void CollectBoogeymen(Transform transform, GameObject built, List<GameObject> pending)
        {
            string name = transform.name;
            bool named = name.IndexOf("Boogey", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Boogie", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (named)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(transform.gameObject);
                if (source != built)
                    pending.Add(transform.gameObject);
                return;
            }

            for (int i = 0; i < transform.childCount; i++)
                CollectBoogeymen(transform.GetChild(i), built, pending);
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
                throw new System.InvalidOperationException("Missing a humanoid bone while building the boogeyman.");

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

        static Transform FindNamed(Transform root, string name)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                    return transform;
            }

            return null;
        }

        static void WriteReport(StringBuilder report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? "Assets/GraveyardTerror/Prefabs");
            File.WriteAllText(LogPath, report.ToString());
        }
    }
}
