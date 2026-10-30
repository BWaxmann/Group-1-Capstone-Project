using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Cat Cafe Waves: bold world-space words that face the player, e.g. a raiding cat's "!" or "Saved!" floating up and fading.
public class FloatingWord : MonoBehaviour
{
    static PlayerInput player;

    float timer;
    float lifetime;
    TextMeshPro text;

    // Finished words wait here to be used again, rather than being made and destroyed every time (a busy moment can
    // pop several a second)
    static readonly Stack<FloatingWord> spare = new Stack<FloatingWord>();
    internal static void ResetStatics() => spare.Clear();   // (a new run: see RunReset)
    const int MaxSpare = 16;

    // Bold outlined text in the world (left where it is: the caller moves it about). From the Cafe Prefabs slot if one
    // is set (the prefab's font and style, this text, colour and size); otherwise built here.
    public static GameObject Make(string words, Color colour, float size)
    {
        FloatingWord prefab = CafePrefabs.FloatingWordPrefab;
        if (prefab != null)
        {
            FloatingWord word = Instantiate(prefab);
            word.name = "Word: " + words;
            word.enabled = false;   // only Show() makes it float and fade
            TextMeshPro label = word.GetComponentInChildren<TextMeshPro>();
            if (label != null)
            {
                label.text = words;
                label.color = colour;
                label.fontSize = size;
            }
            return word.gameObject;
        }

        GameObject holder = new GameObject("Word: " + words);
        TextMeshPro text = holder.AddComponent<TextMeshPro>();
        text.text = words;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.color = colour;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(30, 10, 0, 255);
        text.rectTransform.sizeDelta = new Vector2(2f, 1f);
        return holder;
    }

    // A word that floats up from `at` and fades away
    public static void Show(string words, Vector3 at, Color colour, float size = 3f, float lifetime = 1f)
    {
        if (string.IsNullOrEmpty(words)) return;
        FloatingWord word = null;
        while (word == null && spare.Count > 0) word = spare.Pop();   // (one left over from an earlier scene is gone)
        if (word == null)
        {
            GameObject holder = Make(words, colour, size);
            word = holder.GetComponent<FloatingWord>();
            if (word == null) word = holder.AddComponent<FloatingWord>();
            word.text = holder.GetComponentInChildren<TextMeshPro>();
        }
        else
        {
            word.text.text = words;
            word.text.color = colour;
            word.text.fontSize = size;
            word.gameObject.SetActive(true);
        }
        word.enabled = true;
        word.timer = 0;
        word.lifetime = lifetime;
        word.transform.position = at;
        FacePlayer(word.transform);
    }

    public static void FacePlayer(Transform word)
    {
        if (player == null) player = FindObjectOfType<PlayerInput>();
        Transform eye = player != null ? player.eyePoint : (Camera.main != null ? Camera.main.transform : null);
        if (eye != null) word.rotation = Quaternion.LookRotation(word.position - eye.position);
    }

    void Update()
    {
        timer += Time.deltaTime;
        transform.position += Vector3.up * 0.5f * Time.deltaTime;
        FacePlayer(transform);
        if (text != null) text.alpha = Mathf.Clamp01(1.5f * (1f - timer / lifetime));
        if (timer <= lifetime) return;
        // Done: put away for next time (or gone, if there are plenty spare)
        if (spare.Count < MaxSpare)
        {
            gameObject.SetActive(false);
            spare.Push(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
