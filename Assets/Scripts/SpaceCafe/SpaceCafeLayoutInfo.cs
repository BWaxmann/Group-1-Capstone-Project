using System.Collections.Generic;
using UnityEngine;

// Sits on the generated Space Cafe layout and remembers which existing scene objects the generator
// switched off to make room, so regenerating can switch them back on first. Nothing is deleted.
public class SpaceCafeLayoutInfo : MonoBehaviour
{
    [Tooltip("Scene objects switched off to make room for the layout. Re-enable any you want back.")]
    public List<GameObject> disabledObjects = new List<GameObject>();

    [Tooltip("Colliders switched off so decoration (register, UI boards) doesn't block treats.")]
    public List<Collider> disabledColliders = new List<Collider>();

    [Tooltip("Scene objects the generator moved, and where they were before.")]
    public List<GameObject> movedObjects = new List<GameObject>();
    public List<Vector3> originalPositions = new List<Vector3>();
    public List<Quaternion> originalRotations = new List<Quaternion>();
    public List<Vector3> originalScales = new List<Vector3>();
    public List<Transform> originalParents = new List<Transform>();
}
