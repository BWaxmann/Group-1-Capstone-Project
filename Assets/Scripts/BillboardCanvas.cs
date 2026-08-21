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

    public void UpdateBillboard(Quaternion rotation)
    {
        transform.rotation = rotation;
    }
}