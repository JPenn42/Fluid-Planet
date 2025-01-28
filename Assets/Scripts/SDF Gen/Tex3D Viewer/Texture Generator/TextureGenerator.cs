using System.Collections;
using System.Collections.Generic;
using Seb.Helpers;
using UnityEngine;

namespace Seb.Stuff.Examples
{
	public class TextureGenerator : MonoBehaviour
	{
		public TextureViewer3D viewer;
		public ComputeShader compute;
		RenderTexture texture;

		void Awake()
		{
			const int size = 64;
			ComputeHelper.CreateRenderTexture3D(ref texture, size, ComputeHelper.defaultGraphicsFormat);
			compute.SetTexture(0, "Texture", texture);
			compute.SetInt("size", size);
			ComputeHelper.Dispatch(compute, size, size, size);

			viewer.SetTexture(texture);
		}


		void OnDestroy()
		{
			ComputeHelper.Release(texture);
		}
	}
}