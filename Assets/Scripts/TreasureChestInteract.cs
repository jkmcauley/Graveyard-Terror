using UnityEngine;
using UHFPS.Runtime;

/// <summary>
/// Opens the chest once. After the lid finishes, the chest collider turns off
/// so an item placed inside can be picked up with the interact key.
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

        if (interactCollider != null)
            interactCollider.enabled = false;

        CustomInteractTitle title = GetComponent<CustomInteractTitle>();
        if (title != null)
            title.enabled = false;

        cleared = true;
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
