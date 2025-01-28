using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using static System.Math;
using Random = System.Random;

namespace Seb.Fluid.Simulation
{
	public class Spawner3D : MonoBehaviour
	{
		public int particleCount;
		public float spawnRadiusMin;
		public float spawnRadiusMax;

		public float3 initialVel;
		public bool showSpawnBounds;


		public SpawnData GetSpawnData()
		{
			List<float3> allPoints = new();
			Random rng = new(42);

			allPoints.AddRange(GetPointsOnSphereSurface(particleCount, 1));
			for (int i = 0; i < allPoints.Count; i++)
			{
				float radT = (float)rng.NextDouble();
				radT = Mathf.Pow(radT, 1 / 3f);
				allPoints[i] = allPoints[i] * Mathf.Lerp(spawnRadiusMin, spawnRadiusMax, radT);
			}

			List<float3> allVelocities = allPoints.Select(p => initialVel).ToList();

			return new SpawnData() { points = allPoints.ToArray(), velocities = allVelocities.ToArray() };
		}


		void OnDrawGizmos()
		{
			if (showSpawnBounds && !Application.isPlaying)
			{
				Color col = Color.red;
				Gizmos.color = col * 0.5f;
				Gizmos.DrawWireSphere(Vector3.zero, spawnRadiusMin);
				Gizmos.color = col;
				Gizmos.DrawWireSphere(Vector3.zero, spawnRadiusMax);
			}
		}

		public static float3[] GetPointsOnSphereSurface(int numPoints, float radius = 1)
		{
			// Thanks to https://stackoverflow.com/questions/9600801/evenly-distributing-n-points-on-a-sphere/44164075#44164075
			float3[] points = new float3[numPoints];
			const double goldenRatio = 1.618033988749894; // (1 + sqrt(5)) / 2
			const double angleIncrement = System.Math.PI * 2 * goldenRatio;

			System.Threading.Tasks.Parallel.For(0, numPoints, i =>
			{
				double t = (double)i / numPoints;
				double inclination = System.Math.Acos(1 - 2 * t);
				double azimuth = angleIncrement * i;

				double x = Sin(inclination) * Cos(azimuth);
				double y = Sin(inclination) * Sin(azimuth);
				double z = Cos(inclination);
				points[i] = new float3((float)x, (float)y, (float)z) * radius;
			});
			return points;
		}

		public struct SpawnData
		{
			public float3[] points;
			public float3[] velocities;
		}
	}
}