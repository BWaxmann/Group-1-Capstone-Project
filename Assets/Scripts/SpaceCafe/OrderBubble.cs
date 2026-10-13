using System.Collections;
using UnityEngine;

// The speech bubble above a customer cat: a mini model of the food it wants, in the food's colour,
// with a ring that runs down as the cat's patience goes. Placeholder art built from simple shapes.
public class OrderBubble : MonoBehaviour
{
    const float BubbleSize = 0.26f;

    Transform bubble;
    Transform foodModel;
    LineRenderer patienceRing;
    Coroutine popRoutine;
    Coroutine shakeRoutine;

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

    // Shows the order: a copy of the food's model, tinted to its colour
    public void Show(GameObject foodPrefab, Color foodColour, Vector3 localPosition)
    {
        transform.localPosition = localPosition;
        if (foodModel != null) Destroy(foodModel.gameObject);

        foodModel = new GameObject("Food").transform;
        foodModel.SetParent(bubble, false);
        foodModel.localPosition = new Vector3(0, 0, -0.04f);

        bool madeModel = false;
        if (foodPrefab != null)
        {
            MeshFilter sourceMesh = foodPrefab.GetComponentInChildren<MeshFilter>();
            MeshRenderer sourceRenderer = foodPrefab.GetComponentInChildren<MeshRenderer>();
            if (sourceMesh != null && sourceRenderer != null && sourceMesh.sharedMesh != null)
            {
                // The model sits centred inside a pivot, so it spins in place
                Transform spin = new GameObject("Spin").transform;
                spin.SetParent(foodModel, false);
                GameObject model = new GameObject("Model");
                model.transform.SetParent(spin, false);
                model.AddComponent<MeshFilter>().sharedMesh = sourceMesh.sharedMesh;
                MeshRenderer rend = model.AddComponent<MeshRenderer>();
                rend.sharedMaterials = sourceRenderer.sharedMaterials;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // Scale the model to fit inside the bubble, centred
                Bounds bounds = sourceMesh.sharedMesh.bounds;
                float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.0001f);
                float scale = BubbleSize * 0.5f / largest;
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = -bounds.center * scale;
                madeModel = true;
            }
        }

        // A coloured dot behind the model, so the colour reads even from far away
        GameObject dot = SpaceCafeVisuals.Shape(PrimitiveType.Sphere, foodModel, "Colour", foodColour);
        dot.transform.localScale = madeModel ? new Vector3(0.16f, 0.16f, 0.01f) : new Vector3(0.14f, 0.14f, 0.05f);
        dot.transform.localPosition = new Vector3(0, 0, 0.025f);

        SetPatience(1f);
        gameObject.SetActive(true);
        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopIn());
    }

    public void Hide()
    {
        gameObject.SetActive(false);
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
        if (foodModel != null)
        {
            foodModel.GetChild(0).Rotate(0, 90f * Time.deltaTime, 0, Space.Self);
        }
    }
}
