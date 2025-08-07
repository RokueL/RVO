using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RVoDebugAgent : MonoBehaviour
{
    public Transform Transform;
    public Vector3 Target;
    public float MoveSpeed;
    public float Radius;
    public int Priority;
    public bool isAgent;

    public Vector3 Velocity;

    public List<Vector3> targets = new List<Vector3>();
    public int pathCount = 0;
    public void Awake()
    {
        if (Transform == null)
            Transform = GetComponent<Transform>();
    }
}