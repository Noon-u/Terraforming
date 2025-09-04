using UnityEngine;

public class FlatFirstPersonController : MonoBehaviour
{

	public enum MoveState
	{
		Idle,
		Walk,
		Run,
		Swim
	}

	public float gravity = 15;
	public float buoyancy = 6;
	public float mouseSensitivityX = 1;
	public float mouseSensitivityY = 1;
	public float walkSpeed = 6;
	public float runSpeed = 12;
	public float swimSpeed = 10;
	public float jumpForce = 220;
	public float waterDrag = 0.5f;
	public Vector2 lookAngleMinMax = new Vector2(-75, 80);
	public LayerMask terrainMask;
	public float waterHeight = 0;

	public float groundedRaySizeFactor = 0.7f;
	public float groundedRayLength = 0.1f;
	public bool grounded;
	Vector3 desiredLocalVelocity;
	Vector3 smoothMoveVelocity;
	float verticalLookRotation;
	Transform cameraTransform;
	Rigidbody rigidBody;
	CapsuleCollider capsuleCollider;

	public MoveState currentMoveState { get; private set; }
	bool underwater;
	bool debug_stopMovement;
	bool terraUpdate;
	Vector3 lastHitPoint;

	void Awake()
	{
		Cursor.lockState = CursorLockMode.Locked;
		cameraTransform = Camera.main.transform;
		rigidBody = GetComponent<Rigidbody>();
		rigidBody.useGravity = false;
		rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
		capsuleCollider = GetComponent<CapsuleCollider>();
		Time.fixedDeltaTime = 1f / 60f;
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			debug_stopMovement = !debug_stopMovement;
			Cursor.visible = debug_stopMovement;
			Cursor.lockState = (debug_stopMovement) ? CursorLockMode.None : CursorLockMode.Locked;
			if (debug_stopMovement)
			{
				desiredLocalVelocity = Vector3.zero;
				rigidBody.linearVelocity = Vector3.zero;
			}
		}

		if (debug_stopMovement)
		{
			return;
		}

		underwater = cameraTransform.position.y < waterHeight + 0.25f;

		transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * mouseSensitivityX);
		verticalLookRotation += Input.GetAxis("Mouse Y") * mouseSensitivityY;
		verticalLookRotation = Mathf.Clamp(verticalLookRotation, lookAngleMinMax.x, lookAngleMinMax.y);
		cameraTransform.localEulerAngles = Vector3.left * verticalLookRotation;

		float inputX = Input.GetAxisRaw("Horizontal");
		float inputY = Input.GetAxisRaw("Vertical");
		currentMoveState = MoveState.Idle;
		if (underwater) currentMoveState = MoveState.Swim;
		else if (inputX != 0 || inputY != 0) currentMoveState = Input.GetKey(KeyCode.LeftShift) ? MoveState.Run : MoveState.Walk;

		float desiredMoveSpeed = (currentMoveState == MoveState.Walk) ? walkSpeed : (currentMoveState == MoveState.Run ? runSpeed : (currentMoveState == MoveState.Swim ? swimSpeed : 0));
		Vector3 moveDir = new Vector3(inputX, 0, inputY).normalized;
		Vector3 targetMoveVelocity = moveDir * desiredMoveSpeed;
		desiredLocalVelocity = Vector3.SmoothDamp(desiredLocalVelocity, targetMoveVelocity, ref smoothMoveVelocity, .15f);

		if (Input.GetButtonDown("Jump") && grounded)
		{
			rigidBody.AddForce(transform.up * jumpForce, ForceMode.VelocityChange);
		}

		grounded = IsGrounded();
	}

	void FixedUpdate()
	{
		if (debug_stopMovement) return;
		Vector3 gravityUp = Vector3.up;
		Vector3 localUp = MathUtility.LocalToWorldVector(rigidBody.rotation, Vector3.up);
		rigidBody.rotation = Quaternion.FromToRotation(localUp, gravityUp) * rigidBody.rotation;
		rigidBody.linearVelocity = (underwater) ? CalculateNewVelocitySwim(localUp) : CalculateNewVelocity(localUp);
	}

	void LateUpdate()
	{
		if (terraUpdate)
		{
			Vector3 localUp = MathUtility.LocalToWorldVector(rigidBody.rotation, Vector3.up);
			float heightOffset = 5f;
			Vector3 a = transform.position - localUp * (capsuleCollider.height / 2 + capsuleCollider.radius - heightOffset);
			Vector3 b = transform.position + localUp * (capsuleCollider.height / 2 + capsuleCollider.radius + heightOffset);
			RaycastHit hitInfo;
			if (Physics.CapsuleCast(a, b, capsuleCollider.radius, -localUp, out hitInfo, heightOffset, terrainMask))
			{
				Vector3 newPos = (hitInfo.point + transform.up * 1);
				float deltaY = Vector3.Dot(transform.up, (newPos - transform.position));
				if (deltaY > 0.05f)
				{
					transform.position = newPos;
					grounded = true;
				}
			}
			terraUpdate = false;
		}
	}

	Vector3 CalculateNewVelocitySwim(Vector3 localUp)
	{
		float deltaTime = Time.fixedDeltaTime;
		Vector3 currentVelocity = rigidBody.linearVelocity;
		Vector3 newVelocity = currentVelocity + localUp * (buoyancy - gravity) * deltaTime;
		Vector3 drag = -newVelocity * waterDrag;
		newVelocity += drag * deltaTime;
		Vector3 swimForce = MathUtility.LocalToWorldVector(cameraTransform.rotation, desiredLocalVelocity);
		Vector3 swimDeltaV = swimForce * deltaTime * 5;
		if (newVelocity.x * Mathf.Sign(swimForce.x) < Mathf.Abs(swimForce.x)) newVelocity.x += swimDeltaV.x;
		if (newVelocity.y * Mathf.Sign(swimForce.y) < Mathf.Abs(swimForce.y)) newVelocity.y += swimDeltaV.y;
		if (newVelocity.z * Mathf.Sign(swimForce.z) < Mathf.Abs(swimForce.z)) newVelocity.z += swimDeltaV.z;
		return newVelocity;
	}

	Vector3 CalculateNewVelocity(Vector3 localUp)
	{
		float deltaTime = Time.fixedDeltaTime;
		Vector3 currentLocalVelocity = MathUtility.WorldToLocalVector(rigidBody.rotation, rigidBody.linearVelocity);
		float localYVelocity = currentLocalVelocity.y + (-gravity) * deltaTime;
		Vector3 desiredGlobalVelocity = MathUtility.LocalToWorldVector(rigidBody.rotation, desiredLocalVelocity);
		desiredGlobalVelocity += localUp * localYVelocity;
		return desiredGlobalVelocity;
	}

	bool IsGrounded()
	{
		Vector3 centre = rigidBody.position;
		Vector3 upDir = transform.up;
		Vector3 castOrigin = centre + upDir * (-capsuleCollider.height / 2f + capsuleCollider.radius);
		float groundedRayRadius = capsuleCollider.radius * groundedRaySizeFactor;
		float groundedRayDst = capsuleCollider.radius - groundedRayRadius + groundedRayLength;
		RaycastHit hitInfo;
		if (Physics.SphereCast(castOrigin, groundedRayRadius, -upDir, out hitInfo, groundedRayDst, terrainMask))
		{
			return true;
		}
		return false;
	}

	public void NotifyTerrainChanged(Vector3 point, float radius)
	{
		terraUpdate = true;
		lastHitPoint = point;
	}
}


