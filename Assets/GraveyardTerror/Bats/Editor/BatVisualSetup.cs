using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GraveyardTerror.Editor
{
    [InitializeOnLoad]
    public static class BatVisualSetup
    {
        const string FbxPath = "Assets/Imported Asset/Vampire Bats/Vampire_Bat_Native.fbx";
        const string TextureDir = "Assets/Imported Asset/Vampire Bats/Textures";
        const string MaterialPath = "Assets/GraveyardTerror/Bats/Bat.mat";
        const string ControllerPath = "Assets/GraveyardTerror/Bats/BatFly.controller";
        const string FlyingPrefabPath = "Assets/GraveyardTerror/Bats/FlyingBat.prefab";
        const string SwarmPrefabPath = "Assets/GraveyardTerror/Bats/BatSwarm.prefab";
        const float TargetSize = 4f;

        static BatVisualSetup()
        {
            EditorApplication.delayCall += TryRun;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += TryRun;
        }

        [MenuItem("Graveyard/Setup Bat Visuals")]
        public static void TryRun()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRun;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (IsReady())
                return;

            int attempts = SessionState.GetInt("Graveyard.BatVisualSetup.Attempts3", 0);
            if (attempts >= 2)
                return;

            SessionState.SetInt("Graveyard.BatVisualSetup.Attempts3", attempts + 1);

            try
            {
                Setup();
                Debug.Log("Bat skin and fly animation are ready. Press Play again.");
            }
            catch (System.Exception exception)
            {
                Debug.LogError("Bat visual setup failed: " + exception);
            }
        }

        static void Setup()
        {
            string baseColorPath = FindTexture("base_color");
            string normalPath = FindTexture("normal");
            if (string.IsNullOrEmpty(baseColorPath) || string.IsNullOrEmpty(normalPath))
                throw new FileNotFoundException("Bat textures were not found in " + TextureDir);

            ConfigureTexture(baseColorPath, false);
            ConfigureTexture(normalPath, true);

            Material material = CreateMaterial(baseColorPath, normalPath);
            RemapModelMaterial(material);
            AnimationClip flyClip = ConfigureFlyClip();
            RuntimeAnimatorController controller = CreateController(flyClip);
            Avatar avatar = LoadAvatar();
            float modelScale = AttachModel(FlyingPrefabPath, null, controller, material, avatar);
            AttachModel(SwarmPrefabPath, "FaceBat", controller, material, avatar, modelScale);
            AssetDatabase.SaveAssets();
        }

        static string FindTexture(string token)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureDir });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.ToLowerInvariant().Contains(token))
                    return path;
            }

            return null;
        }

        static void ConfigureTexture(string path, bool normalMap)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        static Material CreateMaterial(string baseColorPath, string normalPath)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new MissingReferenceException("URP Lit shader was not found.");

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseColorPath));
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(baseColorPath));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.SetFloat("_BumpScale", 1f);
            material.SetFloat("_Smoothness", 0.25f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_NORMALMAP");
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void RemapModelMaterial(Material material)
        {
            ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
                throw new FileNotFoundException("Bat model was not found at " + FbxPath);

            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Mat_Vampire_Bat"), material);

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Material source)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), material);
            }

            importer.SaveAndReimport();
        }

        static AnimationClip ConfigureFlyClip()
        {
            ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.clipAnimations;

            for (int i = 0; i < clips.Length; i++)
            {
                string source = (clips[i].name + " " + clips[i].takeName).ToLowerInvariant();
                if (!source.Contains("fly"))
                    continue;

                clips[i].name = "Fly";
                clips[i].loopTime = true;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            if (clips != null && clips.Length > 0)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            AnimationClip fallback = null;
            for (int i = 0; i < assets.Length; i++)
            {
                AnimationClip clip = assets[i] as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview"))
                    continue;

                if (clip.name == "Fly")
                    return clip;

                fallback = clip;
            }

            if (fallback == null)
                throw new MissingReferenceException("The bat FBX has no animation clip.");

            return fallback;
        }

        static RuntimeAnimatorController CreateController(AnimationClip flyClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState flyState = null;
            ChildAnimatorState[] states = machine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state.name == "Fly")
                    flyState = states[i].state;
            }

            if (flyState == null)
                flyState = machine.AddState("Fly");

            flyState.motion = flyClip;
            machine.defaultState = flyState;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static Avatar LoadAvatar()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Avatar avatar)
                    return avatar;
            }

            return null;
        }

        static float AttachModel(string prefabPath, string parentName, RuntimeAnimatorController controller, Material material, Avatar avatar, float forcedScale = 0f)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform parent = contents.transform;
                if (!string.IsNullOrEmpty(parentName))
                {
                    Transform found = contents.transform.Find(parentName);
                    if (found == null)
                        throw new MissingReferenceException(parentName + " is missing on " + prefabPath);
                    parent = found;
                }
                else
                {
                    contents.transform.localScale = Vector3.one;
                }

                for (int i = parent.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(parent.GetChild(i).gameObject);

                GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
                model.name = "BatModel";
                model.transform.SetParent(parent, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                float scale = forcedScale > 0f ? forcedScale : FitScale(model);
                model.transform.localScale = Vector3.one * scale;

                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Material[] slots = renderers[i].sharedMaterials;
                    for (int slot = 0; slot < slots.Length; slot++)
                        slots[slot] = material;
                    renderers[i].sharedMaterials = slots;
                }

                Animator animator = model.GetComponent<Animator>();
                if (animator == null)
                    animator = model.GetComponentInChildren<Animator>();
                if (animator == null)
                    animator = model.AddComponent<Animator>();

                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                return scale;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static float FitScale(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return 1f;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest < 0.001f)
                return 1f;

            return TargetSize / largest;
        }

        static bool IsReady()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlyingPrefabPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (prefab == null || material == null || controller == null)
                return false;

            Renderer renderer = prefab.GetComponentInChildren<Renderer>(true);
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            return renderer != null
                && renderer.sharedMaterial == material
                && animator != null
                && animator.avatar != null
                && animator.runtimeAnimatorController == controller;
        }
    }
}
