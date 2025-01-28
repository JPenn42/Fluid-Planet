using System;
using Seb.Helpers;
using UnityEngine;

public class LoadSDF : MonoBehaviour
{
	public TextAsset sdfFile;
	public ComputeShader compute;

	[Header("SDF")] public Vector3 sdfWorldSize;
	public RenderTexture sdfVolume;

	void Awake()
	{
		string dataString = sdfFile.text;

		string[] entries = dataString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		int sizeX = int.Parse(entries[0]);
		int sizeY = int.Parse(entries[1]);
		int sizeZ = int.Parse(entries[2]);
		sdfWorldSize = new Vector3(float.Parse(entries[3]), float.Parse(entries[4]), float.Parse(entries[5]));

		float[] distances = new float[sizeX * sizeY * sizeZ];
		for (int i = 0; i < distances.Length; i++)
		{
			distances[i] = float.Parse(entries[i + 6]);
		}

		ComputeHelper.CreateRenderTexture3D(ref sdfVolume, sizeX, sizeY, sizeZ, ComputeHelper.defaultGraphicsFormat, TextureWrapMode.Clamp);

		ComputeBuffer buffer = ComputeHelper.CreateStructuredBuffer(distances);
		compute.SetBuffer(0, "Data", buffer);
		compute.SetTexture(0, "VolumeTex", sdfVolume);
		compute.SetInt("sizeX", sizeX);
		compute.SetInt("sizeY", sizeY);
		compute.SetInt("sizeZ", sizeZ);
		ComputeHelper.Dispatch(compute, sizeX, sizeY, sizeZ);

		buffer.Release();
	}

	void OnDestroy()
	{
		ComputeHelper.Release(sdfVolume);
	}
}