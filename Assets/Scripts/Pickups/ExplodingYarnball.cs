using System.Collections.Generic;
using UnityEngine;

public class ExplodingYarnball : Pickup
{
    [SerializeField]
    [Min(0)]
    int confettiParticleIndex = 2;

    [SerializeField]
    [Min(0)]
    float explosionRadius = 2f;

    [SerializeField]
    [Min(0)]
    float stunDuration = 10f;

    public override void OnPickup()
    {
        ParticleManager.instance.ShowParticleEffect(2, transform.position);
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        List<CatMovement> catsConsidered = new List<CatMovement>();
        foreach (Collider collider in colliders)
        {
            CatMovement cat = collider.GetComponentInParent<CatMovement>();
            if (cat != null && !catsConsidered.Contains(cat))
            {
                catsConsidered.Add(cat);
                cat.GetStunned(stunDuration);
            }
        }
    }
}
