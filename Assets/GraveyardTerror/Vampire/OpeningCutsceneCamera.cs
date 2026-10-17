using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

[ExecuteAlways]
public class OpeningCutsceneCamera : MonoBehaviour
{
    const float QuietLevel = 0.1f;
    const float FadeSeconds = 0.5f;

    PlayableDirector director;
    CinemachineCamera cutsceneCamera;
    AudioSource backgroundMusic;
    float normalMusicVolume;
    bool musicDucked;

    void OnEnable()
    {
        director = GetComponent<PlayableDirector>();
        GameObject cameraObject = GameObject.Find("CMVCam");
        if (cameraObject != null)
            cutsceneCamera = cameraObject.GetComponent<CinemachineCamera>();
    }

    void OnDisable()
    {
        ReleaseCutsceneCamera();
        RestoreMusic();
    }

    void Update()
    {
        bool playing = Application.isPlaying && director != null && director.state == PlayState.Playing;
        UpdateBackgroundMusic(playing);

        if (director == null || cutsceneCamera == null)
            return;

        if (IsTimelinePreview() || director.state == PlayState.Playing)
            cutsceneCamera.Priority = 1000;
        else
            ReleaseCutsceneCamera();
    }

    void UpdateBackgroundMusic(bool playing)
    {
        if (backgroundMusic == null)
        {
            GameObject musicObject = GameObject.Find("BGMusicPlayer");
            if (musicObject != null)
                backgroundMusic = musicObject.GetComponent<AudioSource>();
            if (backgroundMusic == null)
                return;
        }

        if (playing && !musicDucked)
        {
            normalMusicVolume = backgroundMusic.volume;
            musicDucked = true;
        }

        if (!musicDucked)
            return;

        float target = playing ? normalMusicVolume * QuietLevel : normalMusicVolume;
        float speed = Mathf.Abs(normalMusicVolume * (1f - QuietLevel)) / FadeSeconds;
        backgroundMusic.volume = Mathf.MoveTowards(backgroundMusic.volume, target, speed * Time.unscaledDeltaTime);

        if (!playing && Mathf.Approximately(backgroundMusic.volume, normalMusicVolume))
            musicDucked = false;
    }

    void RestoreMusic()
    {
        if (!musicDucked || backgroundMusic == null)
            return;

        backgroundMusic.volume = normalMusicVolume;
        musicDucked = false;
    }

    bool IsTimelinePreview()
    {
        if (Application.isPlaying || director.playableAsset == null)
            return false;

#if UNITY_EDITOR
        if (!UnityEditor.AnimationMode.InAnimationMode())
            return false;

        PlayableDirector inspected = UnityEditor.Timeline.TimelineEditor.inspectedDirector;
        if (inspected != null && inspected != director)
            return false;

        return director.time >= 0d && director.time <= director.duration;
#else
        return false;
#endif
    }

    void ReleaseCutsceneCamera()
    {
        if (cutsceneCamera == null)
            return;

        PrioritySettings priority = cutsceneCamera.Priority;
        priority.Enabled = false;
        cutsceneCamera.Priority = priority;
    }
}
