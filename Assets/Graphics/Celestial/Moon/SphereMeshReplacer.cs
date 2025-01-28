using System.Collections;
using System.Collections.Generic;
using Seb.Helpers;
using UnityEngine;

public class SphereMeshReplacer : MonoBehaviour
{
	public int resolution = 10;
	int resOld = -1;

	void Start()
	{
		Replace();
	}

	void Update()
	{
		if (resOld != resolution)
		{
			Replace();
		}
	}

	void Replace()
	{
		resOld = resolution;
		GetComponent<MeshFilter>().sharedMesh = SphereGenerator.GenerateSphereMesh(resolution, 0.5f);
	}
}