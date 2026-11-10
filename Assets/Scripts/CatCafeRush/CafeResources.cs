using System.Collections.Generic;
using UnityEngine;

// Cat Cafe Waves: what the game loads by name while it plays (the announcer's lines and voice, the waves' music and
// stings, the spy cat, the shift sky, the outline materials), listed on one scene object ("Cafe Resources").
// Liminal's app takes only what the scene refers to, never the Resources folders (Tin at Liminal, 10 Nov 2026), so in
// there a Resources.Load finds nothing: these lists are what carries the files in. Catastrophe Cafe > Fill Cafe
// Resources From The Resources Folders fills them, and the scene is saved with them. In the editor and the plain APK a
// file left off the lists is still found in its Resources folder, with a warning that the app would miss it.
public class CafeResources : MonoBehaviour
{
    public static CafeResources instance;

    [Tooltip("Every sound under a Resources folder (the announcer's lines, the waves' music, the stings). Filled by Catastrophe Cafe > Fill Cafe Resources From The Resources Folders.")]
    public List<AudioClip> sounds = new List<AudioClip>();

    [Tooltip("Every prefab under a Resources folder (the spy cat, the shift sky).")]
    public List<GameObject> prefabs = new List<GameObject>();

    [Tooltip("Every text file under a Resources folder (the announcer's lines.txt).")]
    public List<TextAsset> texts = new List<TextAsset>();

    [Tooltip("Every material under a Resources folder (the outlines).")]
    public List<Material> materials = new List<Material>();

    static readonly HashSet<string> warnedAbout = new HashSet<string>();
    static bool warnedNoObject;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            Debug.Log("[Cat Cafe Waves] Cafe Resources: " + Summary() + ".");
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // The sound, prefab, text or material at a Resources path (CafeSounds/coin, SpyCat/Spy Phat Cat...), by its file
    // name: from the lists, or else from its Resources folder (which works in the editor and the APK, never inside
    // Liminal's app, so that comes with a warning). Null when it is in neither, as Resources.Load would be.
    public static T Load<T>(string path) where T : Object
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        CafeResources lists = Lists();
        if (lists != null)
        {
            T listed = lists.Find<T>(FileName(path));
            if (listed != null)
            {
                return listed;
            }
        }
        T fallback = Resources.Load<T>(path);
        if (fallback != null)
        {
            WarnLeftOut(path, lists);
        }
        return fallback;
    }

    // For the settings check in the Console: what the lists hold, or MISSING with no Cafe Resources object in the scene
    public static string Check()
    {
        CafeResources lists = Lists();
        if (lists == null)
        {
            return "MISSING";
        }
        return lists.Summary();
    }

    static CafeResources Lists()
    {
        if (instance == null)
        {
            instance = FindObjectOfType<CafeResources>();
        }
        return instance;
    }

    static string FileName(string path)
    {
        int slash = path.LastIndexOf('/');
        if (slash < 0)
        {
            return path;
        }
        return path.Substring(slash + 1);
    }

    T Find<T>(string name) where T : Object
    {
        T found = Pick<T>(sounds, name);
        if (found == null)
        {
            found = Pick<T>(prefabs, name);
        }
        if (found == null)
        {
            found = Pick<T>(texts, name);
        }
        if (found == null)
        {
            found = Pick<T>(materials, name);
        }
        return found;
    }

    static T Pick<T>(IEnumerable<Object> list, string name) where T : Object
    {
        foreach (Object item in list)
        {
            T typed = item as T;
            if (typed != null && string.Equals(typed.name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                return typed;
            }
        }
        return null;
    }

    // Once per file (and once for a scene with no Cafe Resources object at all): it was found in its Resources folder,
    // so it works here, but inside Liminal's app it would be missing
    static void WarnLeftOut(string path, CafeResources lists)
    {
        if (lists == null)
        {
            if (!warnedNoObject)
            {
                warnedNoObject = true;
                Debug.LogWarning("[Cat Cafe Waves] No Cafe Resources object in the scene: the announcer, the waves' music and stings, the spy cat, " +
                                 "the sky and the outlines all load from the Resources folders, which Liminal's app doesn't ship. " +
                                 "Run Catastrophe Cafe > Fill Cafe Resources From The Resources Folders and save the scene.");
            }
            return;
        }
        if (warnedAbout.Add(path))
        {
            Debug.LogWarning($"[Cat Cafe Waves] {path} isn't in the Cafe Resources lists (it's in a Resources folder, so it works here, but inside " +
                             "Liminal's app it would be missing). Run Catastrophe Cafe > Fill Cafe Resources From The Resources Folders and save the scene.");
        }
    }

    string Summary()
    {
        return $"{Count(sounds)} sound(s), {Count(prefabs)} prefab(s), {Count(texts)} text(s), {Count(materials)} material(s)";
    }

    static int Count(IEnumerable<Object> list)
    {
        int count = 0;
        foreach (Object item in list)
        {
            if (item != null)
            {
                count++;
            }
        }
        return count;
    }
}
