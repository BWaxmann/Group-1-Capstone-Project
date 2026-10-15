using UnityEngine;

public class ParticleManager : MonoBehaviour
{
    [SerializeField] ObjectSpawner particleSpawner;

    public static ParticleManager instance;

    // Size of every effect (1 = as made). A scene can turn effects down when there's a lot going on.
    public static float effectScale = 1f;

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

    void Start()
    {
        if (particleSpawner == null)
        {
            particleSpawner = GetComponent<ObjectSpawner>();
        }
    }

    public void ShowParticleEffect(int index, Vector3 position)
    {
        ShowParticleEffect(index, position, 1f);
    }

    // `size` scales this one effect on top of the scene's effect scale (e.g. 0.85 for a smaller puff)
    public void ShowParticleEffect(int index, Vector3 position, float size)
    {
        GameObject obj = particleSpawner.GetObjectFromPool(index);
        if (obj != null)
        {
            ApplyScale(obj, size);
            obj.SetActive(true);
            obj.transform.position = position;
        }
    }

    public void ShowParticleEffect(int index, Vector3 position, Quaternion rotation)
    {
        GameObject obj = particleSpawner.GetObjectFromPool(index);
        if (obj != null)
        {
            ApplyScale(obj);
            obj.SetActive(true);
            obj.transform.SetPositionAndRotation(position, rotation);
        }
    }

    static readonly System.Collections.Generic.Dictionary<GameObject, Vector3> madeScales = new System.Collections.Generic.Dictionary<GameObject, Vector3>();

    static void ApplyScale(GameObject effect, float size = 1f)
    {
        // Untouched unless a scene turns effects down (or back up), or this one is sized
        float scale = effectScale * size;
        if (!madeScales.TryGetValue(effect, out Vector3 made))
        {
            if (Mathf.Approximately(scale, 1f)) return;
            made = effect.transform.localScale;
            madeScales[effect] = made;
        }
        effect.transform.localScale = made * scale;
        // Make every part of the effect follow that scale (by default particles ignore their parents' scale)
        foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }
}
