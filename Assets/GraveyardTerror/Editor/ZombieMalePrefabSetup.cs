using System.Collections.Generic;
using System.IO;
using System.Text;
using UHFPS.Runtime;
using UHFPS.Scriptable;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace GraveyardTerror.Editor
{
    [InitializeOnLoad]
    public static class ZombieMalePrefabSetup
    {
        const string SourcePrefab = "Assets/ZombieMale_AAB/Prefabs/URP/ZombieMale_AAB_URP.prefab";
        const string DestPrefab = "Assets/GraveyardTerror/Prefabs/ZombieMale.prefab";
        const string ControllerPath = "Assets/ThunderWire Studio/UHFPS/Content/Animation/Zombie/Zombie.controller";
        const string StatesPath = "Assets/ThunderWire Studio/UHFPS/Scriptables/Game/StateMachine/NPC/(States) ZombieAI.asset";
        const string MarkerPath = "Assets/GraveyardTerror/Prefabs/ZombieMale.built";
        const string SceneLogPath = "Assets/GraveyardTerror/Prefabs/ZombieMale.scene-setup.txt";
        const int NpcLayer = 9;
        const int BodyPartLayer = 10;

        static ZombieMalePrefabSetup()
        {
            EditorApplication.delayCall += TryBuildOnce;
        }

        static void TryBuildOnce()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryBuildOnce;
                return;
            }

            if (!File.Exists(MarkerPath) || !File.Exists(DestPrefab))
                Build();

            ConfigureScene();
        }

        [MenuItem("Graveyard/Configure Zombie Male In Scene")]
        public static void ConfigureScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var report = new StringBuilder();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DestPrefab);
            if (prefab == null)
            {
                File.WriteAllText(SceneLogPath, "missing prefab");
                return;
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && IsMaleZombie(stage.prefabContentsRoot) && stage.prefabContentsRoot.GetComponent<NPCStateMachine>() == null)
            {
                ConfigureInstance(stage.prefabContentsRoot);
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);
                report.AppendLine("configured prefab stage " + stage.assetPath);
            }

            var animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int replaced = 0;
            foreach (var animator in animators)
            {
                if (animator == null || EditorUtility.IsPersistent(animator))
                    continue;

                GameObject go = animator.gameObject;
                if (!go.scene.IsValid() || !IsMaleZombie(go))
                    continue;
                if (stage != null && go.scene == stage.scene)
                    continue;
                if (go.GetComponent<NPCStateMachine>() != null && animator.runtimeAnimatorController != null)
                    continue;

                Transform parent = go.transform.parent;
                Vector3 position = go.transform.position;
                Quaternion rotation = go.transform.rotation;
                Vector3 scale = go.transform.localScale;
                int sibling = go.transform.GetSiblingIndex();
                Scene scene = go.scene;
                string previousName = go.name;

                Object.DestroyImmediate(go);
                var spawned = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                spawned.name = "ZombieMale";
                spawned.transform.SetParent(parent, false);
                spawned.transform.SetSiblingIndex(sibling);
                spawned.transform.SetPositionAndRotation(position, rotation);
                spawned.transform.localScale = scale;
                replaced++;
                report.AppendLine("replaced " + previousName + " at " + position);
            }

            if (replaced > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
            }

            if (report.Length == 0)
                return;

            File.WriteAllText(SceneLogPath, report.ToString());
            Debug.Log(report.ToString());
        }

        static bool IsMaleZombie(GameObject go)
        {
            if (go == null)
                return false;
            if (go.name.Contains("ZombieMale"))
                return go.GetComponent<Animator>() != null;

            var animator = go.GetComponent<Animator>();
            if (animator == null || animator.avatar == null)
                return false;

            return AssetDatabase.GetAssetPath(animator.avatar).Contains("ZombieMale_AAB");
        }

        [MenuItem("Graveyard/Build Zombie Male Prefab")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var states = AssetDatabase.LoadAssetAtPath<AIStatesGroup>(StatesPath);
            if (source == null || controller == null || states == null)
            {
                Debug.LogError("Zombie male setup failed. Missing source prefab, animator controller, or ZombieAI states.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "ZombieMale";
            if (!ConfigureInstance(instance))
            {
                Object.DestroyImmediate(instance);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(DestPrefab) ?? "Assets/GraveyardTerror/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(instance, DestPrefab);
            Object.DestroyImmediate(instance);
            File.WriteAllText(MarkerPath, "ok");
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            Debug.Log("Built " + DestPrefab);
        }

        static bool ConfigureInstance(GameObject instance)
        {
            SetLayer(instance.transform, NpcLayer);

            var animator = instance.GetComponent<Animator>();
            if (animator == null)
                return false;

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var states = AssetDatabase.LoadAssetAtPath<AIStatesGroup>(StatesPath);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                Debug.LogError("Zombie male avatar is not a valid humanoid.");
                return false;
            }

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null || head == null)
            {
                Debug.LogError("Zombie male humanoid is missing hips or head.");
                return false;
            }

            FitBodyCollider(instance, out var capsule, out var agent);

            var machine = instance.AddComponent<NPCStateMachine>();
            machine.StatesAsset = states;
            machine.Animator = animator;
            machine.HeadBone = head;
            machine.SightsMask = (1 << 0) | (1 << 2);
            machine.NPCType = NPCStateMachine.NPCTypeEnum.Enemy;
            machine.SightsFOV = 110f;
            machine.SightsDistance = 15f;
            machine.SteeringSpeed = 6f;
            machine.ShowDestination = true;
            machine.ShowSights = true;

            var health = instance.AddComponent<NPCHealth>();
            health.Hips = hips;
            health.BodyPartLayer = BodyPartLayer;
            health.MaxHealth = 50;
            health.StartHealth = 50;
            health.HeadshotMultiplier = 2f;
            health.AllowHeadhsot = false;
            health.RemoveCorpse = true;
            health.DisableCorpse = true;
            health.CorpseRemoveTime = 10f;
            health.DamageVolume = 1f;
            health.DamageSounds = LoadClips(
                "395a20a5940f9074fa6ffe62af919b39",
                "a9ce26b5189a6a245b449f91b3ef5bd4",
                "49b69091c36a8b74597c38f5da65c28b",
                "9ee871d6c15bbbf45a73ae73675a0da9");
            health.DeathSound = new SoundClip(LoadClip("d5fa50f4447ace44d9dc3e7106eedfce"), 1f);
            health.DisableComponents = new List<Component> { animator, agent, capsule, machine };

            var segments = new List<NPCHealth.BodySegment>();
            AddLimb(segments, health, hips, animator.GetBoneTransform(HumanBodyBones.Spine), null, 0.16f, 3.125f, false, 20f, 20f);
            AddLimb(segments, health, animator.GetBoneTransform(HumanBodyBones.Chest), animator.GetBoneTransform(HumanBodyBones.Neck), hips, 0.16f, 2.5f, false, 25f, 20f);
            var headSegment = AddLimb(segments, health, head, null, animator.GetBoneTransform(HumanBodyBones.Chest), 0.12f, 1.25f, true, 40f, 30f);
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
            return true;
        }

        static void FitBodyCollider(GameObject instance, out CapsuleCollider capsule, out NavMeshAgent agent)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float height = Mathf.Max(1.6f, bounds.size.y);
            Vector3 center = instance.transform.InverseTransformPoint(bounds.center);
            center.x = 0f;
            center.z = 0f;

            capsule = instance.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.height = height;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, center.y, 0f);

            agent = instance.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = height;
            agent.baseOffset = center.y - (height * 0.5f);
            agent.speed = 0.5f;
            agent.acceleration = 5f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 1f;
            agent.autoBraking = true;
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
                throw new System.InvalidOperationException("Missing a humanoid bone while building the zombie ragdoll.");

            bone.gameObject.layer = BodyPartLayer;
            bone.gameObject.tag = "Flesh";

            Collider collider;
            if (child == null)
            {
                var sphere = bone.gameObject.AddComponent<SphereCollider>();
                sphere.radius = radius;
                sphere.center = new Vector3(0.04f, 0.08f, 0f);
                sphere.isTrigger = true;
                collider = sphere;
            }
            else
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

            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            body.useGravity = false;

            if (connected != null)
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

            var part = bone.gameObject.AddComponent<NPCBodyPart>();
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

        static AudioClip LoadClip(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static AudioClip[] LoadClips(params string[] guids)
        {
            var clips = new AudioClip[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                clips[i] = LoadClip(guids[i]);
            return clips;
        }

        static void SetLayer(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            for (int i = 0; i < transform.childCount; i++)
                SetLayer(transform.GetChild(i), layer);
        }
    }
}
