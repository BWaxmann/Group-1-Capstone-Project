using UnityEngine;

public class GrabbableLaunchers : MonoBehaviour
{
    [SerializeField]
    Outline[] outlines;

    [SerializeField]
    Color hoverColour = Color.yellow;

    [SerializeField]
    float playerVisionVerticalOffset;

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

        Invoke(nameof(SetPosition), Time.deltaTime);
    }

    void SetPosition()
    {
        Vector3 position = transform.position;
        position.y = FindObjectOfType<PlayerInput>().eyePoint.position.y + playerVisionVerticalOffset;
        transform.position = position;
    }

    public void Highlight(bool turnOn)
    {
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
