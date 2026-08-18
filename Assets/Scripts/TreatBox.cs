using UnityEngine;

public class TreatBox : MonoBehaviour
{
    [SerializeField] 
    PlayerInput player;

    [SerializeField]
    Collider playerHand;

    public void OnTriggerEnter(Collider other)
    {
        if (playerHand != null && other == playerHand)
        {
            player.HandInBox(true);
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (playerHand != null && other == playerHand)
        {
            player.HandInBox(false);
        }
    }
}
