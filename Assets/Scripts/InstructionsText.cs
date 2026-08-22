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

    [SerializeField]
    bool hideOnGameStart;

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
        if (hideOnGameStart)
        {
            GameManager.instance.GameStart += HideInstructions;
        }
    }

    void OnDisable()
    {
        if (hideOnGameStart)
        {
            GameManager.instance.GameStart -= HideInstructions;
        }
    }

    public void HideInstructions()
    {
        textBox.gameObject.SetActive(false);
    }
}
