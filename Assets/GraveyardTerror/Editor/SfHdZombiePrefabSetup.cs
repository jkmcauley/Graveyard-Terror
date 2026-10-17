using System.Collections.Generic;
using System.IO;
using System.Text;
using UHFPS.Runtime;
using UHFPS.Scriptable;
using UnityEditor;
using UnityEngine;

namespace GraveyardTerror.Editor
{
    [InitializeOnLoad]
    public static class SfHdZombiePrefabSetup
    {
        const string Pack = "Assets/Zombie Pack - HD PBR Rigged Characters";
        const string ControllerPath = "Assets/ThunderWire Studio/UHFPS/Content/Animation/Zombie/Zombie.controller";
        const string StatesPath = "Assets/ThunderWire Studio/UHFPS/Scriptables/Game/StateMachine/NPC/(States) ZombieAI.asset";
        const string MarkerPath = "Assets/GraveyardTerror/Prefabs/SfHdZombies.built";
        const string LogPath = "Assets/GraveyardTerror/Prefabs/SfHdZombies.setup.txt";
        const int NpcLayer = 9;
        const int BodyPartLayer = 10;

        static readonly string[] Prefabs =
        {
            Pack + "/Prefabs/SF_Zombie_01.prefab",
            Pack + "/Prefabs/SF_Zombie_02.prefab",
            Pack + "/Prefabs/SF_Zombie_03.prefab",
            Pack + "/Prefabs/SF_Zombie_04.prefab",
            Pack + "/Prefabs/SF_Zombie_05.prefab",
            Pack + "/Prefabs/SF_Zombie_06.prefab",
        };

        static SfHdZombiePrefabSetup()
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

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (File.Exists(MarkerPath))
                return;

            Build();
        }

        [MenuItem("Graveyard/Build HD Zombie Prefabs")]
        public static void Build()
        {
            var report = new StringBuilder();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var states = AssetDatabase.LoadAssetAtPath<AIStatesGroup>(StatesPath);
            if (controller == null || states == null)
            {
                report.AppendLine("missing zombie controller or ZombieAI states");
                WriteReport(report);
                return;
            }

            bool ok = true;
            foreach (string prefabPath in Prefabs)
            {
                if (!PrepareAvatar(prefabPath, report))
                    ok = false;
            }

            if (!ok)
            {
                WriteReport(report);
                return;
            }

            foreach (string prefabPath in Prefabs)
            {
                if (!ConfigurePrefab(prefabPath, controller, states, report))
                    ok = false;
            }

            if (ok)
            {
                File.WriteAllText(MarkerPath, "ok");
                report.AppendLine("all six HD zombie prefabs are ready");
            }

            AssetDatabase.SaveAssets();
            WriteReport(report);
            Debug.Log(report.ToString());
        }

        static bool PrepareAvatar(string prefabPath, StringBuilder report)
        {
            string modelPath = ModelPath(prefabPath);
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                report.AppendLine("missing model " + modelPath);
                return false;
            }

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            Dictionary<string, string> mapping;
            string hierarchy;
            try
            {
                mapping = BuildBoneMap(contents.transform, out hierarchy);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            report.AppendLine(Path.GetFileName(prefabPath));
            report.AppendLine(hierarchy);

            if (mapping == null)
            {
                report.AppendLine("could not map CAT rig to a humanoid");
                return false;
            }

            if (AvatarAlreadyMapped(importer))
            {
                var existing = LoadAvatar(modelPath);
                report.AppendLine(existing != null && existing.isValid && existing.isHuman
                    ? "avatar already humanoid"
                    : "saved bone map is present but avatar is invalid");
                if (existing != null && existing.isValid && existing.isHuman)
                    return true;
            }

            var human = new HumanBone[mapping.Count];
            int index = 0;
            foreach (var pair in mapping)
            {
                var bone = new HumanBone
                {
                    boneName = pair.Key,
                    humanName = pair.Value,
                };
                bone.limit.useDefaultValues = true;
                human[index++] = bone;
            }

            var description = importer.humanDescription;
            description.human = human;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = description;
            importer.SaveAndReimport();

            var avatar = LoadAvatar(modelPath);
            bool valid = avatar != null && avatar.isValid && avatar.isHuman;
            report.AppendLine(valid ? "avatar created" : "avatar import failed");
            return valid;
        }

        static bool AvatarAlreadyMapped(ModelImporter importer)
        {
            var human = importer.humanDescription.human;
            if (human == null)
                return false;

            foreach (var bone in human)
            {
                if (bone.humanName == "Hips" && bone.boneName == "CATRigHub001")
                    return true;
            }

            return false;
        }

        static bool ConfigurePrefab(
            string prefabPath,
            RuntimeAnimatorController controller,
            AIStatesGroup states,
            StringBuilder report)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (!ConfigureInstance(contents, controller, states, out string error))
                {
                    report.AppendLine("configure failed " + prefabPath + ": " + error);
                    return false;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                report.AppendLine("configured " + Path.GetFileName(prefabPath));
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static bool ConfigureInstance(
            GameObject instance,
            RuntimeAnimatorController controller,
            AIStatesGroup states,
            out string error)
        {
            error = null;
            ClearPreviousSetup(instance);
            SetLayer(instance.transform, NpcLayer);

            var animator = instance.GetComponent<Animator>();
            if (animator == null)
            {
                error = "no animator";
                return false;
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                error = "avatar is not a valid humanoid";
                return false;
            }

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null || head == null)
            {
                error = "humanoid is missing hips or head";
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

        static void ClearPreviousSetup(GameObject instance)
        {
            foreach (var bodyPart in instance.GetComponentsInChildren<NPCBodyPart>(true))
            {
                var joint = bodyPart.GetComponent<CharacterJoint>();
                var body = bodyPart.GetComponent<Rigidbody>();
                var capsule = bodyPart.GetComponent<CapsuleCollider>();
                var sphere = bodyPart.GetComponent<SphereCollider>();
                Object.DestroyImmediate(bodyPart);
                if (joint != null) Object.DestroyImmediate(joint);
                if (capsule != null) Object.DestroyImmediate(capsule);
                if (sphere != null) Object.DestroyImmediate(sphere);
                if (body != null) Object.DestroyImmediate(body);
            }

            foreach (var health in instance.GetComponents<NPCHealth>())
                Object.DestroyImmediate(health);
            foreach (var machine in instance.GetComponents<NPCStateMachine>())
                Object.DestroyImmediate(machine);
            foreach (var agent in instance.GetComponents<UnityEngine.AI.NavMeshAgent>())
                Object.DestroyImmediate(agent);
            foreach (var capsule in instance.GetComponents<CapsuleCollider>())
                Object.DestroyImmediate(capsule);
        }

        static Dictionary<string, string> BuildBoneMap(Transform root, out string hierarchy)
        {
            var lines = new StringBuilder();
            Dump(root, 0, lines);
            hierarchy = lines.ToString();

            var hips = FindNamed(root, "CATRigHub001");
            var head = FindHeadHub(root);
            if (hips == null || head == null)
                return null;

            var path = new List<Transform>();
            if (!TryPath(hips, head, path))
                return null;

            var map = new Dictionary<string, string>();
            map[hips.name] = "Hips";
            map[head.name] = "Head";

            Transform neck = head.parent;
            bool neckIsSpine = neck != null && neck != hips && neck.name.Contains("Spine");
            if (neckIsSpine)
                map[neck.name] = "Neck";

            var torso = new List<Transform>();
            foreach (var bone in path)
            {
                if (bone == hips || bone == head)
                    continue;
                if (neckIsSpine && bone == neck)
                    continue;
                torso.Add(bone);
            }

            string[] torsoNames = { "Spine", "Chest", "UpperChest" };
            for (int i = 0; i < torso.Count && i < torsoNames.Length; i++)
                map[torso[i].name] = torsoNames[i];

            Add(map, root, "CATRigLLeg1", "LeftUpperLeg");
            Add(map, root, "CATRigLLeg2", "LeftLowerLeg");
            Add(map, root, "CATRigLLegAnkle", "LeftFoot");
            Add(map, root, "CATRigLLegDigit11", "LeftToes");
            Add(map, root, "CATRigRLeg1", "RightUpperLeg");
            Add(map, root, "CATRigRLeg2", "RightLowerLeg");
            Add(map, root, "CATRigRLegAnkle", "RightFoot");
            Add(map, root, "CATRigRLegDigit11", "RightToes");
            Add(map, root, "CATRigLArmCollarbone", "LeftShoulder");
            Add(map, root, "CATRigLArm1", "LeftUpperArm");
            Add(map, root, "CATRigLArm2", "LeftLowerArm");
            Add(map, root, "CATRigLArmPalm", "LeftHand");
            Add(map, root, "CATRigRArmCollarbone", "RightShoulder");
            Add(map, root, "CATRigRArm1", "RightUpperArm");
            Add(map, root, "CATRigRArm2", "RightLowerArm");
            Add(map, root, "CATRigRArmPalm", "RightHand");
            AddFingers(map, root, "L", "Left");
            AddFingers(map, root, "R", "Right");

            if (!map.ContainsValue("Spine") || !map.ContainsValue("LeftUpperArm") || !map.ContainsValue("LeftUpperLeg"))
                return null;

            return map;
        }

        static void AddFingers(Dictionary<string, string> map, Transform root, string side, string humanSide)
        {
            string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
            string[] joints = { "Proximal", "Intermediate", "Distal" };
            for (int finger = 0; finger < fingers.Length; finger++)
            {
                for (int joint = 0; joint < joints.Length; joint++)
                {
                    string bone = "CATRig" + side + "ArmDigit" + (finger + 1) + (joint + 1);
                    Add(map, root, bone, humanSide + " " + fingers[finger] + " " + joints[joint]);
                }
            }
        }

        static void Add(Dictionary<string, string> map, Transform root, string boneName, string humanName)
        {
            if (FindNamed(root, boneName) != null && !map.ContainsKey(boneName))
                map[boneName] = humanName;
        }

        static Transform FindHeadHub(Transform root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (!transform.name.StartsWith("CATRigHub"))
                    continue;

                for (int i = 0; i < transform.childCount; i++)
                {
                    if (transform.GetChild(i).name.StartsWith(transform.name + "Bone"))
                        return transform;
                }
            }

            return null;
        }

        static bool TryPath(Transform current, Transform target, List<Transform> path)
        {
            path.Add(current);
            if (current == target)
                return true;

            for (int i = 0; i < current.childCount; i++)
            {
                var child = current.GetChild(i);
                if (IsLimb(child.name))
                    continue;
                if (TryPath(child, target, path))
                    return true;
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        static bool IsLimb(string name)
        {
            return name.Contains("Arm") || name.Contains("Leg") || name.Contains("Digit");
        }

        static Transform FindNamed(Transform root, string name)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                    return transform;
            }

            return null;
        }

        static void Dump(Transform transform, int depth, StringBuilder lines)
        {
            lines.Append(' ', depth * 2);
            lines.AppendLine(transform.name);
            if (depth > 8)
                return;

            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name.Contains("Digit") && !child.name.EndsWith("Digit11") && !child.name.EndsWith("ArmDigit11"))
                    continue;
                Dump(child, depth + 1, lines);
            }
        }

        static void FitBodyCollider(GameObject instance, out CapsuleCollider capsule, out UnityEngine.AI.NavMeshAgent agent)
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

            agent = instance.AddComponent<UnityEngine.AI.NavMeshAgent>();
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
            return AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
        }

        static AudioClip[] LoadClips(params string[] guids)
        {
            var clips = new AudioClip[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                clips[i] = LoadClip(guids[i]);
            return clips;
        }

        static Avatar LoadAvatar(string modelPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (asset is Avatar avatar)
                    return avatar;
            }

            return null;
        }

        static string ModelPath(string prefabPath)
        {
            string name = Path.GetFileNameWithoutExtension(prefabPath);
            return Pack + "/Models/" + name + ".fbx";
        }

        static void SetLayer(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            for (int i = 0; i < transform.childCount; i++)
                SetLayer(transform.GetChild(i), layer);
        }

        static void WriteReport(StringBuilder report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? "Assets/GraveyardTerror/Prefabs");
            File.WriteAllText(LogPath, report.ToString());
        }
    }
}
