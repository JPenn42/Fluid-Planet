using System.Collections;
using System.Collections.Generic;
using System.Text;
using Seb.AccelerationStructures;
using Seb.Helpers;
using UnityEngine;
using System.IO;
using Input = UnityEngine.Input;

public class GenSDF : MonoBehaviour
{
	public int resolution;
	public float boundsSize;
	public float pointDisplayRadius;
	public MeshFilter meshFilter;
	public int dirCount = 10;
	public string saveFileName;
	[Header("Debug info")] public int numSamplePoints;

	BVH bvh;
	bool bvhHitTest;
	Vector3 bvhHitPointTest;
	Vector3[] testDirections;

	Vector3[] samplePoints;
	float[] sdf;

	// Start is called before the first frame update
	void Start()
	{
		Mesh mesh = meshFilter.sharedMesh;
		bvh = new BVH(mesh.vertices, mesh.triangles, mesh.normals);


		Debug.Log(bvh.stats);
	}

	// Update is called once per frame
	void Update()
	{
		int numPoints = resolution * resolution * resolution;
		if (samplePoints == null || samplePoints.Length != numPoints)
		{
			samplePoints = new Vector3[numPoints];
			if (sdf == null || sdf.Length != numPoints) sdf = new float[numPoints];
			int i = 0;

			for (int z = 0; z < resolution; z++)
			{
				for (int y = 0; y < resolution; y++)
				{
					for (int x = 0; x < resolution; x++)
					{
						Vector3 pos = (Vector3.Scale(new Vector3(x, y, z) + Vector3.one, Vector3.one / (resolution + 1)) - Vector3.one / 2) * boundsSize;
						samplePoints[i] = pos;
						i++;
					}
				}
			}
		}

		if (testDirections == null || testDirections.Length != numPoints)
		{
			testDirections = Maths.GetPointsOnSphereSurface(dirCount, 1);
		}
		//(bvhHitTest, bvhHitPointTest) = bvh.Search(transform.position, transform.forward);


		//var sw = System.Diagnostics.Stopwatch.StartNew();
		//float dst = EstimateDistance(transform.position);
		//Debug.Log("Dst estim: " + dst + "  Time = " + sw.ElapsedMilliseconds + "ms");

		if (Input.GetKeyDown(KeyCode.Space))
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();

			System.Threading.Tasks.Parallel.For(0, samplePoints.Length, i =>
			{
				Vector3 p = samplePoints[i];
				sdf[i] = EstimateDistance(p);
			});

			Debug.Log("Completed sdf in " + sw.ElapsedMilliseconds + " milliseconds");

			WriteSDF();
		}
	}

	void WriteSDF()
	{
		StringBuilder sb = new();
		sb.Append($"{resolution} {resolution} {resolution} ");
		sb.Append($"{boundsSize} {boundsSize} {boundsSize} ");
		for (int i = 0; i < sdf.Length; i++)
		{
			sb.Append($"{sdf[i]:0.####} ");
		}

		string path = Path.Combine(Application.dataPath, saveFileName + ".txt");
		File.WriteAllText(path, sb.ToString());
		Debug.Log("Written to " + path);
	}

	float EstimateDistance(Vector3 point)
	{
		float dst = float.MaxValue;
		int numBackface = 0;
		int numFrontface = 0;

		for (int i = 0; i < testDirections.Length; i++)
		{
			var hitInfo = bvh.Search(point, testDirections[i]);
			if (hitInfo.hit)
			{
				numBackface += hitInfo.backface ? 1 : 0;
				numFrontface += hitInfo.backface ? 0 : 1;
				if (hitInfo.dst < dst)
				{
					dst = hitInfo.dst;
				}
			}
		}

		bool inside = numBackface > numFrontface;
		return dst * (inside ? -1f : 1f);
	}

	void OnDrawGizmos()
	{
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireCube(Vector3.zero, Vector3.one * boundsSize);

		numSamplePoints = resolution * resolution * resolution;

		if (Application.isPlaying)
		{
			Gizmos.color = new Color(1, 0, 0, 0.3f);

			/*
			for (int i = 0; i < samplePoints.Length; i++)
			{
				Gizmos.DrawSphere(samplePoints[i], pointDisplayRadius);
			}
			*/

			if (bvhHitTest)
			{
				Gizmos.color = Color.green;
				Gizmos.DrawLine(transform.position, bvhHitPointTest);
				Gizmos.DrawSphere(bvhHitPointTest, pointDisplayRadius);
			}
			else
			{
				Gizmos.color = Color.white;
				Gizmos.DrawRay(transform.position, transform.forward * 100);
			}

			Gizmos.color = Color.yellow;
			for (int i = 0; i < testDirections.Length; i++)
			{
				Gizmos.DrawRay(transform.position, testDirections[i]);
			}
		}
	}
}