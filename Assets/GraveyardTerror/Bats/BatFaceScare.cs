using UnityEngine;

namespace GraveyardTerror
{
    /// <summary>
    /// Camera bat. JumpscareDirect enables this object for the scare, then disables it.
    /// Put the attack clip on the animator. The state name defaults to Attack.
    /// </summary>
    public class BatFaceScare : MonoBehaviour
    {
        public string attackStateName = "Attack";
        public Material skin;
        public float mouthDistance = 0.9f;

        Vector3 lockedLocalPosition;
        Quaternion lockedLocalRotation;
        Transform head;

        void OnEnable()
        {
            lockedLocalPosition = transform.localPosition;
            lockedLocalRotation = transform.localRotation;
            head = FindHead(transform);

            ApplySkin();

            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            animator.applyRootMotion = false;

            if (string.IsNullOrEmpty(attackStateName))
            {
                animator.Play(0, 0, 0f);
                return;
            }

            int hash = Animator.StringToHash(attackStateName);
            if (animator.HasState(0, hash))
                animator.Play(hash, 0, 0f);
            else
                animator.Play(0, 0, 0f);
        }

        void LateUpdate()
        {
            transform.localRotation = lockedLocalRotation;

            if (head == null || transform.parent == null)
            {
                transform.localPosition = lockedLocalPosition;
                return;
            }

            Vector3 desired = transform.parent.TransformPoint(new Vector3(0f, 0f, mouthDistance));
            transform.position += desired - head.position;
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
    }
}
