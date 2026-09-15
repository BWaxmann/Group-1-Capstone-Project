using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CafeUI.Preview
{
    // Trial-scene presentation. Never invokes or edits the game's start/countdown logic.
    [DefaultExecutionOrder(-32000)]
    public sealed class CafeGuidePreview : MonoBehaviour
    {
        public Texture neutralCat;
        public Texture happyCat;
        public bool cafeBackdrop;
        Vector3 cafeEyePosition;
        Quaternion cafeEyeRotation = Quaternion.identity;
        readonly List<GameObject> heldFeeders = new List<GameObject>();
        readonly List<Canvas> heldInstructions = new List<Canvas>();
        bool released;

        void Awake()
        {
            if (!cafeBackdrop) return;
            // Hide only the pickup objects. PlayerInput and all game managers stay running.
            foreach (GrabbableLaunchers feeder in FindObjectsOfType<GrabbableLaunchers>())
            {
                heldFeeders.Add(feeder.gameObject);
                feeder.gameObject.SetActive(false);
            }
            foreach (InstructionsText instructions in FindObjectsOfType<InstructionsText>())
            {
                Canvas canvas = instructions.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.enabled && !heldInstructions.Contains(canvas))
                { heldInstructions.Add(canvas); canvas.enabled = false; }
            }
        }
        readonly Color plum = new Color32(57, 30, 66, 255);
        readonly Color cream = new Color32(255, 239, 203, 255);
        readonly Color peach = new Color32(228, 156, 137, 255);
        readonly Color gold = new Color32(255, 198, 88, 255);
        Font font;
        Camera previewCamera;
        RectTransform board;
        RawImage cat;
        Text speech, heading, progress, actionLabel;
        Image actionImage;
        RectTransform action;
        GameObject cards;
        Coroutine typing;
        int page;
        bool revealing, counting, complete;
        readonly string[] titles = { "A LITTLE HELP, HUMAN?", "FOOD FIGHT!", "A HELPING PAW", "READY, AIM... MEOW!" };
        readonly string[] lines = {
            "Meow! Welcome to the cafe!\nMy friends are hungry... and they're making it EVERYONE'S problem.",
            "Aim your food launchers at the cats and fire!\nKeep those tummies full before they wreck the place.",
            "Shoot the power-ups for a helping paw!\nAmmo refills come to you. Handy, when you've got no thumbs.",
            "Ready to save the cafe?\nShoot the START target. I'll supervise... from somewhere comfy."
        };

        IEnumerator Start()
        {
            // Allow the existing player rig to initialise before placing the world-space guide.
            yield return null;
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (cafeBackdrop)
            {
                // Liminal creates/enables the player camera asynchronously; one frame is not enough.
                float cameraDeadline = Time.realtimeSinceStartup + 20f;
                while (previewCamera == null && Time.realtimeSinceStartup < cameraDeadline)
                {
                    foreach (Camera candidate in Camera.allCameras)
                    {
                        if (!candidate.isActiveAndEnabled || candidate.targetTexture != null) continue;
                        if (!candidate.CompareTag("MainCamera")) continue;
                        previewCamera = candidate;
                        if (candidate.name == "CenterEye") break;
                    }
                    if (previewCamera == null) yield return null;
                }
                if (previewCamera == null)
                { Debug.LogError("Cafe guide: no active player camera after 20 seconds. Feeders restored so the scene is not blocked.", this); RestoreFeeders(); enabled = false; yield break; }
                // Allow the newly enabled rig to apply its initial pose before anchoring the guide.
                yield return null;
                yield return new WaitForEndOfFrame();
                cafeEyePosition = previewCamera.transform.position;
                cafeEyeRotation = Quaternion.Euler(0, previewCamera.transform.eulerAngles.y, 0);
                lines[3] = "Ready to save the cafe?\nChoose LET'S PLAY, then pick up your feeders to begin!";
                StyleExistingDisplays();
            }
            else
            {
            var cameraObject = new GameObject("Preview Camera", typeof(Camera));
            previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color32(35, 20, 43, 255);
            previewCamera.transform.position = new Vector3(0, 1.6f, -3.2f);
            previewCamera.fieldOfView = 55;
            }
            var canvasObject = new GameObject("Cafe introduction - world space", typeof(RectTransform), typeof(Canvas));
            board = canvasObject.GetComponent<RectTransform>();
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            board.sizeDelta = new Vector2(1200, 760);
            board.position = new Vector3(0, 1.6f, 0);
            board.localScale = Vector3.one * .0024f;
            if (cafeBackdrop)
            {
                board.position = cafeEyePosition + cafeEyeRotation * new Vector3(0, 0, 3.2f);
                board.rotation = cafeEyeRotation;
            }

            var frame = Box(board, "Frame", 0, 0, 1200, 760, peach);
            var face = Box(board, "Chalkboard", 0, 0, 1176, 736, plum);
            if (cafeBackdrop) { frame.GetComponent<Image>().enabled = false; face.GetComponent<Image>().enabled = false; }
            Label(face, "CAFE CATASTROPHE", -355, 323, 430, 42, 30, gold);
            Label(face, cafeBackdrop ? "WELCOME TO THE CAFE" : "UI PREVIEW  /  NOT LIVE GAMEPLAY", 325, 323, 470, 32, 17, cream);
            heading = Label(face, "", 0, 258, 1080, 66, 43, cream);
            var portrait = Box(face, "Guide portrait", -390, 63, 260, 260, new Color32(105, 59, 100, 255));
            if (cafeBackdrop) portrait.GetComponent<Image>().enabled = false;
            var catRect = Rect(portrait, "Talking cat", 0, 0, 250, 250);
            cat = catRect.gameObject.AddComponent<RawImage>();
            cat.texture = neutralCat;
            cat.raycastTarget = false;
            Label(face, "YOUR CAFE COMPANION", -390, -86, 285, 30, 17, peach);
            var bubble = Box(face, "Speech bubble", 145, 65, 720, 244, cream);
            speech = Label(bubble, "", 0, 0, 662, 202, 31, plum);
            speech.alignment = TextAnchor.MiddleLeft;
            cards = Rect(face, "Power-up reference cards", 0, -166, 1080, 122).gameObject;
            Card(-432, "x2 POINTS", "Double score", gold);
            Card(-216, "UNLIMITED", "Unlimited ammo", peach);
            Card(0, "SHIELD", "Protect the cafe", new Color32(180, 198, 202, 255));
            Card(216, "CATNIP", "Pause nearby cats", new Color32(194, 173, 205, 255));
            Card(432, "+ AMMO", "Refill your launchers", new Color32(202, 203, 143, 255));
            action = Box(face, "Practice target", 280, -279, 440, 80, gold);
            actionImage = action.GetComponent<Image>();
            actionLabel = Label(action, "", 0, 0, 410, 68, 29, plum);
            progress = Label(face, "", -280, -278, 500, 64, 21, cream);
            Label(face, cafeBackdrop ? "Press A / X to continue, or click the gold button." : "Click the gold target to continue. Space reveals text. R restarts the preview.", 0, -339, 1100, 28, 18, peach);
            // Static samples only: these do not read or update actual game state.
            if (!cafeBackdrop)
            {
            var sample = Box(board, "Score and clock placement samples", 0, 437, 1176, 72, plum);
            Label(sample, "CHALKBOARD  /  SCORE  120     x2", -235, 0, 640, 60, 26, cream);
            Label(sample, "CLOCK PLACEHOLDER  /  02:30", 355, 0, 450, 60, 21, peach);
            }
            ShowPage(0);
            if (cafeBackdrop) Debug.Log("Cafe guide ready on " + previewCamera.name + "; feeders held: " + heldFeeders.Count, this);
        }

        void Update()
        {
            if (board == null || released) return;
            if (!cafeBackdrop && Input.GetKeyDown(KeyCode.R)) { StopAllCoroutines(); counting = false; complete = false; ShowPage(0); }
            if (counting) return;
            bool over = RectTransformUtility.RectangleContainsScreenPoint(action, Input.mousePosition, previewCamera);
            actionImage.color = over ? cream : gold;
            if (Input.GetKeyDown(KeyCode.Space) && revealing) RevealAll();
            if (Input.GetMouseButtonDown(0) && over) ActivateTarget();
            else if (cafeBackdrop && (OVRInput.GetDown(OVRInput.Button.One) || OVRInput.GetDown(OVRInput.Button.Three))) ActivateTarget();
        }

        void ActivateTarget()
        {
            if (complete) { complete = false; ShowPage(0); return; }
            if (revealing) { RevealAll(); return; }
            if (page < lines.Length - 1) ShowPage(page + 1);
            else if (cafeBackdrop) StartCoroutine(ReleaseForPickup());
            else StartCoroutine(Countdown());
        }

        void ShowPage(int index)
        {
            page = index;
            heading.text = titles[page];
            progress.text = "YOUR QUICK GUIDE    " + (page + 1) + " / " + lines.Length;
            cards.SetActive(page == 2);
            actionLabel.text = page == 3 ? (cafeBackdrop ? "LET'S PLAY" : "SHOOT TO START") : "CONTINUE  >";
            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(TypeLine());
        }

        IEnumerator TypeLine()
        {
            revealing = true;
            string line = lines[page];
            for (int i = 1; i <= line.Length; i++)
            {
                speech.text = line.Substring(0, i);
                char letter = line[i - 1];
                bool pause = letter == '.' || letter == '!' || letter == '?' || letter == '\n';
                cat.texture = !pause && (i / 4) % 2 == 0 ? happyCat : neutralCat;
                yield return new WaitForSecondsRealtime(pause ? .23f : .025f);
            }
            revealing = false;
            cat.texture = happyCat;
            typing = null;
        }

        void RevealAll()
        {
            if (typing != null) StopCoroutine(typing);
            typing = null;
            revealing = false;
            speech.text = lines[page];
            cat.texture = happyCat;
        }

        IEnumerator Countdown()
        {
            counting = true;
            heading.text = "PRACTICE COUNTDOWN";
            progress.text = "Preview only - the real game is not connected.";
            for (int i = 3; i > 0; i--)
            {
                speech.text = i.ToString();
                actionLabel.text = "GET READY...";
                yield return new WaitForSecondsRealtime(1);
            }
            speech.text = "Meowvellous!\nThis is where gameplay will begin once the start connection is approved.";
            actionLabel.text = "TRY AGAIN";
            complete = true;
            counting = false;
        }

        IEnumerator ReleaseForPickup()
        {
            counting = true;
            speech.text = "Pick up your feeders to begin!";
            actionLabel.text = "YOU'VE GOT THIS!";
            // Wait for the dismiss input to release so it cannot also grab the feeders.
            yield return null;
            while (Input.GetMouseButton(0) || OVRInput.Get(OVRInput.Button.One)
                || OVRInput.Get(OVRInput.Button.Three)
                || OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger)
                || OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger)) yield return null;
            yield return new WaitForSecondsRealtime(.2f);
            RestoreFeeders();
            board.gameObject.SetActive(false);
            released = true;
            // The unchanged PlayerInput now handles pickup and calls GrabbedLaunchers normally.
        }

        void RestoreFeeders()
        {
            foreach (GameObject feeder in heldFeeders) if (feeder != null) feeder.SetActive(true);
            heldFeeders.Clear();
            foreach (Canvas canvas in heldInstructions) if (canvas != null) canvas.enabled = true;
            heldInstructions.Clear();
        }

        void OnDisable()
        {
            StopAllCoroutines();
            RestoreFeeders();
            if (board != null) board.gameObject.SetActive(false);
        }

        void StyleExistingDisplays()
        {
            // Visual-only trial styling. Keep the existing text objects and manager bindings.
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas.gameObject.scene != gameObject.scene) continue;
                bool score = canvas.name == "Score Canvas";
                bool timer = canvas.name == "Timer Canvas";
                if (!score && !timer) continue;
                foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
                    text.color = text.name.Contains("Bonus") ? gold : cream;
            }
        }

        void Card(float x, string title, string description, Color colour)
        {
            var panel = Box(cards.transform, title, x, 0, 202, 116, colour);
            Label(panel, title, 0, 24, 190, 40, 24, plum);
            Label(panel, description, 0, -24, 182, 52, 19, plum);
        }

        static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x, y);
            return rect;
        }

        RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color colour)
        {
            var rect = Rect(parent, name, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return rect;
        }

        Text Label(Transform parent, string value, float x, float y, float w, float h, int size, Color colour)
        {
            var rect = Rect(parent, "Label", x, y, w, h);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }
    }
}
