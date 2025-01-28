using System.Collections;
using System.Collections.Generic;
using Seb.Fluid.Simulation;
using UnityEngine;

[ExecuteAlways]
public class Sun : MonoBehaviour
{
    public float dst;
    public Transform sunLight;
    bool anim;
    float animT = 0;
    Quaternion startRot;
    Quaternion targetRot;


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            anim = true;
            startRot = sunLight.rotation;
            sunLight.Rotate(Vector3.up * 179);
            targetRot = sunLight.rotation;
            
        }

        if (anim)
        {
            animT += Time.deltaTime;
            sunLight.rotation = Quaternion.Slerp(startRot, targetRot, EaseQuadInOut(animT / 3));
        }
        transform.position = -sunLight.forward * dst;
        
        
    }
    
    
    public static float EaseQuadIn(float t) => Square(Clamp01(t));
    public static float EaseQuadOut(float t) => 1 - Square(1 - Clamp01(t));
    public static float EaseQuadInOut(float t) => 3 * Square(Clamp01(t)) - 2 * Cube(Clamp01(t));

    public static float EaseCubeIn(float t) => Cube(Clamp01(t));
    public static float EaseCubeOut(float t) => 1 - Cube(1 - Clamp01(t));

    public static float EaseCubeInOut(float t)
    {
        t = Clamp01(t);
        int r = (int)System.Math.Round(t);
        return 4 * Cube(t) * (1 - r) + (1 - 4 * Cube(1 - t)) * r;
    }

    public static float Clamp01(float t) => Mathf.Clamp(t, 0, 1);
    public static float Square(float x) => x * x;
    public static float Cube(float x) => x * x * x;
    public static float Quart(float x) => x * x * x * x;
    public static float Abs(float x) => System.Math.Abs(x);
    
}
