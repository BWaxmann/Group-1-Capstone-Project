using UnityEngine;

// A little bit of decoration (a leaf, a cactus chunk) that bursts off a knocked prop, flutters down,
// lies on the floor for a moment and shrinks away. Not catchable; it's just there for the chaos.
public class FlutterPiece : MonoBehaviour
{
    Vector3 velocity;
    Vector3 spinAxis;
    float spinSpeed;
    float swayPhase;
    float floorHeight;
    float age;
    float lifetime;
    Vector3 startScale;
    bool landed;

    // Sends `count` pieces (picked at random from `models`) flying out from a point
    public static void Burst(GameObject[] models, int count, float size, Vector3 from, float floorHeight)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject model = models[Random.Range(0, models.Length)];
            if (model == null) continue;

            GameObject piece = Instantiate(model, from, Random.rotation);
            foreach (Collider col in piece.GetComponentsInChildren<Collider>()) col.enabled = false;
            Bounds bounds = new Bounds(from, Vector3.zero);
            bool any = false;
            foreach (Renderer rend in piece.GetComponentsInChildren<Renderer>())
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (!any) { bounds = rend.bounds; any = true; }
                else bounds.Encapsulate(rend.bounds);
            }
            float largest;
            if (any)
            {
                largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.0001f);
            }
            else
            {
                largest = 1f;
            }
            piece.transform.localScale *= size * Random.Range(0.8f, 1.2f) / largest;

            FlutterPiece flutter = piece.AddComponent<FlutterPiece>();
            Vector3 outward = Random.insideUnitCircle.normalized;
            flutter.velocity = new Vector3(outward.x, 0, outward.y) * Random.Range(0.8f, 1.8f) + Vector3.up * Random.Range(1.5f, 2.8f);
            flutter.spinAxis = Random.onUnitSphere;
            flutter.spinSpeed = Random.Range(200f, 500f);
            flutter.swayPhase = Random.Range(0f, 10f);
            flutter.floorHeight = floorHeight;
            flutter.lifetime = Random.Range(2.5f, 3.5f);
            flutter.startScale = piece.transform.localScale;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (!landed)
        {
            // Falls slowly and sways side to side, like a leaf
            velocity += Vector3.down * 3f * dt;
            velocity.y = Mathf.Max(velocity.y, -1.2f);
            velocity.x *= 1f - dt * 1.5f;
            velocity.z *= 1f - dt * 1.5f;
            Vector3 sway = new Vector3(Mathf.Sin(age * 5f + swayPhase), 0, Mathf.Cos(age * 4f + swayPhase)) * 0.4f;
            transform.position += (velocity + sway) * dt;
            transform.Rotate(spinAxis, spinSpeed * dt, Space.World);

            if (transform.position.y <= floorHeight + 0.01f)
            {
                landed = true;
                Vector3 position = transform.position;
                position.y = floorHeight + 0.01f;
                transform.position = position;
            }
        }

        // Shrinks away at the end
        float fade = Mathf.Clamp01((lifetime - age) / 0.5f);
        transform.localScale = startScale * fade;
        if (age >= lifetime) Destroy(gameObject);
    }
}
