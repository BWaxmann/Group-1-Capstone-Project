using UnityEngine;

public class GrabbableLaunchers : MonoBehaviour
{
    [SerializeField]
    Outline[] outlines;

    [SerializeField]
    Color hoverColour = Color.yellow;

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
