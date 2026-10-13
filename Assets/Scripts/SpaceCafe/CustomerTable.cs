using System.Collections.Generic;
using UnityEngine;

// A table customers sit at. Cats come out of the portal, hop onto the stand point and order.
// Created by Catastrophe Cafe > Space Cafe > Generate Layout, but can be moved or set up by hand.
public class CustomerTable : MonoBehaviour
{
    public static readonly List<CustomerTable> all = new List<CustomerTable>();

    [SerializeField]
    [Tooltip("Where the cat stands while ordering (on the table top).")]
    Transform standPoint;

    [SerializeField]
    [Tooltip("The portal the cat arrives and leaves through.")]
    PortalRing portal;

    [SerializeField]
    [Tooltip("Angle from the player's starting direction, in degrees. Tables nearer the front open first.")]
    float angleFromFront;

    public CustomerCat customer { get; set; }
    public bool isFree => customer == null || !customer.gameObject.activeInHierarchy;
    public Transform stand => standPoint;
    public PortalRing portalRing => portal;
    public float angle => Mathf.Abs(angleFromFront);

    public void Setup(Transform stand, PortalRing portalRing, float angleFromPlayerFront)
    {
        standPoint = stand;
        portal = portalRing;
        angleFromFront = angleFromPlayerFront;
    }

    void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    void OnDisable()
    {
        all.Remove(this);
    }

    void OnDrawGizmos()
    {
        if (standPoint != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            Gizmos.DrawWireCube(standPoint.position + Vector3.up * 0.15f, new Vector3(0.22f, 0.3f, 0.22f));
        }
        if (portal != null)
        {
            Gizmos.color = new Color(0.6f, 0.3f, 1f);
            Gizmos.DrawWireSphere(portal.transform.position, 0.3f);
        }
    }
}
