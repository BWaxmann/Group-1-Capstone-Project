using UnityEngine;

public abstract class Launchable : MonoBehaviour
{
    [SerializeField]
    protected AudioSource audioSource;

    [SerializeField]
    protected SoundData treatHittingFloorSound = new SoundData(1f, 1f);

    [SerializeField]
    [Min(0)]
    [Tooltip("A straight shot that hits nothing disappears after this many seconds.")]
    protected float straightShotLifetime = 3f;

    protected Rigidbody rb;
    protected bool isActive;

    public virtual void Launch(Transform launchPosition, float force, bool instaKill)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        CancelInvoke(nameof(Despawn));
        rb.useGravity = true;
        transform.position = launchPosition.position;
        transform.rotation = launchPosition.rotation;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(force * launchPosition.forward, ForceMode.Impulse);
        isActive = true;
    }

    // Straight shots fly without gravity until they hit something, then drop normally
    public void MakeStraightShot()
    {
        if (rb == null) return;

        rb.useGravity = false;
        Invoke(nameof(Despawn), straightShotLifetime);
    }

    protected void RestoreGravity()
    {
        if (rb != null && !rb.useGravity)
        {
            rb.useGravity = true;
            CancelInvoke(nameof(Despawn));
        }
    }

    protected void Despawn()
    {
        gameObject.SetActive(false);
    }

    public abstract void OnCollisionEnter(Collision other);
}
