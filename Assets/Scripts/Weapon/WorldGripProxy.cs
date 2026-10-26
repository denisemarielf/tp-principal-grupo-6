using UnityEngine;

[DefaultExecutionOrder(100)] // después de WorldWeaponSway
public class WorldGripProxy : MonoBehaviour
{
    [SerializeField] private Transform rightProxy;   // hijo RightHandGrip
    [SerializeField] private Transform leftProxy;    // hijo LeftHandGrip
    [SerializeField] private Transform fallbackLeft; // LeftHandsRestPostition
    [SerializeField] private NetworkSpineAim spineAim;

    private Transform rightSource, leftSource;

    public void SetSources(GameObject worldModel)
    {
        rightSource = Find(worldModel.transform, "RightHandGrip");
        leftSource = Find(worldModel.transform, "LeftHandGrip")
                   ?? Find(worldModel.transform, "LeftHandRestPoint")
                   ?? fallbackLeft;
    }

    private void Update()
    {
        if (spineAim != null) spineAim.UndoPitch();
        Copy(rightSource, rightProxy);
        Copy(leftSource, leftProxy);
        if (spineAim != null) spineAim.RedoPitch();
    }

    private static void Copy(Transform src, Transform dst)
    {
        if (src == null || dst == null) return;
        dst.SetPositionAndRotation(src.position, src.rotation);
    }

    private static Transform Find(Transform parent, string name)
    {
        foreach (Transform c in parent)
        {
            if (c.name == name) return c;
            var f = Find(c, name);
            if (f != null) return f;
        }
        return null;
    }
}