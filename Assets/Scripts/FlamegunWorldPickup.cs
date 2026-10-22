using UnityEngine;

/// <summary>
/// The chest flamegun mesh has no collider of its own, so the look ray misses it.
/// Colliders on the visible parts let it be taken and examined like other pickups.
/// </summary>
public class FlamegunWorldPickup : MonoBehaviour
{
    void Awake()
    {
        // The visible mesh stays on the default layer, same as the ammo in this chest.
        // Moving it onto the interact layer hides it from the view.
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;

            filter.gameObject.layer = 0;
        }
    }
}
