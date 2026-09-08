using System.Collections;
using UnityEngine;

public abstract class Pickup : MonoBehaviour
{
    [SerializeField]
    protected ActivationType _activationMethod;

    [SerializeField]
    [Min(0)]
    protected float activationDelay = 1f;

    [SerializeField]
    [Min(0)]
    protected float despawnTime = 30f;

    [Header("Move To Player Settings")]
    [SerializeField]
    [Min(0)]
    protected float slowMovementSpeed = 1f;

    [SerializeField]
    [Min(0)]
    protected float fastMovementSpeed = 5f;

    [SerializeField]
    [Min(0)]
    protected float acceleration = 3f;

    [SerializeField]
    [Min(0)]
    protected float stoppingDistance = 1f;

    protected PlayerInput player;
    protected Renderer rend;

    protected float currentSpeed;
    protected float despawnTimer;

    public ActivationType activationMethod => _activationMethod;

    protected virtual void Start()
    {
        player = FindObjectOfType<PlayerInput>();
        rend = GetComponentInChildren<Renderer>();
    }

    public virtual void Initialise()
    {
        currentSpeed = 0;
        if (_activationMethod != ActivationType.Shoot) // Logic for activating on being shot is in the Treat script.
        {
            StartCoroutine(PrepareToActivate());
        }
        else
        {
            despawnTimer = despawnTime;
        }
    }

    protected virtual void Update()
    {
        if (_activationMethod == ActivationType.Shoot && despawnTimer > 0)
        {
            despawnTimer -= Time.deltaTime;
            if (despawnTimer <= 0)
            {
                Despawn();
            }
        }
    }

    public abstract void OnPickup();

    protected virtual IEnumerator PrepareToActivate()
    {
        yield return new WaitForSeconds(activationDelay);

        if (_activationMethod == ActivationType.MoveToPlayer)
        {
            while (Vector3.Distance(player.pickupMagnet, transform.position) > stoppingDistance)
            {
                float targetSpeed = rend.isVisible ? fastMovementSpeed : slowMovementSpeed;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, Time.deltaTime * acceleration);

                Vector3 direction = (player.pickupMagnet - transform.position).normalized;
                transform.Translate(currentSpeed * Time.deltaTime * direction, Space.World);

                yield return null;
            }
        }

        OnPickup();
        Despawn();
    }

    protected virtual void Despawn()
    {
        gameObject.SetActive(false);
    }

    public enum ActivationType
    {
        Instant,
        MoveToPlayer,
        Shoot
    }
}
