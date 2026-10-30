using UnityEngine;

public class ChangePointerMaterial : MonoBehaviour
{
    [SerializeField]
    Material material;

    void Start()
    {
        if (material == null) return;
        // The pointer line: the second child of the first child (its layout changed once; find it rather than assume)
        LineRenderer line = transform.childCount > 0 && transform.GetChild(0).childCount > 1
            ? transform.GetChild(0).GetChild(1).GetComponent<LineRenderer>()
            : GetComponentInChildren<LineRenderer>(true);
        if (line != null) line.material = material;
    }
}
