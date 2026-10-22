using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BackRock{ 
    public class ShotgunSFX : MonoBehaviour
    {
        public void Shotgun_Cock()
        {
            SoundManager.instance.PlaySound(SoundType.SHOTGUN_COCK, 1f);
        }

        public void Shotgun_Add_Shell() 
        {
            SoundManager.instance.PlaySound(SoundType.SHOTGUN_ADD_SHELL, 1f);
        }

    }

}

