// FPS Weapons Pack: the arms and weapon are drawn by WeaponCamera, a second camera
// under the player's Main Camera that clears only depth, so the weapon never
// clips into walls. The Built-in pipeline draws that as is. URP has no
// depth-only clear for a base camera and wiped the scene, leaving the weapon
// on a flat background; under URP this component makes the camera an Overlay
// in its parent camera's stack instead. It reaches URP by reflection so the
// script also compiles in projects without URP, where it does nothing.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BackRock
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class WeaponCameraStack : MonoBehaviour
    {
        void OnEnable()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null || pipeline.GetType().Name != "UniversalRenderPipelineAsset") return;
            var urp = pipeline.GetType().Assembly;
            var dataType = urp.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
            var renderType = urp.GetType("UnityEngine.Rendering.Universal.CameraRenderType");
            var parent = transform.parent != null ? transform.parent.GetComponentInParent<Camera>() : null;
            if (dataType == null || renderType == null || parent == null) return;

            var data = GetComponent(dataType) ?? gameObject.AddComponent(dataType);
            dataType.GetProperty("renderType").SetValue(data, Enum.Parse(renderType, "Overlay"));
            var parentData = parent.GetComponent(dataType) ?? parent.gameObject.AddComponent(dataType);
            var stack = dataType.GetProperty("cameraStack").GetValue(parentData) as List<Camera>;
            var cam = GetComponent<Camera>();
            if (stack != null && !stack.Contains(cam)) stack.Add(cam);
        }
    }
}
