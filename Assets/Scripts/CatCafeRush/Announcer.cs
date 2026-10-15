using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Cat Cafe Waves: the cafe manager on the speaker. Plays voice lines one after another, each with a subtitle that
// floats a little below the player's view and lazily follows it (so it can always be read, without being stuck to
// the face). A line with no recording still shows its subtitle.
// Lines live in Resources/Announcer/lines.txt ("id | what's said | subtitle if different"), recordings next to it
// as <id>.wav (Tools/make-announcer-voice.ps1 makes placeholder ones with a Windows voice). Stings: Resources/CafeSounds.
public class Announcer : MonoBehaviour
{
    public static Announcer instance;

    [Tooltip("Shown before each subtitle. Empty = no name.")]
    public string speakerName = "Manager";
    public Color speakerColour = new Color(1f, 0.8f, 0.4f);
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("A queued line that has waited this long is dropped (its moment has passed).")]
    [Min(0.5f)] public float maxWait = 4f;

    [Header("Subtitles")]
    [Min(0.5f)] public float subtitleDistance = 1.6f;
    [Tooltip("How far below eye height the subtitle sits, in metres.")]
    public float subtitleDrop = 0.4f;
    [Tooltip("How far the head can turn before the subtitle follows, in degrees.")]
    [Range(0f, 60f)] public float subtitleFollowAngle = 20f;
    [Min(0.1f)] public float subtitleFollowSpeed = 4f;

    const float Width = 760f;
    const float MaxHeight = 300f;
    const float Padding = 18f;
    const float FontSize = 34f;
    const float CanvasScale = 0.0015f;

    class Line
    {
        public string text;
        public AudioClip clip;
    }

    struct Queued
    {
        public Line line;
        public AudioClip sting;
        public float queuedAt;
    }

    readonly Dictionary<string, Line> lines = new Dictionary<string, Line>();
    readonly Queue<Queued> queue = new Queue<Queued>();
    readonly Dictionary<string, AudioClip> stings = new Dictionary<string, AudioClip>();
    AudioSource source;

    RectTransform subtitle;
    CanvasGroup subtitleGroup;
    TextMeshProUGUI subtitleText;
    bool subtitleShowing;
    bool subtitlePlaced;
    bool following;
    float yaw;

    void Awake()
    {
        instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // the cafe speaker: heard the same from everywhere
        LoadLines();
        BuildSubtitle();
        StartCoroutine(Run());
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (subtitle != null) Destroy(subtitle.gameObject);
    }

    void LoadLines()
    {
        TextAsset file = Resources.Load<TextAsset>("Announcer/lines");
        if (file == null)
        {
            Debug.LogWarning("[Announcer] No Resources/Announcer/lines.txt found.");
            return;
        }
        foreach (string raw in file.text.Split('\n'))
        {
            string row = raw.Trim();
            if (row.Length == 0 || row.StartsWith("#")) continue;
            string[] parts = row.Split('|');
            if (parts.Length < 2) continue;
            string id = parts[0].Trim();
            string said = parts[1].Trim();
            string shown = parts.Length > 2 && parts[2].Trim().Length > 0 ? parts[2].Trim() : said;
            lines[id] = new Line { text = shown, clip = Resources.Load<AudioClip>("Announcer/" + id) };
        }
    }

    // Queues a line (by its id in lines.txt), optionally with a sting just before it. Unknown ids are skipped.
    public void Say(string id, string sting = null)
    {
        Line line = null;
        if (!string.IsNullOrEmpty(id) && !lines.TryGetValue(id, out line))
        {
            Debug.LogWarning($"[Announcer] No line called \"{id}\" in lines.txt.");
        }
        AudioClip stingClip = Sting(sting);
        if (line == null && stingClip == null) return;
        queue.Enqueue(new Queued { line = line, sting = stingClip, queuedAt = Time.time });
    }

    AudioClip Sting(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!stings.TryGetValue(name, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>("CafeSounds/" + name);
            stings[name] = clip;
        }
        return clip;
    }

    IEnumerator Run()
    {
        while (true)
        {
            while (queue.Count == 0) yield return null;
            Queued next = queue.Dequeue();
            if (Time.time - next.queuedAt > maxWait) continue;

            if (next.sting != null)
            {
                source.PlayOneShot(next.sting, volume);
                // The voice comes in over the tail of the sting
                yield return new WaitForSeconds(next.line != null ? next.sting.length * 0.55f : next.sting.length);
            }
            if (next.line == null) continue;

            ShowSubtitle(next.line.text);
            float length;
            if (next.line.clip != null)
            {
                source.clip = next.line.clip;
                source.volume = volume;
                source.Play();
                length = next.line.clip.length;
            }
            else
            {
                // No recording: give the subtitle about as long as it'd take to read
                length = Mathf.Clamp(next.line.text.Length * 0.06f, 1.5f, 6f);
            }
            yield return new WaitForSeconds(length + 0.4f);
            if (queue.Count == 0) subtitleShowing = false;
        }
    }

    // ---------- Subtitles ----------

    void BuildSubtitle()
    {
        GameObject root = new GameObject("Announcer Subtitles", typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        subtitle = (RectTransform)root.transform;
        subtitle.sizeDelta = new Vector2(Width, 100);
        subtitle.localScale = Vector3.one * CanvasScale;
        subtitleGroup = root.AddComponent<CanvasGroup>();
        subtitleGroup.alpha = 0;
        subtitleGroup.blocksRaycasts = false;
        subtitleGroup.interactable = false;

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        Stretch((RectTransform)panel.transform, 0);
        Image back = panel.AddComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.65f);
        back.raycastTarget = false;
        back.material = OnTop(new Material(Shader.Find("UI/Default")));

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(panel.transform, false);
        Stretch((RectTransform)textObject.transform, Padding);
        subtitleText = textObject.AddComponent<TextMeshProUGUI>();
        subtitleText.fontSize = FontSize;
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.enableWordWrapping = true;
        subtitleText.overflowMode = TextOverflowModes.Overflow;
        subtitleText.margin = Vector4.zero;
        subtitleText.color = Color.white;
        subtitleText.raycastTarget = false;
        OnTop(subtitleText.fontMaterial);

        root.SetActive(false);
    }

    // Drawn over the room, so a cat or the counter can't hide the subtitle
    static Material OnTop(Material material)
    {
        material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        return material;
    }

    static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    void ShowSubtitle(string text)
    {
        string name = string.IsNullOrEmpty(speakerName) ? "" :
            $"<color=#{ColorUtility.ToHtmlStringRGB(speakerColour)}>{speakerName}:</color> ";
        if (!subtitleShowing && subtitleGroup.alpha <= 0.01f) subtitlePlaced = false;   // appear where the player is looking
        subtitleShowing = true;
        subtitle.gameObject.SetActive(true);

        // Lay the text out in a roomy panel first, measure what it actually drew, then shrink the panel around it
        subtitleText.enableAutoSizing = false;
        subtitleText.fontSize = FontSize;
        subtitleText.text = name + text;
        subtitle.sizeDelta = new Vector2(Width, MaxHeight);
        Canvas.ForceUpdateCanvases();
        subtitleText.ForceMeshUpdate();
        Vector2 drawn = subtitleText.GetRenderedValues(true);
        float width = Mathf.Clamp(drawn.x + Padding * 2 + 12f, 200f, Width);
        float height = Mathf.Clamp(drawn.y + Padding * 2 + 8f, 60f, MaxHeight);
        subtitle.sizeDelta = new Vector2(width, height);

        // Whatever happens, the words stay inside the panel (they shrink a little rather than spill out)
        subtitleText.fontSizeMax = FontSize;
        subtitleText.fontSizeMin = FontSize * 0.6f;
        subtitleText.enableAutoSizing = true;
    }

    void LateUpdate()
    {
        if (subtitle == null) return;

        subtitleGroup.alpha = Mathf.MoveTowards(subtitleGroup.alpha, subtitleShowing ? 1f : 0f, Time.deltaTime / 0.2f);
        if (!subtitleShowing && subtitleGroup.alpha <= 0f)
        {
            if (subtitle.gameObject.activeSelf) subtitle.gameObject.SetActive(false);
            return;
        }

        Transform head = Head();
        if (head == null) return;

        // Follows the head's turn lazily: only once it has turned away a little, then it catches up
        float headYaw = head.eulerAngles.y;
        if (!subtitlePlaced)
        {
            yaw = headYaw;
            following = false;
        }
        float behind = Mathf.Abs(Mathf.DeltaAngle(yaw, headYaw));
        if (behind > subtitleFollowAngle) following = true;
        if (following)
        {
            yaw = Mathf.LerpAngle(yaw, headYaw, Time.deltaTime * subtitleFollowSpeed);
            if (behind < 2f) following = false;
        }

        Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        Vector3 target = head.position + forward * subtitleDistance + Vector3.down * subtitleDrop;
        subtitle.position = subtitlePlaced ? Vector3.Lerp(subtitle.position, target, Time.deltaTime * 10f) : target;
        subtitle.rotation = Quaternion.LookRotation(subtitle.position - head.position);
        subtitlePlaced = true;
    }

    PlayerInput player;

    // The player's eye once the rig is about (Liminal switches it on a little after the scene starts)
    Transform Head()
    {
        if (player == null) player = FindObjectOfType<PlayerInput>();
        if (player != null && player.eyePoint != null && player.eyePoint.gameObject.activeInHierarchy) return player.eyePoint;
        return Camera.main != null ? Camera.main.transform : null;
    }
}
