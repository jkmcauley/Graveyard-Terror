using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BackRock 
{ 
    public class AKSFX : MonoBehaviour
    {
        public void AK_Remove_Mag()
        {
            SoundManager.instance.PlaySound(SoundType.AK_REMOVE_MAG, 0.6f);
        }

        public void AK_Insert_Mag()
        {
            SoundManager.instance.PlaySound(SoundType.AK_INSERT_MAG, 0.6f);
        }

        public void AK_Cock()
        {
            SoundManager.instance.PlaySound(SoundType.AK_COCK, 1.5f);
        }
    }

}