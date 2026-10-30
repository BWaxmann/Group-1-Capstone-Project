using UnityEngine;

// Cat Cafe Waves: the prefabs the game's scripts spawn, set up in the editor on one scene object ("Cafe Prefabs").
// A slot left empty is built in code at runtime instead, as before, so the game keeps working while prefabs are made.
public class CafePrefabs : MonoBehaviour
{
    public static CafePrefabs instance;

    [Header("UI")]

    [SerializeField]
    [Tooltip("A pop-up word (score, Shake!, Shoo!, tips...). Its TextMeshPro sets the font and style; the text, colour and size are set per word.")]
    FloatingWord floatingWord;

    public static FloatingWord FloatingWordPrefab => instance != null ? instance.floatingWord : null;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
}
