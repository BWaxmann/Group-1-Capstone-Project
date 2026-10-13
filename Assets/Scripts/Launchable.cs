using UnityEngine;

public abstract class Launchable : MonoBehaviour
{
    [SerializeField]
    protected AudioSource audioSource;

    [SerializeField]
    protected SoundData treatHittingFloorSound = new SoundData(1f, 1f);

    protected Rigidbody rb;
    protected bool isActive;

    public virtual void Launch(Transform launchPosition, float force, bool instaKill)
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

    protected void Despawn()
    {
        gameObject.SetActive(false);
    }

    public abstract void OnCollisionEnter(Collision other);
}
