using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class FlameThrowerVfx : MonoBehaviour
{
    public VisualEffect flame_Thrower;
    void Start()
    {
        flame_Thrower.Stop();
   
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            flame_Thrower.Play();
        }
        if (Input.GetMouseButtonUp(0))
        {
            flame_Thrower.Stop();
        }
    }
}
