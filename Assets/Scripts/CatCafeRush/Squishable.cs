using System.Collections;
using UnityEngine;

// Cat Cafe Waves: something in the cafe that squashes and springs back when a treat hits it or a hand smacks it
// (plants, cushions, mugs...). Add it in the editor to whatever should react. It scales the object, so the object
// can't be Batching Static (Lightmap Static is fine).
public class Squishable : MonoBehaviour
{
    [SerializeField]
    [Range(0.02f, 0.4f)]
    [Tooltip("How much it squashes, as a fraction of its size.")]
    float amount = 0.15f;

    [SerializeField]
    [Min(0.1f)]
    [Tooltip("How long the wobble lasts, in seconds.")]
    float seconds = 0.4f;

    [SerializeField]
    [Tooltip("Hands can smack it too: a launcher passing through it fast enough.")]
    bool smackable = true;

    [SerializeField]
    [Min(0.05f)]
    float smackRadius = 0.3f;

    [SerializeField]
    [Min(0.1f)]
    [Tooltip("How fast the hand has to be moving, in metres a second, to count as a smack.")]
    float smackSpeed = 1f;

    static PlayerInput player;
    static float nextLook;
    internal static void ResetStatics()   // (a new run: see RunReset)
    {
        player = null;
        nextLook = 0;
    }

    Vector3 restScale;
    Renderer rend;
    Coroutine squishing;
    Vector3 lastLeft;
    Vector3 lastRight;
    bool trackingLeft;
    bool trackingRight;
    float nextSmackAt;
    bool warned;

    void Awake()
    {
        restScale = transform.localScale;
        rend = GetComponentInChildren<Renderer>();
    }

    public void Squish()
    {
        if (!warned && IsBatched())
        {
            warned = true;
            Debug.LogWarning($"[Squishable] {name} is Batching Static, so it can't squish: untick Batching Static on it (Lightmap Static is fine).", this);
            return;
        }
        if (squishing != null) StopCoroutine(squishing);
        squishing = StartCoroutine(SquishRoutine());
    }

    // Static batching merges meshes, and a merged mesh ignores this object's scale
    bool IsBatched()
    {
        MeshFilter filter = GetComponentInChildren<MeshFilter>();
        return filter != null && filter.sharedMesh != null && filter.sharedMesh.name.StartsWith("Combined Mesh");
    }

    IEnumerator SquishRoutine()
    {
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / seconds;
            // A squash that springs back with a wobble, dying out
            float wobble = Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * amount;
            transform.localScale = new Vector3(restScale.x * (1f + wobble), restScale.y * (1f - wobble), restScale.z * (1f + wobble));
            yield return null;
        }
        transform.localScale = restScale;
        squishing = null;
    }

    void Update()
    {
        if (!smackable || rend == null) return;
        if (player == null)
        {
            if (Time.time < nextLook) return;
            nextLook = Time.time + 1f;
            player = FindObjectOfType<PlayerInput>();
            if (player == null) return;
        }
        if (!player.hasLaunchers) return;
        CheckHand(player.Launcher(false), false, ref lastLeft, ref trackingLeft);
        CheckHand(player.Launcher(true), true, ref lastRight, ref trackingRight);
    }

    void CheckHand(Transform hand, bool isRight, ref Vector3 last, ref bool tracking)
    {
        if (hand == null) return;
        Vector3 now = hand.position;
        if (tracking && Time.time >= nextSmackAt)
        {
            float speed = (now - last).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            if (speed >= smackSpeed && rend.bounds.SqrDistance(now) <= smackRadius * smackRadius)
            {
                nextSmackAt = Time.time + seconds;
                Squish();
                Haptics.Pulse(isRight, Haptics.Strength.Light);
            }
        }
        last = now;
        tracking = true;
    }
}
