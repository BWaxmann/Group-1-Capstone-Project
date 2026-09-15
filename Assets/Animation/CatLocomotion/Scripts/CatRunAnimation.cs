using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CatRunAnimation : MonoBehaviour
{
    [SerializeField] Animator runAnimator;
    [SerializeField, Min(0f)] float movingThreshold = 0.05f;

    static readonly int RunSpeed = Animator.StringToHash("RunSpeed");
    NavMeshAgent agent;
    CatLinkJump catJump;

    void Awake()
    {
        if (runAnimator == null || runAnimator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("Assign the animated cat's run Animator.", this);
            enabled = false;
        }
        agent = GetComponent<NavMeshAgent>();
        catJump = GetComponent<CatLinkJump>();
    }

    void OnEnable()
    {
        SetRunning(false);
        StartCoroutine(RunRoutine());
    }

    void OnDisable()
    {
        SetRunning(false);
        StopAllCoroutines();
    }

    IEnumerator RunRoutine()
    {
        WaitWhile waitForJump = null;
        if (catJump != null)
        {
            waitForJump = new WaitWhile(() => catJump.IsCrossing);
        }

        while (true)
        {
            yield return waitForJump;

            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && !agent.isStopped)
            {
                Vector3 velocity = agent.velocity;
                velocity.y = 0f;
                SetRunning(velocity.magnitude > movingThreshold);
            }
            else
            {
                SetRunning(false);
            }
        }
    }

    void SetRunning(bool moving)
    {
        // Only the Run state's clock pauses. Feeding/disappearing stays independent.
        if (runAnimator != null && runAnimator.runtimeAnimatorController != null)
            runAnimator.SetFloat(RunSpeed, moving ? agent.velocity.magnitude : 0f);
    }
}