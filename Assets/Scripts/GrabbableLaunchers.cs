using System.Collections;
using UnityEngine;

public class GrabbableLaunchers : MonoBehaviour
{
    [SerializeField]
    Outline[] outlines;

    [SerializeField]
    Color hoverColour = Color.yellow;

    [SerializeField]
    float playerVisionVerticalOffset;

    [SerializeField]
    [Tooltip("Optional. Switched on while a hand is near (a glow, ring or any effect made in the editor).")]
    GameObject highlightEffect;

    [SerializeField]
    GameObject baseEffect;

    void Start()
    {
        if (outlines == null || outlines.Length == 0)
        {
            outlines = GetComponentsInChildren<Outline>();
        }

        if (outlines != null && outlines.Length > 0)
        {
            Highlight(false);
        }

        StartCoroutine(SetPositionRoutine());
    }

    // Up to the player's eye level, once the rig has it (it can take a few frames after start)
    IEnumerator SetPositionRoutine()
    {
        PlayerInput player = FindObjectOfType<PlayerInput>();
        float giveUpAt = Time.time + 5f;
        while ((player == null || player.eyePoint == null) && Time.time < giveUpAt)
        {
            yield return null;
            if (player == null)
            {
                player = FindObjectOfType<PlayerInput>();
            }
        }
        if (player == null || player.eyePoint == null)
        {
            yield break;
        }

        Vector3 position = transform.position;
        position.y = player.eyePoint.position.y + playerVisionVerticalOffset;
        transform.position = position;
    }

    public void Highlight(bool turnOn)
    {
        baseEffect.SetActive(turnOn);

        if (highlightEffect != null)
        {
            highlightEffect.SetActive(turnOn);
            baseEffect.SetActive(!turnOn);
        }

        foreach (Outline o in outlines)
        {
            if (o != null)
            {
                o.enabled = turnOn;

                if (turnOn)
                {
                    o.OutlineColor = hoverColour;
                }
            }
        }
    }
}
