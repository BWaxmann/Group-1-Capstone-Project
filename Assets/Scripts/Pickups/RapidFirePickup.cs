using UnityEngine;

// Lets the player hold the trigger for rapid fire for a while.
public class RapidFirePickup : Pickup
{
    [Header("Pickup settings")]

    [SerializeField]
    [Min(0)]
    float effectDuration = 8f;

    [SerializeField]
    [Tooltip("Tints the pickup so it stands out from the others.")]
    Color tint = new Color(1f, 0.45f, 0.05f);

    protected override void Start()
    {
        base.Start();

        if (rend != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);
            block.SetColor("_Color", tint);
            rend.SetPropertyBlock(block);
        }
    }

    public override void OnPickup()
    {
        player.EnableRapidFire(effectDuration);
    }
}
