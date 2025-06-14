using System;
using UnityEngine;
using Seb.GPUSorting;
using Unity.Mathematics;
using System.Collections.Generic;
using Seb.Rendering;
using static Seb.Helpers.ComputeHelper;

namespace Seb.Fluid.Simulation
{
	public class FluidSim : MonoBehaviour
	{
		public event Action<FluidSim> SimulationInitCompleted;
		public event Action<float> FluidStep;

		[Header("Planet Test Settings")]
		public bool useGravSim = true;
		public GravitySim gravitySim;
		public bool terraformTest;
		public bool useEarthSDF = true;
		public bool boundToSDF;
		public LoadSDF sdfLoader;
		public TerrainCreator terrainCreator;
		public float sdfCollisionSkin;


		[Header("Time Step")] public float normalTimeScale = 1;
		public float slowTimeScale = 0.1f;
		public float maxTimestepFPS = 60; // if time-step dips lower than this fps, simulation will run slower (set to 0 to disable)
		public int iterationsPerFrame = 3;

		public float smoothingRadius = 0.2f;
		public float targetDensity = 630;
		public float pressureMultiplier = 288;
		public float nearPressureMultiplier = 2.15f;
		public float viscosityStrength = 0;
		[Range(0, 1)] public float collisionDamping = 0.95f;

		[Header("Foam Settings")] public bool foamActive;
		public int maxFoamParticleCount = 1000;
		public float trappedAirSpawnRate = 70;
		public float spawnRateFadeInTime = 0.5f;
		public float spawnRateFadeStartTime = 0;
		public Vector2 trappedAirVelocityMinMax = new(5, 25);
		public Vector2 foamKineticEnergyMinMax = new(15, 80);
		public float bubbleBuoyancy = 1.5f;
		public int sprayClassifyMaxNeighbours = 5;
		public int bubbleClassifyMinNeighbours = 15;
		public float bubbleScale = 0.5f;
		public float bubbleChangeScaleSpeed = 7;

		[Header("Volumetric Render Settings")] public bool renderToTex3D;
		public int densityTextureRes;

		[Header("References")] public ComputeShader compute;
		Spawner3D spawner;

		[HideInInspector] public RenderTexture DensityMap;
		public Vector3 Scale => transform.localScale;

		// Buffers
		public ComputeBuffer foamBuffer { get; private set; }
		public ComputeBuffer foamSortTargetBuffer { get; private set; }
		public ComputeBuffer foamCountBuffer { get; private set; }
		public ComputeBuffer positionBuffer { get; private set; }
		public ComputeBuffer velocityBuffer { get; private set; }
		public ComputeBuffer densityBuffer { get; private set; }
		public ComputeBuffer predictedPositionsBuffer;
		public ComputeBuffer spatialKeys { get; private set; }
		public ComputeBuffer spatialOffsets { get; private set; }
		public ComputeBuffer sortedIndices { get; private set; }
		public ComputeBuffer debugBuffer { get; private set; }

		ComputeBuffer sortTarget_positionBuffer;
		ComputeBuffer sortTarget_velocityBuffer;
		ComputeBuffer sortTarget_predictedPositionsBuffer;

		public ComputeBuffer celestialBodyBuffer { get; private set; }

		// Kernel IDs
		const int externalForcesKernel = 0;
		const int spatialHashKernel = 1;
		const int reorderKernel = 2;
		const int reorderCopybackKernel = 3;
		const int densityKernel = 4;
		const int pressureKernel = 5;
		const int viscosityKernel = 6;
		const int updatePositionsKernel = 7;
		const int renderKernel = 8;
		const int foamUpdateKernel = 9;
		const int foamReorderCopyBackKernel = 10;

		// Sorting
		GPUCountSort gpuSort;
		SpatialOffsetCalculator spatialOffsetsCalc;

		// State
		bool isPaused;
		bool pauseNextFrame;
		float smoothRadiusOld;
		float simTimer;
		bool inSlowMode;
		Spawner3D.SpawnData spawnData;
		Dictionary<ComputeBuffer, string> bufferNameLookup;

		void Start()
		{
			Debug.Log("FluidSim Controls: Space = Play/Pause, Q = SlowMode, R = Reset");
			isPaused = false;

			Initialize();
		}

		void Initialize()
		{
			spawner = FindObjectOfType<Spawner3D>();
			spawnData = spawner.GetSpawnData();

			// Create buffers
			int numParticles = spawnData.points.Length;
			positionBuffer = CreateStructuredBuffer<float3>(numParticles);
			predictedPositionsBuffer = CreateStructuredBuffer<float3>(numParticles);
			velocityBuffer = CreateStructuredBuffer<float3>(numParticles);
			densityBuffer = CreateStructuredBuffer<float2>(numParticles);
			spatialKeys = CreateStructuredBuffer<uint>(numParticles);
			spatialOffsets = CreateStructuredBuffer<uint>(numParticles);
			sortedIndices = CreateStructuredBuffer<uint>(numParticles);
			foamBuffer = CreateStructuredBuffer<FoamParticle>(maxFoamParticleCount);
			foamSortTargetBuffer = CreateStructuredBuffer<FoamParticle>(maxFoamParticleCount);
			foamCountBuffer = CreateStructuredBuffer<uint>(4096);
			debugBuffer = CreateStructuredBuffer<float3>(numParticles);
			if (gravitySim != null)
			{
				celestialBodyBuffer = CreateStructuredBuffer<GravitySim.Body>(gravitySim.bodies.Length);
			}
			else
			{ 
				celestialBodyBuffer = CreateStructuredBuffer<GravitySim.Body>(1);
			}

			sortTarget_positionBuffer = CreateStructuredBuffer<float3>(numParticles);
			sortTarget_predictedPositionsBuffer = CreateStructuredBuffer<float3>(numParticles);
			sortTarget_velocityBuffer = CreateStructuredBuffer<float3>(numParticles);

			bufferNameLookup = new Dictionary<ComputeBuffer, string>
			{
				{ positionBuffer, "Positions" },
				{ predictedPositionsBuffer, "PredictedPositions" },
				{ velocityBuffer, "Velocities" },
				{ densityBuffer, "Densities" },
				{ spatialKeys, "SpatialKeys" },
				{ spatialOffsets, "SpatialOffsets" },
				{ sortedIndices, "SortedIndices" },
				{ sortTarget_positionBuffer, "SortTarget_Positions" },
				{ sortTarget_predictedPositionsBuffer, "SortTarget_PredictedPositions" },
				{ sortTarget_velocityBuffer, "SortTarget_Velocities" },
				{ foamCountBuffer, "WhiteParticleCounters" },
				{ foamBuffer, "WhiteParticles" },
				{ foamSortTargetBuffer, "WhiteParticlesCompacted" },
				{ debugBuffer, "Debug" },
				{ celestialBodyBuffer, "CelestialBodies" },
			};

			// Set buffer data
			SetInitialBufferData(spawnData);

			// External forces kernel
			SetBuffers(compute, externalForcesKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				predictedPositionsBuffer,
				velocityBuffer,
				celestialBodyBuffer
			});

			// Spatial hash kernel
			SetBuffers(compute, spatialHashKernel, bufferNameLookup, new ComputeBuffer[]
			{
				spatialKeys,
				spatialOffsets,
				predictedPositionsBuffer,
				sortedIndices
			});

			// Reorder kernel
			SetBuffers(compute, reorderKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				sortTarget_positionBuffer,
				predictedPositionsBuffer,
				sortTarget_predictedPositionsBuffer,
				velocityBuffer,
				sortTarget_velocityBuffer,
				sortedIndices
			});

			// Reorder copyback kernel
			SetBuffers(compute, reorderCopybackKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				sortTarget_positionBuffer,
				predictedPositionsBuffer,
				sortTarget_predictedPositionsBuffer,
				velocityBuffer,
				sortTarget_velocityBuffer,
				sortedIndices
			});

			// Density kernel
			SetBuffers(compute, densityKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				spatialKeys,
				spatialOffsets
			});

			// Pressure kernel
			SetBuffers(compute, pressureKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				velocityBuffer,
				spatialKeys,
				spatialOffsets,
				foamBuffer,
				foamCountBuffer,
				debugBuffer
			});

			// Viscosity kernel
			SetBuffers(compute, viscosityKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				velocityBuffer,
				spatialKeys,
				spatialOffsets
			});

			// Update positions kernel
			SetBuffers(compute, updatePositionsKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				velocityBuffer,
				celestialBodyBuffer
			});

			// Render to 3d tex kernel
			SetBuffers(compute, renderKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictedPositionsBuffer,
				densityBuffer,
				spatialKeys,
				spatialOffsets,
			});

			// Foam update kernel
			SetBuffers(compute, foamUpdateKernel, bufferNameLookup, new ComputeBuffer[]
			{
				foamBuffer,
				foamCountBuffer,
				predictedPositionsBuffer,
				densityBuffer,
				velocityBuffer,
				spatialKeys,
				spatialOffsets,
				foamSortTargetBuffer,
				celestialBodyBuffer,
				//debugBuffer
			});


			// Foam reorder copyback kernel
			SetBuffers(compute, foamReorderCopyBackKernel, bufferNameLookup, new ComputeBuffer[]
			{
				foamBuffer,
				foamSortTargetBuffer,
				foamCountBuffer,
			});


			compute.SetInt("CelestialBodyCount", celestialBodyBuffer.count);

			compute.SetInt("numParticles", positionBuffer.count);
			compute.SetInt("MaxWhiteParticleCount", maxFoamParticleCount);

			gpuSort = new GPUCountSort(spatialKeys, sortedIndices, (uint)(spatialKeys.count - 1));
			spatialOffsetsCalc = new SpatialOffsetCalculator(spatialKeys, spatialOffsets);

			UpdateSmoothingConstants();


			if (sdfLoader != null)
			{
				compute.SetTexture(updatePositionsKernel, "SDF", sdfLoader.sdfVolume);
				compute.SetTexture(foamUpdateKernel, "SDF", sdfLoader.sdfVolume);
				compute.SetVector("sdfWorldSize", sdfLoader.sdfWorldSize);
			}

			if (terrainCreator != null)
			{
				compute.SetTexture(updatePositionsKernel, "SDF", terrainCreator.densityTexture);
				compute.SetTexture(foamUpdateKernel, "SDF", terrainCreator.densityTexture);
				compute.SetVector("sdfWorldSize", Vector3.one * terrainCreator.scale);
			}

			// Run single frame of sim with deltaTime = 0 to initialize density texture
			// (so that display can work even if paused at start)
			if (renderToTex3D)
			{
				RunSimulationFrame(0);
			}

			SimulationInitCompleted?.Invoke(this);
		}

		void Update()
		{
			// Run simulation
			if (!isPaused)
			{
				float maxDeltaTime = maxTimestepFPS > 0 ? 1 / maxTimestepFPS : float.PositiveInfinity; // If framerate dips too low, run the simulation slower than real-time
				float dt = Mathf.Min(Time.deltaTime * ActiveTimeScale, maxDeltaTime);
				RunSimulationFrame(dt);
				//moon.transform.position = new Vector3(Mathf.Cos(moonAngle), 0, Mathf.Sin(moonAngle)) * moonOrbitDst;
			}

			if (pauseNextFrame)
			{
				isPaused = true;
				pauseNextFrame = false;
			}

			HandleInput();
		}

		void RunSimulationFrame(float frameDeltaTime)
		{
			float subStepDeltaTime = frameDeltaTime / iterationsPerFrame;
			UpdateSettings(subStepDeltaTime, frameDeltaTime);

			// Simulation sub-steps
			for (int i = 0; i < iterationsPerFrame; i++)
			{
				FluidStep?.Invoke(subStepDeltaTime);
				simTimer += subStepDeltaTime;
                if (useGravSim)
				{
					gravitySim.Step(subStepDeltaTime);
				}
				UpdateGravitySimSettings();

				RunSimulationStep();
			}

			// Foam and spray particles
			if (foamActive)
			{
				Dispatch(compute, maxFoamParticleCount, kernelIndex: foamUpdateKernel);
				Dispatch(compute, maxFoamParticleCount, kernelIndex: foamReorderCopyBackKernel);
			}

			// 3D density map
			if (renderToTex3D)
			{
				UpdateDensityMap();
			}
		}

		void UpdateDensityMap()
		{
			float maxAxis = Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
			int w = Mathf.RoundToInt(transform.localScale.x / maxAxis * densityTextureRes);
			int h = Mathf.RoundToInt(transform.localScale.y / maxAxis * densityTextureRes);
			int d = Mathf.RoundToInt(transform.localScale.z / maxAxis * densityTextureRes);
			CreateRenderTexture3D(ref DensityMap, w, h, d, UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat, TextureWrapMode.Clamp);
			//Debug.Log(w + " " + h + "  " + d);
			compute.SetTexture(renderKernel, "DensityMap", DensityMap);
			compute.SetInts("densityMapSize", DensityMap.width, DensityMap.height, DensityMap.volumeDepth);
			Dispatch(compute, DensityMap.width, DensityMap.height, DensityMap.volumeDepth, renderKernel);
		}

		void RunSimulationStep()
		{
			Dispatch(compute, positionBuffer.count, kernelIndex: externalForcesKernel);

			Dispatch(compute, positionBuffer.count, kernelIndex: spatialHashKernel);
			gpuSort.Run();
			spatialOffsetsCalc.Run(false);
			Dispatch(compute, positionBuffer.count, kernelIndex: reorderKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: reorderCopybackKernel);

			Dispatch(compute, positionBuffer.count, kernelIndex: densityKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: pressureKernel);
			if (viscosityStrength != 0) Dispatch(compute, positionBuffer.count, kernelIndex: viscosityKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: updatePositionsKernel);
		}

		void UpdateSmoothingConstants()
		{
			float r = smoothingRadius;
			float spikyPow2 = 15 / (2 * Mathf.PI * Mathf.Pow(r, 5));
			float spikyPow3 = 15 / (Mathf.PI * Mathf.Pow(r, 6));
			float spikyPow2Grad = 15 / (Mathf.PI * Mathf.Pow(r, 5));
			float spikyPow3Grad = 45 / (Mathf.PI * Mathf.Pow(r, 6));

			compute.SetFloat("K_SpikyPow2", spikyPow2);
			compute.SetFloat("K_SpikyPow3", spikyPow3);
			compute.SetFloat("K_SpikyPow2Grad", spikyPow2Grad);
			compute.SetFloat("K_SpikyPow3Grad", spikyPow3Grad);
		}

		void UpdateGravitySimSettings()
		{
            compute.SetBool("firstBodyUseSDF", useEarthSDF);
            compute.SetBool("useGravSim", useGravSim);
            compute.SetBool("boundToSDF", boundToSDF);
            compute.SetFloat("sdfCollisionSkin", sdfCollisionSkin);
            if (useGravSim)
			{
				celestialBodyBuffer.SetData(gravitySim.bodies);
				compute.SetVector("relativeAcc", gravitySim.relativeAcc);
				compute.SetBool("useRelativeAcc", gravitySim.relative);
			}
			

			// Body 0
			if (terraformTest)
			{
				compute.SetMatrix("planetWorldToLocal", Matrix4x4.identity);
			}
            else if (useGravSim)
			{
				Transform body0 = gravitySim.PlanetDisplay.body;
				compute.SetMatrix("planetWorldToLocal", body0.worldToLocalMatrix);
			}
		}

		void UpdateSettings(float stepDeltaTime, float frameDeltaTime)
		{
			if (smoothingRadius != smoothRadiusOld)
			{
				smoothRadiusOld = smoothingRadius;
				UpdateSmoothingConstants();
			}

			Vector3 simBoundsSize = transform.localScale;
			Vector3 simBoundsCentre = transform.position;


			compute.SetFloat("deltaTime", stepDeltaTime);
			compute.SetFloat("whiteParticleDeltaTime", frameDeltaTime);
			compute.SetFloat("simTime", simTimer);

			compute.SetFloat("collisionDamping", collisionDamping);
			compute.SetFloat("smoothingRadius", smoothingRadius);
			compute.SetFloat("targetDensity", targetDensity);
			compute.SetFloat("pressureMultiplier", pressureMultiplier);
			compute.SetFloat("nearPressureMultiplier", nearPressureMultiplier);
			compute.SetFloat("viscosityStrength", viscosityStrength);
			compute.SetVector("boundsSize", simBoundsSize);
			compute.SetVector("centre", simBoundsCentre);

			compute.SetMatrix("localToWorld", transform.localToWorldMatrix);
			compute.SetMatrix("worldToLocal", transform.worldToLocalMatrix);

			// Foam settings
			float fadeInT = (spawnRateFadeInTime <= 0) ? 1 : Mathf.Clamp01((simTimer - spawnRateFadeStartTime) / spawnRateFadeInTime);
			compute.SetVector("trappedAirParams", new Vector3(trappedAirSpawnRate * fadeInT * fadeInT, trappedAirVelocityMinMax.x, trappedAirVelocityMinMax.y));
			compute.SetVector("kineticEnergyParams", foamKineticEnergyMinMax);
			compute.SetFloat("bubbleBuoyancy", bubbleBuoyancy);
			compute.SetInt("sprayClassifyMaxNeighbours", sprayClassifyMaxNeighbours);
			compute.SetInt("bubbleClassifyMinNeighbours", bubbleClassifyMinNeighbours);
			compute.SetFloat("bubbleScaleChangeSpeed", bubbleChangeScaleSpeed);
			compute.SetFloat("bubbleScale", bubbleScale);
		}

		void SetInitialBufferData(Spawner3D.SpawnData spawnData)
		{
			positionBuffer.SetData(spawnData.points);
			predictedPositionsBuffer.SetData(spawnData.points);
			velocityBuffer.SetData(spawnData.velocities);

			foamBuffer.SetData(new FoamParticle[foamBuffer.count]);

			debugBuffer.SetData(new float3[debugBuffer.count]);
			foamCountBuffer.SetData(new uint[foamCountBuffer.count]);
			simTimer = 0;
		}

		void HandleInput()
		{
			if (Input.GetKeyDown(KeyCode.Space))
			{
				isPaused = !isPaused;
			}

			if (Input.GetKeyDown(KeyCode.RightArrow))
			{
				isPaused = false;
				pauseNextFrame = true;
			}

			if (Input.GetKeyDown(KeyCode.R))
			{
				pauseNextFrame = true;
				SetInitialBufferData(spawnData);
				// Run single frame of sim with deltaTime = 0 to initialize density texture
				// (so that display can work even if paused at start)
				if (renderToTex3D)
				{
					RunSimulationFrame(0);
				}
			}

			if (Input.GetKeyDown(KeyCode.Q))
			{
				inSlowMode = !inSlowMode;
			}
		}

		private float ActiveTimeScale => inSlowMode ? slowTimeScale : normalTimeScale;

		void OnDestroy()
		{
			foreach (var kvp in bufferNameLookup)
			{
				Release(kvp.Key);
			}

			gpuSort.Release();
		}


		public struct FoamParticle
		{
			public float3 position;
			public float3 velocity;
			public float lifetime;
			public float scale;
		}
	}
}