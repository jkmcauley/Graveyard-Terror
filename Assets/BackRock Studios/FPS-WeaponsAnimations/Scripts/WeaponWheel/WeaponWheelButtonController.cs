using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BackRock
{
    public class WeaponWheelButtonController : MonoBehaviour
    {
        public int ID;
        private Animator anim;
        public string itemName;
        public TextMeshProUGUI itemText;
        public Image selectedItem;
        private bool selected = false;
        public Sprite icon;
        public WeaponWheelController _weaponWheelController;


        void Start()
        {
            anim = GetComponent<Animator>();
        }

        void Update()
        {
          //  if (selected)
         //   {
          //      selectedItem.sprite = icon;
          //      itemText.text = itemName;
          //  }

        }

        public void Selected()
        {
            SoundManager.instance.PlaySound(SoundType.UI_CLICK);

            selected = true;
            selectedItem.sprite = icon; // Move this here instead
            itemText.text = itemName;
            _weaponWheelController.anim.SetBool("OpenWeaponWheel", false);
            _weaponWheelController.weaponWheelSelected = false;
            WeaponWheelController.weaponID = ID;
        }

        public void Deselected()
        {
            selected = false;
            WeaponWheelController.weaponID = 0;
        }

        public void HoverEnter()
        {
            anim.SetBool("Hover", true);
            itemText.text = itemName;
            SoundManager.instance.PlaySound(SoundType.UI_HOVER);

        }

        public void HoverExit()
        {
            anim.SetBool("Hover", false);
            itemText.text = "";
        }

    }

}