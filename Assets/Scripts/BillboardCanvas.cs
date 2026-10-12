using UnityEngine;

public class BillboardCanvas : MonoBehaviour
{
    void OnEnable()
    {
        PlayerInput.TurnToFaceCamera += UpdateBillboard;
    }

    void OnDisable()
    {
        PlayerInput.TurnToFaceCamera -= UpdateBillboard;
    }

    public void UpdateBillboard(Vector3 position)
    {
        transform.forward = (transform.position - position).normalized;
    }
}