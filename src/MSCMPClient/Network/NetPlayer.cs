using System;
using MSCMP.Game.Objects;
using MSCMP.Network.Messages;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSCMP.Network
{
	/// <summary>
	/// Class representing network player.
	/// </summary>
	internal class NetPlayer 
		: IDisposable
	{

		private readonly Steamworks.CSteamID _steamId;

		/// <summary>
		/// Offset for the character model.
		/// </summary>
		private readonly Vector3 _characterOffset = new Vector3(0.0f, 0.60f, 0.0f);

		/// <summary>
		/// Steam id of the player.
		/// </summary>
		public Steamworks.CSteamID SteamId => _steamId;

		/// <summary>
		/// The network manager managing connection with this player.
		/// </summary>
		protected readonly NetManager NetManager;

		/// <summary>
		/// The anim manager managing connection with this player.
		/// </summary>
		public PlayerAnimManager AnimManager;

		/// <summary>
		/// The game object representing character.
		/// </summary>
		private GameObject _characterGameObject;

		/// <summary>
		/// Character interpolator.
		/// </summary>
		private readonly Math.TransformInterpolator _interpolator = new Math.TransformInterpolator();

		/// <summary>
		/// Picked up object interpolator.
		/// </summary>
		private readonly Math.TransformInterpolator _pickedUpObjectInterpolator = new Math.TransformInterpolator();

		/// <summary>
		/// Interpolation time in miliseconds.
		/// </summary>
		public const ulong INTERPOLATION_TIME = NetLocalPlayer.SYNC_INTERVAL;

		/// <summary>
		/// Network time when sync packet was received.
		/// </summary>
		private ulong _syncReceiveTime;

		/// <summary>
		/// Name of the game object we use as prefab for characters.
		/// </summary>
		private const string CHARACTER_PREFAB_NAME = "Assets/MPPlayerModel/MPPlayerModel.fbx";

		/// <summary>
		/// Current player state.
		/// </summary>
		protected enum State
		{
			OnFoot,
			DrivingVehicle,
			Passenger
		}

		/// <summary>
		/// State of the player.
		/// </summary>
		protected State state = State.OnFoot;

		/// <summary>
		/// The current vehicle player is inside.
		/// </summary>
		protected Game.Components.ObjectSyncComponent CurrentVehicle;

		/// <summary>
		/// Network world this player is spawned in.
		/// </summary>
		private readonly NetWorld _netWorld;

		/// <summary>
		/// The object the player has picked up.
		/// </summary>
		private GameObject _pickedUpObject;

		/// <summary>
		/// The network id of object the player has picked up.
		/// </summary>
		private ushort _pickedUpObjectNetId = NetPickupable.INVALID_ID;

		/// <summary>
		/// The old layer of the pickupable. Used to restore layer after releasing object.
		/// </summary>
		private int _oldPickupableLayer;

		/// <summary>
		/// Is this player spawned?
		/// </summary>
		/// <remarks>
		/// This state is valid only for remote players.
		/// </remarks>
		public bool IsSpawned => _characterGameObject != null;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="netManager">Network manager managing connection to the player.</param>
		/// <param name="netWorld">Network world owning this player.</param>
		/// <param name="steamId">Player's steam id.</param>
		public NetPlayer(NetManager netManager, NetWorld netWorld, Steamworks.CSteamID steamId)
		{
			NetManager = netManager;
			_netWorld = netWorld;
			_steamId = steamId;
		}

		/// <summary>
		/// Spawns character object in world.
		/// </summary>
		public void Spawn()
		{
			GameObject loadedModel = Client.LoadAsset<GameObject>(CHARACTER_PREFAB_NAME);
			_characterGameObject = (GameObject)Object.Instantiate(loadedModel, _interpolator.CurrentPosition, _interpolator.CurrentRotation);

			// If character will disappear we uncomment this
			// GameObject.DontDestroyOnLoad(go);

			//Getting the Animation component of the model, and setting the priority layers of each animation
			if (AnimManager == null) { AnimManager = new PlayerAnimManager(); Logger.Debug("AnimManager: JUST CREATED MORE!"); }
			else Logger.Debug("AnimManager: We had already (from NetLocal) our animManager");
			AnimManager.SetupAnimations(_characterGameObject);

			if (_pickedUpObjectNetId != NetPickupable.INVALID_ID)
			{
				UpdatePickedUpObject(true, false);
			}

			if (CurrentVehicle != null)
			{
				SitInCurrentVehicle();
			}
		}

		/// <summary>
		/// Cleanup all objects before destroying the player.
		/// </summary>
		public void Dispose()
		{
			if (CurrentVehicle != null)
			{
				LeaveVehicle();
			}

			// Destroy player model on disconnect/timeout.
			if (_characterGameObject != null)
			{
				Object.Destroy(_characterGameObject);
				_characterGameObject = null;
			}
		}

		/// <summary>
		/// Send a packet to this player.
		/// </summary>
		/// <param name="data">The data to send.</param>
		/// <param name="sendType">Type of the send.</param>
		/// <param name="channel">The channel to send message.</param>
		/// <returns>true if packet was sent, false otherwise</returns>
		public bool SendPacket(byte[] data, Steamworks.EP2PSend sendType, int channel = 0)
		{
			return Steamworks.SteamNetworking.SendP2PPacket(_steamId, data, (uint)data.Length, sendType, channel);
		}

		/// <summary>
		/// Updates state of the player.
		/// </summary>
		public virtual void Update()
		{
			// Some naive interpolation.
			if (_characterGameObject && _syncReceiveTime > 0)
			{
				float progress = (float)(NetManager.GetNetworkClock() - _syncReceiveTime) / INTERPOLATION_TIME;

				float speed = 0.0f;
				if (progress <= 2.0f)
				{
					Vector3 oldPos = _interpolator.CurrentPosition;
					Vector3 currentPos = Vector3.zero;
					Quaternion currentRot = Quaternion.identity;
					_interpolator.Evaluate(ref currentPos, ref currentRot, progress);
					Vector3 delta = currentPos - oldPos;
					delta.y = 0.0f;
					speed = delta.magnitude;

					UpdateCharacterPosition();

					_pickedUpObjectInterpolator.Evaluate(progress);
					UpdatePickedupPosition();
				}

				if (AnimManager != null)
				{
					AnimManager.HandleOnFootMovementAnimations(speed);
					AnimManager.CheckBlendedOutAnimationStates();
					AnimManager.SyncVerticalHeadLook(_characterGameObject, progress);
				}
			}
		}

		/// <summary>
		/// Draw this player name tag.
		/// </summary>
		/// <param name="playerId">Player ID of player.</param>
		public void DrawNametag(int playerId)
		{
			if (_characterGameObject == null) return;

			Vector3 spos = Camera.main.WorldToScreenPoint(_characterGameObject.transform.position + Vector3.up * 2.0f);
			if (spos.z > 0.0f)
			{
				float width = 100.0f;
				spos.x -= width / 2.0f;
				GUI.color = Color.black;
				GUI.Label(new Rect(spos.x + 1, Screen.height - spos.y + 1, width, 20), GetName() + " (" + playerId + ")");
				GUI.color = Color.cyan;
				GUI.Label(new Rect(spos.x, Screen.height - spos.y, width, 20), GetName() + " (" + playerId + ")");
				GUI.color = Color.white;
			}
		}

		/// <summary>
		/// Handle received synchronization message.
		/// </summary>
		/// <param name="msg">The received synchronization message.</param>
		public void HandleSynchronize(PlayerSyncMessage msg)
		{
			Client.Assert(state == State.OnFoot, "Received on foot update but player is not on foot.");

			Vector3 targetPos = Utils.NetVec3ToGame(msg.position);
			Quaternion targetRot = Utils.NetQuatToGame(msg.rotation);

			_interpolator.SetTarget(targetPos, targetRot);
			_syncReceiveTime = NetManager.GetNetworkClock();

			if (msg.HasPickedUpData)
			{
				PickedUpSync pickedUpData = msg.PickedUpData;
				_pickedUpObjectInterpolator.SetTarget(Utils.NetVec3ToGame(pickedUpData.position), Utils.NetQuatToGame(pickedUpData.rotation));
			}

			if (!IsSpawned)
			{
				Teleport(targetPos, targetRot);
			}
		}

		/// <summary>
		/// Handle received animation synchronization message.
		/// </summary>
		/// <param name="msg">The received synchronization message.</param>
		public void HandleAnimSynchronize(AnimSyncMessage msg)
		{
			AnimManager?.HandleAnimations(msg);
		}

		/// <summary>
		/// Sit in current vehicle.
		/// </summary>
		private void SitInCurrentVehicle()
		{
			if (CurrentVehicle == null) return;

			// Make sure player character is attached as we will not update it's position until he leaves vehicle.
			if (!IsSpawned) return;

			PlayerVehicle vehicleGameObject = CurrentVehicle.GetObjectSubtype() as PlayerVehicle;
			if (vehicleGameObject == null) return;
					
			if (state == State.DrivingVehicle)
			{
				Transform seatTransform = vehicleGameObject.SeatTransform;
				Teleport(seatTransform.position, seatTransform.rotation);
			}
			else if (state == State.Passenger)
			{
				Transform passangerSeatTransform = vehicleGameObject.PassengerSeatTransform;
				Teleport(passangerSeatTransform.position, passangerSeatTransform.rotation);
			}

			_characterGameObject.transform.SetParent(vehicleGameObject.ParentGameObject.transform, false);
		}

		/// <summary>
		/// Enter vehicle.
		/// </summary>
		/// <param name="vehicle">The vehicle to enter.</param>
		/// <param name="passenger">Is player entering vehicle as passenger?</param>
		public virtual void EnterVehicle(Game.Components.ObjectSyncComponent vehicle, bool passenger)
		{
			Client.Assert(CurrentVehicle == null, "Entered vehicle but player is already in vehicle.");
			Client.Assert(state == State.OnFoot, "Entered vehicle but player is not on foot.");

			CurrentVehicle = vehicle;

			if (CurrentVehicle.GetObjectSubtype() is PlayerVehicle vehicleSubtype)
				vehicleSubtype.CurrentDrivingState = !passenger
					? PlayerVehicle.DrivingStates.Driver
					: PlayerVehicle.DrivingStates.Passenger;

			SitInCurrentVehicle();

			// Set state of the player.
			SwitchState(passenger ? State.Passenger : State.DrivingVehicle);
		}

		/// <summary>
		/// Leave vehicle player is currently sitting in.
		/// </summary>
		public virtual void LeaveVehicle()
		{
			Client.Assert(CurrentVehicle != null && state != State.OnFoot, "Player is leaving vehicle but he is not in vehicle.");

			// Detach character game object from vehicle.
			if (IsSpawned)
			{
				_characterGameObject.transform.SetParent(null);

				PlayerVehicle vehicleGameObject = CurrentVehicle.GetObjectSubtype() as PlayerVehicle;
				Transform seatTransform = vehicleGameObject.SeatTransform;
				Teleport(seatTransform.position, seatTransform.rotation);

				// Notify vehicle that the player left.
				vehicleGameObject.CurrentDrivingState = PlayerVehicle.DrivingStates.None;
			}

			CurrentVehicle = null;

			// Set state of the player.
			SwitchState(State.OnFoot);
		}

		/// <summary>
		/// Switches state of this player.
		/// </summary>
		/// <param name="newState">The state to switch to.</param>
		protected virtual void SwitchState(State newState)
		{
			state = newState;
		}

		/// <summary>
		/// Teleport player to the given location.
		/// </summary>
		/// <param name="pos">The position to teleport to.</param>
		/// <param name="rot">The rotation to teleport to.</param>
		public virtual void Teleport(Vector3 pos, Quaternion rot)
		{
			_interpolator.Teleport(pos, rot);
			UpdateCharacterPosition();
		}

		/// <summary>
		/// Update character position from interpolator.
		/// </summary>
		private void UpdateCharacterPosition()
		{
			if (_characterGameObject == null) return;

			_characterGameObject.transform.position = _interpolator.CurrentPosition + _characterOffset;
			_characterGameObject.transform.rotation = _interpolator.CurrentRotation;
		}

		/// <summary>
		/// Update position of the picked up object.
		/// </summary>
		private void UpdatePickedupPosition()
		{
			if (_pickedUpObject == null) return;

			_pickedUpObject.transform.position = _pickedUpObjectInterpolator.CurrentPosition;
			_pickedUpObject.transform.rotation = _pickedUpObjectInterpolator.CurrentRotation;
		}

		/// <summary>
		/// Get world position of the character.
		/// </summary>
		/// <returns>World position of the player character.</returns>
		public virtual Vector3 GetPosition()
		{
			return _interpolator.CurrentPosition;
		}

		/// <summary>
		/// Get world rotation of the character.
		/// </summary>
		/// <returns>World rotation of the player character.</returns>
		public virtual Quaternion GetRotation()
		{
			return _interpolator.CurrentRotation;
		}

		/// <summary>
		/// Get steam name of the player.
		/// </summary>
		/// <returns>Steam name of the player.</returns>
		public virtual string GetName()
		{
			return Steamworks.SteamFriends.GetFriendPersonaName(_steamId);
		}

		/// <summary>
		/// Pickup the object.
		/// </summary>
		/// <param name="netId">netId of the object to pickup</param>
		public void PickupObject(ushort netId)
		{
			_pickedUpObjectNetId = netId;

			// Teleport picked up object position to perform much nicer transition
			// of object interpolation. Previously the object was interpolated from last frame.
			_pickedUpObjectInterpolator.Teleport(_interpolator.CurrentPosition, _interpolator.CurrentRotation);
			if (IsSpawned)
			{
				UpdatePickedUpObject(true, false);
			}
		}

		/// <summary>
		/// Release the object.
		/// </summary>
		/// <param name="drop">Is it drop or throw?</param>
		public void ReleaseObject(bool drop)
		{
			if (IsSpawned)
			{
				UpdatePickedUpObject(false, drop);
			}
			_pickedUpObjectNetId = NetPickupable.INVALID_ID;
		}

		/// <summary>
		/// Update picked up object.
		/// </summary>
		/// <param name="pickup">Is this pickup action?</param>
		/// <param name="drop">If not pickup is it drop or throw?</param>
		private void UpdatePickedUpObject(bool pickup, bool drop)
		{
			if (pickup)
			{
				_pickedUpObject = _netWorld.GetPickupableGameObject(_pickedUpObjectNetId);
				Client.Assert(_pickedUpObject != null, "Player tried to pickup object that does not exists in world. Net id: " + _pickedUpObjectNetId);
				_oldPickupableLayer = _pickedUpObject.layer;
				_pickedUpObject.layer = Utils.LAYER_IGNORE_RAYCAST;
				_pickedUpObject.GetComponent<Rigidbody>().isKinematic = true;
			}
			else
			{
				Client.Assert(_pickedUpObject != null, "Tried to drop item however player has no item in hands.");
				_pickedUpObject.layer = _oldPickupableLayer;
				_pickedUpObject.GetComponent<Rigidbody>().isKinematic = false;
				if (!drop)
				{
					float thrust = 50;
					_pickedUpObject.GetComponent<Rigidbody>().AddForce(_pickedUpObject.transform.forward * thrust, ForceMode.Impulse);
				}
				_pickedUpObject = null;
			}
		}
	}
}
