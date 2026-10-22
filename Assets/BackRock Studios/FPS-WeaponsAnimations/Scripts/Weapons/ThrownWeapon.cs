using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BackRock
{

    public class ThrownWeapon : Weapon
    {
        public Transform spawnPoint;
        public GameObject granada;
        public float range = 10f;
        private bool _throwing;

        // Update is called once per frame
        void Update()
        {
            if (isAppearing || isReloading || _throwing) return;

            if (PackInput.GetButtonDown("Fire1")) Throw();


            if (PackInput.GetButton("Fire2"))
            {
             //   isAiming = true;
               // animator.SetBool("isAiming", true);
               // Aiming(true);
            }
            else if (PackInput.GetButtonUp("Fire2"))
            {
               // isAiming = false;
               // animator.SetBool("isAiming", false);
               // Aiming(false);
            }
        }

        void Throw()
        {
            
            _throwing = true;
            animator.SetTrigger("Shoot");
            currentAmmo--;
        }

        public void InstantiateProjectile() // Use in animator event
        {
            SoundManager.instance.PlaySound(SoundType.GRANADE_THROW, 0.1f);
            StartCoroutine(Reload());
            GameObject instanceGranada = Instantiate(granada, spawnPoint.position, spawnPoint.rotation);
            instanceGranada.GetComponent<Rigidbody>().AddForce(spawnPoint.forward * range, ForceMode.Impulse);
        }

        IEnumerator Reload()
        {
            _throwing = false;
            isReloading = true;

            // Exit ADS state when reloading
            isAiming = false;
            animator.SetBool("isAiming", false);
            fpsCamera.fieldOfView = normalFOV;


            animator.SetTrigger("Reload");
            yield return new WaitForSeconds(reloadTime - 0.1f); // Subtract a small time to sync animation
            currentAmmo = maxAmmo;
            isReloading = false;
        }
    }
}