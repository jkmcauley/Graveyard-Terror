using System.Collections.Generic;
using UnityEngine;
using UHFPS.Runtime;

public class GhostRagdoll : MonoBehaviour
{
    const int BodyPartLayer = 10;

    struct Limb
    {
        public string Bone;
        public string Child;
        public string Connected;
        public float Radius;
        public float Mass;
        public bool Head;
        public float Swing;
        public float Twist;
    }

    static readonly Limb[] Limbs =
    {
        new Limb { Bone = "pelvis", Child = "spine_01", Radius = 0.14f, Mass = 3.125f, Swing = 20f, Twist = 20f },
        new Limb { Bone = "spine_01", Child = "spine_02", Connected = "pelvis", Radius = 0.13f, Mass = 2.5f, Swing = 25f, Twist = 20f },
        new Limb { Bone = "spine_02", Child = "neck_01", Connected = "spine_01", Radius = 0.12f, Mass = 2.2f, Swing = 20f, Twist = 15f },
        new Limb { Bone = "neck_01", Child = "head", Connected = "spine_02", Radius = 0.07f, Mass = 1f, Swing = 30f, Twist = 20f },
        new Limb { Bone = "head", Connected = "neck_01", Radius = 0.11f, Mass = 1.25f, Head = true, Swing = 40f, Twist = 30f },
        new Limb { Bone = "upperarm_l", Child = "lowerarm_l", Connected = "spine_02", Radius = 0.06f, Mass = 1.2f, Swing = 70f, Twist = 40f },
        new Limb { Bone = "lowerarm_l", Child = "hand_l", Connected = "upperarm_l", Radius = 0.05f, Mass = 0.8f, Swing = 20f, Twist = 70f },
        new Limb { Bone = "upperarm_r", Child = "lowerarm_r", Connected = "spine_02", Radius = 0.06f, Mass = 1.2f, Swing = 70f, Twist = 40f },
        new Limb { Bone = "lowerarm_r", Child = "hand_r", Connected = "upperarm_r", Radius = 0.05f, Mass = 0.8f, Swing = 20f, Twist = 70f },
        new Limb { Bone = "thigh_l", Child = "calf_l", Connected = "pelvis", Radius = 0.08f, Mass = 2f, Swing = 40f, Twist = 20f },
        new Limb { Bone = "calf_l", Child = "foot_l", Connected = "thigh_l", Radius = 0.06f, Mass = 1.2f, Swing = 10f, Twist = 40f },
        new Limb { Bone = "thigh_r", Child = "calf_r", Connected = "pelvis", Radius = 0.08f, Mass = 2f, Swing = 40f, Twist = 20f },
        new Limb { Bone = "calf_r", Child = "foot_r", Connected = "thigh_r", Radius = 0.06f, Mass = 1.2f, Swing = 10f, Twist = 40f }
    };

    public static void Prepare(GameObject ghost)
    {
        if (ghost == null || ghost.GetComponent<GhostRagdoll>() != null)
            return;

        ghost.AddComponent<GhostRagdoll>();
    }

    void Awake()
    {
        NPCHealth health = GetComponent<NPCHealth>();
        if (health == null)
            return;

        if (health.BodySegments == null || health.BodySegments.Count == 0)
            Build(health);

        health.OnDeath.AddListener(Release);
    }

    void Build(NPCHealth health)
    {
        var bones = new Dictionary<string, Transform>();
        foreach (Limb limb in Limbs)
        {
            if (bones.ContainsKey(limb.Bone))
                continue;

            Transform bone = FindBone(transform, limb.Bone);
            if (bone != null)
                bones.Add(limb.Bone, bone);
        }

        var segments = new List<NPCHealth.BodySegment>();
        var bodies = new Dictionary<string, Rigidbody>();
        foreach (Limb limb in Limbs)
        {
            if (!bones.TryGetValue(limb.Bone, out Transform bone))
                continue;

            Transform child = null;
            if (!string.IsNullOrEmpty(limb.Child))
                bones.TryGetValue(limb.Child, out child);

            Rigidbody connected = null;
            if (!string.IsNullOrEmpty(limb.Connected))
                bodies.TryGetValue(limb.Connected, out connected);

            NPCHealth.BodySegment segment = AddLimb(health, bone, child, connected, limb);
            bodies[limb.Bone] = segment.Rigidbody;
            segments.Add(segment);
            if (limb.Head)
                health.Head = segment.Collider;
        }

        if (bones.TryGetValue("pelvis", out Transform pelvis))
            health.Hips = pelvis;

        health.BodySegments = segments;
    }

    static NPCHealth.BodySegment AddLimb(NPCHealth health, Transform bone, Transform child, Rigidbody connected, Limb limb)
    {
        bone.gameObject.layer = BodyPartLayer;
        bone.gameObject.tag = "Flesh";

        Collider collider = bone.GetComponent<Collider>();
        if (collider == null)
        {
            if (child == null)
            {
                SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
                sphere.radius = limb.Radius;
                sphere.center = Vector3.zero;
                sphere.isTrigger = true;
                collider = sphere;
            }
            else
            {
                Vector3 localEnd = bone.InverseTransformPoint(child.position);
                Vector3 axis = localEnd.sqrMagnitude > 0.0001f ? localEnd.normalized : Vector3.up;
                int direction = 0;
                float ax = Mathf.Abs(axis.x);
                float ay = Mathf.Abs(axis.y);
                float az = Mathf.Abs(axis.z);
                if (ay >= ax && ay >= az)
                    direction = 1;
                else if (az >= ax && az >= ay)
                    direction = 2;

                CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
                capsule.direction = direction;
                capsule.radius = limb.Radius;
                capsule.height = Mathf.Max(localEnd.magnitude, limb.Radius * 2f);
                capsule.center = localEnd * 0.5f;
                capsule.isTrigger = true;
                collider = capsule;
            }
        }

        Rigidbody body = bone.GetComponent<Rigidbody>();
        if (body == null)
            body = bone.gameObject.AddComponent<Rigidbody>();

        body.mass = limb.Mass;
        body.isKinematic = true;
        body.useGravity = false;

        if (connected != null && bone.GetComponent<CharacterJoint>() == null)
        {
            CharacterJoint joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = connected;
            Vector3 localDir = child != null ? bone.InverseTransformPoint(child.position) : Vector3.up;
            if (localDir.sqrMagnitude < 0.001f)
                localDir = Vector3.up;
            localDir.Normalize();
            joint.axis = localDir;
            Vector3 swingAxis = Vector3.Cross(localDir, Vector3.up);
            if (swingAxis.sqrMagnitude < 0.001f)
                swingAxis = Vector3.Cross(localDir, Vector3.right);
            joint.swingAxis = swingAxis.normalized;
            joint.enableProjection = true;
            joint.lowTwistLimit = Limit(-limb.Twist);
            joint.highTwistLimit = Limit(limb.Twist);
            joint.swing1Limit = Limit(limb.Swing);
            joint.swing2Limit = Limit(limb.Swing);
        }

        NPCBodyPart part = bone.GetComponent<NPCBodyPart>();
        if (part == null)
            part = bone.gameObject.AddComponent<NPCBodyPart>();

        part.HealthScript = health;
        part.IsHeadDamage = limb.Head;
        return new NPCHealth.BodySegment(body, collider, part);
    }

    void Release()
    {
        Animator animator = GetComponent<Animator>();
        if (animator != null)
            animator.enabled = false;

        foreach (Collider collider in GetComponents<Collider>())
            collider.enabled = false;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
            return;

        body.isKinematic = true;
        body.detectCollisions = false;
        Destroy(body);
    }

    static Transform FindBone(Transform root, string boneName)
    {
        if (root.name == boneName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindBone(root.GetChild(i), boneName);
            if (found != null)
                return found;
        }

        return null;
    }

    static SoftJointLimit Limit(float degrees)
    {
        return new SoftJointLimit { limit = degrees };
    }
}
