using UnityEngine;
using UnityEngine.Playables;

[ExecuteAlways]
public class SpellFlashCutscene : MonoBehaviour
{
    public GameObject[] flashes;
    public double start = 7.6;
    public double duration = 2.83;

    PlayableDirector director;

    void OnEnable()
    {
        director = GetComponent<PlayableDirector>();
    }

    void LateUpdate()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();

        if (director == null || flashes == null)
            return;

        double time = director.time;
        bool show = time >= start && time < start + duration;
        float localTime = Mathf.Max(0f, (float)(time - start));

        for (int i = 0; i < flashes.Length; i++)
        {
            GameObject flash = flashes[i];
            if (flash == null)
                continue;

            if (flash.activeSelf != show)
                flash.SetActive(show);

            if (!show)
                continue;

            ParticleSystem[] systems = flash.GetComponentsInChildren<ParticleSystem>(true);
            for (int s = 0; s < systems.Length; s++)
                systems[s].Simulate(localTime, true, true, false);
        }
    }
}
