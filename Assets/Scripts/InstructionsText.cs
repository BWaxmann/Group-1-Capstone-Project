using TMPro;
using UnityEngine;

public class InstructionsText : MonoBehaviour
{
    [SerializeField]
    TMP_Text textBox;

    [SerializeField]
    [TextArea]
    string editorInstructions;

    [SerializeField]
    [TextArea]
    string buildInstructions;

    void Start()
    {
#if UNITY_EDITOR
        textBox.text = editorInstructions;
#else
        textBox.text = buildInstructions;
#endif
    }

    void OnEnable()
    {
        GameManager.instance.StartCountdown += HideInstructions;
    }

    void OnDisable()
    {
        GameManager.instance.StartCountdown -= HideInstructions;
    }

    public void HideInstructions()
    {
        textBox.gameObject.SetActive(false);
    }
}
