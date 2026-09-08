using UnityEngine;

public class PinToFloor : MonoBehaviour
{
    [SerializeField]
    Transform referencePoint;

    [SerializeField]
    [Tooltip("This is the multiplier for how much the object scales per unit distance to the floor.")]
    float distanceScaling = 1f;

    [SerializeField]
    [Min(0)]
    float maxDistance = 10f;

    [SerializeField]
    Vector3 offset = new Vector3(0, 0.015f, 0);

    Vector3 initialScale;

    void Start()
    {
        initialScale = transform.localScale;
    }

    void Update()
    {
        if (Physics.Raycast(referencePoint.position, Vector3.down, out RaycastHit hit, maxDistance))
        {
            transform.position = hit.point + offset;
            transform.localScale = Mathf.Pow(distanceScaling, hit.distance) * initialScale;
        }
    }
}
