using System.Collections;
using System.Collections.Generic;
using Seb.Fluid.Simulation;
using Seb.Helpers;
using UnityEngine;

public class PlanetDisplay : MonoBehaviour
{
	public int meshResolution;

	public FluidSim sim;
	public Shader shaderShaded;
	public float displayRadiusOffset;
	public Transform sun;
	public float shadingPow;

	Mesh mesh;
	Material mat;
	ComputeBuffer argsBuffer;
	int meshResOld = -1;

	void Awake()
	{
		sim.SimulationInitCompleted += Init;
	}

	void Init(FluidSim sim)
	{
		

		mat = new Material(shaderShaded);
		mat.SetBuffer("Bodies", sim.celestialBodyBuffer);
	}

	void LateUpdate()
	{
		if (meshResOld != meshResolution)
		{
			mesh = SphereGenerator.GenerateSphereMesh(meshResolution);
			ComputeHelper.CreateArgsBuffer(ref argsBuffer, mesh, sim.celestialBodyBuffer.count);
			meshResOld = meshResolution;
		}
		
		mat.SetVector("dirToSun", -sun.forward);
		mat.SetFloat("shadingPow", shadingPow);
		
		mat.SetFloat("radiusOffset", displayRadiusOffset);
		Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
		Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, bounds, argsBuffer);
	}
}