using UnityEngine;

public class TreatBox : MonoBehaviour
{
    [SerializeField] 
    PlayerInput player;

    [SerializeField]
    Collider playerPrimaryHand;

    [SerializeField]
    Collider playerSecondaryHand;

    public void OnTriggerEnter(Collider other)
    {
        if (playerPrimaryHand != null && other == playerPrimaryHand)
        {
            player.HandInBox(true, true);
        }
        else if (playerSecondaryHand != null && other == playerSecondaryHand)
        {
            player.HandInBox(true, false);
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (playerPrimaryHand != null && other == playerPrimaryHand)
        {
            player.HandInBox(false, true);
        }
        else if (playerSecondaryHand != null && other == playerSecondaryHand)
        {
            player.HandInBox(false, false);
        }
    }
}
