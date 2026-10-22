using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BackRock { 
    public class UZISFX : MonoBehaviour
    {
        public void UZI_Remove_Mag()
        {
            SoundManager.instance.PlaySound(SoundType.UZI_REMOVE_MAG, 1f);
        }

        public void UZI_Insert_Mag()
        {
            SoundManager.instance.PlaySound(SoundType.UZI_INSERT_MAG, 1f);
        }

    }
}
