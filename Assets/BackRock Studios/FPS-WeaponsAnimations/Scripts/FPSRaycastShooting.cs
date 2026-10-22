using UnityEngine;
namespace BackRock
{
    public class FPSRaycastShooting : MonoBehaviour
    {
        public float range = 100.0f;
        public float damage = 10.0f;
        public Camera fpsCamera;
        public ParticleSystem muzzleFlash;
        public GameObject impactEffect;

        void Update()
        {
            if (PackInput.GetButtonDown("Fire1"))
            {
                Shoot();
            }
        }

        void Shoot()
        {
            muzzleFlash.Play();

            RaycastHit hit;
            if (Physics.Raycast(fpsCamera.transform.position, fpsCamera.transform.forward, out hit, range))
            {
                // Apply damage if the object has a health script
                Target target = hit.transform.GetComponent<Target>();
                if (target != null)
                {
                    target.TakeDamage(damage);
                }

                // Instantiate impact effect at the hit point
                GameObject impactGO = Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impactGO, 2f);
            }
        }
    }
}