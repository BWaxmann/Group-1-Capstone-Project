using UnityEngine;

// Chunky cats with no run animation (the phat cats and the Armoured cat) waddle as they move: a side-to-side rock,
// a little twist and a bob with each step, from how fast they're actually going (so it works however they're moved:
// walking, running off, being a kitten). Standing still, they settle back upright.
// CatMovement adds it to cats that have no run animation of their own.
public class CatWaddle : MonoBehaviour
{
    [Tooltip("How far the body rocks side to side at full speed, in degrees.")]
    public float rockDegrees = 8f;
    [Tooltip("How far the body twists with each step, in degrees.")]
    public float twistDegrees = 3f;
    [Tooltip("How high each step lifts the body, in metres.")]
    public float bobHeight = 0.03f;
    [Tooltip("Waddles (one left and one right step) per metre travelled.")]
    public float waddlesPerMetre = 1.6f;
    [Tooltip("Speed (m/s) at which the waddle is at its fullest.")]
    public float fullSpeed = 1f;

    Transform body;
    Vector3 restLocalPosition;
    Quaternion restLocalRotation;
    Quaternion restRelativeRotation;
    Vector3 restRelativePosition;
    Vector3 lastPosition;
    float phase;
    float amount;

    void Awake()
    {
        // The model sits under its animator object (the animator only squashes and shrinks it)
        Animator animator = GetComponentInChildren<Animator>();
        body = animator != null && animator.transform != transform ? animator.transform : null;
        if (body == null)
        {
            enabled = false;
            return;
        }
        restLocalPosition = body.localPosition;
        restLocalRotation = body.localRotation;
        restRelativeRotation = Quaternion.Inverse(transform.rotation) * body.rotation;
        restRelativePosition = transform.InverseTransformPoint(body.position);
    }

    void OnEnable()
    {
        lastPosition = transform.position;
        amount = 0;
    }

    void OnDisable()
    {
        if (body == null) return;
        body.localPosition = restLocalPosition;
        body.localRotation = restLocalRotation;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0 || body == null) return;

        Vector3 moved = transform.position - lastPosition;
        moved.y = 0;
        lastPosition = transform.position;
        float speed = moved.magnitude / dt;
        if (speed > 15f) speed = 0;   // teleported (into a hole, out of the pool), not walking

        amount = Mathf.MoveTowards(amount, Mathf.Clamp01(speed / fullSpeed), dt * 4f);
        phase += speed * waddlesPerMetre * dt;
        float step = Mathf.Sin(phase * Mathf.PI * 2f);

        // Rock and twist around the cat's own forward and up, on top of how the model normally sits
        body.rotation = transform.rotation * Quaternion.Euler(0, twistDegrees * step * amount, rockDegrees * step * amount) * restRelativeRotation;
        body.position = transform.TransformPoint(restRelativePosition) + Vector3.up * (bobHeight * Mathf.Abs(step) * amount);
    }
}
