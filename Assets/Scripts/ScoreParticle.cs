using TMPro;
using UnityEngine;

public class ScoreParticle : MonoBehaviour
{
    [SerializeField]
    TMP_Text scoreText;

    [SerializeField]
    [Min(0)]
    float displayDuration = 0.8f;

    [SerializeField]
    Color plusScoreColour = Color.green;

    [SerializeField]
    Color minusScoreColour = Color.red;

    public void Initialise(float scoreValue, Vector3 position)
    {
        if (scoreValue > 0)
        {
            Show($"+{scoreValue}", true, position);
        }
        else if (scoreValue < 0)
        {
            Show(scoreValue.ToString(), false, position);
        }
        else
        {
            Disappear();
        }
    }

    // Any text, green for a gain and red for a loss (the waves game shows tips, e.g. "+$1.50")
    public void Show(string text, bool gain, Vector3 position)
    {
        scoreText.color = gain ? plusScoreColour : minusScoreColour;
        scoreText.text = text;
        transform.position = position;

        CancelInvoke(nameof(Disappear));
        Invoke(nameof(Disappear), displayDuration);
    }

    void Disappear()
    {
        gameObject.SetActive(false);
    }
}
