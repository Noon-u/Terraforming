using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Terraformer : MonoBehaviour
{

	public event System.Action onTerrainModified;

	public LayerMask terrainMask;

	public float terraformRadius = 5;
	public float terraformSpeedNear = 0.1f;
	public float terraformSpeedFar = 0.25f;


	Transform cam;
	GenTest genTest;
	FlatGen flatGen;
	bool hasHit;
	Vector3 hitPoint;
	FirstPersonController firstPersonController;
	FlatFirstPersonController flatFirstPersonController;
	bool fallbackLastHit;

	bool isTerraforming;
	Vector3 lastTerraformPointLocal;

	void Start()
	{
		genTest = FindFirstObjectByType<GenTest>();
		flatGen = FindFirstObjectByType<FlatGen>();
		cam = Camera.main.transform;
		firstPersonController = FindFirstObjectByType<FirstPersonController>();
		flatFirstPersonController = FindFirstObjectByType<FlatFirstPersonController>();
	}

	void Update()
	{
		RaycastHit hit;
		hasHit = false;

		bool wasTerraformingLastFrame = isTerraforming;
		isTerraforming = false;

		int numIterations = 5;
		bool rayHitTerrain = false;
		fallbackLastHit = false;



		for (int i = 0; i < numIterations; i++)
		{
			float rayRadius = terraformRadius * Mathf.Lerp(0.01f, 1, i / (numIterations - 1f));
			if (Physics.SphereCast(cam.position, rayRadius, cam.forward, out hit, 1000, terrainMask))
			{
				lastTerraformPointLocal = MathUtility.WorldToLocalVector(cam.rotation, hit.point);
				Terraform(hit.point);
				rayHitTerrain = true;
				break;
			}
			else if (Physics.SphereCast(cam.position, rayRadius, cam.forward, out hit, 1000, ~0))
			{
				lastTerraformPointLocal = MathUtility.WorldToLocalVector(cam.rotation, hit.point);
				Terraform(hit.point);
				rayHitTerrain = true;
				fallbackLastHit = true;
				break;
			}
		}


		if (!rayHitTerrain && wasTerraformingLastFrame)
		{
			Vector3 terraformPoint = MathUtility.LocalToWorldVector(cam.rotation, lastTerraformPointLocal);
			Terraform(terraformPoint);
		}

	}

	void Terraform(Vector3 terraformPoint)
	{
		//Debug.DrawLine(cam.position, point, Color.green);
		hasHit = true;
		hitPoint = terraformPoint;

		const float dstNear = 10;
		const float dstFar = 60;

		float dstFromCam = (terraformPoint - cam.position).magnitude;
		float weight01 = Mathf.InverseLerp(dstNear, dstFar, dstFromCam);
		float weight = Mathf.Lerp(terraformSpeedNear, terraformSpeedFar, weight01);

		// Add terrain
		if (Input.GetMouseButton(0))
		{
			isTerraforming = true;
			if (flatGen)
			{
				flatGen.Terraform(terraformPoint, -weight, terraformRadius);
				flatFirstPersonController?.NotifyTerrainChanged(terraformPoint, terraformRadius);
			}
			else if (genTest)
			{
				genTest.Terraform(terraformPoint, -weight, terraformRadius);
				firstPersonController.NotifyTerrainChanged(terraformPoint, terraformRadius);
			}
		}
		// Subtract terrain
		else if (Input.GetMouseButton(1))
		{
			isTerraforming = true;
			if (flatGen)
			{
				flatGen.Terraform(terraformPoint, weight, terraformRadius);
			}
			else if (genTest)
			{
				genTest.Terraform(terraformPoint, weight, terraformRadius);
			}
		}

		if (isTerraforming)
		{
			onTerrainModified?.Invoke();
		}
	}

	void OnDrawGizmos()
	{
		if (!cam)
		{
			var main = Camera.main;
			if (main) cam = main.transform;
		}
		if (cam)
		{
			Gizmos.color = new Color(1,1,1,0.25f);
			Gizmos.DrawRay(cam.position, cam.forward * 3);
		}
		if (hasHit || fallbackLastHit)
		{
			Gizmos.color = Color.green;
			Gizmos.DrawSphere(hitPoint, 0.25f);
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(hitPoint, terraformRadius);
		}
	}
}
