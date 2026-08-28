using System.Collections;
using UnityEngine;

public class TrajectoryPreview : MonoBehaviour
{
    [SerializeField]
    LineRenderer trajectoryPreviewLine;

    [SerializeField]
    Transform launchPoint;

    [SerializeField]
    [Tooltip("This should be the same as the Player Input's 'Launch Force'.")]
    [Min(0)]
    float launchSpeed = 10f;

    [SerializeField]
    [Min(0)]
    float cornerSpacing = 0.2f;

    float gravity;
    Vector3 xAxis;
    float cos;
    float sin;

    void Start()
    {
        gravity = Physics.gravity.y;
    }

    void OnEnable()
    {
        trajectoryPreviewLine.gameObject.SetActive(true);
        StartCoroutine(UpdateTrajectoryRoutine());
    }

    void OnDisable()
    {
        trajectoryPreviewLine.gameObject.SetActive(false);
        StopAllCoroutines();
    }

    IEnumerator UpdateTrajectoryRoutine()
    {
        while (true)
        {
            if (trajectoryPreviewLine.gameObject.activeInHierarchy)
            {
                xAxis = launchPoint.forward;
                xAxis.y = 0;
                xAxis.Normalize();

                float angle = Vector3.SignedAngle(xAxis, launchPoint.forward, -launchPoint.right) * Mathf.Deg2Rad;
                cos = Mathf.Cos(angle);
                sin = Mathf.Sin(angle);

                float endTime = GetHitGroundTime();
                int points = Mathf.Max(Mathf.CeilToInt(endTime / cornerSpacing), -1);
                trajectoryPreviewLine.positionCount = points + 1;

                if (points >= 0)
                {
                    for (int i = 0; i < points; i++)
                    {
                        trajectoryPreviewLine.SetPosition(i, GetPositionAtTime(i * cornerSpacing));
                    }
                    trajectoryPreviewLine.SetPosition(points, GetPositionAtTime(endTime));
                }
            }

            yield return null;
        }
    }

    Vector3 GetPositionAtTime(float time)
    {
        return launchPoint.position + launchSpeed * cos * time * xAxis 
            + (launchSpeed * sin + 0.5f * gravity * time) * time * Vector3.up;
    }

    float GetHitGroundTime()
    {
        float ss = launchSpeed * sin;
        return (ss + Mathf.Sqrt(ss * ss - 2 * gravity * launchPoint.position.y)) / (-gravity);
    }
}
