using UnityEditor;
using UnityEngine;

namespace GraveyardTerror.Editor
{
    public static class SceneViewReset
    {
        static readonly Vector3 FallbackPlayer = new Vector3(657.18f, 5.33f, 379.77f);

        [InitializeOnLoadMethod]
        static void RestoreWhenEditorIsReady()
        {
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGui;
            EditorApplication.delayCall += () => FramePlayer(instant: true);
        }

        [MenuItem("Graveyard/Reset Scene View")]
        public static void ResetFromMenu()
        {
            FramePlayer(instant: true);
        }

        static void OnSceneGui(SceneView view)
        {
            if (ConsumeFrameKey())
                FrameCurrentSelection(view);
        }

        static void OnHierarchyGui(int instanceID, Rect selectionRect)
        {
            if (!ConsumeFrameKey())
                return;
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
                FrameCurrentSelection(view);
        }

        static bool ConsumeFrameKey()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown || e.keyCode != KeyCode.F || !e.command)
                return false;
            GameObject selected = Selection.activeGameObject;
            if (selected != null && selected.GetComponent<RectTransform>() != null)
                return false;
            e.Use();
            return true;
        }

        static void FrameCurrentSelection(SceneView view)
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null && selected.scene.IsValid())
                FrameObject(view, selected);
            else
                FramePlayer(instant: true);
        }

        static void FramePlayer(bool instant)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (SceneView.sceneViews == null || SceneView.sceneViews.Count == 0)
            {
                EditorApplication.delayCall += () => FramePlayer(instant);
                return;
            }

            GameObject player = GameObject.Find("HEROPLAYER");
            Vector3 feet = player != null ? player.transform.position : FallbackPlayer;
            Vector3 back = Vector3.back;
            if (player != null)
            {
                back = -player.transform.forward;
                back.y = 0f;
                if (back.sqrMagnitude < 0.01f)
                    back = Vector3.back;
                back.Normalize();
            }

            Vector3 pivot = feet + Vector3.up * 1.3f;
            Vector3 camera = pivot + back * 4.5f + Vector3.up * 1.5f;
            Quaternion rotation = Quaternion.LookRotation((pivot - camera).normalized, Vector3.up);

            foreach (SceneView view in SceneView.sceneViews)
                Look(view, pivot, rotation, 2.4f, instant);
        }

        static void FrameObject(SceneView view, GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(target.transform.position + Vector3.up, Vector3.one * 1.6f);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                if (bounds.size.magnitude > 25f)
                    bounds = new Bounds(target.transform.position + Vector3.up, Vector3.one * 1.6f);
            }

            Prepare(view);
            view.Frame(bounds, false);
            view.Repaint();
        }

        static void Look(SceneView view, Vector3 pivot, Quaternion rotation, float size, bool instant)
        {
            Prepare(view);
            view.LookAt(pivot, rotation, size, false, instant);
            view.Repaint();
        }

        static void Prepare(SceneView view)
        {
            view.in2DMode = false;
            view.orthographic = false;
            view.sceneLighting = true;
            view.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);

            SceneView.SceneViewState state = view.sceneViewState;
            state.showImageEffects = true;
            state.showFog = false;
            state.showSkybox = true;
            view.sceneViewState = state;

            SceneView.CameraSettings settings = view.cameraSettings;
            settings.dynamicClip = true;
            settings.nearClip = 0.1f;
            settings.farClip = 2000f;
            view.cameraSettings = settings;
        }
    }
}
