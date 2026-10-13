using UnityEngine;

public class Treat : Launchable
{
    [SerializeField]
    [Min(0)]
    float treatOnFloorLifetime = 1.5f;

    [SerializeField]
    Outline instaKillOutline;

    bool instaKill;

    public bool isInstaKill => instaKill;

    // Which food this treat is (its position in the treat pool), or -1 if not set
    public int foodVariation { get; set; } = -1;

    public override void Launch(Transform launchPosition, float force, bool instaKill)
    {
        base.Launch(launchPosition, force, instaKill);

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
            RestoreGravity();
            CatMovement cat = other.gameObject.GetComponentInParent<CatMovement>();
            if (cat != null)
            {
                if (!cat.isFullyFed)
                {
                    cat.HitByTreat(this);
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