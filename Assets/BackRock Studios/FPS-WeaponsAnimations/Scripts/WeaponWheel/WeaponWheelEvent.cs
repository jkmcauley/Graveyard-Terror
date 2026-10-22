using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BackRock
{
    public class WeaponWheelEvent : MonoBehaviour
    {
        public void CloseWheel()
        {
            gameObject.SetActive(false);
        }
    }

}