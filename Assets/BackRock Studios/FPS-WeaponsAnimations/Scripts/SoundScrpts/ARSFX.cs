using UnityEngine;
namespace BackRock { 
    public class ARSFX : MonoBehaviour
    {
        public void AR_Remove_Mag()
        {
            SoundManager.instance.PlaySound(SoundType.AR_REMOVE_MAG, 1f);
        }

        public void AR_Insert_Mag()
        {
            SoundManager.instance.PlaySound(SoundType.AR_INSERT_MAG, 1f);
        }
    }
}