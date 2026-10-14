using UnityEngine;

public abstract class Launchable : MonoBehaviour
{
    [SerializeField]
    protected AudioSource audioSource;

    [SerializeField]
    protected SoundData treatHittingFloorSound = new SoundData(1f, 1f);

    [SerializeField]
    [Min(0)]
    protected float initialOffset = 0.2f;

    protected Rigidbody rb;
    protected bool isActive;

    public virtual void Launch(Transform launchPosition, float force, float launchAngle, bool instaKill)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        Quaternion rotation = Quaternion.AngleAxis(launchAngle, launchPosition.up);
        Vector3 direction = rotation * launchPosition.forward;
        transform.position = launchPosition.position + initialOffset * direction;
        transform.rotation = rotation * launchPosition.rotation;
        rb.velocity = Vector3.zero;
        rb.AddForce(force * direction, ForceMode.Impulse);
        isActive = true;
    }

    protected void Despawn()
    {
        gameObject.SetActive(false);
    }

    public abstract void OnCollisionEnter(Collision other);
}
