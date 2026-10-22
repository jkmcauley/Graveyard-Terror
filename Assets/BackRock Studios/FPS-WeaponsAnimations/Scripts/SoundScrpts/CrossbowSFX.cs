using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BackRock { 
    public class CrossbowSFX : MonoBehaviour
    {
        public void Crossbow_String_Pull()
        {
            SoundManager.instance.PlaySound(SoundType.CROSSBOW_STRING_PULL, 0.25f);
        }

        public void Crossbow_Remove_Mag()
        {
            SoundManager.instance.PlaySound(SoundType.CROSSBOW_REMOVE_MAG, 1f);
        }

        public void Crossbow_Insert_Mag()
        {
            SoundManager.instance.PlaySound(SoundType.CROSSBOW_INSERT_MAG, 1f);
        }
    }

}
