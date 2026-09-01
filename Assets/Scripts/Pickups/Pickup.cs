using System.Collections;
using UnityEngine;

public abstract class Pickup : MonoBehaviour
{
    [SerializeField]
    [Min(0)]
    protected float moveToPlayerDelay = 1f;

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
    protected float stoppingDistance = 2f;

    protected PlayerInput player;
    protected Renderer rend;

    protected float currentSpeed;

    protected virtual void Start()
    {
        player = FindObjectOfType<PlayerInput>();
        rend = GetComponentInChildren<Renderer>();
    }

    public virtual void Initialise()
    {
        currentSpeed = 0;
        StartCoroutine(MoveToPlayer());
    }

    public abstract void OnPickup();

    protected virtual IEnumerator MoveToPlayer()
    {
        yield return new WaitForSeconds(moveToPlayerDelay);

        while (Vector3.Distance(player.pickupMagnet, transform.position) > stoppingDistance)
        {
            float targetSpeed = rend.isVisible ? fastMovementSpeed : slowMovementSpeed;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, Time.deltaTime * acceleration);

            Vector3 direction = (player.pickupMagnet - transform.position).normalized;
            transform.Translate(currentSpeed * Time.deltaTime * direction);

            yield return null;
        }

        OnPickup();
        gameObject.SetActive(false);
    }
}
