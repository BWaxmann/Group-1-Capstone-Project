using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders pictures of a scene from the player's eye position (front, left, right, back) and from
// above, so a layout can be checked without opening the editor. Command line:
// Unity -batchmode -executeMethod SceneSnapshot.Capture -snapshotScene <scene path> -snapshotFolder <folder>
public static class SceneSnapshot
{
    public static void Capture()
    {
        string scenePath = Argument("-snapshotScene", "Assets/Scenes/SpaceCafe_Prototype.unity");
        string folder = Argument("-snapshotFolder", "Temp/Snapshots");
        Directory.CreateDirectory(folder);

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Optionally hide some objects for the pictures (e.g. canvases that only show at certain times)
        foreach (string hideName in Argument("-hide", "").Split(','))
        {
            if (string.IsNullOrWhiteSpace(hideName)) continue;
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.gameObject.scene.path == scenePath && t.name == hideName.Trim()) t.gameObject.SetActive(false);
            }
        }

        PlayerInput player = Object.FindObjectOfType<PlayerInput>();
        Vector3 centre = player != null ? player.transform.position : Vector3.zero;
        Vector3 forward = player != null ? player.transform.forward : Vector3.forward;
        forward.y = 0;
        forward.Normalize();

        Camera cam = new GameObject("Snapshot Camera").AddComponent<Camera>();
        cam.fieldOfView = 90f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;

        // Eye height above the real floor (the player rig itself may sit above the floor)
        float floorY = centre.y;
        float best = float.MinValue;
        foreach (RaycastHit hit in Physics.RaycastAll(centre + Vector3.up * 2f, Vector3.down, 10f))
        {
            if (player != null && hit.collider.transform.IsChildOf(player.transform.root)) continue;
            if (hit.point.y <= centre.y + 0.05f && hit.point.y > best) best = hit.point.y;
        }
        if (best > float.MinValue) floorY = best;
        Vector3 eye = new Vector3(centre.x, floorY + 1.6f, centre.z);
        string[] names = { "1_front", "2_right", "3_back", "4_left" };
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = Quaternion.Euler(0, i * 90f, 0) * forward;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(Quaternion.AngleAxis(-15f, Vector3.Cross(dir, Vector3.up)) * dir));
            Save(cam, Path.Combine(folder, names[i] + ".png"), 1280, 800);
        }

        // Looking straight down, with everything above head height cut away (ceiling, lights)
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.nearClipPlane = 9f - 2.2f;
        cam.transform.SetPositionAndRotation(centre + Vector3.up * 9f, Quaternion.LookRotation(Vector3.down, forward));
        Save(cam, Path.Combine(folder, "5_top.png"), 1000, 1000);
        cam.orthographic = false;

        // Optional close-up: from the eye position, looking at a named object
        string lookAt = Argument("-lookAt", "");
        if (!string.IsNullOrEmpty(lookAt))
        {
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.gameObject.scene.path != scenePath || t.name != lookAt) continue;
                Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
                Vector3 target = t.position;
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                    target = b.center;
                }
                cam.fieldOfView = 45f;
                cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
                Save(cam, Path.Combine(folder, "6_" + lookAt + ".png"), 1280, 800);
                break;
            }
        }

        Object.DestroyImmediate(cam.gameObject);
        Debug.Log($"[Snapshot] Saved pictures to {Path.GetFullPath(folder)}");
    }

    // Writes where named objects and every canvas are, so a layout can be planned without the editor.
    // Command line: -executeMethod SceneSnapshot.Report -snapshotScene <scene> -reportNames "Cat_Box5,UI"
    public static void Report()
    {
        string scenePath = Argument("-snapshotScene", "Assets/Scenes/CC_LEVEL.unity");
        string[] names = Argument("-reportNames", "Cat_Box5").Split(',');
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        PlayerInput player = Object.FindObjectOfType<PlayerInput>();
        Vector3 centre = player != null ? player.transform.position : Vector3.zero;
        System.Text.StringBuilder report = new System.Text.StringBuilder($"[Report] {scenePath}, player at {centre}, facing {(player != null ? player.transform.forward : Vector3.forward)}\n");
        foreach (RaycastHit hit in Physics.RaycastAll(centre + Vector3.up * 3f, Vector3.down, 10f))
        {
            report.AppendLine($"BELOW PLAYER: {HierarchyPath(hit.collider.transform)} at y={hit.point.y:F2}");
        }

        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.gameObject.scene.path != scenePath) continue;
            foreach (string name in names)
            {
                if (t.name.Trim() == name.Trim())
                {
                    report.AppendLine($"OBJECT {HierarchyPath(t)} active={t.gameObject.activeInHierarchy} pos={t.position} rot={t.eulerAngles} {BoundsText(t.gameObject, centre)}");
                }
            }
        }

        foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (canvas.gameObject.scene.path != scenePath || canvas.renderMode != RenderMode.WorldSpace) continue;
            RectTransform rect = canvas.GetComponent<RectTransform>();
            Vector2 size = Vector2.Scale(rect.rect.size, new Vector2(rect.lossyScale.x, rect.lossyScale.y));
            string texts = "";
            foreach (TMPro.TMP_Text text in canvas.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                texts += $" [{text.name}: \"{text.text.Replace("\n", " ").Substring(0, Mathf.Min(30, text.text.Length))}\"]";
            }
            float distance = Vector3.Distance(new Vector3(centre.x, 0, centre.z), new Vector3(rect.position.x, 0, rect.position.z));
            report.AppendLine($"CANVAS {HierarchyPath(canvas.transform)} active={canvas.gameObject.activeInHierarchy} pos={rect.position} rot={rect.eulerAngles} size={size.x:F2}x{size.y:F2}m dist={distance:F2}m{texts}");
        }

        Debug.Log(report.ToString());
    }

    static string HierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    static string BoundsText(GameObject go, Vector3 centre)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return "no renderers";
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        float distance = Vector3.Distance(new Vector3(centre.x, 0, centre.z), new Vector3(b.center.x, 0, b.center.z));
        return $"bounds centre={b.center} size={b.size} dist={distance:F2}m";
    }

    static void Save(Camera cam, string path, int width, int height)
    {
        RenderTexture target = new RenderTexture(width, height, 24);
        cam.targetTexture = target;
        cam.Render();
        RenderTexture.active = target;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(target);
    }

    static string Argument(string name, string fallback)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return fallback;
    }
}
