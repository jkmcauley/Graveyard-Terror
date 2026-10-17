using UnityEngine;

/// <summary>
/// Walk into this trigger to take pockets into the next level.
/// Put it on an object with a trigger collider.
/// </summary>
public class GraveyardNextLevel : MonoBehaviour
{
    public void Go()
    {
        var flow = FindAnyObjectByType<GraveyardLevelFlow>();
        if (flow != null)
            flow.ContinueToNextLevel();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            Go();
    }
}
