using UnityEngine;

public class ChangePointerMaterial : MonoBehaviour
{
    [SerializeField]
    Material material;

    void Start()
    {
        if (material != null)
        {
            transform.GetChild(0).GetChild(1).GetComponent<LineRenderer>().material = material;
        }
    }
}
