using UnityEngine;
using Seb.Helpers;
using UnityEngine.Experimental.Rendering;

namespace Seb.Rendering
{
	public class TerrainCreator : MonoBehaviour
	{
		[Header("Terrain Settings")]
		public int resolution;

		public float isoLevel;
		public float scale = 10;
		public float radius;

		[Header("Noise Settings")]
		public int numLayers;

		public float lacunarity;
		public float persistence;
		public float noiseScale;
		public float noiseStrength;
		public float noiseValueOffset = -0.28f;
		public Vector3 noisePositionOffset;

		[Header("Terraform Settings")]
		public float terraformRadius;

		public float terraformStrength;


		[Header("References")]
		public Material drawMat;

		public ComputeShader renderArgsCompute;
		public ComputeShader densityCompute;

		ComputeBuffer renderArgs;
		MarchingCubes marchingCubes;
		ComputeBuffer triangleBuffer;
		Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 1000); //
		[HideInInspector] public RenderTexture densityTexture;
		bool needsUpdate;

		void Awake()
		{
			Debug.Log("Terraforming controls: Shift + Left/Right Mouse");
			marchingCubes = new MarchingCubes();
			needsUpdate = true;
			UpdateDensityMap();
		}

		void LateUpdate()
		{
			if (needsUpdate)
			{
				UpdateDensityMap();
			}

			HandleInput();

			Render(densityTexture, Position);
			needsUpdate = false;
		}

		void HandleInput()
		{
			if ((Input.GetMouseButton(0) || Input.GetMouseButton(1)) && Input.GetKey(KeyCode.LeftShift))
			{
				float strMul = Input.GetMouseButton(1) ? -1 : 1;
				Camera cam = Camera.main;
				Ray ray = cam.ScreenPointToRay(Input.mousePosition);

				Vector3 boundsHalfSize = Vector3.one * scale / 2;
				var hitInfo = Maths.RayBoundingBox(ray.origin, ray.direction, -boundsHalfSize, boundsHalfSize);

				if (hitInfo.hit && !hitInfo.isInside)
				{
					ray.origin = ray.origin + ray.direction * (hitInfo.dst + 0.1f);
				}
				//Debug.DrawRay(ray.origin, ray.direction * 10, Color.red);

				densityCompute.SetVector("rayOrigin", ray.origin);
				densityCompute.SetVector("rayDir", ray.direction);
				densityCompute.SetFloat("terraformRadius", terraformRadius);
				densityCompute.SetFloat("terraformStrength", terraformStrength * strMul);
				densityCompute.SetFloat("deltaTime", Time.deltaTime);
				densityCompute.SetTexture(1, "DensityMap", densityTexture);
				ComputeHelper.Dispatch(densityCompute, resolution, resolution, resolution, kernelIndex: 1);
				needsUpdate = true;
			}
		}

		void UpdateDensityMap()
		{
			// Create volume texture
			ComputeHelper.CreateRenderTexture3D(ref densityTexture, resolution, resolution, resolution, GraphicsFormat.R32_SFloat, TextureWrapMode.Clamp, "TerrainDensity");

			// ---- Assign to shader ----
			densityCompute.SetFloat("noiseScale", noiseScale);
			densityCompute.SetFloat("noiseStrength", noiseStrength);
			densityCompute.SetFloat("lacunarity", lacunarity);
			densityCompute.SetFloat("persistence", persistence);
			densityCompute.SetFloat("noiseOffset", noiseValueOffset);
			densityCompute.SetVector("noisePositionOffset", noisePositionOffset);
			densityCompute.SetInt("numLayers", numLayers);

			densityCompute.SetTexture(0, "DensityMap", densityTexture);
			densityCompute.SetInt("resolution", resolution);
			densityCompute.SetVector("worldSize", Vector3.one * scale);
			densityCompute.SetVector("worldCentre", Position);
			densityCompute.SetFloat("radius", radius);

			// Dispatch
			ComputeHelper.Dispatch(densityCompute, resolution, resolution, resolution, kernelIndex: 0);
		}

		void Render(RenderTexture densityTexture, Vector3 position)
		{
			if (needsUpdate)
			{
				// Run marching cubes compute shader and get back buffer containing triangle data
				triangleBuffer = marchingCubes.Run(densityTexture, Vector3.one * scale, -isoLevel);
			}

			// Each triangle contains 3 vertices: assign these all to the vertex buffer on the draw material
			drawMat.SetBuffer("VertexBuffer", triangleBuffer);
			//drawMat.SetColor("col", col);
			drawMat.SetVector("offset", position);

			// Create render arguments. This stores 5 values:
			// (triangle index count, instance count, sub-mesh index, base vertex index, byte offset)
			if (renderArgs == null)
			{
				renderArgs = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
				renderArgsCompute.SetBuffer(0, "RenderArgs", renderArgs);
			}

			// Copy the current number of triangles from the append buffer into the render arguments.
			// (Each triangle contains 3 vertices, so we then need to multiply this value by 3 with another dispatch)
			ComputeBuffer.CopyCount(triangleBuffer, renderArgs, 0);
			renderArgsCompute.Dispatch(0, 1, 1, 1);

			// Draw the mesh using ProceduralIndirect to avoid having to read any data back to the CPU
			Graphics.DrawProceduralIndirect(drawMat, bounds, MeshTopology.Triangles, renderArgs);
		}

		void OnValidate()
		{
			needsUpdate = true;
		}

		Vector3 Position => Vector3.zero;

		private void OnDestroy()
		{
			Release();
		}

		void Release()
		{
			ComputeHelper.Release(densityTexture);
			ComputeHelper.Release(triangleBuffer, renderArgs);
			marchingCubes.Release();
		}

		void OnDrawGizmos()
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireCube(Position, Vector3.one * scale);
		}
	}
}