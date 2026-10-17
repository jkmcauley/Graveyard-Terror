using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Marks the flamethrower Visual Effect. The pistol GunItem plays and stops it
/// from the same fire button that casts the shot.
/// </summary>
public class FlamegunStream : MonoBehaviour
{
    public VisualEffect flame;

    void Awake()
    {
        if (flame == null)
            flame = GetComponent<VisualEffect>();
        if (flame != null)
            flame.Stop();
    }
}
