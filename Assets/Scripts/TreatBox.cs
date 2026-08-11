using UnityEngine;
using UnityEngine.EventSystems;

public class TreatBox : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] 
    PlayerInput player;

    public void OnPointerEnter(PointerEventData eventData)
    {
        player.HandInBox(true, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        player.HandInBox(false, true);
    }
}
