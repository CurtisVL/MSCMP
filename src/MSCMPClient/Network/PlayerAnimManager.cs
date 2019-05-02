using System.Collections.Generic;
using UnityEngine;

namespace MSCMP.Network
{
	/// <summary>
	/// Class managing the animations of the player.
	/// </summary>
	internal class PlayerAnimManager
	{
		private readonly List<AnimState> _states = new List<AnimState>();

		private GameObject _characterGameObject;
		private Animation _characterAnimationComponent;

		/// <summary>
		/// Currently played animation id.
		/// </summary>
		public AnimationId CurrentAnim = AnimationId.Standing;
		public AnimationState ActiveAnimationState;

		/// <summary>
		/// The amount of movement packets needed left to send animation sync packet
		/// </summary>
		public int PacketsLeftToSync = 0;

		/// <summary>
		/// The total amount of movement packets we need
		/// </summary>
		public int PacketsTotalForSync = 2;

		#region Animations
		/// <summary>
		/// The animation ids.
		/// </summary>
		public enum AnimationId
		{
			Walk,
			Standing,
			Jumping,
			Drunk,
			Leaning,
			Finger,
			Hitchhike,
			Crouching,
			CrouchingLow,
			CrouchingWalk,
			CrouchingLowWalk,
			Running,
			Hitting,
			Pushing,
			Drinking
		}

		private readonly string[] _animationNames = {
			"Walk",
			"Idle",
			"Jump",
			"Drunk",
			"Lean",
			"Finger",
			"Hitchhike",
			"Crouch",
			"CrouchLow",
			"CrouchWalk",
			"CrouchLowWalk",
			"Run",
			"Hit",
			"Push",
			"Drink"
		};

		/// <summary>
		/// Convert animation id to it's name.
		/// </summary>
		/// <param name="animation">The id of the animation.</param>
		/// <returns>Name of the animation.</returns>
		private string GetAnimationName(AnimationId animation)
		{
			return _animationNames[(int)animation];
		}
		#endregion
		#region Stances (Stand/Crouch/Crouch Low)
		private enum StanceId
		{
			Standing,
			Crouching,
			CrouchingLow
		}

		/// <summary>
		/// Convert stance id to animation id.
		/// </summary>
		/// <param name="stance">The id of the stance.</param>
		/// <param name="standingAnim">True if it's standing, or else moving.</param>
		/// <returns>Id of the animation.</returns>
		private AnimationId GetAnimationFromStance(StanceId stance, bool standingAnim = true)
		{
			switch (stance)
			{
				case StanceId.Crouching:
					if (standingAnim) return AnimationId.Crouching;
					else return AnimationId.CrouchingWalk;
				case StanceId.CrouchingLow:
					if (standingAnim) return AnimationId.CrouchingLow;
					else return AnimationId.CrouchingLowWalk;
				default:
					if (standingAnim) return AnimationId.Standing;
					else return AnimationId.Walk;
			}
		}

		#endregion
		#region HandStates
		/// <summary>
		/// The hand state ids.
		/// </summary>
		public enum HandStateId
		{
			MiddleFingering,
			Lifting,
			Hitting,
			Pushing,
			Drinking
		}

		/// <summary>
		/// The hand state GameObject names.
		/// </summary>
		private readonly string[] _handStateNames = {
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
		public HandStateId GetHandState(byte handState)
		{
			return (HandStateId)handState;
		}

		/// <summary>
		/// Gets the active hand state of the gameObject
		/// </summary>
		/// <param name="gameObject">The object to get it from.</param>
		/// <returns>The ID of the active state or else 255 if none.</returns>
		public byte GetActiveHandState(GameObject gameObject)
		{
			GameObject handHandleObject = gameObject.transform.FindChild("Pivot/Camera/FPSCamera/FPSCamera").gameObject;

			for (byte i = 0; i < _handStateNames.Length; i++)
			{
				string handStateName = _handStateNames[i];
				GameObject handStateObject = handHandleObject.transform.FindChild(handStateName).gameObject;

				if (handStateObject.activeInHierarchy) return i;
			}

			return 255;
		}
		#endregion
		#region Drink States

		private static readonly List<GameObject> _drinks = new List<GameObject>();
		private GameObject _ourDrinkObject;

		/// <summary>
		/// The drink GameObject names.
		/// </summary>
		private readonly string[] _drinkObjectNames = {
			"HandJuice",
			"HandMilk",
			"HandSpray",
			"Coffee",
			"CoffeeGranny",
			"BeerBottle",
			"BoozeBottle",
			"ShotGlass",
			"MilkGlass"
		};

		private readonly float[,] _drinkOffsets = {
			{ -0.008f, -0.016f, 0.005f },
			{ -0.025f, -0.02f, 0.015f },
			{ -0.01f, 0.0f, 0.01f },
			{ -0.015f, 0.01f, 0.01f },
			{ -0.015f, 0.011f, 0.01f },
			{ -0.012f, -0.008f, 0.015f },
			{ -0.02f, -0.02f, 0.021f },
			{ -0.02f, 0.01f, 0.01f },
			{ -0.02f, 0.005f, 0.012f }
		};

		private readonly float[,] _drinkRotations = {
			{ 5, 140, 295 },
			{ 5, 140, 295 },
			{ 350, 190, 210 },
			{ 310, 150, 273 },
			{ 310, 150, 273 },
			{ 308, 147, 295 },
			{ 308, 147, 295 },
			{ 308, 147, 295 },
			{ 310, 150, 273 }
		};

		public bool AreDrinksPreloaded() { return _drinks.Count != 0; }

		/// <summary>
		/// Preloads the drink game objects of the game player to use them later while drinking
		/// </summary>
		/// <param name="character">The player object to get the drink objects from</param>
		public void PreloadDrinkObjects(GameObject character)
		{
			GameObject handHandleObject = character.transform.FindChild("Pivot/Camera/FPSCamera/FPSCamera/Drink/Hand").gameObject;

			for (byte i = 0; i < _drinkObjectNames.Length; i++)
			{
				GameObject drinkObject = handHandleObject.transform.FindChild(_drinkObjectNames[i]).gameObject;
				Client.Assert(drinkObject, "Unable to find drink object - " + _drinkObjectNames[i]);
				_drinks.Add(drinkObject);
			}
		}

		/// <summary>
		/// Gets the drink game object player is using
		/// </summary>
		/// <param name="character">The player object to get the drink object from</param>
		/// <returns>255 if player is not drinking, or else its ID</returns>
		public byte GetDrinkingObject(GameObject character)
		{
			GameObject handHandleObject = character.transform.FindChild("Pivot/Camera/FPSCamera/FPSCamera/Drink/Hand").gameObject;

			for (byte i = 0; i < _drinkObjectNames.Length; i++)
			{
				GameObject drinkObject = handHandleObject.transform.FindChild(_drinkObjectNames[i]).gameObject;
				if (drinkObject.activeInHierarchy) return i;
			}

			return 255;
		}

		/// <summary>
		/// Sets the drinking object for the specific player
		/// </summary>
		/// <param name="character">The player object to set the drink object</param>
		/// /// <param name="drinkingObjectId">The id of the drink object</param>
		public void SetDrinkingObject(byte drinkingObjectId)
		{
			if (drinkingObjectId == 255)
			{
				PlayActionAnim(AnimationId.Drinking, false);

				if (_ourDrinkObject != null)
				{
					Object.DestroyObject(_ourDrinkObject);
					_ourDrinkObject = null;
				}
				return;
			}

			string drinkObjectName = _drinkObjectNames[(int)drinkingObjectId];

			GameObject ourDrinkObjectToSpawn = null;
			foreach (GameObject drink in _drinks)
			{
				if (drink.name == drinkObjectName) ourDrinkObjectToSpawn = drink;
			}

			if (_ourDrinkObject != null) Object.DestroyObject(_ourDrinkObject);
			_ourDrinkObject = Object.Instantiate(ourDrinkObjectToSpawn);

			_ourDrinkObject.SetActive(true);

			Transform playerFingers = _characterGameObject.transform.FindChild("pelvis/spine_mid/shoulders/collar_left/shoulder(leftx)/arm(leftx)/hand_left/finger_left");
			_ourDrinkObject.transform.SetParent(playerFingers);

			_ourDrinkObject.transform.localPosition = new Vector3(_drinkOffsets[drinkingObjectId, 0], _drinkOffsets[drinkingObjectId, 1], _drinkOffsets[drinkingObjectId, 2]);
			_ourDrinkObject.transform.localEulerAngles = new Vector3(_drinkRotations[drinkingObjectId, 0], _drinkRotations[drinkingObjectId, 1], _drinkRotations[drinkingObjectId, 2]);
			_ourDrinkObject.layer = 0;

			if (_ourDrinkObject.transform.childCount != 0)
			{
				if (_ourDrinkObject.name.StartsWith("Hand"))
				{
					_ourDrinkObject.transform.GetChild(2).gameObject.layer = 0;
					_ourDrinkObject.transform.GetChild(2).localPosition = new Vector3(0, 0, 0);

					Object.DestroyObject(_ourDrinkObject.transform.GetChild(0).gameObject); //Destroying 'Armature'
					Object.DestroyObject(_ourDrinkObject.transform.GetChild(1).gameObject); //Destroying 'hand_rigged'
				}
				else _ourDrinkObject.transform.GetChild(0).gameObject.layer = 0;
			}

			PlayActionAnim(AnimationId.Drinking, true);
		}
		#endregion

		/// <summary>
		/// Sets up the animation component and the layers for each animation. Also registers the animation states.
		/// </summary>
		/// <param name="character"></param>
		public void SetupAnimations(GameObject character)
		{
			_characterGameObject = character;
			_characterAnimationComponent = _characterGameObject.GetComponentInChildren<Animation>();

			_characterAnimationComponent["Jump"].layer = 1;
			_characterAnimationComponent["Drunk"].layer = 2;
			_characterAnimationComponent["Drunk"].blendMode = AnimationBlendMode.Additive;
			_characterAnimationComponent["Lean"].layer = 3;
			_characterAnimationComponent["Lean"].blendMode = AnimationBlendMode.Additive;
			_characterAnimationComponent["Finger"].layer = 3;
			_characterAnimationComponent["Finger"].blendMode = AnimationBlendMode.Additive;
			_characterAnimationComponent["Hitchhike"].layer = 3;
			_characterAnimationComponent["Hitchhike"].blendMode = AnimationBlendMode.Additive;
			_characterAnimationComponent["Hit"].layer = 3;
			_characterAnimationComponent["Hit"].blendMode = AnimationBlendMode.Additive;
			_characterAnimationComponent["Push"].layer = 3;
			_characterAnimationComponent["Push"].blendMode = AnimationBlendMode.Additive;
			_characterAnimationComponent["Drink"].layer = 3;
			_characterAnimationComponent["Drink"].blendMode = AnimationBlendMode.Additive;

			RegisterAnimStates();
		}

		/// <summary>
		/// Play selected animation.
		/// </summary>
		/// <param name="animation">The id of the animation.</param>
		/// <param name="force">If it should be forced, or just crossfaded</param>
		/// <param name="mainLayer">If it's into the main movement layer, or an action to be played simultaneously</param>
		public void PlayAnimation(AnimationId animation, bool force = false, bool mainLayer = true)
		{
			if (_characterAnimationComponent == null) return;
			if (!force && CurrentAnim == animation && mainLayer) return;

			string animName = GetAnimationName(animation);

			if (force) _characterAnimationComponent.Play(animName);
			else _characterAnimationComponent.CrossFade(animName);

			if (mainLayer)
			{
				CurrentAnim = animation;
				ActiveAnimationState = _characterAnimationComponent[animName];
			}
		}

		/// <summary>
		/// Blends out an animation smoothly (it's not getting disabled that way. That's why we use 'CheckBlendedOutAnimationStates' function to disable it there)
		/// </summary>
		/// <param name="animation">The name of the animation</param>
		private void BlendOutAnimation(AnimationId animation)
		{
			if (_characterAnimationComponent == null) return;
			_characterAnimationComponent.Blend(GetAnimationName(animation), 0);
		}

		/// <summary>
		/// Plays an Action Animation from start to end, or the opposite
		/// </summary>
		/// <param name="animation">The name of the animation</param>
		/// <param name="play">Start or Stop the animation</param>
		private void PlayActionAnim(AnimationId animation, bool play)
		{
			if (_characterAnimationComponent == null) return;
			string animName = GetAnimationName(animation);

			if (play)
			{
				_characterAnimationComponent[animName].wrapMode = WrapMode.ClampForever;
				_characterAnimationComponent[animName].speed = 1;
				_characterAnimationComponent[animName].enabled = true;
				_characterAnimationComponent[animName].weight = 1.0f;
			}
			else
			{
				_characterAnimationComponent[animName].wrapMode = WrapMode.Once;
				if (_characterAnimationComponent[animName].time > _characterAnimationComponent[animName].length)
				{
					_characterAnimationComponent[animName].time = _characterAnimationComponent[animName].length;
				}
				_characterAnimationComponent[animName].speed = -1;
				_characterAnimationComponent[animName].weight = 1.0f;
			}
		}

		/// <summary>
		/// Check if an animation has been blended with 0 weight and disables it
		/// </summary>
		public void CheckBlendedOutAnimationStates()
		{
			if (_characterAnimationComponent == null) return;

			if (_characterAnimationComponent["Jump"].time != 0.0f && _characterAnimationComponent["Jump"].weight == 0.0f)
			{
				_characterAnimationComponent["Jump"].enabled = false;
				_characterAnimationComponent["Jump"].time = 0;
			}

			if (_characterAnimationComponent["Drunk"].time != 0.0f && _characterAnimationComponent["Drunk"].weight == 0.0f)
			{
				_characterAnimationComponent["Drunk"].enabled = false;
				_characterAnimationComponent["Drunk"].time = 0;
			}
		}

		private class AnimState : PlayerAnimManager
		{
			private bool _isActive;

			public virtual bool CanActivate(Messages.AnimSyncMessage msg)
			{
				// condition if this state can be activated (must also return true if state is active)
				return false;
			}

			public virtual void Activate()
			{
				// start anim here
			}

			public virtual void Deactivate()
			{
				// stop anim here
			}

			public void TryActivate(Messages.AnimSyncMessage msg)
			{
				bool canActivate = CanActivate(msg);
				if (!_isActive && canActivate)
				{
					Activate();
					_isActive = true;
				}
				else if (_isActive && !canActivate)
				{
					Deactivate();
					_isActive = false;
				}
			}
		}

		private class LeaningState : AnimState
		{
			public override bool CanActivate(Messages.AnimSyncMessage msg) { return msg.isLeaning; }
			public override void Activate() { PlayActionAnim(AnimationId.Leaning, true); }
			public override void Deactivate() { PlayActionAnim(AnimationId.Leaning, false); }
		}

		private class JumpState : AnimState
		{
			public override bool CanActivate(Messages.AnimSyncMessage msg) { return !msg.isGrounded; }
			public override void Activate() { PlayAnimation(AnimationId.Jumping, false, false); }
			public override void Deactivate() { BlendOutAnimation(AnimationId.Jumping); }
		}

		private class FingerState : AnimState
		{
			public override bool CanActivate(Messages.AnimSyncMessage msg) { return GetHandState(msg.activeHandState) == HandStateId.MiddleFingering; }
			public override void Activate() { PlayActionAnim(AnimationId.Finger, true); }
			public override void Deactivate() { PlayActionAnim(AnimationId.Finger, false); }
		}

		private class HitchhikeState : AnimState
		{
			public override bool CanActivate(Messages.AnimSyncMessage msg) { return GetHandState(msg.activeHandState) == HandStateId.Lifting; }
			public override void Activate() { PlayActionAnim(AnimationId.Hitchhike, true); }
			public override void Deactivate() { PlayActionAnim(AnimationId.Hitchhike, false); }
		}

		private class DrunkState : AnimState
		{
			public override bool CanActivate(Messages.AnimSyncMessage msg) { return msg.isDrunk; }
			public override void Activate() { PlayAnimation(AnimationId.Drunk, false, false); }
			public override void Deactivate() { BlendOutAnimation(AnimationId.Drunk); }
		}

		private class HitState : AnimState
		{
			public override bool CanActivate(Messages.AnimSyncMessage msg) { return GetHandState(msg.activeHandState) == HandStateId.Hitting; }
			public override void Activate() { PlayActionAnim(AnimationId.Hitting, true); }
			public override void Deactivate() { PlayActionAnim(AnimationId.Hitting, false); }
		}

		private class PushState : AnimState
		{
			public override bool CanActivate(Messages.AnimSyncMessage msg) { return GetHandState(msg.activeHandState) == HandStateId.Pushing; }
			public override void Activate() { PlayActionAnim(AnimationId.Pushing, true); }
			public override void Deactivate() { PlayActionAnim(AnimationId.Pushing, false); }
		}

		private void RegisterAnimStates()
		{
			_states.Add(new LeaningState());
			_states.Add(new JumpState());
			_states.Add(new FingerState());
			_states.Add(new HitchhikeState());
			_states.Add(new DrunkState());
			_states.Add(new HitState());
			_states.Add(new PushState());
		}

		//Animation Variables
		private bool _isRunning;
		private float _aimRot;
		private StanceId _currentStance = StanceId.Standing;
		private byte _currentDrinkId = 255;

		/// <summary>
		/// Handles the Action Animations
		/// </summary>
		public void HandleAnimations(Messages.AnimSyncMessage msg)
		{
			_isRunning = msg.isRunning;
			_aimRot = msg.aimRot;
			HandleCrouchStates(msg.crouchPosition);
			HandleDrinking(msg.drinkId);
			HandleSwearing(msg.swearId);

			foreach (AnimState state in _states)
			{
				state.TryActivate(msg);
			}
		}

		/// <summary>
		/// Handles the Foot Movement Animations
		/// </summary>
		public void HandleOnFootMovementAnimations(float speed)
		{
			if (speed > 0.001f)
			{ //Moving
				if (_isRunning) PlayAnimation(AnimationId.Running); //Running
				else PlayAnimation(GetAnimationFromStance(_currentStance, false)); //Walking
			}
			else PlayAnimation(GetAnimationFromStance(_currentStance)); //Standing
		}

		/// <summary>
		/// Moves the head according to the vertical look position
		/// </summary>
		public void SyncVerticalHeadLook(GameObject characterGameObject, float progress)
		{
			Transform head = characterGameObject.transform.FindChild("pelvis/spine_mid/shoulders/head");
			//float newAimRot = Mathf.LerpAngle(head.rotation.eulerAngles.z, aimRot, progress); COMMENTED OUT CAUSE IT DOESNT WORK! NEED NEW INTERPOLATION
			head.rotation *= Quaternion.Euler(0, 0, -_aimRot);
		}

		/// <summary>
		/// Takes care of crouch states
		/// </summary>
		private void HandleCrouchStates(float crouchRotation)
		{
			if (crouchRotation < 0.85f) _currentStance = StanceId.CrouchingLow;
			else if (crouchRotation < 1.4f) _currentStance = StanceId.Crouching;
			else _currentStance = StanceId.Standing;
		}

		/// <summary>
		/// Takes care of drinking objects and animation
		/// </summary>
		private void HandleDrinking(byte drinkId)
		{
			byte oldDrinkingId = _currentDrinkId;
			_currentDrinkId = drinkId;

			if (oldDrinkingId != _currentDrinkId) SetDrinkingObject(_currentDrinkId);
		}

		/// <summary>
		/// Takes care of Finger Swearing/Swearing/Saying 'Yes'/Drunk Speaking
		/// </summary>
		/// <param name="swearId"></param>
		private void HandleSwearing(int swearId)
		{
			if (swearId != int.MaxValue && swearId != _currentSwearId)
			{
				if (swearId >= DrunkSpeakingOffset) MasterAudio.PlaySound3DFollowTransformAndForget("Drunk", _characterGameObject.transform, 1, 1, 0, swearId.ToString());
				else if (swearId >= AgreeingOffset) MasterAudio.PlaySound3DFollowTransformAndForget("Yes", _characterGameObject.transform, 8, 1, 0, swearId.ToString());
				else if (swearId >= SwearsOffset) MasterAudio.PlaySound3DFollowTransformAndForget("Swearing", _characterGameObject.transform, 1, 1, 0, swearId.ToString());
				else MasterAudio.PlaySound3DFollowTransformAndForget("Fuck", _characterGameObject.transform, 1, 1, 0, swearId.ToString());
			}
			_currentSwearId = swearId;
		}

		private int _currentSwearId = int.MaxValue;

		public int SwearsOffset = 100;
		public int AgreeingOffset = 200;
		public int DrunkSpeakingOffset = 300;
	}
}
