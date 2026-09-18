using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CatLinkJump : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] AnimationCurve upForward;
    [SerializeField] AnimationCurve upHeight;
    [SerializeField] AnimationCurve downForward;
    [SerializeField] AnimationCurve downHeight;
    [SerializeField] float upDuration = 1.708333f;
    [SerializeField] float downDuration = 1.625f;
    [SerializeField] float minimumJumpHeight = 0.08f;
    [SerializeField] float jumpUpAnimationLength = 1f;
    [SerializeField] float jumpDownAnimationLength = 1f;

    NavMeshAgent agent;
    CatMovement movement;
    bool previousUpdatePosition;
    bool crossing;
    bool jumping;
    bool upwards;
    float duration;
    Vector3 start;
    Vector3 end;
    Quaternion facing;
    static readonly int jumpUpHash = Animator.StringToHash("JumpingUp");
    static readonly int jumpDownHash = Animator.StringToHash("JumpingDown");
    static readonly int jumpSpeedHash = Animator.StringToHash("JumpSpeed");

    public bool IsCrossing { get { return crossing; } }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.autoTraverseOffMeshLink = false;
        }
        movement = GetComponent<CatMovement>();
    }

    void OnEnable()
    {
        if (agent != null)
        {
            StartCoroutine(JumpRoutine());
        }
    }

    void OnDisable()
    {
        Finish(false);
        StopAllCoroutines();
    }

    IEnumerator JumpRoutine()
    {
        yield return null;

        WaitUntil waitForJump = new WaitUntil(() => agent.isOnOffMeshLink);
        float time;
        bool complete;
        while (!movement.isFullyFed)
        {
            yield return waitForJump;

            Begin();

            time = 0;
            complete = true;

            while (time < duration)
            {
                time += Time.deltaTime;
                JumpMovement(time);

                yield return null;

                if (!agent.isActiveAndEnabled || !agent.isOnNavMesh || !agent.isOnOffMeshLink)
                {
                    complete = false;
                    break;
                }
                if (movement.isFullyFed)
                {
                    yield break;
                }
            }

            Finish(complete);
        }
    }

    void Begin()
    {
        start = transform.position;
        end = agent.currentOffMeshLinkData.endPos + Vector3.up * agent.baseOffset;
        Vector3 direction = end - start;
        upwards = direction.y > 0f;
        jumping = Mathf.Abs(direction.y) >= minimumJumpHeight && animator != null;
        direction.y = 0f;
        facing = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : transform.rotation;
        duration = jumping ? (upwards ? upDuration : downDuration) : Mathf.Max(0.1f, Vector3.Distance(start, end) / Mathf.Max(agent.speed, 0.1f));
        previousUpdatePosition = agent.updatePosition;
        agent.updatePosition = false;
        crossing = true;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(upwards ? jumpUpHash : jumpDownHash, true);
            animator.SetFloat(jumpSpeedHash, (upwards ? jumpUpAnimationLength : jumpDownAnimationLength) / duration);
        }
    }

    void JumpMovement(float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / duration);
        float forward = jumping ? (upwards ? upForward : downForward).Evaluate(t) : t;
        float height = jumping ? (upwards ? upHeight : downHeight).Evaluate(t) : t;
        Vector3 horizontal = end - start;
        horizontal.y = 0f;
        transform.position = start + horizontal * forward + Vector3.up * ((end.y - start.y) * height);
        transform.rotation = facing;
    }

    void Finish(bool complete)
    {
        crossing = false;
        if (complete && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.isOnOffMeshLink)
        {
            transform.position = end;
            agent.CompleteOffMeshLink();
        }
        agent.updatePosition = previousUpdatePosition;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(upwards ? jumpUpHash : jumpDownHash, false);
        }
    }
}
