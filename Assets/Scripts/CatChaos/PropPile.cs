using System.Collections.Generic;
using UnityEngine;

// A pile of cafe props (plates, cups, jars...), usually replacing a defend point's treat tin.
// Wandering cats that come near can knock its top prop flying (see CatChaosDirector). On a defend point, a cat that
// marches over to attack it makes it rattle and glow as a warning, and each paw swipe sends the top prop flying.
public class PropPile : MonoBehaviour
{
    static readonly List<CatMovement> noCats = new List<CatMovement>();
    static readonly List<PropPile> all = new List<PropPile>();
    internal static void ResetStatics()
    {
        all.Clear();
    }   // (a new run: see RunReset)

    [Tooltip("The object holding the props; it's what rattles.")]
    public Transform pile;

    [Tooltip("The props on this pile, knocked off top first.")]
    public List<FlyingProp> props = new List<FlyingProp>();

    [Min(0.05f)]
    public float warningRingRadius = 0.3f;

    [Tooltip("How far away (in metres) a cat heading here starts the warning.")]
    [Min(0.5f)]
    public float warningDistance = 6f;

    public Color warningColour = new Color(1f, 0.55f, 0.1f);
    public Color dangerColour = new Color(1f, 0.15f, 0.1f);

    DefendPoint defendPoint;
    LineRenderer warningRing;
    Vector3 pileRestPosition;
    Quaternion pileRestRotation;
    float warning;          // 0 = no cat coming, 1 = a cat is right here
    float checkTimer;
    float shake;            // extra rattle from a swipe that couldn't knock anything off
    Transform viewer;
    UnityEngine.UI.Slider healthBar;
    GameObject barHolder;       // the canvas the health bar is on
    Vector3 barHolderScale;
    bool barShown;
    float barPop = 1f;          // 0 to 1 as the bar pops in
    bool wasWhole = true;

    void Awake()
    {
        defendPoint = GetComponent<DefendPoint>();
        if (pile != null)
        {
            pileRestPosition = pile.localPosition;
            pileRestRotation = pile.localRotation;

            // A flat ring on the surface under the pile
            Transform ringHolder = new GameObject("Warning Ring").transform;
            ringHolder.SetParent(pile.parent, false);
            ringHolder.localPosition = pile.localPosition + Vector3.up * 0.01f;
            ringHolder.localRotation = Quaternion.Euler(90f, 0, 0);
            float scale = Mathf.Max(0.0001f, pile.parent.lossyScale.x);
            warningRing = SpaceCafeVisuals.Ring(ringHolder, "Ring", warningRingRadius / scale, 0.025f, warningColour);
            warningRing.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        all.Add(this);
        if (defendPoint != null)
        {
            defendPoint.Hit += OnHit;
        }
    }

    void OnDisable()
    {
        all.Remove(this);
        if (defendPoint != null)
        {
            defendPoint.Hit -= OnHit;
        }
    }

    // Cat Cafe Rush trial: a paw swipe tips the top prop over instead of sending it flying
    public static bool toppleOnHit;

    // A paw swipe (from a cat that marched over to attack the pile)
    void OnHit()
    {
        Vector3 playerPosition;
        if (Viewer() != null)
        {
            playerPosition = viewer.position;
        }
        else
        {
            playerPosition = transform.position - transform.forward;
        }
        bool knocked;
        if (toppleOnHit)
        {
            knocked = ToppleTop();
        }
        else
        {
            knocked = KnockTop(playerPosition, 0f);
        }
        if (knocked)
        {
            shake = Mathf.Max(shake, 0.6f);
        }
        else
        {
            shake = 1f;
        }
    }

    // Tips the topmost prop that's home off the pile. False if nothing could go.
    public bool ToppleTop()
    {
        FlyingProp top = null;
        foreach (FlyingProp prop in props)
        {
            if (prop == null || prop.state != FlyingProp.State.Home)
            {
                continue;
            }
            if (top == null || prop.transform.position.y > top.transform.position.y)
            {
                top = prop;
            }
        }
        return top != null && top.Topple();
    }

    // How much of the pile is standing (props at home), 0 to 1
    public float standing
    {
        get
        {
            int home = 0, total = 0;
            foreach (FlyingProp prop in props)
            {
                if (prop == null)
                {
                    continue;
                }
                total++;
                if (prop.state == FlyingProp.State.Home)
                {
                    home++;
                }
            }
            if (total > 0)
            {
                return home / (float)total;
            }
            return 1f;
        }
    }

    // Sends the topmost prop that's home (and has been for restTime seconds) flying. False if nothing could go.
    public bool KnockTop(Vector3 playerPosition, float restTime)
    {
        FlyingProp top = null;
        foreach (FlyingProp prop in props)
        {
            if (prop == null || prop.state != FlyingProp.State.Home || prop.timeAtHome < restTime)
            {
                continue;
            }
            if (top == null || prop.transform.position.y > top.transform.position.y)
            {
                top = prop;
            }
        }
        if (top != null && top.Knock(playerPosition))
        {
            shake = Mathf.Max(shake, 0.6f);
            return true;
        }
        return false;
    }

    // The whole pile goes up at once (the ninja's swipe in Cat Cafe Waves). Returns how many props went flying.
    public int Explode(Vector3 playerPosition)
    {
        int sent = 0;
        foreach (FlyingProp prop in props)
        {
            if (prop != null && prop.state == FlyingProp.State.Home && prop.Knock(playerPosition, false, true))
            {
                sent++;
            }
        }
        shake = 1f;
        if (sent > 0 && barsWaitForNinja && !barShown)
        {
            ShowBar();
        }
        return sent;
    }

    // Cat Cafe Waves: a pile's bar stays hidden until the ninja first blows the pile up. Then it pops in empty and
    // fills back up as the props are caught and put back.
    public static bool barsWaitForNinja;

    // Puts every pile's bar away again (when the ninja's phase is over); the next time the ninja blows a pile up, its
    // bar pops back up
    public static void HideBars()
    {
        foreach (PropPile pile in all)
        {
            pile.barShown = false;
        }
    }

    void ShowBar()
    {
        barShown = true;
        FindBar();
        if (barHolder == null)
        {
            return;
        }
        barHolder.SetActive(true);
        healthBar.value = 0;
        barPop = 0;
        wasWhole = false;
    }

    void FindBar()
    {
        if (healthBar != null || defendPoint == null)
        {
            return;
        }
        healthBar = defendPoint.GetComponentInChildren<UnityEngine.UI.Slider>(true);
        if (healthBar == null)
        {
            return;
        }
        // The topmost canvas between the bar and the defend point (GetComponent finds it even while it's hidden)
        barHolder = healthBar.gameObject;
        for (Transform up = healthBar.transform; up != null && up != defendPoint.transform; up = up.parent)
        {
            if (up.GetComponent<Canvas>() != null)
            {
                barHolder = up.gameObject;
            }
        }
        barHolderScale = barHolder.transform.localScale;
    }

    // Props still sitting on the pile
    public int propsHome
    {
        get
        {
            int home = 0;
            foreach (FlyingProp prop in props)
            {
                if (prop != null && prop.state == FlyingProp.State.Home)
                {
                    home++;
                }
            }
            return home;
        }
    }

    // A wobble, e.g. when a cat brushes past
    public void Rattle(float amount)
    {
        shake = Mathf.Max(shake, amount);
    }

    void Update()
    {
        if (pile == null)
        {
            return;
        }

        // If the defend point's health bar is showing, it shows how much of the pile is standing
        FindBar();
        if (barsWaitForNinja && barHolder != null)
        {
            if (!barShown)
            {
                if (barHolder.activeSelf)
                {
                    barHolder.SetActive(false);
                }
            }
            else if (barPop < 1f)
            {
                // Pops in with a little overshoot
                barPop = Mathf.MoveTowards(barPop, 1f, Time.deltaTime / 0.3f);
                float back = barPop - 1f;
                float size = 1f + 2.7f * back * back * back + 1.7f * back * back;
                barHolder.transform.localScale = barHolderScale * Mathf.LerpUnclamped(0.2f, 1f, size);
            }
        }
        if (healthBar != null && healthBar.isActiveAndEnabled)
        {
            float target = standing;
            healthBar.value = Mathf.MoveTowards(healthBar.value, barsWaitForNinja ? target : Mathf.Max(0.05f, target), Time.deltaTime * 2f);

            // Every prop caught and back on the pile: a little cheer
            bool whole = target >= 0.999f;
            if (barsWaitForNinja && barShown && whole && !wasWhole)
            {
                FloatingWord.Show("Tidy!", healthBar.transform.position + Vector3.up * 0.15f, new Color(0.4f, 1f, 0.5f), 3f, 1.2f);
            }
            wasWhole = whole;
        }

        // Checking every few frames is plenty for a warning
        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0)
        {
            checkTimer = 0.2f;
            warning = 0;
            // Only a pile on a defend point can be marched on (a pile without one just gets knocked in passing)
            foreach (CatMovement cat in defendPoint != null ? CatMovement.active : noCats)
            {
                if (cat == null || cat.headingFor != defendPoint)
                {
                    continue;
                }
                float distance = Vector3.Distance(cat.transform.position, transform.position);
                warning = Mathf.Max(warning, 1f - Mathf.Clamp01((distance - 0.8f) / warningDistance));
            }
        }

        shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * 3f);
        float rattle = Mathf.Max(warning * warning * 0.6f, shake);

        if (rattle > 0.01f)
        {
            float t = Time.time * 38f;
            pile.localRotation = pileRestRotation * Quaternion.Euler(Mathf.Sin(t) * 4f * rattle, 0, Mathf.Cos(t * 1.3f) * 4f * rattle);
            pile.localPosition = pileRestPosition + new Vector3(0, Mathf.Abs(Mathf.Sin(t * 0.9f)) * 0.01f * rattle, 0);
        }
        else
        {
            pile.localRotation = pileRestRotation;
            pile.localPosition = pileRestPosition;
        }

        if (warningRing != null)
        {
            bool show = warning > 0.01f;
            if (warningRing.gameObject.activeSelf != show)
            {
                warningRing.gameObject.SetActive(show);
            }
            if (show)
            {
                // Pulses faster and redder as the cat gets closer
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(4f, 14f, warning));
                Color colour = Color.Lerp(warningColour, dangerColour, warning);
                colour.a = Mathf.Lerp(0.35f, 1f, pulse);
                warningRing.startColor = warningRing.endColor = colour;
                // Line width is in world units (it ignores scale), unlike the ring's points
                warningRing.widthMultiplier = Mathf.Lerp(0.02f, 0.05f, warning);
            }
        }
    }

    Transform Viewer()
    {
        if (viewer == null && Camera.main != null)
        {
            viewer = Camera.main.transform;
        }
        return viewer;
    }
}
