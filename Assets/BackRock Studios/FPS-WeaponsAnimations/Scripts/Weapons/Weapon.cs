using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BackRock
{
    public abstract class Weapon : MonoBehaviour
    {
        public string weaponName;
        public Camera fpsCamera;
        public Animator animator;
        public GameObject reticle; // Reference to the weapon's reticle
        public float fireRate = 0.1f;
        public float reloadTime = 1.5f;
        public int maxAmmo = 30;

        public float normalFOV = 60f;
        public float aimFOV = 30f;
        public float aimSpeed = 8f;
        private protected bool isAiming = false;
        private protected bool isAppearing = false; // Add flag to track weapon appearance state

        private protected int currentAmmo;
        private protected bool isReloading = false;

        public void Aiming(bool aiming)
        {
            if (aiming) fpsCamera.fieldOfView = Mathf.Lerp(fpsCamera.fieldOfView, aimFOV, aimSpeed * Time.deltaTime);
            else fpsCamera.fieldOfView = Mathf.Lerp(fpsCamera.fieldOfView, normalFOV, aimSpeed * Time.deltaTime);
        }
    }
}
