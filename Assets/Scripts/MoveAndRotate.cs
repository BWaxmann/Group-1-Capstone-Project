using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveAndRotate : MonoBehaviour
{
    public Vector3andSpace rotateDegreesPerSecond;
    public Vector3andSpace moveUnitsPerSecond;

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(rotateDegreesPerSecond.value * Time.deltaTime, moveUnitsPerSecond.space);
        transform.Translate(moveUnitsPerSecond.value * Time.deltaTime, moveUnitsPerSecond.space);
    }

    //Nested class
    [Serializable]

    public class Vector3andSpace
    {
        public Vector3 value;
        public Space space = Space.Self;

    }
}
