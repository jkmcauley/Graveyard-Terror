using UnityEngine;
using UnityEngine.UI;

namespace BackRock
{
    public class WeaponWheelController : MonoBehaviour
    {
        public Animator anim;
        public bool weaponWheelSelected = false;
        public Image selectedItem;
        public Sprite[] weaponSprites;
        public static int weaponID;
        public WeaponManager _weaponManager;

        private int previousWeaponID = -1; // Keep track of the last selected weapon


        void Update()
        {
            if (PackInput.GetKeyDown(KeyCode.Tab))
            {
                weaponWheelSelected = !weaponWheelSelected;
            }

            if (weaponWheelSelected)
            {
                Time.timeScale = 0;
                FPSMovement.canMoveCamera = false;
                anim.gameObject.SetActive(true);
                anim.SetBool("OpenWeaponWheel", true);

                // Show mouse cursor when the weapon wheel is active
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None; // Free the mouse
            }
            else
            {
                Time.timeScale = 1;
                FPSMovement.canMoveCamera = true;
                anim.SetBool("OpenWeaponWheel", false);
                //anim.gameObject.SetActive(false);
                // Hide mouse cursor when the weapon wheel is inactive
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked; // Lock the mouse to the screen center
            }


            if (weaponID != previousWeaponID) // Only update when weaponID changes
            {
                switch (weaponID)
                {
                    case 0: //AK47
                        selectedItem.sprite = weaponSprites[0];
                        _weaponManager.selectedWeapon = 0;
                        break;
                    case 1: //AR15
                        selectedItem.sprite = weaponSprites[1];
                        _weaponManager.selectedWeapon = 1;
                        break;
                    case 2: //UZI
                        selectedItem.sprite = weaponSprites[2];
                        _weaponManager.selectedWeapon = 2;
                        break;
                    case 3: //REVOLVER
                        selectedItem.sprite = weaponSprites[3];
                        _weaponManager.selectedWeapon = 3;
                        break;
                    case 4: //SHOTGUN
                        selectedItem.sprite = weaponSprites[4];
                        _weaponManager.selectedWeapon = 4;
                        break;
                    case 5: //CROSSBOW
                        selectedItem.sprite = weaponSprites[5];
                        _weaponManager.selectedWeapon = 5;
                        break;
                    case 6: //PISTOL
                        selectedItem.sprite = weaponSprites[6];
                        _weaponManager.selectedWeapon = 6;
                        break;
                    case 7: //RIFLE
                        selectedItem.sprite = weaponSprites[7];
                        _weaponManager.selectedWeapon = 7;
                        break;
                    case 8: //SPAS
                        selectedItem.sprite = weaponSprites[8];
                        _weaponManager.selectedWeapon = 8;
                        break;
                    case 9: //GRANADE
                        selectedItem.sprite = weaponSprites[9];
                        _weaponManager.selectedWeapon = 9;
                        break;
                    default:
                        selectedItem.sprite = weaponSprites[10];
                        break;

                }

                _weaponManager.SelectWeapon();

                previousWeaponID = weaponID;
            }
        }
    }
}

