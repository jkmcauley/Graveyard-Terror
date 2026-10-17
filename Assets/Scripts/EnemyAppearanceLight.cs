using GraveyardTerror;
using UHFPS.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Marks enemy meshes for the Enemy rendering layer so EnemyMoonlight can
/// reach them without lighting the cemetery. That layer is the 4th entry in
/// ProjectSettings/TagManager (bit 3).
/// </summary>
public static class EnemyAppearanceLight
{
    public const uint LayerBit = 1u << 3;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void HookSceneLoads()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToSceneEnemies();
    }

    static void ApplyToSceneEnemies()
    {
        NPCHealth[] health = Object.FindObjectsByType<NPCHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < health.Length; i++)
            Apply(health[i].gameObject);

        Vampire[] vampires = Object.FindObjectsByType<Vampire>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < vampires.Length; i++)
            Apply(vampires[i].gameObject);

        BatFaceScare[] scares = Object.FindObjectsByType<BatFaceScare>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < scares.Length; i++)
            Apply(scares[i].gameObject);
    }

    public static void Apply(GameObject enemy)
    {
        if (enemy == null)
            return;

        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer)
                continue;

            renderer.renderingLayerMask |= LayerBit;
        }
    }
}
