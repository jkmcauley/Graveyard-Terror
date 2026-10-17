using System.Collections;
using UHFPS.Runtime;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TimelineTrigger : MonoBehaviour
{
    [SerializeField] private PlayableDirector _director;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private Transform _boss;
    [SerializeField] private float fadeSpeed = 1.15f;

    bool _hasPlayed;

    void Awake()
    {
        if (_spawnManager == null)
            _spawnManager = GetComponent<SpawnManager>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || _hasPlayed || _director == null)
            return;

        _hasPlayed = true;
        StartCoroutine(PlayWithFade());
    }

    IEnumerator PlayWithFade()
    {
        GameManager game = GameManager.Instance;

        if (game != null)
            yield return game.StartBackgroundFade(false, 0f, fadeSpeed);

        _director.Play();

        if (game != null)
            yield return game.StartBackgroundFade(true, 0.15f, fadeSpeed);

        double speechEnd = CutsceneSpeechEnd();
        while (_director.state == PlayState.Playing && _director.time < speechEnd)
            yield return null;

        if (game != null)
            yield return game.StartBackgroundFade(false, 0f, fadeSpeed);

        if (_director.state == PlayState.Playing)
            _director.Stop();

        if (_spawnManager != null)
            _spawnManager.gameObject.SetActive(true);

        if (_boss != null)
            _boss.gameObject.SetActive(false);

        if (game != null)
            yield return game.StartBackgroundFade(true, 0.1f, fadeSpeed);
    }

    double CutsceneSpeechEnd()
    {
        double cameraEnd = CutsceneCameraEnd();
        double speechEnd = cameraEnd;

        if (_director.playableAsset is TimelineAsset timeline)
        {
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (track is not AudioTrack)
                    continue;

                foreach (TimelineClip clip in track.GetClips())
                {
                    bool backgroundBed = clip.start < 1d && clip.end > cameraEnd + 2d;
                    if (!backgroundBed)
                        speechEnd = System.Math.Max(speechEnd, clip.end);
                }
            }
        }

        return speechEnd;
    }

    double CutsceneCameraEnd()
    {
        if (_director.playableAsset is TimelineAsset timeline)
        {
            double end = 0d;
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (track is not CinemachineTrack)
                    continue;

                foreach (TimelineClip clip in track.GetClips())
                    end = System.Math.Max(end, clip.end);
            }

            if (end > 0d)
                return end;
        }

        return _director.duration;
    }
}
