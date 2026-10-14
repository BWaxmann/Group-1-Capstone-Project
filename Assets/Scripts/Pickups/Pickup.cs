using System.Collections;
using UnityEngine;

public abstract class Pickup : MonoBehaviour
{
    [SerializeField]
    [Min(0)]
    protected float despawnTime = 30f;

    protected PlayerInput player;
    protected Renderer rend;

    protected float despawnTimer;

    protected virtual void Start()
    {
        player = FindObjectOfType<PlayerInput>();
        rend = GetComponentInChildren<Renderer>();
    }

    public virtual void Initialise()
    {
        StartCoroutine(DespawnRoutine());
    }

    protected virtual IEnumerator DespawnRoutine()
    {
        yield return new WaitForSeconds(despawnTime);
        Despawn();
    }

    public abstract void OnPickup();

    protected virtual void Despawn()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }
}
