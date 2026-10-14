using UnityEngine;

public class Treat : Launchable
{
    [SerializeField]
    [Min(0)]
    float treatOnFloorLifetime = 1.5f;

    [SerializeField]
    Outline instaKillOutline;

    bool instaKill;

    public override void Launch(Transform launchPosition, float force, float launchAngle, bool instaKill)
    {
        base.Launch(launchPosition, force, launchAngle, instaKill);

        this.instaKill = instaKill;
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
            CatMovement cat = other.gameObject.GetComponentInParent<CatMovement>();
            if (cat != null)
            {
                if (!cat.isFullyFed)
                {
                    cat.FeedCat(instaKill);
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
                    pickup.OnPickup();
                    pickup.gameObject.SetActive(false);
                    Despawn();
                }
                else
                {
                    if (instaKillOutline != null)
                    {
                        instaKillOutline.enabled = false;
                    }
                    if (audioSource != null && treatHittingFloorSound.soundClip != null)
                    {
                        treatHittingFloorSound.PlayOneShot(audioSource);
                    }
                    Invoke(nameof(Despawn), treatOnFloorLifetime);
                }
            }
        }
    }
}