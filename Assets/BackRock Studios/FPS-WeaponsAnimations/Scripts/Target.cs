using UnityEngine;

namespace BackRock
{
    public class Target : MonoBehaviour
    {
        public float health = 50.0f;
        public bool isDead = false;

        [SerializeField]
        private bool isCharacter;
        private Animator anim;
        private Rigidbody[] rigidbodies;

        private void Start()
        {
            if (isCharacter)
            {
                anim = GetComponent<Animator>();
                rigidbodies = transform.GetComponentsInChildren<Rigidbody>();
                SetEnabled(false);
            }
        }

        void SetEnabled(bool enabled) 
        {
            bool isKinematic = !enabled;
            foreach(Rigidbody rb in rigidbodies) 
            {
                rb.isKinematic = isKinematic;
            }

            anim.enabled = !enabled;
        }



        public void TakeDamage(float amount)
        {
            health -= amount;
            if (health <= 0f)
            {
                Die();
            }
        }

        void Die()
        {
            if (!isCharacter)
            {
                isDead = true;
                Destroy(gameObject);
            }
            else 
            {
                isDead = true;
                SetEnabled(true);
                gameObject.GetComponent<Target>().enabled = false;
                gameObject.transform.tag = "Untagged";

                SetTagRecursively(gameObject.transform, "Untagged");
            }
        }


        void SetTagRecursively(Transform parent, string tag)
        {
            foreach (Transform child in parent)
            {
                child.tag = tag; // Set the tag for each child
                SetTagRecursively(child, tag); // Recursively set the tag for all children's children
            }
        }
    }
}