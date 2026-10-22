using UnityEngine;

public class ArrowBoltVisual : MonoBehaviour
{
    private void Awake()
    {
        MeshFilter filter = GetComponentInChildren<MeshFilter>();
        if (filter == null)
            return;

        MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.allowOcclusionWhenDynamic = false;

        filter.sharedMesh = ArrowMeshData.Create();
        filter.transform.localPosition = Vector3.zero;
        filter.transform.localRotation = Quaternion.identity;
        filter.transform.localScale = new Vector3(2f, 2f, 1f);
    }
}
