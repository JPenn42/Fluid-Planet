using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GravitySim : MonoBehaviour
{
	const float G = 1;
	
	public Body[] bodies;

	public bool relative;
	public bool autoStep;
	public int simSteps = 1;

	public bool showPrediction;
	public bool hidePredictionOnPlay = true;
	public int predictionStepCount;
	public float predictionDt;
	public bool stopPredictionAtImpact;

	public BodyDisplay[] bodyDisplays;
	public float bodyDisplayRadiusOffset;
	public bool usePredictionDeltaTime;

	public BodyDisplay PlanetDisplay => bodyDisplays[0];
	public Vector3 relativeAcc { get; private set; }
	Body[] nextStates;

	public void Step(float deltaTime)
	{
		relativeAcc = Step(bodies, deltaTime, relative);
		UpdateDisplayBodies();
	}

	public static Vector3 Step(Span<Body> bodies, float deltaTime, bool relative)
	{
		Span<Vector3> accelerations = stackalloc Vector3[bodies.Length];
		

		for (int a = 0; a < bodies.Length - 1; a++)
		{
			for (int b = a + 1; b < bodies.Length; b++)
			{
				float massA = bodies[a].Mass;
				float massB = bodies[b].Mass;
				Vector3 offset = bodies[a].Position - bodies[b].Position;
				float sqrDst = offset.sqrMagnitude;
				float force = G * massA * massB / sqrDst;

				Vector3 dir = offset.normalized;
				Vector3 accA = -dir * force / massA;
				Vector3 accB = dir * force / massB;

				accelerations[a] += accA;
				accelerations[b] += accB;
			}
		}

		Vector3 relativeAcc = Vector3.zero;

		if (relative)
		{
			relativeAcc = accelerations[0];
			for (int i = 0; i < accelerations.Length; i++)
			{
				accelerations[i] -= relativeAcc;
			}
		}

		for (int i = 0; i < bodies.Length; i++)
		{
			bodies[i].Velocity += accelerations[i] * deltaTime;
			bodies[i].Position += bodies[i].Velocity * deltaTime;
		}

		return relativeAcc;
	}


	void Update()
	{
		if (bodies == null) return;

		if (Application.isPlaying && autoStep)
		{
			for (int i = 0; i < simSteps; i++)
			{
				Step(bodies, usePredictionDeltaTime ? predictionDt : Time.deltaTime, relative);
			}

			UpdateDisplayBodies();
		}

		bool pred = showPrediction && (!Application.isPlaying || !hidePredictionOnPlay);
		if (pred)
		{
			Span<Body> predictedBodies = stackalloc Body[bodies.Length];

			for (int i = 0; i < bodies.Length; i++)
			{
				predictedBodies[i] = bodies[i];
				bodyDisplays[i].body.position = bodies[i].Position;

				if (bodyDisplays[i].applyScaleToBody)
				{
					bodyDisplays[i].body.localScale = Vector3.one * (bodies[i].Radius * 2 + bodyDisplayRadiusOffset);
				}

				bodyDisplays[i].pathRenderer.positionCount = predictionStepCount;
			}

			if (showPrediction)
			{
				for (int i = 0; i < predictedBodies.Length; i++)
				{
					bodyDisplays[i].StartDrawingPath();
				}

				for (int stepIndex = 0; stepIndex < predictionStepCount; stepIndex++)
				{
					Step(predictedBodies, predictionDt, relative);
					if (stopPredictionAtImpact && Impact(predictedBodies))
					{
						break;
					}

					for (int i = 0; i < predictedBodies.Length; i++)
					{
						bodyDisplays[i].AddPathPoint(predictedBodies[i].Position);
					}
				}

				for (int i = 0; i < predictedBodies.Length; i++)
				{
					bodyDisplays[i].FinishDrawingPath();
				}
			}
		}

		for (int i = 0; i < bodies.Length; i++)
		{
			bodyDisplays[i].pathRenderer.enabled = pred;
		}
	}

	bool Impact(Span<Body> bodies)
	{
		for (int i = 0; i < bodies.Length - 1; i++)
		{
			for (int j = i + 1; j < bodies.Length; j++)
			{
				float r = bodies[i].Radius / 2 + bodies[j].Radius / 2;
				//r *= r;

				if ((bodies[i].Position - bodies[j].Position).magnitude < r) return true;
			}
		}

		return false;
	}

	void UpdateDisplayBodies()
	{
		for (int i = 0; i < bodies.Length; i++)
		{
			bodyDisplays[i].body.position = bodies[i].Position;

			if (bodyDisplays[i].applyScaleToBody)
			{
				bodyDisplays[i].body.localScale = Vector3.one * bodies[i].Radius * 2;
			}
		}
	}


	[System.Serializable]
	public struct Body
	{
		public Vector3 Position;
		public Vector3 Velocity;
		public float Mass;
		public float Radius;
	}


	[System.Serializable]
	public class BodyDisplay
	{
		public Transform body;
		public LineRenderer pathRenderer;
		public bool applyScaleToBody;

		List<Vector3> pathPoints = new();

		public void StartDrawingPath()
		{
			pathPoints.Clear();
		}


		public void AddPathPoint(Vector3 p)
		{
			if (pathPoints.Count == 0 || (p - pathPoints[^1]).sqrMagnitude > 0.05f)
			{
				pathPoints.Add(p);
			}
		}

		public void FinishDrawingPath()
		{
			pathRenderer.positionCount = pathPoints.Count;
			for (int i = 0; i < pathPoints.Count; i++)
			{
				pathRenderer.SetPosition(i, pathPoints[i]);
			}
		}
	}
}