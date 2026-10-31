using UnityEngine;
using UnityEngine.UI;

public class DefendPoint : MonoBehaviour
{
    [SerializeField]
    [Min(0)]
    int hitScorePenalty = 1;

    [SerializeField]
    Transform scoreDisplayPosition;

    [SerializeField]
    Transform defendObject;

    [SerializeField]
    [Min(0)]
    int hitParticlesIndex = 0;

    [SerializeField]
    SoundData hitSound = new SoundData(1f, 1f);

    [SerializeField]
    AudioSource hitSource;

    [SerializeField]
    GameObject shield;

    [SerializeField]
    SoundData shieldHitSound = new SoundData(1f, 1f);

    [Header("Health Bar")]

    [SerializeField]
    Slider healthBar;

    [SerializeField]
    [Min(0)]
    float healthLossPerHit = 0.01f;

    [SerializeField]
    [Min(0)]
    float minimumDisplayedHealth = 0.165f;

    float shieldTimer;

    public Vector3 position
    {
        get
        {
            return transform.position;
        }
    }
    public bool shieldActive
    {
        get
        {
            return shieldTimer > 0;
        }
    }

    // Raised each time a cat's paw lands on this point while it's unshielded (Cat Chaos props listen for it)
    public event System.Action Hit;

    void Start()
    {
        healthBar.value = 1;
        RemoveShield();
        GameManager.instance.RegisterDefendPoint(this);
    }

    void Update()
    {
        if (shieldActive)
        {
            shieldTimer -= Time.deltaTime;
            if (shieldTimer <= 0)
            {
                RemoveShield();
            }
        }
    }

    public void GetHit()
    {
        if (shieldActive)
        {
            shieldHitSound.PlayOneShot(hitSource);
        }
        else
        {
            // A penalty of 0 shows nothing (rather than a "0" popping up on every hit)
            if (hitScorePenalty > 0)
            {
                GameManager.instance.ReceivePenalty(hitScorePenalty, scoreDisplayPosition.position, false);
            }
            Hit?.Invoke();
            ParticleManager.instance.ShowParticleEffect(hitParticlesIndex, defendObject != null ? defendObject.position : position);
            hitSound.PlayOneShot(hitSource);
            healthBar.value = Mathf.Max(healthBar.value * (1 - healthLossPerHit), minimumDisplayedHealth);
        }
    }

    public void DeployShield(float duration)
    {
        shieldTimer += duration;
        shield?.SetActive(true);
    }

    void RemoveShield()
    {
        shieldTimer = 0;
        shield?.SetActive(false);
    }
}
