using UnityEngine;

public class Treat : MonoBehaviour
{
    [SerializeField]
    [Min(0)]
    float treatOnFloorLifetime = 1.5f;

    Rigidbody rb;
    bool isActive;

    public void LaunchTreat(Transform launchPosition, float force)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        transform.position = launchPosition.position;
        transform.rotation = launchPosition.rotation;
        rb.velocity = Vector3.zero;
        rb.AddForce(force * launchPosition.forward, ForceMode.Impulse);
        isActive = true;
    }

    void DespawnTreat()
    {     
        gameObject.SetActive(false);
    }

    public void OnCollisionEnter(Collision other)
    {
        if (isActive && other.gameObject.GetComponentInParent<Treat>() == null
                && other.gameObject.GetComponentInParent<PlayerInput>() == null)
        {
            isActive = false;
            CatMovement cat = other.gameObject.GetComponentInParent<CatMovement>();
            if (cat != null)
            {
                if (!cat.isFullyFed)
                {
                    cat.FeedCat();
                    DespawnTreat();
                }
                else
                {
                    Invoke(nameof(DespawnTreat), treatOnFloorLifetime);
                }
            }
            else
            {
                Invoke(nameof(DespawnTreat), treatOnFloorLifetime);
            }
        }
    }
}