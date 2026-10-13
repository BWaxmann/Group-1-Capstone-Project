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
        PlayerInput player = Object.FindObjectOfType<PlayerInput>();
        Vector3 centre = player != null ? player.transform.position : Vector3.zero;
        Vector3 forward = player != null ? player.transform.forward : Vector3.forward;
        forward.y = 0;
        forward.Normalize();

        Camera cam = new GameObject("Snapshot Camera").AddComponent<Camera>();
        cam.fieldOfView = 90f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;

        Vector3 eye = centre + Vector3.up * 1.6f;
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

        Object.DestroyImmediate(cam.gameObject);
        Debug.Log($"[Snapshot] Saved pictures to {Path.GetFullPath(folder)}");
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
