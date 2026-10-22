// FPS Weapons Pack: sets the pack's materials up for URP.
//
// The materials ship for the Built-in Render Pipeline. This script only
// compiles when URP is installed (see the .asmdef beside it); when the active
// render pipeline is URP it converts every Built-in material inside the pack's
// folder with URP's own material upgraders, the same ones Unity's Render
// Pipeline Converter uses. Materials outside the pack are never touched.
// Run it again from Tools > BackRock/FPS Weapons Pack > Set Up Materials For URP.
//
// To go back to the Built-in Render Pipeline, reimport the pack from the
// Package Manager: the converted materials are replaced by the originals.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BackRock.Editor
{
    [InitializeOnLoad]
    public static class URPMaterialSetup
    {
        const string MenuPath = "Tools/BackRock/FPS Weapons Pack/Set Up Materials For URP";

        static URPMaterialSetup()
        {
            EditorApplication.delayCall += () => Apply(false);
        }

        [MenuItem(MenuPath)]
        static void ApplyFromMenu()
        {
            Apply(true);
        }

        static List<MaterialUpgrader> Upgraders()
        {
            return new List<MaterialUpgrader>
            {
                new StandardUpgrader("Standard"),
                new StandardUpgrader("Standard (Specular setup)"),
                new ParticleUpgrader("Particles/Standard Surface"),
                new ParticleUpgrader("Particles/Standard Unlit"),
                new ParticleUpgrader("Particles/VertexLit Blended"),
                new AutodeskInteractiveUpgrader("Autodesk Interactive"),
            };
        }

        static string PackRoot()
        {
            foreach (var guid in AssetDatabase.FindAssets("URPMaterialSetup t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                // <pack>/Editor/URPSetup/URPMaterialSetup.cs
                if (path.EndsWith("/Editor/URPSetup/URPMaterialSetup.cs") && path.Contains("FPS-WeaponsAnimations"))
                    return Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path))).Replace('\\', '/');
            }
            return null;
        }

        /// <summary>Converts the pack's Built-in materials when URP is active. Returns how many changed.</summary>
        public static int Apply(bool reportAlways)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
            {
                if (reportAlways)
                    Debug.Log("FPS Weapons Pack: the active render pipeline is not URP; materials left unchanged.");
                return 0;
            }
            var root = PackRoot();
            if (root == null) return 0;

            var upgraders = Upgraders();
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat")) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                var shader = material.shader.name;
                if (shader.StartsWith("Universal Render Pipeline/") || shader.StartsWith("Skybox/") || shader.StartsWith("UI/")
                    || shader.StartsWith("TextMeshPro/") || shader.StartsWith("Sprites/") || shader.StartsWith("Shader Graphs/"))
                    continue;
                if (shader.StartsWith("Unlit/"))
                {
                    ToUniversalUnlit(material, shader);
                }
                else
                {
                    MaterialUpgrader.Upgrade(material, upgraders, MaterialUpgrader.UpgradeFlags.None);
                    if (!material.shader.name.StartsWith("Universal Render Pipeline/")) continue;
                }
                EditorUtility.SetDirty(material);
                changed++;
            }

            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("FPS Weapons Pack: set up " + changed + " materials for URP.");
            }
            else if (reportAlways)
            {
                Debug.Log("FPS Weapons Pack: materials are already set up for URP.");
            }
            return changed;
        }

        // URP's upgraders for the legacy Unlit shaders are internal, so these
        // few are mapped by hand onto URP's Unlit shader.
        static void ToUniversalUnlit(Material m, string builtin)
        {
            var target = Shader.Find("Universal Render Pipeline/Unlit");
            if (target == null) return;
            var texture = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            var scale = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            var offset = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            var color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
            bool transparent = builtin == "Unlit/Transparent";
            bool cutout = builtin.Contains("Cutout");

            m.shader = target;
            m.SetTexture("_BaseMap", texture);
            m.SetTextureScale("_BaseMap", scale);
            m.SetTextureOffset("_BaseMap", offset);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", transparent ? 1f : 0f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            m.SetFloat("_Cutoff", cutoff);
            m.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            m.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", transparent ? 0f : 1f);
            m.SetOverrideTag("RenderType", transparent ? "Transparent" : cutout ? "TransparentCutout" : "Opaque");
            if (transparent) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (cutout) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = transparent ? (int)RenderQueue.Transparent : cutout ? (int)RenderQueue.AlphaTest : -1;
        }
    }
}
