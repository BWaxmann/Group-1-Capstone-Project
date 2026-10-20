using UnityEngine;

// Makes a cat react when it's hit by a treat: the body squishes and springs back,
// and the head bobbles a moment later and keeps nodding after the body settles.
// Added automatically to every cat; works on top of the cat's animations.
public class CatJuice : MonoBehaviour
{
    const float SquashStiffness = 260f;
    const float SquashDamping = 11f;
    const float HeadStiffness = 140f;
    const float HeadDamping = 5f;
    const float HeadDelay = 0.06f;

    Transform model;
    Vector3 modelBaseScale;
    Transform head;

    float squash;
    float squashVelocity;
    float headAngle;
    float headVelocity;
    float pendingHeadKick;
    float headKickTimer;
    bool settled = true;

    public static CatJuice AddTo(CatMovement cat, Animator animator)
    {
        CatJuice juice = cat.GetComponent<CatJuice>();
        if (juice == null)
        {
            juice = cat.gameObject.AddComponent<CatJuice>();
            juice.model = animator != null ? animator.transform : cat.transform;
            juice.modelBaseScale = juice.model.localScale;
            juice.head = FindHead(juice.model);
        }
        return juice;
    }

    static Transform FindHead(Transform root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            string lower = child.name.ToLowerInvariant();
            if (lower.StartsWith("head") && lower.EndsWith("jnt"))
            {
                return child;
            }
        }
        return null;
    }

    // strength: about 1 for a normal hit, more for a big one
    public void Hit(float strength)
    {
        squashVelocity -= 9f * strength;
        pendingHeadKick += 220f * strength;
        headKickTimer = HeadDelay;
        settled = false;
    }

    public void ResetPose()
    {
        squash = squashVelocity = headAngle = headVelocity = pendingHeadKick = 0;
        settled = true;
        if (model != null) model.localScale = modelBaseScale;
    }

    void OnDisable()
    {
        ResetPose();
    }

    // LateUpdate runs after the animator, so these reactions add on top of the animation
    void LateUpdate()
    {
        if (settled) return;

        float dt = Mathf.Min(Time.deltaTime, 0.033f);

        squashVelocity += (-SquashStiffness * squash - SquashDamping * squashVelocity) * dt;
        squash += squashVelocity * dt;

        if (pendingHeadKick != 0)
        {
            headKickTimer -= dt;
            if (headKickTimer <= 0)
            {
                headVelocity += pendingHeadKick;
                pendingHeadKick = 0;
            }
        }
        headVelocity += (-HeadStiffness * headAngle - HeadDamping * headVelocity) * dt;
        headAngle += headVelocity * dt;

        // Squash down and wide, then stretch up and thin as it springs back
        float s = Mathf.Clamp(squash, -0.35f, 0.35f);
        model.localScale = Vector3.Scale(modelBaseScale, new Vector3(1f - s * 0.6f, 1f + s, 1f - s * 0.6f));

        if (head != null)
        {
            head.localRotation *= Quaternion.Euler(Mathf.Clamp(headAngle, -35f, 35f), 0, 0);
        }

        if (Mathf.Abs(squash) < 0.001f && Mathf.Abs(squashVelocity) < 0.01f
            && Mathf.Abs(headAngle) < 0.1f && Mathf.Abs(headVelocity) < 0.5f && pendingHeadKick == 0)
        {
            ResetPose();
        }
    }
}
