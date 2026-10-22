using UnityEngine;
using UHFPS.Runtime;

public class CrossbowLoadedBolt : MonoBehaviour
{
    public GameObject arrowPrefab;

    const float MeshLength = 0.22f;

    private Animator anim;
    private GameObject nocked;
    private Transform packArrow;
    private Transform barrel;

    public bool HasBolt => nocked != null && nocked.activeInHierarchy;
    public Vector3 BoltPosition => nocked != null ? nocked.transform.position : transform.position;
    public Vector3 BoltDirection => nocked != null ? nocked.transform.forward : transform.forward;
    public Vector3 BoltWorldScale => nocked != null ? nocked.transform.lossyScale : Vector3.one;

    private GameObject stuckBolt;

    public void StickIn(Transform parent, Vector3 point, Vector3 direction)
    {
        if (arrowPrefab == null || parent == null)
            return;

        if (stuckBolt != null)
            Destroy(stuckBolt);

        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        const float stuckScale = 1.15f;
        const float exposed = 0.045f;
        Transform bone = ClosestBodyBone(parent, point);

        stuckBolt = Instantiate(arrowPrefab);
        Bullet flying = stuckBolt.GetComponent<Bullet>();
        if (flying != null)
            Destroy(flying);

        stuckBolt.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        stuckBolt.transform.position = point - dir * exposed;
        stuckBolt.transform.localScale = Vector3.one * stuckScale;
        stuckBolt.transform.SetParent(bone, true);
    }

    static Transform ClosestBodyBone(Transform body, Vector3 point)
    {
        Transform best = body;
        float bestDistance = float.PositiveInfinity;
        Transform[] bones = body.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            string boneName = bones[i].name;
            if (boneName.IndexOf("Chest", System.StringComparison.OrdinalIgnoreCase) < 0
                && boneName.IndexOf("Spine", System.StringComparison.OrdinalIgnoreCase) < 0
                && boneName.IndexOf("Hips", System.StringComparison.OrdinalIgnoreCase) < 0
                && boneName.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) < 0
                && boneName.IndexOf("Neck", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            float distance = (bones[i].position - point).sqrMagnitude;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = bones[i];
        }

        return best;
    }

    private void Start()
    {
        anim = GetComponent<Animator>();
        packArrow = FindDeep(transform, "CROSS_BOW_FINAL_RIG:ARROW");
        barrel = FindDeep(transform, "CROSS_BOW_FINAL_RIG:BARREL");
        if (arrowPrefab == null || packArrow == null)
            return;

        nocked = Instantiate(arrowPrefab, transform);
        Bullet bullet = nocked.GetComponent<Bullet>();
        if (bullet != null)
            Destroy(bullet);

        PlaceNockedBolt();
    }

    private void LateUpdate()
    {
        if (packArrow != null)
            packArrow.localScale = Vector3.zero;

        if (nocked == null || anim == null)
            return;

        bool inFlight = anim.GetCurrentAnimatorStateInfo(0).IsName("CROSSBOW-Shoot");
        if (nocked.activeSelf == inFlight)
            nocked.SetActive(!inFlight);

        if (nocked.activeSelf)
            PlaceNockedBolt();
    }

    private void PlaceNockedBolt()
    {
        Camera cam = Camera.main;
        Vector3 aim = cam != null ? cam.transform.forward : transform.forward;
        Vector3 up = cam != null ? cam.transform.up : transform.up;
        Vector3 rail = barrel != null ? barrel.position - packArrow.position : aim;
        if (Vector3.Dot(rail, aim) < 0f)
            rail = -rail;
        if (rail.sqrMagnitude < 0.000001f)
            rail = aim;

        Vector3 dir = rail.normalized;
        Vector3 mid = barrel != null ? (barrel.position + packArrow.position) * 0.5f : packArrow.position;
        float scale = 0.42f;

        nocked.transform.rotation = Quaternion.LookRotation(dir, up);
        nocked.transform.position = mid - dir * (MeshLength * scale * 0.45f + 0.012f) + up * 0.008f;

        Vector3 parentScale = transform.lossyScale;
        nocked.transform.localScale = new Vector3(
            scale / Mathf.Max(0.0001f, parentScale.x),
            scale / Mathf.Max(0.0001f, parentScale.y),
            scale / Mathf.Max(0.0001f, parentScale.z));
    }

    private static Transform FindDeep(Transform root, string boneName)
    {
        if (root.name == boneName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), boneName);
            if (found != null)
                return found;
        }

        return null;
    }
}
