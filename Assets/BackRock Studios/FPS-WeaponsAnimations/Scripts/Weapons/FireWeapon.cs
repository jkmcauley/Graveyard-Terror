using UnityEngine;
using System.Collections;

namespace BackRock
{
    public class FireWeapon : Weapon
    {
        public float range = 100f;
        public float damage = 10f;
        public ParticleSystem muzzleFlash;
        public ParticleSystem casingDrop;

        public GameObject concreteMist;
        public GameObject concreteImpact;

        public GameObject mudMist;
        public GameObject mudImpact;


        public GameObject impactEffect;//Blood Mist
        public GameObject impactBloodEffect;//Blood Effect


        private Coroutine reloadCoroutine;
        public bool isDroppingBulletCasing;

        //ADS
        private float nextTimeToFire = 0f;

        public SoundType shoot_sType;
        public SoundType reload_sType;

        //HITMARKER
        private UIManager uiManager; 


        void Start()
        {
            currentAmmo = maxAmmo;
            fpsCamera.fieldOfView = normalFOV;
            uiManager = FindObjectOfType<UIManager>(); 


        }

        void OnEnable()
        {
            isAppearing = true; // Set isAppearing to true when the weapon appears
            animator.SetTrigger("Appear");
            StartCoroutine(EndAppear()); // Start coroutine to end appearance state after a delay

            // Enable the weapon's reticle
            if (reticle != null)
            {
                reticle.SetActive(true);
            }
        }

        void OnDisable()
        {
            if (reloadCoroutine != null)
            {
                StopCoroutine(reloadCoroutine); // Stop reload coroutine if it's running
            }
            isReloading = false; // Reset reloading state
            isAiming = false; // Reset aiming state
            animator.SetBool("isAiming", false); // Reset animator aiming parameter
            fpsCamera.fieldOfView = normalFOV; // Reset FOV

        
            // Transition to an idle/empty state in the animator
            animator.Play("EMPTYSTATE"); // Replace "Idle" with the name of your idle animation or empty state


            // Disable the weapon's reticle
            if (reticle != null)
            {
                reticle.SetActive(false);
            }
        }



        void Update()
        {
            if (isReloading || isAppearing)
                return;

            if (currentAmmo <= 0)
            {
                reloadCoroutine = StartCoroutine(Reload());
                return;
            }

            if (PackInput.GetButton("Fire1") && Time.time >= nextTimeToFire)
            {
                nextTimeToFire = Time.time + 1f / fireRate;
                Shoot();
            }


            if (PackInput.GetKeyDown(KeyCode.R) && currentAmmo != maxAmmo )
            {
                reloadCoroutine = StartCoroutine(Reload());
            }


            if (PackInput.GetButtonDown("Fire2"))
            {
                isAiming = true;
                animator.SetBool("isAiming", true);
                Aiming(true);
            }
            else if (PackInput.GetButtonUp("Fire2"))
            {
                isAiming = false;
                animator.SetBool("isAiming", false);
                Aiming(false);
            }

            if (isAiming)
            {
                fpsCamera.fieldOfView = Mathf.Lerp(fpsCamera.fieldOfView, aimFOV, aimSpeed * Time.deltaTime);
            }
            else
            {
                fpsCamera.fieldOfView = Mathf.Lerp(fpsCamera.fieldOfView, normalFOV, aimSpeed * Time.deltaTime);
            }
        }

        IEnumerator Reload()
        {
            isReloading = true;

            // Exit ADS state when reloading
            if (isAiming)
            {
                isAiming = false;
                animator.SetBool("isAiming", false);
                fpsCamera.fieldOfView = normalFOV;
            }


            animator.SetTrigger("Reload");
            yield return new WaitForSeconds(reloadTime - 0.1f); // Subtract a small time to sync animation
            currentAmmo = maxAmmo;
            isReloading = false;
        }

        IEnumerator EndAppear()
        {
            yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length); // Wait for the appear animation to finish
            isAppearing = false; // Set isAppearing to false after the animation ends
        }


        void Shoot()
        {
            SoundManager.instance.PlaySound(shoot_sType, 1.0f);
            muzzleFlash.Play();

            if (isDroppingBulletCasing)
                DropCasing();


            animator.SetTrigger("Shoot");
            currentAmmo--;

            RaycastHit hit;
            if (Physics.Raycast(fpsCamera.transform.position, fpsCamera.transform.forward, out hit, range))
            {
                if (hit.transform.CompareTag("Enemy"))
                {
                    Target target = hit.transform.GetComponentInParent<Target>();
                    if (target != null)
                    {
                        target.TakeDamage(damage);
                        if (uiManager != null)
                        {
                            if (target.isDead)
                                uiManager.ShowHitMarker(true);
                            else
                                uiManager.ShowHitMarker();

                            SoundManager.instance.PlayHitSound(SoundType.HIT_MARKER, 1.0f);
                        }
                    }


                    GameObject impactGO = ObjectPooler.SharedInstance.GetPooledObject("BloodMist");
                    if (impactGO != null)
                    {
                        impactGO.transform.position = hit.point;
                        impactGO.transform.rotation = Quaternion.LookRotation(hit.normal);
                        impactGO.SetActive(true);
                    }

                    GameObject impactGOBlood = ObjectPooler.SharedInstance.GetPooledObject("BloodImpact");
                    if (impactGO != null)
                    {
                        impactGOBlood.transform.position = hit.point;
                        impactGOBlood.transform.rotation = Quaternion.LookRotation(hit.normal);
                        impactGOBlood.SetActive(true);
                    }

                }


                if (hit.transform.CompareTag("Concrete"))
                {
                    /*
                    Vector3 offset = hit.normal * 0.01f; // Small offset to avoid clipping
                    Vector3 directionToCamera = (fpsCamera.transform.position - hit.point).normalized;

                    Quaternion lookRotation = Quaternion.LookRotation(directionToCamera);


                    GameObject impactConcreteMist = ObjectPooler.SharedInstance.GetPooledObject("ConcreteMist");
                    if (impactConcreteMist != null)
                    {
                        impactConcreteMist.transform.position = hit.point + offset;
                        impactConcreteMist.transform.rotation = lookRotation;
                        impactConcreteMist.SetActive(true);
                    }

                    GameObject impactConcrete = ObjectPooler.SharedInstance.GetPooledObject("ConcreteImpact");
                    if (impactConcrete != null)
                    {
                        impactConcrete.transform.position = hit.point + offset;
                        impactConcrete.transform.rotation = lookRotation;
                        impactConcrete.SetActive(true);
                    }

                    */


                    GameObject impactConcreteMist = ObjectPooler.SharedInstance.GetPooledObject("ConcreteMist");
                    if (impactConcreteMist != null)
                    {
                        impactConcreteMist.transform.position = hit.point;
                        impactConcreteMist.transform.rotation = Quaternion.LookRotation(hit.normal);
                        impactConcreteMist.SetActive(true);
                    }

                    GameObject impactConcrete = ObjectPooler.SharedInstance.GetPooledObject("ConcreteImpact");
                    if (impactConcreteMist != null)
                    {
                        impactConcrete.transform.position = hit.point;
                        impactConcrete.transform.rotation = Quaternion.LookRotation(hit.normal);
                        impactConcrete.SetActive(true);
                    }
                }


                if (hit.transform.CompareTag("Mud"))
                {
                    GameObject impactMudMist = ObjectPooler.SharedInstance.GetPooledObject("MudMist");
                    if (impactMudMist != null)
                    {
                        impactMudMist.transform.position = hit.point;
                        impactMudMist.transform.rotation = Quaternion.LookRotation(hit.normal);
                        impactMudMist.SetActive(true);
                    }

                    GameObject impactMud = ObjectPooler.SharedInstance.GetPooledObject("MudImpact");
                    if (impactMudMist != null)
                    {
                        impactMud.transform.position = hit.point;
                        impactMud.transform.rotation = Quaternion.LookRotation(hit.normal);
                        impactMud.SetActive(true);
                    }

                    /*
                    Vector3 offset = hit.normal * 0.01f; // Small offset to avoid clipping
                    Vector3 directionToCamera = (fpsCamera.transform.position - hit.point).normalized;

                    Quaternion lookRotation = Quaternion.LookRotation(directionToCamera);

                    GameObject impactMudMist = ObjectPooler.SharedInstance.GetPooledObject("MudMist");
                    if (impactMudMist != null)
                    {
                        impactMudMist.transform.position = hit.point + offset;
                        impactMudMist.transform.rotation = lookRotation;
                        impactMudMist.SetActive(true);
                    }

                    GameObject impactMud = ObjectPooler.SharedInstance.GetPooledObject("MudImpact");
                    if (impactMud != null)
                    {
                        impactMud.transform.position = hit.point + offset;
                        impactMud.transform.rotation = lookRotation;
                        impactMud.SetActive(true);
                    }

                    */
                }

                if (hit.transform.CompareTag("Destructible"))
                {
                    Rigidbody rb = hit.transform.gameObject.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        // Make sure the object is not kinematic to apply physics
                        rb.isKinematic = false;

                        // Calculate the direction from the hit point to the object
                        Vector3 direction = hit.transform.position - hit.point;

                        // Apply force in the direction away from the hit point
                        rb.AddForce(direction.normalized * 100f, ForceMode.Impulse);
                    }
                }

            }


        }


        public void DropCasing()
        {
            casingDrop.Play();
        }
    }

}