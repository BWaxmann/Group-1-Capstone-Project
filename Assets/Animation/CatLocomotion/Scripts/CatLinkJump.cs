using UnityEngine;
using UnityEngine.AI;

namespace CatAnimation
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CatLinkJump : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] AnimationCurve upForward;
        [SerializeField] AnimationCurve upHeight;
        [SerializeField] AnimationCurve downForward;
        [SerializeField] AnimationCurve downHeight;
        [SerializeField] float upDuration = 1.708333f;
        [SerializeField] float downDuration = 1.625f;
        [SerializeField] float minimumJumpHeight = 0.08f;

        NavMeshAgent agent;
        bool previousAutoTraverse;
        bool previousUpdatePosition;
        bool crossing;
        bool jumping;
        bool upwards;
        float elapsed;
        float duration;
        Vector3 start;
        Vector3 end;
        Quaternion facing;
        static readonly int JumpUp = Animator.StringToHash("JumpUp");
        static readonly int JumpDown = Animator.StringToHash("JumpDown");
        static readonly int Run = Animator.StringToHash("Run");

        public bool IsCrossing { get { return crossing; } }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        void OnEnable()
        {
            previousAutoTraverse = agent.autoTraverseOffMeshLink;
            agent.autoTraverseOffMeshLink = false;
            crossing = false;
        }

        void Update()
        {
            if (!agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                Finish(false);
                return;
            }
            if (crossing && !agent.isOnOffMeshLink)
            {
                // A warp or reset interrupted the crossing.
                Finish(false);
                return;
            }
            if (!crossing)
            {
                if (!agent.isOnOffMeshLink || agent.isStopped) return;
                Begin();
            }

            // Finish a committed jump even if attack/stun stops navigation in flight.
            // Leave isStopped untouched, so gameplay remains in control after landing.
            if (jumping || !agent.isStopped) elapsed += Time.deltaTime;
            if (jumping) animator.Play(upwards ? JumpUp : JumpDown, 0, Mathf.Clamp01(elapsed / duration));
        }

        void LateUpdate()
        {
            if (!crossing) return;
            float t = Mathf.Clamp01(elapsed / duration);
            float forward = jumping ? (upwards ? upForward : downForward).Evaluate(t) : t;
            float height = jumping ? (upwards ? upHeight : downHeight).Evaluate(t) : t;
            Vector3 horizontal = end - start;
            horizontal.y = 0f;
            transform.position = start + horizontal * forward + Vector3.up * ((end.y - start.y) * height);
            transform.rotation = facing;
            if (t >= 1f) Finish(true);
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
            elapsed = 0f;
            previousUpdatePosition = agent.updatePosition;
            agent.updatePosition = false;
            crossing = true;
        }

        void Finish(bool complete)
        {
            if (!crossing) return;
            crossing = false;
            if (complete && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.isOnOffMeshLink)
            {
                transform.position = end;
                agent.CompleteOffMeshLink();
            }
            agent.updatePosition = previousUpdatePosition;
            if (animator != null && animator.isActiveAndEnabled)
                animator.CrossFadeInFixedTime(Run, 0.1f, 0, 0f);
        }

        void OnDisable()
        {
            Finish(false);
            agent.autoTraverseOffMeshLink = previousAutoTraverse;
        }
    }
}
