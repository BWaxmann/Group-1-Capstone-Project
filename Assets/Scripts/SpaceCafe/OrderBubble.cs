using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The speech bubble above a customer cat. The big bubble shows the treat the cat wants right now;
// any treats it wants after that queue up to the right, smaller and dimmer (like the active/next
// marks in Arcane Practicum). A ring around the big bubble runs down as the cat's patience goes.
// Placeholder art built from simple shapes.
public class OrderBubble : MonoBehaviour
{
    const float BubbleSize = 0.26f;
    const float NextScale = 0.5f;
    const float NextSpacing = 0.12f;

    public struct Item
    {
        public GameObject prefab;
        public Color colour;

        public Item(GameObject prefab, Color colour)
        {
            this.prefab = prefab;
            this.colour = colour;
        }
    }

    Transform bubble;
    Transform current;
    Transform queue;
    LineRenderer patienceRing;
    readonly List<Item> items = new List<Item>();
    Coroutine popRoutine;
    Coroutine shakeRoutine;
    Coroutine faceRoutine;
    SpriteRenderer faceRenderer;

    public static OrderBubble Create(Transform cat)
    {
        OrderBubble order = new GameObject("Order Bubble").AddComponent<OrderBubble>();
        order.transform.SetParent(cat, false);
        order.Build();
        order.gameObject.SetActive(false);
        return order;
    }

    void Build()
    {
        bubble = new GameObject("Bubble").transform;
        bubble.SetParent(transform, false);

        GameObject back = SpaceCafeVisuals.Shape(PrimitiveType.Sphere, bubble, "Back", new Color(1f, 1f, 1f, 0.95f));
        back.transform.localScale = new Vector3(BubbleSize, BubbleSize, 0.03f);

        GameObject tail = SpaceCafeVisuals.Shape(PrimitiveType.Sphere, bubble, "Tail", new Color(1f, 1f, 1f, 0.95f));
        tail.transform.localPosition = new Vector3(0.04f, -BubbleSize * 0.55f, 0);
        tail.transform.localScale = new Vector3(0.06f, 0.06f, 0.02f);

        patienceRing = SpaceCafeVisuals.Ring(bubble, "Patience", BubbleSize * 0.58f, 0.012f, Color.green);
        patienceRing.transform.localPosition = new Vector3(0, 0, -0.02f);
    }

    // Shows a whole order: the first treat big, the rest queued after it
    public void Show(List<Item> order, Vector3 localPosition)
    {
        transform.localPosition = localPosition;
        if (faceRoutine != null) StopCoroutine(faceRoutine);
        faceRoutine = null;
        if (faceRenderer != null) faceRenderer.gameObject.SetActive(false);

        items.Clear();
        items.AddRange(order);
        Rebuild();

        SetPatience(1f);
        gameObject.SetActive(true);
        PopBubble();
    }

    // The current treat was served: the next one in the queue moves into the big bubble
    public void Advance()
    {
        if (items.Count > 0) items.RemoveAt(0);
        Rebuild();
        PopBubble();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void Rebuild()
    {
        if (current != null) Destroy(current.gameObject);
        if (queue != null) Destroy(queue.gameObject);
        if (items.Count == 0) return;

        current = MakeFoodIcon(bubble, items[0], 1f, "Current");
        current.localPosition = new Vector3(0, 0, -0.04f);
        // If a face is flashing, the treat reappears when it finishes
        if (faceRenderer != null && faceRenderer.gameObject.activeSelf) current.gameObject.SetActive(false);

        // The rest of the order, smaller and dimmer, lined up to the right
        queue = new GameObject("Queue").transform;
        queue.SetParent(transform, false);
        for (int i = 1; i < items.Count; i++)
        {
            Transform chip = new GameObject($"Next {i}").transform;
            chip.SetParent(queue, false);
            chip.localPosition = new Vector3(BubbleSize * 0.5f + NextSpacing * (i - 0.4f), -BubbleSize * 0.25f, 0);
            chip.localScale = Vector3.one * NextScale;

            GameObject back = SpaceCafeVisuals.Shape(PrimitiveType.Sphere, chip, "Back", new Color(1f, 1f, 1f, 0.7f));
            back.transform.localScale = new Vector3(BubbleSize, BubbleSize, 0.03f);

            Item dimmed = items[i];
            dimmed.colour = Color.Lerp(dimmed.colour, new Color(0.45f, 0.45f, 0.5f), 0.35f);
            Transform icon = MakeFoodIcon(chip, dimmed, 1f, "Icon");
            icon.localPosition = new Vector3(0, 0, -0.04f);
        }
    }

    // A mini model of the treat with a coloured dot behind it, so the colour reads from far away
    static Transform MakeFoodIcon(Transform parent, Item item, float scale, string name)
    {
        Transform icon = new GameObject(name).transform;
        icon.SetParent(parent, false);
        icon.localScale = Vector3.one * scale;

        bool madeModel = false;
        if (item.prefab != null)
        {
            MeshFilter sourceMesh = item.prefab.GetComponentInChildren<MeshFilter>();
            MeshRenderer sourceRenderer = item.prefab.GetComponentInChildren<MeshRenderer>();
            if (sourceMesh != null && sourceRenderer != null && sourceMesh.sharedMesh != null)
            {
                // The model sits centred inside a pivot, so it spins in place
                Transform spin = new GameObject("Spin").transform;
                spin.SetParent(icon, false);
                GameObject model = new GameObject("Model");
                model.transform.SetParent(spin, false);
                model.AddComponent<MeshFilter>().sharedMesh = sourceMesh.sharedMesh;
                MeshRenderer rend = model.AddComponent<MeshRenderer>();
                rend.sharedMaterials = sourceRenderer.sharedMaterials;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                Bounds bounds = sourceMesh.sharedMesh.bounds;
                float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.0001f);
                float fit = BubbleSize * 0.5f / largest;
                model.transform.localScale = Vector3.one * fit;
                model.transform.localPosition = -bounds.center * fit;
                madeModel = true;
            }
        }

        GameObject dot = SpaceCafeVisuals.Shape(PrimitiveType.Sphere, icon, "Colour", item.colour);
        dot.transform.localScale = madeModel ? new Vector3(0.16f, 0.16f, 0.01f) : new Vector3(0.14f, 0.14f, 0.05f);
        dot.transform.localPosition = new Vector3(0, 0, 0.025f);
        return icon;
    }

    public void SetPatience(float fraction)
    {
        SpaceCafeVisuals.SetRing(patienceRing, BubbleSize * 0.58f, fraction);
        Color colour = fraction > 0.5f ? Color.Lerp(new Color(1f, 0.8f, 0.1f), new Color(0.3f, 0.95f, 0.4f), (fraction - 0.5f) * 2f)
                                       : Color.Lerp(new Color(1f, 0.35f, 0.2f), new Color(1f, 0.8f, 0.1f), fraction * 2f);
        patienceRing.startColor = patienceRing.endColor = colour;
    }

    // "That's not what I ordered!"
    public void Shake()
    {
        if (!gameObject.activeInHierarchy) return;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    // Briefly swaps the order for a cat face (angry for a wrong treat, happy when served).
    // A duration of 0 keeps the face showing.
    public void FlashFace(Sprite face, float duration)
    {
        if (face == null || !gameObject.activeInHierarchy) return;
        if (faceRoutine != null) StopCoroutine(faceRoutine);
        faceRoutine = StartCoroutine(FaceRoutine(face, duration));
    }

    IEnumerator FaceRoutine(Sprite face, float duration)
    {
        if (faceRenderer == null)
        {
            GameObject faceObject = new GameObject("Face");
            faceObject.transform.SetParent(bubble, false);
            faceObject.transform.localPosition = new Vector3(0, 0, -0.05f);
            // The bubble's front faces the player along -Z; turn the sprite round to match
            faceObject.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            faceRenderer = faceObject.AddComponent<SpriteRenderer>();
        }

        faceRenderer.sprite = face;
        float largest = Mathf.Max(face.bounds.size.x, face.bounds.size.y, 0.0001f);
        faceRenderer.transform.localScale = Vector3.one * (BubbleSize * 0.85f / largest);
        faceRenderer.gameObject.SetActive(true);
        if (current != null) current.gameObject.SetActive(false);
        PopBubble();

        if (duration <= 0) yield break;
        yield return new WaitForSeconds(duration);

        faceRenderer.gameObject.SetActive(false);
        if (current != null) current.gameObject.SetActive(true);
        faceRoutine = null;
    }

    void PopBubble()
    {
        if (!gameObject.activeInHierarchy) return;
        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopIn());
    }

    IEnumerator PopIn()
    {
        float timer = 0;
        while (timer < 0.3f)
        {
            timer += Time.deltaTime;
            bubble.localScale = Vector3.one * Mathf.Max(0.01f, SpaceCafeVisuals.EaseOutBack(timer / 0.3f));
            yield return null;
        }
        bubble.localScale = Vector3.one;
    }

    IEnumerator ShakeRoutine()
    {
        float timer = 0;
        while (timer < 0.35f)
        {
            timer += Time.deltaTime;
            bubble.localPosition = new Vector3(Mathf.Sin(timer * 70f) * 0.03f * (1f - timer / 0.35f), 0, 0);
            yield return null;
        }
        bubble.localPosition = Vector3.zero;
    }

    void LateUpdate()
    {
        // Always face the player
        Camera cam = Camera.main;
        if (cam != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }

        // Gently spin the treat models
        SpinIcons(current);
        if (queue != null)
        {
            foreach (Transform chip in queue) SpinIcons(chip);
        }
    }

    void SpinIcons(Transform root)
    {
        if (root == null) return;
        foreach (Transform t in root.GetComponentsInChildren<Transform>())
        {
            if (t.name == "Spin") t.Rotate(0, 90f * Time.deltaTime, 0, Space.Self);
        }
    }
}
