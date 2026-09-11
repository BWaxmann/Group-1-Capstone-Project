using System.Collections;
using UnityEngine;

public class Speen : MonoBehaviour
{
    [SerializeField]
    Vector3 axis = Vector3.up;

    [SerializeField]
    [Min(0)]
    float rotationSpeed = 10f;

    [SerializeField]
    [Min(0)]
    float updateFrequency = 0.03f;

    void OnEnable()
    {
        StartCoroutine(SpeenRoutine());
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    IEnumerator SpeenRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(updateFrequency);
        while (true)
        {
            transform.Rotate(axis, rotationSpeed * updateFrequency);
            yield return wait;
        }
    }
}
