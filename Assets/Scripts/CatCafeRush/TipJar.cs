using System.Collections;
using TMPro;
using UnityEngine;

// Cat Cafe Waves: a glass tip jar on the counter. Coins pop out of each fed cat and arc into it with a clink,
// and the jar slowly fills with gold. Placeholder look (simple shapes) until proper art is made.
public class TipJar : MonoBehaviour
{
    const float JarRadius = 0.08f;
    const float JarHeight = 0.22f;
    const float FullAfter = 60;   // coins until the jar looks full

    public Color glassColour = new Color(0.75f, 0.9f, 1f, 0.3f);
    public Color coinColour = new Color(1f, 0.8f, 0.15f);
    [Min(0.1f)] public float coinFlightTime = 0.7f;

    public int coins { get; private set; }

    // The jar's mouth (where coins drop in)
    public Vector3 top => transform.position + Vector3.up * JarHeight;

    Transform fill;
    Transform body;
    AudioSource clinkSource;
    AudioClip clink;
    Coroutine punch;

    // Builds a jar standing at `position`, its label turned towards `viewer`
    public static TipJar Create(Vector3 position, Vector3 viewer)
    {
        GameObject root = new GameObject("Tip Jar");
        root.transform.position = position;
        Vector3 away = position - viewer;
        away.y = 0;
        if (away.sqrMagnitude > 0.0001f) root.transform.rotation = Quaternion.LookRotation(away);
        TipJar jar = root.AddComponent<TipJar>();
        jar.Build();
        return jar;
    }

    void Build()
    {
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
        text.fontMaterial.renderQueue = 3003;   // drawn over the glass

        clinkSource = gameObject.AddComponent<AudioSource>();
        clinkSource.playOnAwake = false;
        clinkSource.spatialBlend = 1f;
        clinkSource.minDistance = 1.5f;
        clink = Resources.Load<AudioClip>("CafeSounds/coin");
    }

    void SetFill(float amount)
    {
        float height = Mathf.Lerp(0.004f, JarHeight * 0.85f, Mathf.Clamp01(amount));
        fill.localScale = new Vector3(JarRadius * 1.8f, height * 0.5f, JarRadius * 1.8f);
        fill.localPosition = Vector3.up * height * 0.5f;
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
        GameObject coin = SpaceCafeVisuals.Shape(PrimitiveType.Cylinder, transform, "Coin", coinColour);
        coin.transform.localScale = new Vector3(0.05f, 0.004f, 0.05f);
        coin.transform.position = from;

        Vector3 to = transform.position + Vector3.up * (JarHeight + 0.02f);
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
        Destroy(coin.GetComponent<Renderer>().sharedMaterial);
        Destroy(coin);

        coins++;
        SetFill(coins / FullAfter);
        if (clink != null)
        {
            clinkSource.pitch = Random.Range(0.95f, 1.05f) + Mathf.Min(index, 6) * 0.06f;
            clinkSource.PlayOneShot(clink);
        }
        if (punch != null) StopCoroutine(punch);
        punch = StartCoroutine(Punch());
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
        GameObject coin = SpaceCafeVisuals.Shape(PrimitiveType.Cylinder, transform, "Spilt Coin", coinColour);
        coin.transform.localScale = new Vector3(0.05f, 0.004f, 0.05f);
        coin.transform.position = top;
        Vector3 sideways = Vector3.Cross(Vector3.up, away) * Random.Range(-0.7f, 0.7f);
        Vector3 velocity = away * Random.Range(0.8f, 1.6f) + sideways + Vector3.up * Random.Range(1.2f, 2f);
        if (clink != null)
        {
            clinkSource.pitch = Random.Range(0.8f, 1.2f);
            clinkSource.PlayOneShot(clink, 0.7f);
        }

        float timer = 0;
        while (timer < 1f)
        {
            timer += Time.deltaTime;
            velocity += Physics.gravity * Time.deltaTime;
            coin.transform.position += velocity * Time.deltaTime;
            coin.transform.Rotate(900f * Time.deltaTime, 0, 0, Space.Self);
            yield return null;
        }
        Destroy(coin.GetComponent<Renderer>().sharedMaterial);
        Destroy(coin);
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
