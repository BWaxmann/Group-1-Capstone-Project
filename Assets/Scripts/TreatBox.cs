using UnityEngine;

public class TreatBox : MonoBehaviour
{
    [SerializeField] 
    PlayerInput player;

    [SerializeField]
    Collider playerHand;

    [Header("Outline")]

    [SerializeField]
    Outline outline;

    [SerializeField]
    Color handInBoxColour = Color.yellow;

    void Start()
    {
        if (outline == null)
        {
            outline = GetComponentInChildren<Outline>();
        }

        if (outline != null)
        {
            outline.OutlineColor = handInBoxColour;
            outline.enabled = false;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (playerHand != null && other == playerHand)
        {
            player.HandInBox(true);
            EnableOutline(true);
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (playerHand != null && other == playerHand)
        {
            player.HandInBox(false);
            EnableOutline(false);
        }
    }

    void EnableOutline(bool enabled)
    {
        if (outline != null)
        {
            outline.enabled = enabled;
        }
    }
}
