using UnityEngine;
using UHFPS.Runtime;

/// <summary>
/// Opens the chest once. After the lid finishes, chest colliders turn off
/// so the item inside can be taken and examined like any other pickup.
/// </summary>
public class TreasureChestInteract : MonoBehaviour
{
    [SerializeField] DynamicObject chest;
    [SerializeField] Collider interactCollider;

    bool cleared;
    float openedAt = -1f;

    void Awake()
    {
        if (chest == null)
            chest = GetComponent<DynamicObject>();

        if (interactCollider == null)
            interactCollider = GetComponent<Collider>();
    }

    void Update()
    {
        if (cleared || chest == null || !chest.IsOpened)
        {
            openedAt = -1f;
            return;
        }

        if (openedAt < 0f)
            openedAt = Time.time;

        if (!LidIsOpen() && Time.time - openedAt < 1.2f)
            return;

        chest.isInteractLocked = true;
        ClearBlockers();
        cleared = true;
    }

    void ClearBlockers()
    {
        if (interactCollider != null)
            interactCollider.enabled = false;

        foreach (Collider blocker in GetComponentsInChildren<Collider>(true))
        {
            if (blocker == null || blocker.GetComponent<InteractableItem>() != null)
                continue;

            blocker.enabled = false;
        }

        CustomInteractTitle title = GetComponent<CustomInteractTitle>();
        if (title != null)
            title.enabled = false;
    }

    bool LidIsOpen()
    {
        Animator animator = chest.animator;
        if (animator == null || animator.runtimeAnimatorController == null || animator.layerCount == 0)
            return false;

        if (animator.IsInTransition(0))
            return false;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool openState = state.IsName("Open") || state.IsName("Base Layer.Open");
        return openState && state.normalizedTime >= 0.9f;
    }
}
