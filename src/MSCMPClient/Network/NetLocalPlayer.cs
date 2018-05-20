using MSCMP.Game;
using MSCMP.Game.Objects;
using UnityEngine;

namespace MSCMP.Network {

	/// <summary>
	/// Class handling local player state.
	/// </summary>
	class NetLocalPlayer : NetPlayer {

		/// <summary>
		/// How much time in seconds left until next synchronization packet will be sent.
		/// </summary>
		private float timeToUpdate = 0.0f;

		/// <summary>
		/// Synchronization interval in milliseconds.
		/// </summary>
		public const ulong SYNC_INTERVAL = 100;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="netManager">The network manager owning this player.</param>
		/// <param name="netWorld">Network world owning this player.</param>
		/// <param name="steamId">The steam id of the player.</param>
		public NetLocalPlayer(NetManager netManager, NetWorld netWorld, Steamworks.CSteamID steamId) : base(netManager, netWorld, steamId) {

			GameDoorsManager.Instance.onDoorsOpen = (GameObject door) => {
				Messages.OpenDoorsMessage msg = new Messages.OpenDoorsMessage();
				msg.position = Utils.GameVec3ToNet(door.transform.position);
				msg.open = true;
				netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			};

			GameDoorsManager.Instance.onDoorsClose = (GameObject door) => {
				Messages.OpenDoorsMessage msg = new Messages.OpenDoorsMessage();
				msg.position = Utils.GameVec3ToNet(door.transform.position);
				msg.open = false;
				netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			};

			GameCallbacks.onObjectPickup = (GameObject gameObj) => {
				Messages.PickupObjectMessage msg = new Messages.PickupObjectMessage();
				msg.netId = netWorld.GetPickupableNetId(gameObj);
				Client.Assert(msg.netId != NetPickupable.INVALID_ID, "Tried to pickup not network pickupable.");
				netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			};

			GameCallbacks.onObjectRelease = (bool drop) => {
				Messages.ReleaseObjectMessage msg = new Messages.ReleaseObjectMessage();
				msg.drop = drop;
				netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			};

			BeerCaseManager.Instance.onBottleConsumed = (GameObject bcase) => {
				Messages.RemoveBottleMessage msg = new Messages.RemoveBottleMessage();
				msg.netId = netWorld.GetPickupableNetId(bcase);
				Client.Assert(msg.netId != NetPickupable.INVALID_ID, "Tried to drink from not network beercase.");
				netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			};

			LightSwitchManager.Instance.onLightSwitchUsed = (GameObject lswitch, bool turnedOn) => {
				Messages.LightSwitchMessage msg = new Messages.LightSwitchMessage();
				msg.pos = Utils.GameVec3ToNet(lswitch.transform.position);
				msg.toggle = turnedOn;
				netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			};

			if (animManager == null) animManager = new PlayerAnimManager();
		}

		/// <summary>
		/// Update state of the local player.
		/// </summary>
		public override void Update() {
			if (state == State.Passenger) {
				// Skip update when we don't have anything to do.
				return;
			}

			// Synchronization sending.

			timeToUpdate -= Time.deltaTime;
			if (timeToUpdate <= 0.0f && netManager.IsPlaying) {
				if (animManager != null) {
					animManager.PACKETS_LEFT_TO_SYNC--;
					if (animManager.PACKETS_LEFT_TO_SYNC <= 0) {
						animManager.PACKETS_LEFT_TO_SYNC = animManager.PACKETS_TOTAL_FOR_SYNC;
						SendAnimSync();
					}
				}

				switch (state) {
					case State.DrivingVehicle:
						SendInVehicleSync();
						break;

					case State.OnFoot:
						SendOnFootSync();
						break;
				}
			}
		}

		/// <summary>
		/// Send in vehicle synchronization.
		/// </summary>
		/// <returns>true if sync was set, false otherwise</returns>
		private bool SendInVehicleSync() {
			Client.Assert(currentVehicle != null, "Tried to send in vehicle sync packet but no current vehicle is set.");

			Messages.VehicleSyncMessage message = new Messages.VehicleSyncMessage();
			if (!currentVehicle.WriteSyncMessage(message)) {
				return false;
			}
			if (!netManager.BroadcastMessage(message, Steamworks.EP2PSend.k_EP2PSendUnreliable)) {
				return false;
			}

			timeToUpdate = (float)NetVehicle.SYNC_DELAY / 1000;
			return true;
		}

		/// <summary>
		/// Send on foot sync to the server.
		/// </summary>
		/// <returns>true if sync message was sent false otherwise</returns>
		private bool SendOnFootSync() {
			GamePlayer player = GameWorld.Instance.Player;
			if (player == null) {
				return false;
			}
			GameObject playerObject = player.Object;
			if (playerObject == null) {
				return false;
			}

			Messages.PlayerSyncMessage message = new Messages.PlayerSyncMessage();

			message.position = Utils.GameVec3ToNet(playerObject.transform.position);
			message.rotation = Utils.GameQuatToNet(playerObject.transform.rotation);

			if (player.PickedUpObject) {
				Transform objectTrans = player.PickedUpObject.transform;
				var data = new Messages.PickedUpSync();
				data.position = Utils.GameVec3ToNet(objectTrans.position);
				data.rotation = Utils.GameQuatToNet(objectTrans.rotation);

				message.PickedUpData = data;
			}

			if (!netManager.BroadcastMessage(message, Steamworks.EP2PSend.k_EP2PSendUnreliable)) {
				return false;
			}

			timeToUpdate = (float)SYNC_INTERVAL / 1000;
			return true;
		}

		/// <summary>
		/// Send anim sync to the server.
		/// </summary>
		/// <returns>true if sync message was sent false otherwise</returns>
		private bool SendAnimSync() {
			GamePlayer player = GameWorld.Instance.Player;
			if (player == null) return false;

			GameObject playerObject = player.Object;
			if (playerObject == null) return false;

			Messages.AnimSyncMessage message = new Messages.AnimSyncMessage();

			message.isRunning = (Utils.GetPlaymakerScriptByName(playerObject, "Running").Fsm.ActiveStateName == "Run");

			float leanRotation = Utils.GetPlaymakerScriptByName(playerObject, "Reach").Fsm.GetFsmFloat("Position").Value;
			if (leanRotation != 0.0f) message.isLeaning = true;
			else message.isLeaning = false;

			message.isGrounded = playerObject.GetComponentInChildren<CharacterMotor>().grounded;
			message.activeHandState = animManager.GetActiveHandState(playerObject);
			message.aimRot = playerObject.transform.FindChild("Pivot/Camera/FPSCamera").transform.rotation.eulerAngles.x;
			message.crouchPosition = Utils.GetPlaymakerScriptByName(playerObject, "Crouch").Fsm.GetFsmFloat("Position").Value;

			GameObject DrunkObject = playerObject.transform.FindChild("Pivot/Camera/FPSCamera/FPSCamera").gameObject;
			float DrunkValue = Utils.GetPlaymakerScriptByName(DrunkObject, "Drunk Mode").Fsm.GetFsmFloat("DrunkYmax").Value;
			if (DrunkValue >= 4.5f) message.isDrunk = true;
			else message.isDrunk = false;

			if (!animManager.AreDrinksPreloaded()) animManager.PreloadDrinkObjects(playerObject);

			//if (animManager.GetHandState(message.activeHandState) == PlayerAnimManager.HandStateId.Drinking)
				message.drinkId = animManager.GetDrinkingObject(playerObject);

			if (!netManager.BroadcastMessage(message, Steamworks.EP2PSend.k_EP2PSendUnreliable)) {
				return false;
			}

			return true;
		}

		/// <summary>
		/// Enter vehicle.
		/// </summary>
		/// <param name="vehicle">The vehicle to enter.</param>
		/// <param name="passenger">Is player entering vehicle as passenger?</param>
		public override void EnterVehicle(NetVehicle vehicle, bool passenger) {
			base.EnterVehicle(vehicle, passenger);

			Messages.VehicleEnterMessage msg = new Messages.VehicleEnterMessage();
			msg.vehicleId = vehicle.NetId;
			msg.passenger = false;
			netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Leave vehicle player is currently sitting in.
		/// </summary>
		public override void LeaveVehicle() {
			base.LeaveVehicle();

			netManager.BroadcastMessage(new Messages.VehicleLeaveMessage(), Steamworks.EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Write player state into the network message.
		/// </summary>
		/// <param name="msg">Message to write to.</param>
		public void WriteSpawnState(Messages.FullWorldSyncMessage msg) {
			msg.spawnPosition = Utils.GameVec3ToNet(GetPosition());
			msg.spawnRotation = Utils.GameQuatToNet(GetRotation());

			if (state == State.OnFoot) {
				msg.occupiedVehicleId = NetVehicle.INVALID_ID;
			}
			else {
				msg.occupiedVehicleId = currentVehicle.NetId;
				msg.passenger = state == State.Passenger;
			}

			msg.pickedUpObject = NetPickupable.INVALID_ID;

			GamePlayer player = GameWorld.Instance.Player;
			if (player != null) {
				// It is possible the local player is not spawned yet. If so we do not
				// care about those values as those can be only set when player is actually spawned.

				var pickedUpObject = player.PickedUpObject;
				if (pickedUpObject != null) {
					msg.pickedUpObject = netWorld.GetPickupableNetId(pickedUpObject);
				}
			}
		}

		/// <summary>
		/// Switches state of this player.
		/// </summary>
		/// <param name="newState">The state to switch to.</param>
		protected override void SwitchState(State newState) {
			if (state == newState) {
				return;
			}

			base.SwitchState(newState);

			// Force synchronization to be send on next frame.
			timeToUpdate = 0.0f;
		}

		/// <summary>
		/// Get world position of the character.
		/// </summary>
		/// <returns>World position of the player character.</returns>
		public override Vector3 GetPosition() {
			GamePlayer player = GameWorld.Instance.Player;
			if (player == null) {
				return Vector3.zero;
			}
			var playerObject = player.Object;
			if (playerObject == null) {
				return Vector3.zero;
			}
			return playerObject.transform.position;
		}

		/// <summary>
		/// Get world rotation of the character.
		/// </summary>
		/// <returns>World rotation of the player character.</returns>
		public override Quaternion GetRotation() {
			GamePlayer player = GameWorld.Instance.Player;
			if (player == null) {
				return Quaternion.identity;
			}
			var playerObject = player.Object;
			if (playerObject == null) {
				return Quaternion.identity;
			}
			return playerObject.transform.rotation;
		}

		/// <summary>
		/// Get steam name of the player.
		/// </summary>
		/// <returns>Steam name of the player.</returns>
		public override string GetName() {
			return Steamworks.SteamFriends.GetPersonaName();
		}

		/// <summary>
		/// Teleport player to the given location.
		/// </summary>
		/// <param name="pos">The position to teleport to.</param>
		/// <param name="rot">The rotation to teleport to.</param>
		public override void Teleport(Vector3 pos, Quaternion rot) {
			GamePlayer player = GameWorld.Instance.Player;
			if (player == null) {
				return;
			}
			var playerObject = player.Object;
			if (playerObject == null) {
				return;
			}
			playerObject.transform.position = pos;
			playerObject.transform.rotation = rot;
		}
	}
}
