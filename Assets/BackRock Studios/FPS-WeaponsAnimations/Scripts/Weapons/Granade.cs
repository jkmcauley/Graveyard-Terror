using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BackRock
{
    public class Granade : MonoBehaviour
    {
        public GameObject efectoExplosion;
        public float delay = 3f;

        public float radius = 20f;
        public float explosionForce = 10f;
        public float damage = 20f;

        void Start()
        {
            Invoke("Explode", delay);
        }

        void Explode()
        {
            SoundManager.instance.PlaySound(SoundType.EXPLOSION);
            Collider[] colliders = Physics.OverlapSphere(transform.position, radius);

            foreach (Collider near in colliders)
            {
                // Check if the object is an enemy by getting the Target component
                Target target = near.GetComponentInParent<Target>();

                // If the object is not an enemy (no Target component), apply explosion physics
                if (target == null)
                {
                    Rigidbody rb = near.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false; // Apply physics only to non-enemy objects
                        rb.AddExplosionForce(explosionForce, transform.position, radius, 1f, ForceMode.Impulse);
                    }
                }
                else
                {
                    // Apply damage to enemies without affecting their physics
                    Debug.Log("target found");
                    target.TakeDamage(damage);
                }
            }

            //ADD THIS TO OBJECT POOLER
            GameObject instanciaParticula = Instantiate(efectoExplosion, transform.position, transform.rotation);
            Destroy(instanciaParticula, 3.5f);
            Destroy(gameObject);
        }
    }
}
