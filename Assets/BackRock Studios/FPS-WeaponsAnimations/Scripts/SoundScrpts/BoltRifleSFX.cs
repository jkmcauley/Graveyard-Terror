using UnityEngine;

namespace BackRock { 
    public class BoltRifleSFX : MonoBehaviour
    {
        public void Bolt_Open()
        {
            SoundManager.instance.PlaySound(SoundType.BOLT_RIFLE_OPEN, 0.25f);
        }

        public void Bolt_Close()
        {
            SoundManager.instance.PlaySound(SoundType.BOLT_RIFLE_CLOSE, 0.5f);
        }

        public void Bolt_RemoveClip() 
        {
            SoundManager.instance.PlaySound(SoundType.BOLT_RIFLE_REMOVE_CLIP,1.0f);
        }

        public void Bolt_InsertClip() 
        {
            SoundManager.instance.PlaySound(SoundType.BOLT_RIFLE_INSERT_CLIP, 0.4f);
        }
    }

}