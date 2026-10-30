using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Cat Cafe Waves: the chalkboard shows tips, in money, instead of a score. Every text on the board is made to fit its
// box (sized to fit, one line where it's a word or a number), "Score" becomes "Tips", and the final tally ends with a
// funny line about what the tips could buy (better the more you earned). No best tips: Liminal keeps nothing between
// sessions.
public class TipsBoard : MonoBehaviour
{
    [Serializable]
    public class Line
    {
        [Tooltip("This line shows from this much money up (until the next line's amount).")]
        public float from;
        public string text;
    }

    [Serializable]
    public class Settings
    {
        [Min(0.01f)]
        [Tooltip("What one coin in the tip jar is worth.")]
        public float coinValue = 0.5f;
        public string currency = "$";
        public string liveLabel = "Tips:";
        public string finalLabel = "Tips:";
        [Min(0.5f)]
        [Tooltip("How much bigger the end-of-round tally is drawn on the board.")]
        public float tallyScale = 1.25f;
        [Tooltip("Shown at the end whatever the tips (for now, for laughs). Empty = the line for the amount, from the list below.")]
        public string alwaysLine = "That's more than enough for a Liminal VR Premium Subscription!";
        [Tooltip("What the tips could buy, from the smallest amount up.")]
        public List<Line> lines = new List<Line>
        {
            new Line { from = 0, text = "Hmm... that's one fancy cat biscuit." },
            new Line { from = 5, text = "Enough for a nice coffee. Treat yourself!" },
            new Line { from = 12, text = "That's a brand new cat toy!" },
            new Line { from = 20, text = "A whole month of premium cat food!" },
            new Line { from = 30, text = "That's more than enough for a Liminal VR Premium Subscription!" },
            new Line { from = 45, text = "Wow! That's practically a new VR headset!" },
        };
    }

    GameManager game;
    TipJar jar;
    Settings settings;
    readonly HashSet<TMP_Text> fitted = new HashSet<TMP_Text>();
    bool scaledTally;
    bool roomMade;

    public static TipsBoard Begin(TipJar jar, Settings settings)
    {
        GameManager game = GameManager.instance;
        if (game == null || jar == null || game.scoreDisplay == null) return null;
        TipsBoard board = game.gameObject.GetComponent<TipsBoard>();
        if (board == null) board = game.gameObject.AddComponent<TipsBoard>();
        board.game = game;
        board.jar = jar;
        board.settings = settings;
        board.Restyle();
        return board;
    }

    string Money(int coins) => $"{settings.currency}{coins * settings.coinValue:0.00}";

    // Every text on the board fits its box
    void Restyle()
    {
        foreach (TMP_Text anchor in new[] { game.scoreDisplay, game.countdownDisplay, game.finalScoreDisplay })
        {
            Canvas canvas = anchor != null ? anchor.canvas : null;
            if (canvas == null) continue;
            canvas = canvas.rootCanvas;
            foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true)) Fit(text);
        }

        // "Score" becomes "Tips" (the labels are the value's parent on the live board, and its neighbour at the end)
        TMP_Text live = game.scoreDisplay.transform.parent != null ? game.scoreDisplay.transform.parent.GetComponent<TMP_Text>() : null;
        if (live != null) live.text = settings.liveLabel;
        // The tally panel only took the middle of the board: a bit bigger
        Canvas tally = game.finalScoreDisplay != null ? game.finalScoreDisplay.canvas : null;
        if (tally != null && !scaledTally)
        {
            scaledTally = true;
            tally.rootCanvas.transform.localScale *= settings.tallyScale;
        }
        if (game.finalScorePanelObject != null)
        {
            foreach (TMP_Text text in game.finalScorePanelObject.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Final Score") text.text = settings.finalLabel;
            }
        }
    }

    // Sized to fit its box (never bigger than it was made), and on one line unless it's a block of words; a box wider
    // than its board is brought back inside it
    void Fit(TMP_Text text)
    {
        if (text == null || !fitted.Add(text)) return;
        float largest = Mathf.Max(text.fontSize, text.fontSizeMax);
        text.enableAutoSizing = true;
        text.fontSizeMax = largest;
        text.fontSizeMin = Mathf.Max(6f, largest * 0.2f);
        bool paragraph = text.text.Length > 24 || text.text.Contains("\n");
        text.enableWordWrapping = paragraph;
        text.overflowMode = TextOverflowModes.Overflow;

        RectTransform box = text.rectTransform;
        RectTransform board = box.parent as RectTransform;
        if (board != null && box.anchorMin.x == box.anchorMax.x && box.rect.width > board.rect.width)
        {
            // Too wide for the board: stretch it across instead, with a small margin
            box.anchorMin = new Vector2(0, box.anchorMin.y);
            box.anchorMax = new Vector2(1, box.anchorMax.y);
            box.sizeDelta = new Vector2(-board.rect.width * 0.08f, box.sizeDelta.y);
            box.anchoredPosition = new Vector2(0, box.anchoredPosition.y);
        }
    }

    // Kept up to date every frame, after the game's own score updates
    void LateUpdate()
    {
        if (jar == null || game == null) return;
        int coins = jar.total;
        if (game.scoreDisplay != null) game.scoreDisplay.text = Money(coins);

        GameObject panel = game.finalScorePanelObject;
        if (panel == null || !panel.activeInHierarchy) return;
        if (game.finalScoreDisplay != null) game.finalScoreDisplay.text = Money(coins);
        if (!roomMade && game.rankDisplay != null)
        {
            roomMade = true;
            Arrange((RectTransform)panel.transform);
        }
        if (game.rankDisplay != null)
        {
            game.rankDisplay.enableWordWrapping = true;
            game.rankDisplay.text = Quip(coins * settings.coinValue);
        }
        // No best tips: Liminal keeps nothing between sessions (and doesn't allow saving)
        if (game.bestScoreDisplay != null) game.bestScoreDisplay.gameObject.SetActive(false);
    }

    // The tally: Cats Fed at the top, the tips in the middle (where the penalty goes, when it's switched off), and the
    // funny line under them, wider than the panel and on two lines if it needs them, with the best tips at the bottom
    void Arrange(RectTransform panel)
    {
        TMP_Text penaltyLabel = game.penaltyDisplay != null ? Sibling(game.penaltyDisplay, "Penalty") : null;
        TMP_Text tipsLabel = game.finalScoreDisplay != null ? Sibling(game.finalScoreDisplay, "Final Score") : null;
        bool middleFree = game.penaltyDisplay != null && !game.penaltyDisplay.gameObject.activeSelf
                          && (penaltyLabel == null || !penaltyLabel.gameObject.activeSelf);
        if (middleFree && game.finalScoreDisplay != null)
        {
            CopyPlace(game.penaltyDisplay.rectTransform, game.finalScoreDisplay.rectTransform);
            if (penaltyLabel != null && tipsLabel != null) CopyPlace(penaltyLabel.rectTransform, tipsLabel.rectTransform);
        }

        float wider = panel.rect.width * 0.8f;
        Place(game.rankDisplay, middleFree ? -14f : -20f, middleFree ? 134f : 90f, wider, 46f);
        if (game.bestScoreDisplay != null) Place(game.bestScoreDisplay, middleFree ? -74f : -116f, 52f, wider, 34f);
    }

    // `bottom` up from the panel's bottom edge (negative = under it)
    void Place(TMP_Text text, float bottom, float height, float wider, float largest)
    {
        Fit(text);
        RectTransform box = text.rectTransform;
        box.anchorMin = new Vector2(0, 0);
        box.anchorMax = new Vector2(1, 0);
        box.pivot = new Vector2(0.5f, 0);
        box.anchoredPosition = new Vector2(0, bottom);
        box.sizeDelta = new Vector2(wider, height);
        text.enableAutoSizing = true;
        text.fontSizeMax = largest;
        text.fontSizeMin = largest * 0.5f;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.Center;
    }

    static void CopyPlace(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;
        to.anchoredPosition = from.anchoredPosition;
        to.sizeDelta = from.sizeDelta;
    }

    // The label beside a value on the tally (same panel, name containing `part`)
    static TMP_Text Sibling(TMP_Text value, string part)
    {
        if (value.transform.parent == null) return null;
        foreach (Transform child in value.transform.parent)
        {
            if (child != value.transform && child.name.Contains(part) && !child.name.Contains("Value"))
            {
                TMP_Text label = child.GetComponent<TMP_Text>();
                if (label != null) return label;
            }
        }
        return null;
    }

    // The funny line for this much money: the highest one it's reached
    string Quip(float money)
    {
        if (!string.IsNullOrEmpty(settings.alwaysLine)) return settings.alwaysLine;
        string chosen = "";
        float reached = float.MinValue;
        foreach (Line line in settings.lines)
        {
            if (line != null && money >= line.from && line.from >= reached)
            {
                reached = line.from;
                chosen = line.text;
            }
        }
        return chosen;
    }
}
