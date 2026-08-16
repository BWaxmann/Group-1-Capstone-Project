using UnityEngine;

public class Treat : MonoBehaviour
{
    [SerializeField]
    [Min(0)]
    float treatOnFloorLifetime = 1.5f;

    Rigidbody rb;
    bool isActive;

    public void AttachTreatToHand(Transform position)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.SetParent(position, false);
    }

    public void LaunchTreatFromHand(Vector3 direction, float force)
    {
        rb.isKinematic = false;
        transform.SetParent(null);
        rb.AddForce(force * direction.normalized, ForceMode.Impulse);
        isActive = true;
    }

    void DespawnTreat()
    {
        gameObject.SetActive(false);
    }

    public void OnCollisionEnter(Collision other)
    {
        if (isActive)
        {
            isActive = false;
            CatMovement cat = other.gameObject.GetComponentInParent<CatMovement>();
            if (cat != null)
            {
                if (!cat.isFed)
                {
                    cat.FeedCat();
                    gameObject.SetActive(false);
                }         
            }
            else
            {
                Invoke(nameof(DespawnTreat), treatOnFloorLifetime);
            }
        }
    }
}