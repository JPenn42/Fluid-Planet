using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Moon : MonoBehaviour
{
	public Transform lookTarget;
	public float angle;

	// Start is called before the first frame update
	void Start()
	{
	}

	// Update is called once per frame
	void Update()
	{
		transform.LookAt(lookTarget.position, Vector3.up);
		transform.Rotate(transform.up * angle);
	}
}