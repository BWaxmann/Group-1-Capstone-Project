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
        Initialise(scoreValue, position, null);
    }

    // label is shown above the score, e.g. "PURR-FECT!"
    public void Initialise(float scoreValue, Vector3 position, string label)
    {
        if (scoreValue > 0)
        {
            scoreText.color = plusScoreColour;
            scoreText.text = string.IsNullOrEmpty(label) ? $"+{scoreValue}" : $"{label}\n+{scoreValue}";
        }
        else if (scoreValue < 0)
        {
            scoreText.color = minusScoreColour;
            scoreText.text = scoreValue.ToString();
        }
        else
        {
            Disappear();
            return;
        }

        transform.position = position;

        Invoke(nameof(Disappear), displayDuration);
    }

    void Disappear()
    {
        gameObject.SetActive(false);
    }
}
