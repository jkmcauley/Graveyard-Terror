using UnityEngine;
using System;
using System.Collections;

namespace BackRock
{
    public enum SoundType
    {
        SHOOT_PISTOL,
        SHOOT_REVOLVER,
        SHOOT_SUBMACHINE,
        SHOOT_RIFLE,
        SHOOT_SHOTGUN,
        SHOOT_ARROW,
        SHOOT_BOLTRIFLE,
        RELOAD_PISTOL,
        RELOAD_REVOLVER,
        RELOAD_SUBMACHINE,
        RELOAD_RIFLE,
        RELOAD_SHOTGUN,
        RELOAD_ARROW,
        BOLT_RIFLE_OPEN,
        BOLT_RIFLE_CLOSE,
        BOLT_RIFLE_INSERT_CLIP,
        BOLT_RIFLE_REMOVE_CLIP,
        AK_INSERT_MAG,
        AK_REMOVE_MAG,
        AK_COCK,
        SHOTGUN_COCK,
        SHOTGUN_ADD_SHELL,
        UZI_INSERT_MAG,
        UZI_REMOVE_MAG,
        REVOLVER_SPIN,
        REVOLVER_OPEN,
        REVOLVER_BULLET_DROPPING,
        REVOLVER_CYLLINDER_INSERT,
        AR_REMOVE_MAG,
        AR_INSERT_MAG,
        AR_SHOOT,
        CROSSBOW_STRING_PULL,
        CROSSBOW_REMOVE_MAG,
        CROSSBOW_INSERT_MAG,
        PISTOL_INSERT_MAG,
        PISTOL_REMOVE_MAG,
        PISTOL_COCK,
        HIT_MARKER,
        EXPLOSION,
        UI_HOVER,
        UI_CLICK,
        GRANADE_THROW
    }


    [RequireComponent(typeof(AudioSource)), ExecuteInEditMode]
    public class SoundManager : MonoBehaviour
    {
        [SerializeField] private SoundList[] soundList;
        [SerializeField] private AudioClip[] musicList;
        public static SoundManager instance;
        public AudioSource audSource;
        public AudioSource hitAudSource;


        private void Awake()
        {
            instance = this;
            audSource = GetComponent<AudioSource>();


        }



        //Sound type and volume
        public void PlaySound(SoundType sound, float vol = 0.5f)
        {
            //instance.audSource.PlayOneShot(instance.soundList[(int)sound], vol);
            AudioClip[] clips = soundList[(int)sound].Sounds;
            if (clips.Length <= 0) return;
            AudioClip rndClip = clips[UnityEngine.Random.Range(0, clips.Length)];

            audSource.PlayOneShot(rndClip, vol);
        }

        public void PlayHitSound(SoundType sound, float vol = 0.5f)
        {
            //instance.audSource.PlayOneShot(instance.soundList[(int)sound], vol);
            AudioClip[] clips = soundList[(int)sound].Sounds;
            if (clips.Length <= 0) return;
            AudioClip rndClip = clips[UnityEngine.Random.Range(0, clips.Length)];

            hitAudSource.PlayOneShot(rndClip, vol);
        }



        public void StopSound()
        {
            audSource.Stop();
        }

        private void PlaySoundOnSource(AudioSource source, SoundType sound, float vol = 0.5f)
        {
            AudioClip[] clips = soundList[(int)sound].Sounds;
            if (clips.Length <= 0) return;
            AudioClip rndClip = clips[UnityEngine.Random.Range(0, clips.Length)];
            source.PlayOneShot(rndClip, vol);
        }


#if UNITY_EDITOR
        private void OnEnable()
        {
            string[] names = Enum.GetNames(typeof(SoundType));
            Array.Resize(ref soundList, names.Length);
            for (int i = 0; i < soundList.Length; i++)
            {
                soundList[i].name = names[i];
            }


        }
#endif
    }

    [Serializable]
    public struct SoundList
    {
        public readonly AudioClip[] Sounds { get => sounds; }
        [HideInInspector] public string name;
        [SerializeField] private AudioClip[] sounds;
    }

}