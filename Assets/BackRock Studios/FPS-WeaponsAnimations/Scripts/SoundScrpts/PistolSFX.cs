using UnityEngine;

namespace BackRock { 
    public class PistolSFX : MonoBehaviour
    {
        public void Pistol_Remove_Mag()
        {
            SoundManager.instance.PlaySound(SoundType.PISTOL_REMOVE_MAG, 1f);
        }

        public void Pistol_Insert_Mag()
        {
            SoundManager.instance.PlaySound(SoundType.PISTOL_INSERT_MAG, 0.65f);
        }

        public void Pistol_Cock() 
        {
            SoundManager.instance.PlaySound(SoundType.PISTOL_COCK, 1f);
        }
    }
}
