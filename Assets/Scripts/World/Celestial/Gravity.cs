using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class Gravity : MonoBehaviour
{
    [SerializeField]
    [Header("Gravity Settings")]
    private int gravityRadius;
    
    private static List<Gravity> gravities = new List<Gravity>();
    public static IReadOnlyList<Gravity> ActiveGravities => gravities;

    private void Awake()
    {
        Rigidbody rigidbody = GetComponent<Rigidbody>();
        rigidbody.isKinematic = true;
    }

    private void Start()
    {
        if(!gravities.Contains(this)) gravities.Add(this);
    }

    private void OnEnable()
    {
        if(!gravities.Contains(this)) gravities.Add(this);
    }

    private void OnDisable()
    {
        if(gravities.Contains(this)) gravities.Remove(this);
    }

    public int GravityRadius
    {
        get { return gravityRadius; }
        set
        {
            gravityRadius = Mathf.Max(0, value);
        }
    }

    public List<Gravity> Gravities
    {
        get { return gravities; }
    }
}
