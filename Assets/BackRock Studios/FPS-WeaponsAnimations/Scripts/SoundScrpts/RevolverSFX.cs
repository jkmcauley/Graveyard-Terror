using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BackRock { 
    public class RevolverSFX : MonoBehaviour
    {
        public void Revolver_Barrel_Spin() 
        {
            SoundManager.instance.PlaySound(SoundType.REVOLVER_SPIN, 1.0f);
        }

        public void Revolver_Barrel_Open()
        {
            SoundManager.instance.PlaySound(SoundType.REVOLVER_OPEN, 1.0f);
        }

        public void Revolver_Bullet_Dropping() 
        {
            SoundManager.instance.PlaySound(SoundType.REVOLVER_BULLET_DROPPING, 1.0f);
        }

        public void Insert_Bullet_Cyllinder() 
        {
            SoundManager.instance.PlaySound(SoundType.REVOLVER_CYLLINDER_INSERT, 2.0f);
        }
    }

}