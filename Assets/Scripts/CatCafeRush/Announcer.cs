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

    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("A queued line that has waited this long is dropped (its moment has passed).")]
    [Min(0.5f)]
    public float maxWait = 4f;

    [Tooltip("The speaker's chime before the first line of the run, so the player is listening before she talks. Empty = Resources/CafeSounds/pa_chime.")]
    public AudioClip firstLineChime;

    [Tooltip("Seconds of quiet between the end of that chime and the first line.")]
    [Min(0f)]
    public float chimeGap = 0.7f;

    [Header("Subtitles")]
    [Min(0.5f)]
    public float subtitleDistance = 1.6f;

    [Tooltip("How far below eye height the subtitle sits, in metres.")]
    public float subtitleDrop = 0.4f;

    [Tooltip("How far the head can turn before the subtitle follows, in degrees.")]
    [Range(0f, 60f)]
    public float subtitleFollowAngle = 20f;

    [Min(0.1f)]
    public float subtitleFollowSpeed = 4f;

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

        public bool urgent;

        public bool hold;     // the game holds off what's next until it's been said

        public string id;

        public System.Action<bool> done;   // true when it starts playing, false if it was dropped (waited too long)
    }

    readonly Dictionary<string, Line> lines = new Dictionary<string, Line>();
    readonly List<Queued> queue = new List<Queued>();
    Queued speaking;
    bool chimed;
    bool isSpeaking;

    // A line the game is waiting on (one that explains something) is queued or being said
    public bool holding
    {
        get
        {
            return (isSpeaking && speaking.hold) || queue.Exists(waiting => waiting.hold);
        }
    }

    // Anything queued or being said
    public bool talking
    {
        get
        {
            return isSpeaking || queue.Count > 0;
        }
    }

    // The id of the line being said (null if none)
    public string saying
    {
        get
        {
            if (isSpeaking)
            {
                return speaking.id;
            }
            return null;
        }
    }

    readonly Dictionary<string, AudioClip> stings = new Dictionary<string, AudioClip>();
    
    [SerializeField]
    AudioSource source;

    [SerializeField]
    RectTransform subtitleCanvas;

    [SerializeField]
    RectTransform subtitlePanel;

    CanvasGroup subtitleGroup;

    [SerializeField]
    TextMeshProUGUI subtitleText;

    bool customSubtitlePanel;
    bool subtitleShowing;
    bool subtitlePlaced;
    bool following;
    float yaw;

    void Awake()
    {
        instance = this;
        if (source == null)
        {
            source = GetComponent<AudioSource>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;   // the cafe speaker: heard the same from everywhere
            }
        }
        LoadLines();
        if (subtitleCanvas == null)
        {
            customSubtitlePanel = false;
            BuildSubtitle();
        }
        else
        {
            customSubtitlePanel = true;
            // Allow for using subtitle canvas built in the editor
            if (subtitlePanel == null)
            {
                subtitlePanel = (RectTransform)subtitleCanvas.GetChild(0);
            }
            subtitleGroup = subtitleCanvas.GetComponent<CanvasGroup>();
            if (subtitleGroup == null)
            {
                subtitleGroup = subtitleCanvas.gameObject.AddComponent<CanvasGroup>();
                SetUpSubtitleGroup();
            }
            if (subtitleText == null)
            {
                subtitleText = subtitleCanvas.GetComponentInChildren<TextMeshProUGUI>();
            }
            OnTop(subtitleText.fontMaterial);
            subtitleCanvas.gameObject.SetActive(false);
        }
        StartCoroutine(RunRoutine());
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
        if (subtitleCanvas != null)
        {
            Destroy(subtitleCanvas.gameObject);
        }
    }

    void LoadLines()
    {
        TextAsset file = CafeResources.Load<TextAsset>("Announcer/lines");
        if (file == null)
        {
            Debug.LogWarning("[Announcer] Announcer/lines.txt wasn't found (in the Cafe Resources lists or in Resources): no lines.");
            return;
        }
        foreach (string raw in file.text.Split('\n'))
        {
            string row = raw.Trim();
            if (row.Length == 0 || row.StartsWith("#"))
            {
                continue;
            }
            string[] parts = row.Split('|');
            if (parts.Length < 2)
            {
                continue;
            }
            string id = parts[0].Trim();
            string said = parts[1].Trim();
            string shown;
            if (parts.Length > 2 && parts[2].Trim().Length > 0)
            {
                shown = parts[2].Trim();
            }
            else
            {
                shown = said;
            }
            lines[id] = new Line { text = shown, clip = CafeResources.Load<AudioClip>("Announcer/" + id) };
        }
    }

    // Queues a line (by its id in lines.txt), optionally with a sting just before it. Unknown ids are skipped.
    // Other takes of a line are listed as id_2, id_3... and one is picked at random (not the same one twice running).
    // An urgent line (a hint about what's happening right now) goes ahead of anything waiting, though it never cuts off
    // the line being said. `done` hears whether it was said (true, as it starts) or dropped for waiting too long.
    public void Say(string id, string sting = null, System.Action<bool> done = null, bool urgent = false, bool hold = false)
    {
        Line line = null;
        if (!string.IsNullOrEmpty(id))
        {
            line = PickTake(id);
            if (line == null)
            {
                Debug.LogWarning($"[Announcer] No line called \"{id}\" in lines.txt.");
            }
        }
        AudioClip stingClip = Sting(sting);
        if (line == null && stingClip == null)
        {
            done?.Invoke(false);
            return;
        }
        Queued queued = new Queued { line = line, sting = stingClip, queuedAt = Time.time, urgent = urgent, hold = hold, id = id, done = done };
        if (!urgent)
        {
            queue.Add(queued);
        }
        else
        {
            int at = 0;
            while (at < queue.Count && queue[at].urgent)
            {
                at++;
            }
            queue.Insert(at, queued);
        }
    }

    readonly Dictionary<string, string> lastTake = new Dictionary<string, string>();

    Line PickTake(string id)
    {
        List<string> takes = new List<string>();
        if (lines.ContainsKey(id))
        {
            takes.Add(id);
        }
        for (int n = 2; n <= 9; n++)
        {
            if (lines.ContainsKey(id + "_" + n))
            {
                takes.Add(id + "_" + n);
            }
        }
        if (takes.Count == 0)
        {
            return null;
        }
        // Takes with a recording win over ones that would only show a subtitle
        if (takes.Exists(take => lines[take].clip != null))
        {
            takes.RemoveAll(take => lines[take].clip == null);
        }
        if (takes.Count > 1 && lastTake.TryGetValue(id, out string last))
        {
            takes.Remove(last);
        }
        string chosen = takes[Random.Range(0, takes.Count)];
        lastTake[id] = chosen;
        return lines[chosen];
    }

    AudioClip Sting(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        if (!stings.TryGetValue(name, out AudioClip clip))
        {
            clip = CafeResources.Load<AudioClip>("CafeSounds/" + name);
            stings[name] = clip;
        }
        return clip;
    }

    IEnumerator RunRoutine()
    {
        while (true)
        {
            while (queue.Count == 0)
            {
                yield return null;
            }
            Queued next = queue[0];
            queue.RemoveAt(0);
            if (Time.time - next.queuedAt > maxWait)
            {
                next.done?.Invoke(false);
                if (queue.Count == 0)
                {
                    subtitleShowing = false;
                }
                continue;
            }

            speaking = next;
            isSpeaking = true;
            if (!chimed)
            {
                // The first thing heard is the speaker's chime, then a breath, so the first words aren't missed
                chimed = true;
                AudioClip chime = firstLineChime;
                if (chime == null)
                {
                    chime = CafeResources.Load<AudioClip>("CafeSounds/pa_chime");
                }
                if (chime != null)
                {
                    source.PlayOneShot(chime, volume);
                    yield return new WaitForSeconds(chime.length + chimeGap);
                }
            }
            if (next.sting != null)
            {
                source.PlayOneShot(next.sting, volume);
                // The voice comes in over the tail of the sting
                yield return new WaitForSeconds(next.line != null ? next.sting.length * 0.55f : next.sting.length);
            }
            next.done?.Invoke(true);
            if (next.line == null) {
                isSpeaking = false;
                continue;
            }

            ShowSubtitle(next.line.text);
            float length;
            if (next.line.clip != null)
            {
                source.clip = next.line.clip;
                source.volume = volume;
                source.Play();
                length = next.line.clip.length;
                Debug.Log($"Playing voice line {source.clip.name}");
            }
            else
            {
                // No recording: give the subtitle about as long as it'd take to read
                length = Mathf.Clamp(next.line.text.Length * 0.06f, 1.5f, 6f);
            }
            yield return new WaitForSeconds(length + 0.4f);
            Debug.Log($"Voice Line {source.clip.name} has finished playing.");
            isSpeaking = false;
            if (queue.Count == 0)
            {
                subtitleShowing = false;
            }
        }
    }

    // ---------- Subtitles ----------

    void BuildSubtitle()
    {
        GameObject root = new GameObject("Announcer Subtitles", typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        subtitleCanvas = (RectTransform)root.transform;
        subtitleCanvas.sizeDelta = new Vector2(Width, MaxHeight);
        subtitleCanvas.localScale = Vector3.one * CanvasScale;
        subtitleGroup = root.AddComponent<CanvasGroup>();
        SetUpSubtitleGroup();

        // The panel grows to fit the words: Unity's layout measures them (wrapped to the panel's width) every time
        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        subtitlePanel = (RectTransform)panel.transform;
        subtitlePanel.anchorMin = subtitlePanel.anchorMax = subtitlePanel.pivot = new Vector2(0.5f, 0.5f);
        subtitlePanel.sizeDelta = new Vector2(Width, 100);
        Image back = panel.AddComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.65f);
        back.raycastTarget = false;
        back.material = OnTop(new Material(Shader.Find("UI/Default")));
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        int pad = Mathf.RoundToInt(Padding);
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fit = panel.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(panel.transform, false);
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

    void SetUpSubtitleGroup()
    {
        subtitleGroup.alpha = 0;
        subtitleGroup.blocksRaycasts = false;
        subtitleGroup.interactable = false;
    }

    // Drawn over the room, so a cat or the counter can't hide the subtitle
    static Material OnTop(Material material)
    {
        material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        return material;
    }

    void ShowSubtitle(string text)
    {
        string name;
        if (string.IsNullOrEmpty(speakerName))
        {
            name = "";
        }
        else
        {
            name = $"<color=#{ColorUtility.ToHtmlStringRGB(speakerColour)}>{speakerName}:</color> ";
        }
        if (!subtitleShowing && subtitleGroup.alpha <= 0.01f)
        {
            subtitlePlaced = false;   // appear where the player is looking
        }
        subtitleShowing = true;
        subtitleCanvas.gameObject.SetActive(true);

        // A short line gets a narrower panel; anything longer wraps inside the full width. The layout then sizes
        // the panel's height to the wrapped words.
        subtitleText.text = name + text;

        if (customSubtitlePanel)
        {
            subtitlePanel.sizeDelta = new Vector2(subtitlePanel.sizeDelta.x, subtitleText.GetPreferredValues(10000f, 1f).y);
        }
        else
        {
            float oneLine = subtitleText.GetPreferredValues(subtitleText.text, 100000f, 100000f).x;   // unwrapped
            subtitlePanel.sizeDelta = new Vector2(Mathf.Clamp(oneLine + Padding * 2 + 16f, 200f, Width), subtitlePanel.sizeDelta.y);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(subtitlePanel);
    }

    void LateUpdate()
    {
        if (subtitleCanvas == null)
        {
            return;
        }

        subtitleGroup.alpha = Mathf.MoveTowards(subtitleGroup.alpha, subtitleShowing ? 1f : 0f, Time.deltaTime / 0.2f);
        if (!subtitleShowing && subtitleGroup.alpha <= 0f)
        {
            if (subtitleCanvas.gameObject.activeSelf)
            {
                subtitleCanvas.gameObject.SetActive(false);
            }
            return;
        }

        Transform head = Head();
        if (head == null)
        {
            return;
        }

        // Follows the head's turn lazily: only once it has turned away a little, then it catches up
        float headYaw = head.eulerAngles.y;
        if (!subtitlePlaced)
        {
            yaw = headYaw;
            following = false;
        }
        float behind = Mathf.Abs(Mathf.DeltaAngle(yaw, headYaw));
        if (behind > subtitleFollowAngle)
        {
            following = true;
        }
        if (following)
        {
            yaw = Mathf.LerpAngle(yaw, headYaw, Time.deltaTime * subtitleFollowSpeed);
            if (behind < 2f)
            {
                following = false;
            }
        }

        Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        Vector3 target = head.position + forward * subtitleDistance + Vector3.down * subtitleDrop;
        if (subtitlePlaced)
        {
            subtitleCanvas.position = Vector3.Lerp(subtitleCanvas.position, target, Time.deltaTime * 10f);
        }
        else
        {
            subtitleCanvas.position = target;
        }
        subtitleCanvas.rotation = Quaternion.LookRotation(subtitleCanvas.position - head.position);
        subtitlePlaced = true;
    }

    PlayerInput player;

    // The player's eye once the rig is about (Liminal switches it on a little after the scene starts)
    Transform Head()
    {
        if (player == null)
        {
            player = FindObjectOfType<PlayerInput>();
        }
        if (player != null && player.eyePoint != null && player.eyePoint.gameObject.activeInHierarchy)
        {
            return player.eyePoint;
        }
        if (Camera.main != null)
        {
            return Camera.main.transform;
        }
        return null;
    }
}
