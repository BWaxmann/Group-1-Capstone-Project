using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PowerUpManager : MonoBehaviour
{
    [Header("Power-Up Durations")]
    public float infiniteAmmoDuration = 10f;
    public float scoreMultiplierDuration = 10f;
    public float yarnTranceDuration = 5f;
    public float shieldDuration = 10f;

    private void Start()
    {
        Debug.Log("PowerUpManager ready.");
    }
}