using UnityEngine;

public class Treat : Launchable
{
    [SerializeField]
    [Min(0)]
    float treatOnFloorLifetime = 1.5f;

    [SerializeField]
    Outline instaKillOutline;

    [SerializeField]
    [Min(1)]
    [Tooltip("How many bites a Perfect (on-beat) shot counts as.")]
    int perfectShotBites = 2;

    bool instaKill;
    BeatClock.ShotQuality quality = BeatClock.ShotQuality.Good;

    // How close to the beat this treat was fired. Set by PlayerInput after launching.
    public BeatClock.ShotQuality shotQuality { set { quality = value; } }

    public override void Launch(Transform launchPosition, float force, bool instaKill)
    {
        base.Launch(launchPosition, force, instaKill);

        this.instaKill = instaKill;
        quality = BeatClock.ShotQuality.Good;
        if (instaKillOutline != null)
        {
            instaKillOutline.enabled = instaKill;
        }
    }

    public override void OnCollisionEnter(Collision other)
    {
        if (isActive && other.gameObject.GetComponentInParent<Launchable>() == null
                && other.gameObject.GetComponentInParent<PlayerInput>() == null)
        {
            isActive = false;
            RestoreGravity();
            CatMovement cat = other.gameObject.GetComponentInParent<CatMovement>();
            if (cat != null)
            {
                if (!cat.isFullyFed)
                {
                    GameManager.instance.RegisterHit(quality, transform.position);
                    cat.FeedCat(instaKill, quality == BeatClock.ShotQuality.Perfect ? perfectShotBites : 1);
                    Despawn();
                }
                else
                {
                    isActive = true;
                }
            }
            else
            {
                Pickup pickup = other.gameObject.GetComponentInParent<Pickup>();
                if (pickup != null)
                {
                    if (pickup.activationMethod == Pickup.ActivationType.Shoot)
                    {
                        pickup.OnPickup();
                        pickup.gameObject.SetActive(false);
                        Despawn();
                    }
                    else
                    {
                        isActive = true;
                    }
                }
                else
                {
                    Invoke(nameof(Despawn), treatOnFloorLifetime);
                    if (audioSource != null && treatHittingFloorSound.soundClip != null)
                    {
                        treatHittingFloorSound.PlayOneShot(audioSource);
                    }
                }
            }
        }
    }
}