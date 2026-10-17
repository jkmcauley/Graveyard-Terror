using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class VampireAnimatorSetup
{
    const string PrefabPath = "Assets/Monsters and Undead - Horror Pack/Prefabs/Vampire.prefab";
    const string ControllerPath = "Assets/GraveyardTerror/Vampire/Vampire.controller";

    static readonly string[] OnceClips =
    {
        "Assets/Vampire_Boss_Set/Art/Animations/Vampire_Attack_LeftUnderswing.fbx",
        "Assets/Vampire_Boss_Set/Art/Animations/Vampire_Attack_RightUnderswing.fbx",
        "Assets/Vampire_Boss_Set/Art/Animations/Paired_Vampire_Float_Bite_Att.fbx",
        "Assets/Vampire_Boss_Set/Art/Animations/Vampire_Energy_EpicSpellCast.fbx",
        "Assets/Vampire_Boss_Set/Art/Animations/Vampire_Standing_To_Float.fbx",
        "Assets/Vampire_Boss_Set/Art/Animations/Vampire_Float_To_Standing.fbx",
        "Assets/Vampire_Boss_Set/Art/Animations/Vampire_Sleep_Horizontal_End.fbx"
    };

    static VampireAnimatorSetup()
    {
        EditorApplication.delayCall += Setup;
    }

    [MenuItem("Graveyard Terror/Setup Vampire Animator")]
    static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller == null)
            return;

        foreach (string clipPath in OnceClips)
            SetLoop(clipPath, false);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
            return;

        Animator animator = root.GetComponent<Animator>();
        if (animator == null)
            animator = root.GetComponentInChildren<Animator>(true);

        if (animator != null && (animator.runtimeAnimatorController != controller || animator.applyRootMotion))
        {
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }

        PrefabUtility.UnloadPrefabContents(root);
    }

    static void SetLoop(string path, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0 || clips[0].loopTime == loop)
            return;

        clips[0].loopTime = loop;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }
}
