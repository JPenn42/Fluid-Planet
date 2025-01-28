using System.Collections;
using System.Collections.Generic;
using Seb.Fluid.Simulation;
using UnityEngine;

public class Spin : MonoBehaviour
{
    public float spinSpeed;
    
    void Start()
    {
        FindObjectOfType<FluidSim>().FluidStep += UpdateRot;
    }


    // Update is called once per frame
    void UpdateRot(float dt)
    {
        transform.Rotate(Vector3.up * spinSpeed * dt);
    }
}
