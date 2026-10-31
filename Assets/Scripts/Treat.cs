using UnityEngine;

public class Treat : Launchable
{
    [SerializeField]
    [Min(0)]
    float treatOnFloorLifetime = 1.5f;

    [SerializeField]
    Outline instaKillOutline;

    bool instaKill;

    public bool isInstaKill
    {
        get
        {
            return instaKill;
        }
    }

    // Which food this treat is (its position in the treat pool), or -1 if not set
    public int foodVariation { get; set; } = -1;

    // "Clean shots" (Cat Cafe Rush): treats fly through furniture and only count when they reach a cat or a pickup.
    // Each physics step checks the whole stretch the treat just flew, so fast treats can't skip past a cat.
    public static bool cleanShots;

    // Light aim assist (clean shots only): a treat curves gently towards the point this returns, if any
    // (Cat Cafe Rush gives the nearest cat that wants this colour, when the shot is already close)
    public static System.Func<Treat, Vector3?> aimAssistTarget;
    public static float aimAssistTurnSpeed = 120f;   // degrees per second

    const float SweepRadius = 0.05f;

    Collider[] ownColliders;
    bool[] ownTriggerSettings;
    Vector3 lastPosition;

    public Vector3 velocity
    {
        get
        {
            if (rb != null)
            {
                return rb.velocity;
            }
            return Vector3.zero;
        }
    }

    // Which trigger pull fired it (a scatter or buckshot volley shares one), 0 = not set
    public int shotId { get; set; }

    // Disappears sooner than usual (buckshot is short-range)
    public void ShortenLife(float seconds)
    {
        CancelInvoke(nameof(Despawn));
        Invoke(nameof(Despawn), seconds);
    }

    public override void Launch(Transform launchPosition, float force, float launchAngle, bool instaKill)
    {
        base.Launch(launchPosition, force, launchAngle, instaKill);

        this.instaKill = instaKill;
        if (instaKillOutline != null)
        {
            instaKillOutline.enabled = instaKill;
        }

        // Clean shots don't bump into anything; the sweep below decides what they hit
        if (ownColliders == null)
        {
            ownColliders = GetComponentsInChildren<Collider>(true);
            ownTriggerSettings = new bool[ownColliders.Length];
            for (int i = 0; i < ownColliders.Length; i++)
            {
                ownTriggerSettings[i] = ownColliders[i].isTrigger;
            }
        }
        for (int i = 0; i < ownColliders.Length; i++)
        {
            ownColliders[i].isTrigger = cleanShots || ownTriggerSettings[i];
        }
        lastPosition = transform.position;
    }

    void FixedUpdate()
    {
        if (!cleanShots || !isActive || rb == null)
        {
            return;
        }

        // The transform, not rb.position: this project doesn't auto-sync transforms, so right after a launch the
        // rigidbody still holds the treat's old spot until the next physics step
        Vector3 now = transform.position;

        // Aim assist: turn a little towards a cat that wants this treat
        if (aimAssistTarget != null)
        {
            Vector3? target = aimAssistTarget(this);
            if (target.HasValue)
            {
                Vector3 speed = rb.velocity;
                Vector3 toTarget = (target.Value - now).normalized * speed.magnitude;
                rb.velocity = Vector3.RotateTowards(speed, toTarget, aimAssistTurnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
            }
        }

        // What did it fly through since the last step? The nearest cat or pickup on that stretch is hit.
        // (A jump far longer than one step's flight means it was moved, not flown: skip that stretch.)
        Vector3 travelled = now - lastPosition;
        float distance = travelled.magnitude;
        if (distance > 0.0001f && distance < 3f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(lastPosition, SweepRadius, travelled / distance, distance,
                                                      Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }
                if (CleanHit(hit))
                {
                    return;
                }
            }
        }
        lastPosition = now;
    }

    bool CleanHit(RaycastHit hit)
    {
        Collider other = hit.collider;
        CatMovement cat = other.GetComponentInParent<CatMovement>();
        if (cat != null)
        {
            if (cat.isFullyFed || !cat.takesTreats)
            {
                return false;   // flies on past
            }
            isActive = false;
            cat.HitByTreat(this);
            Despawn();
            return true;
        }
        Pickup pickup = other.GetComponentInParent<Pickup>();
        if (pickup != null)
        {
            isActive = false;
            pickup.OnPickup();
            pickup.gameObject.SetActive(false);
            Despawn();
            return true;
        }

        // Clean shots fly through the things that move (kittens, flying props, the vents' lids) but splat on the cafe
        // itself: walls, floor, furniture, props sitting on a pile, and anything squishable
        if (other.isTrigger || other.GetComponentInParent<TreatsPassThrough>() != null)
        {
            return false;
        }
        if (other.GetComponentInParent<PlayerInput>() != null || other.GetComponentInParent<Launchable>() != null)
        {
            return false;
        }
        FlyingProp prop = other.GetComponentInParent<FlyingProp>();
        if (prop != null && prop.state != FlyingProp.State.Home)
        {
            return false;
        }
        Squishable squishable = other.GetComponentInParent<Squishable>();
        if (prop == null && squishable == null && other.attachedRigidbody != null)
        {
            return false;
        }
        Splat(hit, squishable, prop);
        return true;
    }

    // Hit the cafe: a splat where it landed, the thing it hit reacts, and it's gone
    void Splat(RaycastHit hit, Squishable squishable, FlyingProp prop)
    {
        isActive = false;
        if (squishable != null)
        {
            squishable.Squish();
        }
        if (prop != null)
        {
            PropPile pile = prop.GetComponentInParent<PropPile>();
            if (pile != null)
            {
                pile.Rattle(0.6f);
            }
        }

        ParticleSystem splat = CafePrefabs.SplatPrefab;
        if (splat != null)
        {
            ParticleSystem made = Instantiate(splat, hit.point, Quaternion.LookRotation(hit.normal));
            Destroy(made.gameObject, made.main.duration + made.main.startLifetime.constantMax + 0.5f);
        }
        else if (ParticleManager.instance != null)
        {
            ParticleManager.instance.ShowParticleEffect(SplatParticleIndex, hit.point, 0.5f);
        }

        // One soft pap per moment, not one per pellet of a scatter shot
        if (treatHittingFloorSound.soundClip != null && Time.time >= nextSplatSound)
        {
            nextSplatSound = Time.time + 0.08f;
            AudioSource.PlayClipAtPoint(treatHittingFloorSound.soundClip, hit.point, 0.6f * SoundLevels.world);
        }
        Despawn();
    }

    // The cats' eating crumbs, small (the Particle Manager's pool), when no splat prefab is set
    const int SplatParticleIndex = 0;
    static float nextSplatSound;

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