using System.Collections.Generic;
using UnityEngine;

public class ExplodingYarnball : Pickup
{
    [Header("Explosion")]
    [SerializeField]
    [Min(0)]
    int confettiParticleIndex = 2;

    [SerializeField]
    [Min(0)]
    float explosionRadius = 2f;

    [SerializeField]
    [Min(0)]
    float stunDuration = 10f;

    [SerializeField]
    bool randomiseYarnballMaterial;

    [SerializeField]
    Renderer yarnballRenderer;

    [SerializeField]
    Material[] yarnballMaterials;

    void OnEnable()
    {
        if (randomiseYarnballMaterial && yarnballMaterials != null && yarnballMaterials.Length > 0)
        {
            int index = Random.Range(0, yarnballMaterials.Length);
            if (yarnballMaterials[index] != null)
            {
                yarnballRenderer.material = yarnballMaterials[index];
            }
        }
    }

    public override void OnPickup()
    {
        ParticleManager.instance.ShowParticleEffect(confettiParticleIndex, transform.position);
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
