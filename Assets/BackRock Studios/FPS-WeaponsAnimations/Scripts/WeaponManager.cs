using UnityEngine;

namespace BackRock
{
    public class WeaponManager : MonoBehaviour
    {
        public Weapon[] weapons;
        public int selectedWeapon = 0;
      

        public int previousSelectedWeapon;

        public Weapon currentWeapon; // Variable to store the currently active weapon



        void Start()
        {
            SelectWeapon();
        }

        void Update()
        {
           previousSelectedWeapon = selectedWeapon;

           /* if (PackInput.GetAxis("Mouse ScrollWheel") > 0f)
            {
                if (selectedWeapon >= weapons.Length - 1)
                    selectedWeapon = 0;
                else
                    selectedWeapon++;
            }

            if (PackInput.GetAxis("Mouse ScrollWheel") < 0f)
            {
                if (selectedWeapon <= 0)
                    selectedWeapon = weapons.Length - 1;
                else
                    selectedWeapon--;
            }*/


            // Handle number key input
           /*
            for (int i = 0; i < weapons.Length; i++)
            {
                if (PackInput.GetKeyDown((KeyCode)System.Enum.Parse(typeof(KeyCode), "Alpha" + (i + 1))))
                {
                    selectedWeapon = i;
                    break;
                }
            }*/

            if (previousSelectedWeapon != selectedWeapon)
            {
                SelectWeapon();
            }

          //  previousSelectedWeapon = selectedWeapon;
        }

        public void SelectWeapon()
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                if (i == selectedWeapon)
                {
                    weapons[i].gameObject.SetActive(false); // Temporarily disable the weapon to reset its state
                    weapons[i].gameObject.SetActive(true);  // Re-enable the weapon to trigger OnEnable
                    currentWeapon = weapons[i]; // Assign the current active weapon to currentWeapon

                    Debug.Log("Selected Weapon: " + weapons[i].gameObject.name);
                }
                else
                {
                    weapons[i].gameObject.SetActive(false);
                }
            }
        }
    }
}