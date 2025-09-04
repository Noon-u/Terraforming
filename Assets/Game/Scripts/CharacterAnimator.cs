using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
	Animator animator;
	FirstPersonController controller;
	FlatFirstPersonController flatController;

	float speedPercent;

	void Start()
	{
		animator = GetComponentInChildren<Animator>();
		controller = GetComponent<FirstPersonController>();
		flatController = GetComponent<FlatFirstPersonController>();
	}


	void Update()
	{
		if (!animator)
		{
			return;
		}

		float targetSpeedPercent = 0;

		if (controller)
		{
			var state = controller.currentMoveState;
			if (state == FirstPersonController.MoveState.Walk)
			{
				targetSpeedPercent = 0.5f;
			}
			else if (state == FirstPersonController.MoveState.Run)
			{
				targetSpeedPercent = 1;
			}
			else if (state == FirstPersonController.MoveState.Swim)
			{
				targetSpeedPercent = 0.5f;
			}
			animator.SetBool("Air", !controller.grounded);
		}
		else if (flatController)
		{
			var state = flatController.currentMoveState;
			if (state == FlatFirstPersonController.MoveState.Walk)
			{
				targetSpeedPercent = 0.5f;
			}
			else if (state == FlatFirstPersonController.MoveState.Run)
			{
				targetSpeedPercent = 1;
			}
			else if (state == FlatFirstPersonController.MoveState.Swim)
			{
				targetSpeedPercent = 0.5f;
			}
			animator.SetBool("Air", !flatController.grounded);
		}
		else
		{
			// No controller found; nothing to animate
			return;
		}

		speedPercent = Mathf.Lerp(speedPercent, targetSpeedPercent, Time.deltaTime * 3);
		animator.SetFloat("Speed Percent", speedPercent);
	}
}
