using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class PostProcessing : MonoBehaviour
{

	public AtmosphereSettings atmosphere;
	public Transform sun;
	public float planetRadius;
	Material mat;


    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
	   // Camera.main.depthTextureMode = DepthTextureMode.Depth;
	    bool initMat = false;
	    
	    if (mat == null)
	    {
		    mat = new Material(atmosphere.atmosphereShader);
		    initMat = true;
	    }
	    
	    atmosphere.SetProperties(mat, planetRadius, Vector3.zero, -sun.forward, initMat);
	    Graphics.Blit(source, destination, mat);
    }
}
