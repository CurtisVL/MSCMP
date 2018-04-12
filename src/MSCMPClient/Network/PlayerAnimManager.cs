﻿using System.Collections.Generic;
using UnityEngine;

namespace MSCMP.Network {
	/// <summary>
	/// Class managing the animations of the player.
	/// </summary>
	class PlayerAnimManager {
		public static PlayerAnimManager Instance = null;

		public PlayerAnimManager() {
			Instance = this;
		}

		~PlayerAnimManager() {
			Instance = null;
		}

		static List<AnimState> states = new List<AnimState>();

		static Animation characterAnimationComponent = null;

		/// <summary>
		/// Currently played animation id.
		/// </summary>
		public AnimationId currentAnim = AnimationId.Standing;
		public AnimationState activeAnimationState = null;

		/// <summary>
		/// The amount of movement packets needed left to send animation sync packet
		/// </summary>
		public int PACKETS_LEFT_TO_SYNC = 0;

		/// <summary>
		/// The total amount of movement packets we need
		/// </summary>
		public int PACKETS_TOTAL_FOR_SYNC = 2;

		/// <summary>
		/// The animation ids.
		/// </summary>
		public enum AnimationId {
			Walk,
			Standing,
			Jumping,
			Drunk,
			Leaning,
			Finger,
			Hitchhike
		}

		private string[] AnimationNames = new string[] {
			"Walk",
			"Idle",
			"Jump",
			"Drunk",
			"Lean",
			"Finger",
			"Hitchhike"
		};

		/// <summary>
		/// Convert animation id to it's name.
		/// </summary>
		/// <param name="animation">The id of the animation.</param>
		/// <returns>Name of the animation.</returns>
		private string GetAnimationName(AnimationId animation) {
			return AnimationNames[(int)animation];
		}

		/// <summary>
		/// The hand state ids.
		/// </summary>
		private enum HandStateId {
			MiddleFingering,
			Lifting,
			Fisting,
			Pushing,
			Drinking
		}

		/// <summary>
		/// The hand state GameObject names.
		/// </summary>
		private string[] HandStateNames = new string[] {
			"MiddleFinger",
			"Lift",
			"Fist",
			"Hand Push",
			"Drink/Hand"
		};

		/// <summary>
		/// Convert hand state id to it's name.
		/// </summary>
		/// <param name="handState">The id of the hand state.</param>
		/// <returns>Name of the hand state.</returns>
		private HandStateId GetHandState(byte handState) {
			return (HandStateId)handState;
		}

		/// <summary>
		/// Gets the active hand state of the gameObject
		/// </summary>
		/// <param name="gameObject">The object to get it from.</param>
		/// <returns>The ID of the active state or else 255 if none.</returns>
		public byte GetActiveHandState(GameObject gameObject) {
			byte isActive = 255;
			GameObject HandHandleObject = gameObject.transform.FindChild("Pivot/Camera/FPSCamera/FPSCamera").gameObject;

			for (byte i = 0; i < HandStateNames.Length; i++) {
				string HandStateName = HandStateNames[i];
				GameObject HandStateObject = HandHandleObject.transform.FindChild(HandStateName).gameObject;

				if (HandStateObject.activeInHierarchy) isActive = i;
			}

			return isActive;
		}

		/// <summary>
		/// Sets up the animation component and the layers for each animation. Also registers the animation states.
		/// </summary>
		/// <param name="animComponent">The animation component of the player.</param>
		public void SetupAnimations(Animation animComponent) {
			characterAnimationComponent = animComponent;

			characterAnimationComponent["Jump"].layer = 1;
			characterAnimationComponent["Drunk"].layer = 2;
			characterAnimationComponent["Drunk"].blendMode = AnimationBlendMode.Additive;
			characterAnimationComponent["Lean"].layer = 3;
			characterAnimationComponent["Lean"].blendMode = AnimationBlendMode.Additive;
			characterAnimationComponent["Finger"].layer = 3;
			characterAnimationComponent["Finger"].blendMode = AnimationBlendMode.Additive;
			characterAnimationComponent["Hitchhike"].layer = 3;
			characterAnimationComponent["Hitchhike"].blendMode = AnimationBlendMode.Additive;

			RegisterAnimStates();
		}

		/// <summary>
		/// Play selected animation.
		/// </summary>
		/// <param name="animation">The id of the animation.</param>
		/// <param name="force">If it should be forced, or just crossfaded</param>
		/// <param name="mainLayer">If it's into the main movement layer, or an action to be played simultaneously</param>
		public void PlayAnimation(AnimationId animation, bool force = false, bool mainLayer = true) {
			if (characterAnimationComponent == null) return;
			if (!force && currentAnim == animation && mainLayer) return;

			string animName = GetAnimationName(animation);

			if (force) characterAnimationComponent.Play(animName);
			else characterAnimationComponent.CrossFade(animName);

			if (mainLayer) {
				currentAnim = animation;
				activeAnimationState = characterAnimationComponent[animName];
			}
		}

		/// <summary>
		/// Blends out an animation smoothly (it's not getting disabled that way. That's why we use 'CheckBlendedOutAnimationStates' function to disable it there)
		/// </summary>
		/// <param name="animation">The name of the animation</param>
		private void BlendOutAnimation(AnimationId animation) {
			if (characterAnimationComponent == null) return;
			characterAnimationComponent.Blend(GetAnimationName(animation), 0);
		}

		/// <summary>
		/// Plays an Action Animation from start to end, or the opposite
		/// </summary>
		/// <param name="animName">The name of the animation</param>
		/// <param name="play">Start or Stop the animation</param>
		private void PlayActionAnim(AnimationId animation, bool play) {
			if (characterAnimationComponent == null) return;
			string animName = GetAnimationName(animation);

			if (play) {
				characterAnimationComponent[animName].wrapMode = WrapMode.ClampForever;
				characterAnimationComponent[animName].speed = 1;
				characterAnimationComponent[animName].enabled = true;
				characterAnimationComponent[animName].weight = 1.0f;
			}
			else {
				characterAnimationComponent[animName].wrapMode = WrapMode.Once;
				if (characterAnimationComponent[animName].time > characterAnimationComponent[animName].length) {
					characterAnimationComponent[animName].time = characterAnimationComponent[animName].length;
				}
				characterAnimationComponent[animName].speed = -1;
				characterAnimationComponent[animName].weight = 1.0f;
			}
		}

		/// <summary>
		/// Checks if player is playing a specific animation
		/// </summary>
		/// <param name="animName">The name of the animation</param>
		private bool IsPlayingAnim(AnimationId animation) {
			if (characterAnimationComponent == null) return false;

			string animName = GetAnimationName(animation);
			if (characterAnimationComponent[animName].enabled == true) return true;

			return false;
		}

		/// <summary>
		/// Will stop hand gestures except the one in the variable
		/// </summary>
		/// <param name="exceptThis">Except this gesture.</param>
		private void StopOtherHandGesturesExcept(AnimationId exceptThis) {
			if (exceptThis != AnimationId.Finger && IsPlayingAnim(AnimationId.Finger)) PlayActionAnim(AnimationId.Finger, false);
			if (exceptThis != AnimationId.Hitchhike && IsPlayingAnim(AnimationId.Hitchhike)) PlayActionAnim(AnimationId.Hitchhike, false);
		}

		/// <summary>
		/// Check if an animation has been blended with 0 weight and disables it
		/// </summary>
		public void CheckBlendedOutAnimationStates() {
			if (characterAnimationComponent == null) return;

			if (characterAnimationComponent["Jump"].time != 0.0f && characterAnimationComponent["Jump"].weight == 0.0f) {
				characterAnimationComponent["Jump"].enabled = false;
				characterAnimationComponent["Jump"].time = 0;
			}

			if (characterAnimationComponent["Drunk"].time != 0.0f && characterAnimationComponent["Drunk"].weight == 0.0f) {
				characterAnimationComponent["Drunk"].enabled = false;
				characterAnimationComponent["Drunk"].time = 0;
			}
		}

		private class AnimState : PlayerAnimManager {
			bool isActive = false;

			public virtual bool CanActivate(Messages.AnimSyncMessage msg) {
				// condition if this state can be activated (must also return true if state is active)
				return false;
			}

			public virtual void Activate() {
				// start anim here
			}

			public virtual void Deactivate() {
				// stop anim here
			}

			public void TryActivate(Messages.AnimSyncMessage msg) {
				bool canActivate = CanActivate(msg);
				if (!isActive && canActivate) {
					Activate();
					isActive = true;
				}
				else if (isActive && !canActivate) {
					Deactivate();
					isActive = false;
				}
			}
		}

		private class LeaningState : AnimState {
			public override bool CanActivate(Messages.AnimSyncMessage msg) { return msg.isLeaning; }
			public override void Activate() { PlayActionAnim(AnimationId.Leaning, true); }
			public override void Deactivate() { PlayActionAnim(AnimationId.Leaning, false); }
		}

		private class JumpState : AnimState {
			public override bool CanActivate(Messages.AnimSyncMessage msg) { return !msg.isGrounded; }
			public override void Activate() { PlayAnimation(AnimationId.Jumping, false, false); }
			public override void Deactivate() { BlendOutAnimation(AnimationId.Jumping); }
		}

		private class FingerState : AnimState {
			public override bool CanActivate(Messages.AnimSyncMessage msg) { return GetHandState(msg.activeHandState) == HandStateId.MiddleFingering; }
			public override void Activate() { PlayActionAnim(AnimationId.Finger, true); }
			public override void Deactivate() { PlayActionAnim(AnimationId.Finger, false); }
		}

		private class HitchhikeState : AnimState {
			public override bool CanActivate(Messages.AnimSyncMessage msg) { return GetHandState(msg.activeHandState) == HandStateId.Lifting; }
			public override void Activate() { PlayActionAnim(AnimationId.Hitchhike, true); }
			public override void Deactivate() { PlayActionAnim(AnimationId.Hitchhike, false); }
		}

		private class DrunkState : AnimState {
			public override bool CanActivate(Messages.AnimSyncMessage msg) { return msg.isDrunk; }
			public override void Activate() { PlayAnimation(AnimationId.Drunk, false, false); }
			public override void Deactivate() { BlendOutAnimation(AnimationId.Drunk); }
		}

		private static void RegisterAnimStates() {
			states.Add(new LeaningState());
			states.Add(new JumpState());
			states.Add(new FingerState());
			states.Add(new HitchhikeState());
			states.Add(new DrunkState());
		}

		float aimRot = 0.0f;
		/// <summary>
		/// Handles the Action Animations
		/// </summary>
		public void HandleAnimations(Messages.AnimSyncMessage msg) {
			aimRot = msg.aimRot;

			foreach (AnimState state in states) {
				state.TryActivate(msg);
			}
		}

		/// <summary>
		/// Handles the Foot Movement Animations
		/// </summary>
		public void HandleOnFootMovementAnimations(float speed) {
			if (speed > 0.001f) {
				PlayAnimation(AnimationId.Walk);

				// Set speed of the animation according to the speed of movement.
				activeAnimationState.speed = (speed * 25.0f) / activeAnimationState.length;
			}
			else PlayAnimation(AnimationId.Standing);
		}

		/// <summary>
		/// Moves the head according to the vertical look position
		/// </summary>
		public void SyncVerticalHeadLook(GameObject characterGameObject, float progress) {
			Transform head = characterGameObject.transform.FindChild("pelvis/spine_mid/shoulders/head");
			//float newAimRot = Mathf.LerpAngle(head.rotation.eulerAngles.z, aimRot, progress); COMMENTED OUT CAUSE IT DOESNT WORK! NEED NEW INTERPOLATION
			head.rotation *= Quaternion.Euler(0, 0, -aimRot);
		}
	}
}
