using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Cat Cafe Waves: a glass tip jar on the counter. Coins pop out of each fed cat and arc into it with a clink,
// and the jar slowly fills with gold. When it's full it's sealed and set aside on the counter (up to two full jars stand
// beside it), safe from raids, and this one starts again empty. Made from the Cafe Prefabs tip jar when one is set (its
// own art for the parts set below); whatever isn't set is built from simple shapes.
public class TipJar : MonoBehaviour
{
    const float JarRadius = 0.08f;
    const float JarHeight = 0.22f;
    const float FullAfter = 60;   // coins until the jar looks full

    public Color glassColour = new Color(0.75f, 0.9f, 1f, 0.3f);
    public Color coinColour = new Color(1f, 0.8f, 0.15f);
    [Min(0.1f)] public float coinFlightTime = 0.7f;

    [Header("Prefab parts (optional: built in code when empty)")]

    [SerializeField]
    [Tooltip("The jar itself (rocks when swatted, bounces when a coin lands).")]
    Transform body;

    [SerializeField]
    [Tooltip("The gold inside, authored FULL with its pivot at its base: it's squashed down in Y as the jar fills.")]
    Transform fill;

    [SerializeField]
    [Tooltip("The lid, switched off; shown once the jar is full and set aside.")]
    GameObject lid;

    [SerializeField]
    [Tooltip("A coin (copied for each one that flies in or spills out). Empty = a small gold disc.")]
    GameObject coinPrefab;

    [SerializeField]
    [Tooltip("How tall the jar is, in metres at scale 1: where coins drop in and words float up.")]
    float height = JarHeight;

    public int coins { get; private set; }
    // Coins in the full jars set aside
    public int banked { get; private set; }
    // Every coin earned and kept: the full jars and this one
    public int total
    {
        get
        {
            return banked + coins;
        }
    }
    public string fullText = "Jar full!";

    const int MaxFullJars = 2;
    readonly List<TipJar> fullJars = new List<TipJar>();
    Vector3 viewer;
    float size = 1f;
    bool sealedFull;   // a full jar standing on the counter (just for show)

    // The jar's mouth (where coins drop in)
    public Vector3 top
    {
        get
        {
            return transform.position + Vector3.up * height * transform.lossyScale.y;
        }
    }

    Vector3 fillScale;      // a prefab fill's authored (full) scale
    bool fillFromPrefab;
    AudioSource clinkSource;
    AudioClip clink;
    Coroutine punch;

    // Builds a jar standing at `position`, its label turned towards `viewer` (scale 1 = about 22 cm tall)
    public static TipJar Create(Vector3 position, Vector3 viewer, float scale = 1f)
    {
        TipJar prefab = CafePrefabs.TipJarPrefab;
        GameObject root = prefab != null ? Instantiate(prefab.gameObject) : new GameObject("Tip Jar");
        root.name = "Tip Jar";
        root.SetActive(true);   // (a template kept switched off in the scene comes out switched off)
        root.transform.position = position;
        root.transform.localScale = Vector3.one * scale;
        Vector3 away = position - viewer;
        away.y = 0;
        if (away.sqrMagnitude > 0.0001f) root.transform.rotation = Quaternion.LookRotation(away);
        TipJar jar = root.GetComponent<TipJar>();
        if (jar == null) jar = root.AddComponent<TipJar>();
        jar.viewer = viewer;
        jar.size = scale;
        jar.Build();
        return jar;
    }

    // Whatever the prefab didn't bring
    void Build()
    {
        fillFromPrefab = fill != null;
        if (fillFromPrefab) fillScale = fill.localScale;
        if (lid != null) lid.SetActive(false);
        if (clinkSource == null) clinkSource = GetComponent<AudioSource>();
        if (clinkSource == null)
        {
            clinkSource = gameObject.AddComponent<AudioSource>();
            clinkSource.playOnAwake = false;
            clinkSource.spatialBlend = 1f;
            clinkSource.minDistance = 1.5f;
        }
        clink = Resources.Load<AudioClip>("CafeSounds/coin");
        if (body != null)
        {
            SetFill(0);
            return;
        }

        body = new GameObject("Jar").transform;
        body.SetParent(transform, false);

        // The gold inside draws first, then the glass over it
        GameObject gold = SpaceCafeVisuals.Shape(PrimitiveType.Cylinder, body, "Coins Inside", coinColour);
        gold.GetComponent<Renderer>().sharedMaterial.renderQueue = 3000;
        fill = gold.transform;
        SetFill(0);

        GameObject glass = SpaceCafeVisuals.Shape(PrimitiveType.Cylinder, body, "Glass", glassColour);
        glass.GetComponent<Renderer>().sharedMaterial.renderQueue = 3001;
        glass.transform.localScale = new Vector3(JarRadius * 2, JarHeight * 0.5f, JarRadius * 2);
        glass.transform.localPosition = Vector3.up * JarHeight * 0.5f;

        GameObject rim = SpaceCafeVisuals.Shape(PrimitiveType.Cylinder, body, "Rim", new Color(0.85f, 0.95f, 1f, 0.6f));
        rim.GetComponent<Renderer>().sharedMaterial.renderQueue = 3002;
        rim.transform.localScale = new Vector3(JarRadius * 2.15f, 0.006f, JarRadius * 2.15f);
        rim.transform.localPosition = Vector3.up * JarHeight;

        // "TIPS" on the front, facing the player
        GameObject label = new GameObject("Label");
        label.transform.SetParent(body, false);
        label.transform.localPosition = new Vector3(0, JarHeight * 0.6f, -JarRadius - 0.003f);
        TextMeshPro text = label.AddComponent<TextMeshPro>();
        text.text = "TIPS";
        text.fontSize = 0.6f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.95f, 0.8f);
        text.enableWordWrapping = false;
        text.rectTransform.sizeDelta = new Vector2(0.2f, 0.08f);
        if (text.fontSharedMaterial != null) text.fontMaterial.renderQueue = 3003;   // drawn over the glass
    }

    void SetFill(float amount)
    {
        if (fill == null) return;
        if (fillFromPrefab)
        {
            fill.localScale = new Vector3(fillScale.x, fillScale.y * Mathf.Max(0.02f, Mathf.Clamp01(amount)), fillScale.z);
            return;
        }
        float gold = Mathf.Lerp(0.004f, JarHeight * 0.85f, Mathf.Clamp01(amount));
        fill.localScale = new Vector3(JarRadius * 1.8f, gold * 0.5f, JarRadius * 1.8f);
        fill.localPosition = Vector3.up * gold * 0.5f;
    }

    // A coin, from the prefab if there is one
    GameObject MakeCoin(string coinName)
    {
        if (coinPrefab != null)
        {
            GameObject made = Instantiate(coinPrefab, transform);
            made.name = coinName;
            made.SetActive(true);
            return made;
        }
        GameObject coin = SpaceCafeVisuals.Shape(PrimitiveType.Cylinder, transform, coinName, coinColour);
        coin.transform.localScale = new Vector3(0.05f, 0.004f, 0.05f);
        return coin;
    }

    void DestroyCoin(GameObject coin)
    {
        if (coinPrefab == null) Destroy(coin.GetComponent<Renderer>().sharedMaterial);   // (a material made for it)
        Destroy(coin);
    }

    // Coins pop out at `from` and arc into the jar one after another
    public void AddCoins(Vector3 from, int count)
    {
        if (count > 0 && isActiveAndEnabled) StartCoroutine(CoinShower(from, count));
    }

    IEnumerator CoinShower(Vector3 from, int count)
    {
        for (int i = 0; i < count; i++)
        {
            StartCoroutine(FlyCoin(from + Random.insideUnitSphere * 0.08f, i));
            yield return new WaitForSeconds(0.09f);
        }
    }

    IEnumerator FlyCoin(Vector3 from, int index)
    {
        GameObject coin = MakeCoin("Coin");
        coin.transform.position = from;

        Vector3 to = top + Vector3.up * 0.02f;
        float height = 0.5f + Vector3.Distance(from, to) * 0.15f;
        float flight = coinFlightTime + Vector3.Distance(from, to) * 0.05f;
        float timer = 0;
        while (timer < flight)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / flight);
            float eased = t * t * (3f - 2f * t);
            coin.transform.position = Vector3.Lerp(from, to, eased) + Vector3.up * Mathf.Sin(t * Mathf.PI) * height;
            coin.transform.Rotate(720f * Time.deltaTime, 0, 0, Space.Self);
            yield return null;
        }
        DestroyCoin(coin);

        coins++;
        SetFill(coins / FullAfter);
        Clink(Random.Range(0.78f, 0.84f) + Mathf.Min(index, 4) * 0.03f, CoinVolume);
        if (punch != null) StopCoroutine(punch);
        punch = StartCoroutine(Punch());
        if (!sealedFull && coins >= FullAfter) JarFull();
    }

    // Full! It's banked (safe from raids from now on): a sealed full jar appears beside it on the counter, and this one
    // starts again empty
    void JarFull()
    {
        banked += (int)FullAfter;
        coins -= (int)FullAfter;
        SetFill(coins / FullAfter);
        FloatingWord.Show(fullText, top + Vector3.up * 0.25f, coinColour, 3f, 1.6f);
        AudioClip ding = Resources.Load<AudioClip>("CafeSounds/order_ding");
        if (ding != null) clinkSource.PlayOneShot(ding, 0.9f);
        if (fullJars.Count >= MaxFullJars) return;

        Vector3? spot = FreeSpot();
        if (spot == null) return;
        TipJar full = Create(spot.Value, viewer, size);
        full.sealedFull = true;
        full.coins = (int)FullAfter;
        full.SetFill(1f);
        full.Seal();
        fullJars.Add(full);
        full.StartCoroutine(full.PopIn());
    }

    // A clear spot on the counter beside it (the same surface, nothing in the way, not on another jar)
    Vector3? FreeSpot()
    {
        float step = 0.24f * size;
        for (int k = 1; k <= 3; k++)
        {
            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 spot = transform.position + transform.right * step * side * k;
                if (fullJars.Exists(jar => jar != null && (jar.transform.position - spot).magnitude < step * 0.9f)) continue;
                if (!Physics.Raycast(spot + Vector3.up * 0.3f, Vector3.down, out RaycastHit surface, 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(surface.point.y - transform.position.y) > 0.04f) continue;
                if (Physics.CheckSphere(surface.point + Vector3.up * height * 0.55f * size, JarRadius * 1.2f * size,
                                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                return surface.point;
            }
        }
        return null;
    }

    // A full jar's lid
    void Seal()
    {
        if (lid != null)
        {
            lid.SetActive(true);
            return;
        }
        GameObject made = SpaceCafeVisuals.Shape(PrimitiveType.Cylinder, body, "Lid", new Color(0.55f, 0.4f, 0.25f));
        made.transform.localScale = new Vector3(JarRadius * 2.25f, 0.012f, JarRadius * 2.25f);
        made.transform.localPosition = Vector3.up * (JarHeight + 0.012f);
    }

    IEnumerator PopIn()
    {
        Vector3 scale = transform.localScale;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.35f;
            transform.localScale = scale * Mathf.Max(0.01f, SpaceCafeVisuals.EaseOutBack(Mathf.Min(t, 1f)));
            yield return null;
        }
        transform.localScale = scale;
    }

    // Coin clinks: soft and a little low, and never more than one every ClinkGap seconds, so a shower of coins (a
    // stampede pays out a lot at once) jingles instead of overloading the ears
    const float CoinVolume = 0.45f;
    const float ClinkGap = 0.07f;
    float lastClink = -10f;

    void Clink(float pitch, float volume)
    {
        if (clink == null || Time.time - lastClink < ClinkGap) return;
        lastClink = Time.time;
        clinkSource.pitch = pitch;
        clinkSource.PlayOneShot(clink, volume);
    }

    // A cat swatted the jar (from `from`): it rocks over and up to `count` coins spill out. Returns how many were lost.
    public int Spill(Vector3 from, int count)
    {
        int lost = Mathf.Min(count, coins);
        coins -= lost;
        SetFill(coins / FullAfter);
        Vector3 away = transform.position - from;
        away.y = 0;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;
        StartCoroutine(Rock(away));
        for (int i = 0; i < lost; i++) StartCoroutine(SpillCoin(away, i));
        return lost;
    }

    IEnumerator Rock(Vector3 away)
    {
        Vector3 axis = Vector3.Cross(Vector3.up, away);
        float timer = 0;
        while (timer < 0.7f)
        {
            timer += Time.deltaTime;
            // A hard knock over, then wobbling back upright
            float angle = 35f * Mathf.Sin(timer / 0.7f * Mathf.PI * 3f) * (1f - timer / 0.7f);
            body.rotation = Quaternion.AngleAxis(angle, axis) * transform.rotation;
            yield return null;
        }
        body.localRotation = Quaternion.identity;
    }

    IEnumerator SpillCoin(Vector3 away, int index)
    {
        yield return new WaitForSeconds(index * 0.05f);
        GameObject coin = MakeCoin("Spilt Coin");
        coin.transform.position = top;
        Vector3 sideways = Vector3.Cross(Vector3.up, away) * Random.Range(-0.7f, 0.7f);
        Vector3 velocity = away * Random.Range(0.8f, 1.6f) + sideways + Vector3.up * Random.Range(1.2f, 2f);
        Clink(Random.Range(0.7f, 0.9f), CoinVolume * 0.8f);

        float timer = 0;
        while (timer < 1f)
        {
            timer += Time.deltaTime;
            velocity += Physics.gravity * Time.deltaTime;
            coin.transform.position += velocity * Time.deltaTime;
            coin.transform.Rotate(900f * Time.deltaTime, 0, 0, Space.Self);
            yield return null;
        }
        DestroyCoin(coin);
    }

    IEnumerator Punch()
    {
        float timer = 0;
        while (timer < 0.18f)
        {
            timer += Time.deltaTime;
            body.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(timer / 0.18f * Mathf.PI));
            yield return null;
        }
        body.localScale = Vector3.one;
        punch = null;
    }
}
