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

	// Max distance from camera at which terraforming can occur
	public float maxEditDistance = 60f;

	// If true, clicking toggles edit on/off instead of needing to hold
	public bool toggleEditMode = false;

	// If true, apply edits at fixed intervals rather than every frame
	public bool useEditInterval = false;
	public float editIntervalSeconds = 0.15f;
	// Simulated hold duration per allowed pulse when interval gating is enabled
	public float clickPulseSeconds = 1f;


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

	// Internal state for toggle/interval modes
	bool addEditingActive;
	bool subtractEditingActive;
	float nextEditTime;

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

		// Update toggle state based on mouse button clicks (if enabled)
		if (toggleEditMode)
		{
			if (Input.GetMouseButtonDown(0))
			{
				addEditingActive = !addEditingActive;
				if (addEditingActive) subtractEditingActive = false;
			}
			if (Input.GetMouseButtonDown(1))
			{
				subtractEditingActive = !subtractEditingActive;
				if (subtractEditingActive) addEditingActive = false;
			}
		}



		for (int i = 0; i < numIterations; i++)
		{
			float rayRadius = terraformRadius * Mathf.Lerp(0.01f, 1, i / (numIterations - 1f));
			if (Physics.SphereCast(cam.position, rayRadius, cam.forward, out hit, maxEditDistance, terrainMask))
			{
				lastTerraformPointLocal = MathUtility.WorldToLocalVector(cam.rotation, hit.point);
				Terraform(hit.point);
				rayHitTerrain = true;
				break;
			}
			else if (Physics.SphereCast(cam.position, rayRadius, cam.forward, out hit, maxEditDistance, ~0))
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

		// Respect max edit distance
		if (dstFromCam > maxEditDistance)
		{
			return;
		}

		// Determine intended edit state (toggle or hold)
		bool addIntent = toggleEditMode ? addEditingActive : Input.GetMouseButton(0);
		bool subtractIntent = toggleEditMode ? subtractEditingActive : Input.GetMouseButton(1);
		isTerraforming = addIntent || subtractIntent;

		// Interval gating (cooldown). Only schedule/advance when there is an edit intent.
		bool canApplyThisFrame = true;
		if (useEditInterval && isTerraforming)
		{
			if (Time.time < nextEditTime)
			{
				canApplyThisFrame = false;
			}
			else
			{
				nextEditTime = Time.time + editIntervalSeconds;
			}
		}

		// Scale weight so each allowed pulse equals holding for clickPulseSeconds
		float weightMultiplier = 1f;
		if (useEditInterval && isTerraforming && canApplyThisFrame)
		{
			weightMultiplier = Mathf.Max(0.0001f, clickPulseSeconds / Mathf.Max(Time.deltaTime, 0.0001f));
		}

		// Add terrain
		if (addIntent && canApplyThisFrame)
		{
			if (flatGen)
			{
				flatGen.Terraform(terraformPoint, -(weight * weightMultiplier), terraformRadius);
				flatFirstPersonController?.NotifyTerrainChanged(terraformPoint, terraformRadius);
			}
			else if (genTest)
			{
				genTest.Terraform(terraformPoint, -(weight * weightMultiplier), terraformRadius);
				firstPersonController.NotifyTerrainChanged(terraformPoint, terraformRadius);
			}
		}
		// Subtract terrain
		else if (subtractIntent && canApplyThisFrame)
		{
			if (flatGen)
			{
				flatGen.Terraform(terraformPoint, (weight * weightMultiplier), terraformRadius);
			}
			else if (genTest)
			{
				genTest.Terraform(terraformPoint, (weight * weightMultiplier), terraformRadius);
			}
		}

		if (isTerraforming && canApplyThisFrame)
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
