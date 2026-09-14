using UnityEngine;
using UnityEngine.AI;

namespace CatAnimation
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CatRunAnimation : MonoBehaviour
    {
        [SerializeField] Animator runAnimator;
        [SerializeField, Min(0f)] float movingThreshold = 0.05f;

        static readonly int RunSpeed = Animator.StringToHash("RunSpeed");
        NavMeshAgent agent;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (runAnimator == null || runAnimator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("Assign the animated cat's run Animator.", this);
                enabled = false;
            }
        }

        void OnEnable()
        {
            SetRunning(false);
        }

        void Update()
        {
            bool moving = false;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && !agent.isStopped)
            {
                Vector3 velocity = agent.velocity;
                velocity.y = 0f;
                moving = velocity.sqrMagnitude > movingThreshold * movingThreshold;
            }
            SetRunning(moving);
        }

        void OnDisable()
        {
            SetRunning(false);
        }

        void SetRunning(bool moving)
        {
            // Only the Run state's clock pauses. Feeding/disappearing stays independent.
            if (runAnimator != null && runAnimator.runtimeAnimatorController != null)
                runAnimator.SetFloat(RunSpeed, moving ? 1f : 0f);
        }
    }
}
