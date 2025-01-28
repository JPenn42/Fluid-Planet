using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Seb.Stuff.Examples
{
	public class TextureViewer3D : MonoBehaviour
	{

		[Header("View Settings")]
		[Range(0, 1)] public float sliceDepth = 0.5f;
		public Vector2 remapRange = new Vector2(0, 1);
		public MeshRenderer display;
		RenderTexture texture;

		void Update()
		{
			if (texture != null)
			{
				display.sharedMaterial.SetFloat("sliceDepth", sliceDepth);
				display.sharedMaterial.SetVector("remapRange", remapRange);
				display.sharedMaterial.SetTexture("DisplayTexture", texture);
			}
		}

		public void SetTexture(RenderTexture texture)
		{
			this.texture = texture;
		}
	}
}