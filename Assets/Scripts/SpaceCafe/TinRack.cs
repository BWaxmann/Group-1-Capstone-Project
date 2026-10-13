using System.Collections;
using UnityEngine;

// The food tins on the counter. Reach with your left hand, squeeze the grip to pick one up,
// bring it to the launcher and it snaps on: the launcher now fires that food.
// The rack never runs out. In the editor, number keys 1-3 load tins directly for testing.
public class TinRack : MonoBehaviour
{
    [SerializeField]
    [Tooltip("One slot per food, in the same order as the Space Cafe Service's food list.")]
    Transform[] slots;

    [SerializeField]
    [Tooltip("The model used for the tins (tinted to each food's colour).")]
    GameObject tinPrefab;

    [SerializeField]
    [Min(0.02f)]
    float tinSize = 0.11f;

    [SerializeField]
    [Min(0.02f)]
    [Tooltip("How close your hand must be to a tin to pick it up, in metres.")]
    float grabRadius = 0.2f;

    [SerializeField]
    [Min(0.02f)]
    [Tooltip("How close a held tin must come to the launcher to snap on, in metres.")]
    float snapRadius = 0.25f;

    [SerializeField]
    SoundData loadSound = new SoundData(1f, 1f);

    [SerializeField]
    AudioSource audioSource;

    GameObject[] rackTins;
    LineRenderer[] slotRings;
    int foodsAvailable;
    int highlighted = -1;

    GameObject heldTin;
    int heldFood = -1;
    GameObject loadedTin;
    Vector3 launcherAttachPoint;
    bool attachPointFound;

    PlayerInput player;

    public void Setup(Transform[] tinSlots, GameObject tinModel)
    {
        slots = tinSlots;
        tinPrefab = tinModel;
    }

    void Start()
    {
        player = FindObjectOfType<PlayerInput>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
        }

        int count = slots != null ? slots.Length : 0;
        rackTins = new GameObject[count];
        slotRings = new LineRenderer[count];
        for (int i = 0; i < count; i++)
        {
            Color colour = SpaceCafeService.instance != null ? SpaceCafeService.instance.FoodColour(i) : Color.white;
            rackTins[i] = MakeTin(slots[i], colour);

            // A glowing ring under each tin so its colour reads from a glance
            slotRings[i] = SpaceCafeVisuals.Ring(slots[i], "Slot Ring", tinSize * 0.8f, 0.008f, colour);
            slotRings[i].transform.localRotation = Quaternion.Euler(90, 0, 0);
            slotRings[i].transform.localPosition = Vector3.up * 0.005f;
        }
        SetFoodsAvailable(0);
    }

    GameObject MakeTin(Transform parent, Color colour)
    {
        GameObject tin;
        if (tinPrefab != null)
        {
            tin = Instantiate(tinPrefab, parent);
            foreach (Collider col in tin.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (Rigidbody body in tin.GetComponentsInChildren<Rigidbody>()) Destroy(body);
            FitToSize(tin, tinSize);
            SpaceCafeVisuals.Tint(tin, colour);
        }
        else
        {
            tin = SpaceCafeVisuals.Shape(PrimitiveType.Cylinder, parent, "Tin", colour);
            tin.transform.localScale = new Vector3(tinSize, tinSize * 0.6f, tinSize);
            tin.transform.localPosition = Vector3.up * tinSize * 0.6f;
        }
        tin.name = "Tin";
        return tin;
    }

    // Scales a model so its largest side is the given size, sitting on its parent's origin
    static void FitToSize(GameObject model, float size)
    {
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer rend in renderers) bounds.Encapsulate(rend.bounds);

        float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.0001f);
        float scale = size / largest;
        model.transform.localScale = Vector3.one * scale;

        Vector3 bottomCentre = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        Vector3 offset = model.transform.parent.position - bottomCentre;
        model.transform.position += offset * scale;
    }

    // Reveals the first few tins as foods are added to the menu
    public void SetFoodsAvailable(int count)
    {
        if (rackTins == null) return;
        for (int i = 0; i < rackTins.Length; i++)
        {
            bool show = i < count;
            bool wasShown = i < foodsAvailable;
            rackTins[i].SetActive(show);
            slotRings[i].gameObject.SetActive(show);
            if (show && !wasShown && count > 1) StartCoroutine(Pop(rackTins[i].transform));
        }
        foodsAvailable = count;
    }

    IEnumerator Pop(Transform tin)
    {
        Vector3 size = tin.localScale;
        float timer = 0;
        while (timer < 0.4f)
        {
            timer += Time.deltaTime;
            tin.localScale = size * Mathf.Max(0.01f, SpaceCafeVisuals.EaseOutBack(timer / 0.4f));
            yield return null;
        }
        tin.localScale = size;
    }

    void Update()
    {
        if (player == null || !player.isBaristaMode || !player.hasLaunchers) return;

#if UNITY_EDITOR
        // Desktop testing without a headset
        for (int i = 0; i < Mathf.Min(foodsAvailable, 9); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Load(i);
        }
#endif

        Transform hand = player.leftHand;
        if (hand == null) return;

        if (heldTin == null)
        {
            int nearest = NearestTin(hand.position);
            Highlight(nearest);
            if (nearest >= 0 && player.leftGripPressed)
            {
                PickUp(nearest, hand);
            }
        }
        else
        {
            if (Vector3.Distance(heldTin.transform.position, LauncherAttachWorld()) <= snapRadius)
            {
                Load(heldFood);
                Destroy(heldTin);
                heldTin = null;
            }
            else if (!player.leftGripHeld)
            {
                // Let go away from the launcher: the tin just goes back to the rack
                Destroy(heldTin);
                heldTin = null;
            }
        }
    }

    int NearestTin(Vector3 handPosition)
    {
        int nearest = -1;
        float best = grabRadius;
        for (int i = 0; i < foodsAvailable && i < rackTins.Length; i++)
        {
            float distance = Vector3.Distance(handPosition, rackTins[i].transform.position);
            if (distance < best)
            {
                best = distance;
                nearest = i;
            }
        }
        return nearest;
    }

    void Highlight(int index)
    {
        if (index == highlighted) return;
        if (highlighted >= 0 && highlighted < rackTins.Length) rackTins[highlighted].transform.localScale /= 1.25f;
        if (index >= 0) rackTins[index].transform.localScale *= 1.25f;
        highlighted = index;
    }

    void PickUp(int index, Transform hand)
    {
        Highlight(-1);
        heldFood = index;
        heldTin = MakeTin(hand, SpaceCafeService.instance.FoodColour(index));
        heldTin.transform.localPosition = Vector3.zero;
        Haptics.Pulse(false, Haptics.Strength.Light);
    }

    public void Load(int foodIndex)
    {
        SpaceCafeService service = SpaceCafeService.instance;
        if (service == null || foodIndex < 0 || foodIndex >= service.foodCount) return;

        player.LoadFood(service.FoodVariation(foodIndex));

        // Show the loaded tin hanging under the launcher
        Transform launcher = player.rightLauncher;
        Vector3 attach = LauncherAttachLocal(launcher);
        if (loadedTin != null) Destroy(loadedTin);
        loadedTin = new GameObject("Loaded Tin");
        loadedTin.transform.SetParent(launcher, false);
        loadedTin.transform.localPosition = attach;
        loadedTin.transform.localRotation = Quaternion.Euler(180f, 0, 0);
        MakeTin(loadedTin.transform, service.FoodColour(foodIndex));

        Haptics.PulseBoth(Haptics.Strength.Medium);
        loadSound.PlayOneShot(audioSource);
        StartCoroutine(Pop(loadedTin.transform));
    }

    public void SetLoadSound(SoundData sound)
    {
        loadSound = sound;
    }

    Vector3 LauncherAttachWorld()
    {
        Transform launcher = player.rightLauncher;
        return launcher.TransformPoint(LauncherAttachLocal(launcher));
    }

    // The bottom-middle of the launcher model, in the launcher's own space
    Vector3 LauncherAttachLocal(Transform launcher)
    {
        if (attachPointFound) return launcherAttachPoint;

        bool any = false;
        Bounds local = new Bounds();
        foreach (MeshFilter mesh in launcher.GetComponentsInChildren<MeshFilter>())
        {
            if (mesh.sharedMesh == null || (loadedTin != null && mesh.transform.IsChildOf(loadedTin.transform))) continue;
            Bounds b = mesh.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? b.min.x : b.max.x,
                    (corner & 2) == 0 ? b.min.y : b.max.y,
                    (corner & 4) == 0 ? b.min.z : b.max.z);
                Vector3 inLauncher = launcher.InverseTransformPoint(mesh.transform.TransformPoint(point));
                if (!any) { local = new Bounds(inLauncher, Vector3.zero); any = true; }
                else local.Encapsulate(inLauncher);
            }
        }

        launcherAttachPoint = any ? new Vector3(local.center.x, local.min.y, local.center.z) : Vector3.down * 0.1f;
        attachPointFound = true;
        return launcherAttachPoint;
    }
}
