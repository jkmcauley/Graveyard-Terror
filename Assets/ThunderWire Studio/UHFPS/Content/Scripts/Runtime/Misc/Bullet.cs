using UnityEngine;

namespace UHFPS.Runtime
{
    public class Bullet : MonoBehaviour
    {
        public LayerMask CheckMask;
        public bool DestroyAfterTime = true;
        public float TimeAlive = 2f;
        public float IgnoreHitDistance;

        private float force;
        private Vector3 direction;
        private Vector3 lastPosition;
        private float traveled;
        private bool forceSet;
        private bool timerStarted;

        private void Awake()
        {
            lastPosition = transform.position;
        }

        private void Update()
        {
            if (forceSet)
            {
                float step = force * Time.deltaTime;
                transform.Translate(direction * step, Space.World);
                traveled += step;
            }

            if (traveled < IgnoreHitDistance)
            {
                lastPosition = transform.position;
                return;
            }

            int mask = CheckMask.value;
            int npc = LayerMask.NameToLayer("NPC");
            if (npc >= 0)
                mask |= 1 << npc;

            if (Physics.Linecast(lastPosition, transform.position, mask, QueryTriggerInteraction.Ignore))
                Destroy(gameObject);

            lastPosition = transform.position;
        }

        public void SetDirection(Vector3 direction, float force)
        {
            this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            this.force = force;
            forceSet = true;
            transform.rotation = Quaternion.LookRotation(this.direction);

            if (DestroyAfterTime && !timerStarted)
            {
                Destroy(gameObject, TimeAlive);
                timerStarted = true;
            }
        }
    }
}