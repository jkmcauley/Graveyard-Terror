using UnityEngine;
using UHFPS.Runtime;

/// <summary>
/// Look-at interact for a first aid kit. Use restores the player to max health.
/// The collider and this script must be on the same object, on the Interact layer.
/// The collider must not be a trigger; the interact ray ignores triggers.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FirstAidKitInteract : MonoBehaviour, IInteractStart
{
    bool used;

    public void InteractStart()
    {
        if (used)
            return;

        PlayerHealth health = PlayerPresenceManager.Instance.Player.GetComponentInChildren<PlayerHealth>();
        if (health == null || health.IsDead)
            return;

        health.ApplyHeal(health.MaxEntityHealth);
        used = true;
        gameObject.SetActive(false);
    }
}
