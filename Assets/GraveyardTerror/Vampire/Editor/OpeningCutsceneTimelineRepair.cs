using System;
using System.Linq;
using System.Reflection;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

[InitializeOnLoad]
static class OpeningCutsceneTimelineRepair
{
    const string BrokenPath = "Assets/OpeningSpawnSceneTimeline.playable";
    const string FreshPath = "Assets/GraveyardTerror/Vampire/OpeningCutscene.playable";
    const string PrefKey = "GraveyardTerror.OpeningTimelineRepair.v2";
    const string PreviewKey = "GraveyardTerror.OpeningPreview.v1";
    const double Blend = 0.6;

    static readonly string[] ClipPaths =
    {
        "Assets/Vampire_Boss_Set/Art/Animations/Vampire_Sleep_Horizontal_Loop.fbx",
        "Assets/Vampire_Boss_Set/Art/Animations/Vampire_Sleep_Horizontal_End.fbx",
        "Assets/Vampire_Boss_Set/Art/Animations/Vampire_Fly_Up.fbx",
        "Assets/Vampire_Boss_Set/Art/Animations/Vampire_Energy_EpicSpellCast.fbx"
    };

    static readonly string[] ClipNames =
    {
        "Sleep Horizontal Loop",
        "Sleep Horizontal End",
        "Fly Up",
        "Epic Spell Cast"
    };

    static OpeningCutsceneTimelineRepair()
    {
        EditorApplication.delayCall += AutoRepair;
        EditorApplication.delayCall += FocusScenePreview;
    }

    [MenuItem("Graveyard Terror/Preview Opening Cutscene")]
    static void PreviewFromMenu()
    {
        EditorPrefs.SetBool(PreviewKey, false);
        FocusScenePreview();
    }

    [MenuItem("Graveyard Terror/Repair Opening Timeline")]
    static void RepairFromMenu()
    {
        EditorPrefs.SetBool(PrefKey, false);
        if (Repair())
            EditorPrefs.SetBool(PrefKey, true);
    }

    static void AutoRepair()
    {
        if (EditorPrefs.GetBool(PrefKey, false))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (Repair())
            EditorPrefs.SetBool(PrefKey, true);
    }

    static void FocusScenePreview()
    {
        if (EditorPrefs.GetBool(PreviewKey, false))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        PlayableDirector director = FindDirector();
        GameObject boss = FindNamed("BossEnemy");
        if (director == null || boss == null)
            return;

        Selection.activeGameObject = director.gameObject;
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            view.Frame(new Bounds(boss.transform.position + Vector3.up * 1.2f, new Vector3(4f, 3f, 4f)), false);
            view.Repaint();
        }

        EditorPrefs.SetBool(PreviewKey, true);
        EditorApplication.delayCall += () => EnablePreview();
    }

    static void EnablePreview()
    {
        TimelineEditorWindow window = TimelineEditor.GetOrCreateWindow();
        if (window == null)
            return;

        object state = window.GetType().GetProperty("state", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(window);
        if (state == null)
            return;

        state.GetType().GetProperty("previewMode")?.SetValue(state, true);
        state.GetType().GetMethod("Evaluate")?.Invoke(state, null);
        window.Repaint();
    }

    static bool Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return false;

        try
        {
            CloseTimelineWindows();
            ForgetBrokenTimeline();

            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(FreshPath);
            if (!IsHealthy(timeline))
            {
                if (timeline != null)
                    AssetDatabase.DeleteAsset(FreshPath);

                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, FreshPath);
                if (!Build(timeline))
                    return false;
            }

            Rebind(timeline);
            AssetDatabase.SaveAssets();
            AssetDatabase.OpenAsset(timeline);
            Debug.Log("Opening cutscene timeline is ready on " + FreshPath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    static void ForgetBrokenTimeline()
    {
        TimelineAsset broken = AssetDatabase.LoadAssetAtPath<TimelineAsset>(BrokenPath);
        if (broken == null)
            return;

        try
        {
            SerializedObject serialized = new SerializedObject(broken);
            SerializedProperty tracks = serialized.FindProperty("m_Tracks");
            if (tracks != null && tracks.isArray)
            {
                tracks.ClearArray();
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        catch (Exception)
        {
            // The broken asset cannot be edited. Leave it and use the new timeline.
        }

        EditorUtility.ClearDirty(broken);
    }

    static bool IsHealthy(TimelineAsset timeline)
    {
        if (timeline == null)
            return false;

        try
        {
            int animClips = 0;
            int shots = 0;
            foreach (TrackAsset track in timeline.GetRootTracks())
            {
                if (track == null)
                    return false;

                _ = track.name;
                if (track is AnimationTrack anim)
                    animClips += anim.GetClips().Count(clip => clip != null && clip.asset != null);
                if (track is CinemachineTrack cine)
                    shots += cine.GetClips().Count(clip => clip != null && clip.asset != null);
            }

            return animClips >= 4 && shots >= 1;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static bool Build(TimelineAsset timeline)
    {
        AnimationTrack anim = timeline.CreateTrack<AnimationTrack>(null, "Animation Track");
        double cursor = 0;
        for (int i = 0; i < ClipPaths.Length; i++)
        {
            AnimationClip source = LoadClip(ClipPaths[i]);
            if (source == null)
                return false;

            TimelineClip clip = anim.CreateClip(source);
            clip.displayName = ClipNames[i];
            double blend = Math.Min(Blend, clip.duration * 0.4);
            clip.start = cursor;
            if (i > 0)
                clip.blendInDuration = blend;
            if (i < ClipPaths.Length - 1)
                clip.blendOutDuration = blend;

            cursor += clip.duration - (i < ClipPaths.Length - 1 ? blend : 0);
        }

        CinemachineTrack cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "Cinemachine Track");
        TimelineClip shotClip = cameraTrack.CreateClip<CinemachineShot>();
        shotClip.displayName = "CMVCam";
        shotClip.start = 0;
        shotClip.duration = Math.Max(timeline.duration, cursor);

        EditorUtility.SetDirty(timeline);
        return true;
    }

    static void Rebind(TimelineAsset timeline)
    {
        PlayableDirector director = FindDirector();
        if (director == null)
        {
            Debug.LogWarning("OpeningCutscene was not in a loaded scene, so the new timeline was not assigned.");
            return;
        }

        SerializedObject serializedDirector = new SerializedObject(director);
        SerializedProperty bindings = serializedDirector.FindProperty("m_SceneBindings");
        if (bindings != null)
            bindings.ClearArray();
        serializedDirector.ApplyModifiedPropertiesWithoutUndo();

        director.playableAsset = timeline;

        AnimationTrack anim = timeline.GetRootTracks().OfType<AnimationTrack>().FirstOrDefault();
        CinemachineTrack cameraTrack = timeline.GetRootTracks().OfType<CinemachineTrack>().FirstOrDefault();
        Animator animator = FindNamed("BossEnemy")?.GetComponentInChildren<Animator>(true);
        CinemachineBrain brain = FindNamed("MainCameraBrain")?.GetComponent<CinemachineBrain>();
        CinemachineCamera camera = FindNamed("CMVCam")?.GetComponent<CinemachineCamera>();

        if (anim != null && animator != null)
            director.SetGenericBinding(anim, animator);
        if (cameraTrack != null && brain != null)
            director.SetGenericBinding(cameraTrack, brain);

        TimelineClip shotClip = cameraTrack?.GetClips().FirstOrDefault();
        if (shotClip != null && shotClip.asset is CinemachineShot shot && camera != null)
        {
            shot.VirtualCamera.exposedName = new PropertyName(GUID.Generate().ToString());
            director.SetReferenceValue(shot.VirtualCamera.exposedName, camera);
            EditorUtility.SetDirty(shot);
        }

        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        EditorSceneManager.SaveScene(director.gameObject.scene);
    }

    static AnimationClip LoadClip(string path)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                return clip;
        }

        Debug.LogError("Missing boss animation at " + path);
        return null;
    }

    static PlayableDirector FindDirector()
    {
        GameObject cutscene = FindNamed("OpeningCutscene");
        return cutscene != null ? cutscene.GetComponent<PlayableDirector>() : null;
    }

    static GameObject FindNamed(string name)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameObject found = FindNamed(root.transform, name);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    static GameObject FindNamed(Transform transform, string name)
    {
        if (transform.name == name)
            return transform.gameObject;

        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject found = FindNamed(transform.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    static void CloseTimelineWindows()
    {
        System.Reflection.Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => candidate.GetName().Name == "Unity.Timeline.Editor");
        if (assembly == null)
            return;

        Type windowType = assembly.GetType("UnityEditor.Timeline.TimelineWindow");
        if (windowType == null)
            return;

        foreach (UnityEngine.Object window in Resources.FindObjectsOfTypeAll(windowType))
        {
            if (window is EditorWindow editorWindow)
                editorWindow.Close();
        }
    }
}
