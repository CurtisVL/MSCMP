using MSCMP.Game;
using MSCMP.Game.Objects;
using MSCMP.Network.Messages;
using UnityEngine;

namespace MSCMP.Network
{
	/// <summary>
	/// Class handling local player state.
	/// </summary>
	internal class NetLocalPlayer
		: NetPlayer
	{
		/// <summary>
		/// Instance.
		/// </summary>
		public static NetLocalPlayer Instance;

		/// <summary>
		/// How much time in seconds left until next synchronization packet will be sent.
		/// </summary>
		private float _timeToUpdate;

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
		public NetLocalPlayer(NetManager netManager, NetWorld netWorld, Steamworks.CSteamID steamId) : base(netManager, netWorld, steamId)
		{
			Instance = this;

			GameDoorsManager.Instance.onDoorsOpen = door =>
			{
				OpenDoorsMessage msg = new OpenDoorsMessage
				{
					position = Utils.GameVec3ToNet(door.transform.position),
					open = true
				};
				netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			};

			GameDoorsManager.Instance.onDoorsClose = door =>
			{
				OpenDoorsMessage msg = new OpenDoorsMessage
				{
					position = Utils.GameVec3ToNet(door.transform.position),
					open = false
				};
				netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			};

			LightSwitchManager.Instance.onLightSwitchUsed = (lswitch, turnedOn) =>
			{
				LightSwitchMessage msg = new LightSwitchMessage
				{
					pos = Utils.GameVec3ToNet(lswitch.transform.position),
					toggle = turnedOn
				};
				netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			};

			if (AnimManager == null) AnimManager = new PlayerAnimManager();
		}

		/// <summary>
		/// Update state of the local player.
		/// </summary>
		public override void Update()
		{
			if (state == State.Passenger)
			{
				// Skip update when we don't have anything to do.
				return;
			}

			// Synchronization sending.
			_timeToUpdate -= Time.deltaTime;
			if (_timeToUpdate <= 0.0f && NetManager.IsPlaying)
			{
				if (AnimManager != null)
				{
					AnimManager.PacketsLeftToSync--;
					if (AnimManager.PacketsLeftToSync <= 0)
					{
						AnimManager.PacketsLeftToSync = AnimManager.PacketsTotalForSync;
						SendAnimSync();
					}
				}

				switch (state)
				{
					case State.OnFoot:
						SendOnFootSync();
						break;
				}
			}
		}

		/// <summary>
		/// Send on foot sync to the server.
		/// </summary>
		/// <returns>true if sync message was sent false otherwise</returns>
		private void SendOnFootSync()
		{
			GamePlayer player = GameWorld.Instance.Player;
			if (player == null) return;

			GameObject playerObject = player.Object;
			if (playerObject == null) return;


			PlayerSyncMessage message = new PlayerSyncMessage
			{
				position = Utils.GameVec3ToNet(playerObject.transform.position),
				rotation = Utils.GameQuatToNet(playerObject.transform.rotation)
			};

			if (player.PickedUpObject)
			{
				Transform objectTrans = player.PickedUpObject.transform;
				PickedUpSync data = new PickedUpSync
				{
					position = Utils.GameVec3ToNet(objectTrans.position),
					rotation = Utils.GameQuatToNet(objectTrans.rotation)
				};

				message.PickedUpData = data;
			}

			if (!NetManager.BroadcastMessage(message, Steamworks.EP2PSend.k_EP2PSendUnreliable))
			{
				return;
			}

			_timeToUpdate = (float)SYNC_INTERVAL / 1000;
		}

		/// <summary>
		/// Send anim sync to the server.
		/// </summary>
		/// <returns>true if sync message was sent false otherwise</returns>
		private void SendAnimSync()
		{
			GamePlayer player = GameWorld.Instance.Player;
			if (player == null) return;

			GameObject playerObject = player.Object;
			if (playerObject == null) return;
			if (playerObject.GetComponentInChildren<CharacterMotor>() == null) return;

			AnimSyncMessage message = new AnimSyncMessage
			{
				isRunning = Utils.GetPlaymakerScriptByName(playerObject, "Running").Fsm.ActiveStateName == "Run",
				isLeaning = Utils.GetPlaymakerScriptByName(playerObject, "Reach").Fsm.GetFsmFloat("Position").Value != 0.0f,
				isGrounded = playerObject.GetComponentInChildren<CharacterMotor>().grounded,
				activeHandState = AnimManager.GetActiveHandState(playerObject),
				swearId = int.MaxValue
			};

			if (AnimManager.GetHandState(message.activeHandState) == PlayerAnimManager.HandStateId.MiddleFingering)
			{
				message.swearId = Utils.GetPlaymakerScriptByName(playerObject, "PlayerFunctions").Fsm.GetFsmInt("RandomInt").Value;
			}

			PlayMakerFSM speechFsm = Utils.GetPlaymakerScriptByName(playerObject, "Speech");
			switch (speechFsm.ActiveStateName)
			{
				case "Swear":
					message.swearId = AnimManager.SwearsOffset + speechFsm.Fsm.GetFsmInt("RandomInt").Value;
					break;
				case "Drunk speech":
					message.swearId = AnimManager.DrunkSpeakingOffset + speechFsm.Fsm.GetFsmInt("RandomInt").Value;
					break;
				case "Yes gestures":
					message.swearId = AnimManager.AgreeingOffset + speechFsm.Fsm.GetFsmInt("RandomInt").Value;
					break;
			}

			message.aimRot = playerObject.transform.FindChild("Pivot/Camera/FPSCamera").transform.rotation.eulerAngles.x;
			message.crouchPosition = Utils.GetPlaymakerScriptByName(playerObject, "Crouch").Fsm.GetFsmFloat("Position").Value;

			GameObject drunkObject = playerObject.transform.FindChild("Pivot/Camera/FPSCamera/FPSCamera").gameObject;
			float drunkValue = Utils.GetPlaymakerScriptByName(drunkObject, "Drunk Mode").Fsm.GetFsmFloat("DrunkYmax").Value;
			message.isDrunk = drunkValue >= 4.5f;

			if (!AnimManager.AreDrinksPreloaded()) AnimManager.PreloadDrinkObjects(playerObject);
			message.drinkId = AnimManager.GetDrinkingObject(playerObject);

			if (!NetManager.BroadcastMessage(message, Steamworks.EP2PSend.k_EP2PSendUnreliable))
			{
				return;
			}
		}

		/// <summary>
		/// Enter vehicle.
		/// </summary>
		/// <param name="vehicle">The vehicle to enter.</param>
		/// <param name="passenger">Is player entering vehicle as passenger?</param>
		public override void EnterVehicle(Game.Components.ObjectSyncComponent vehicle, bool passenger)
		{
			base.EnterVehicle(vehicle, passenger);

			VehicleEnterMessage msg = new VehicleEnterMessage
			{
				objectID = vehicle.ObjectId,
				passenger = passenger
			};
			NetManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			vehicle.TakeSyncControl();
		}

		/// <summary>
		/// Leave vehicle player is currently sitting in.
		/// </summary>
		public override void LeaveVehicle()
		{
			base.LeaveVehicle();

			NetManager.BroadcastMessage(new VehicleLeaveMessage(), Steamworks.EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Write player state into the network message.
		/// </summary>
		/// <param name="msg">Message to write to.</param>
		public void WriteSpawnState(FullWorldSyncMessage msg)
		{
			msg.spawnPosition = Utils.GameVec3ToNet(GetPosition());
			msg.spawnRotation = Utils.GameQuatToNet(GetRotation());

			msg.pickedUpObject = NetPickupable.INVALID_ID;
		}

		/// <summary>
		/// Send EventHook sync message.
		/// </summary>
		/// <param name="fsmId">FSM ID</param>
		/// <param name="fsmEventId">FSM Event ID</param>
		/// <param name="fsmEventName">Optional FSM Event name</param>
		public void SendEventHookSync(int fsmId, int fsmEventId, string fsmEventName = "none")
		{
			EventHookSyncMessage msg = new EventHookSyncMessage
			{
				fsmID = fsmId,
				fsmEventID = fsmEventId,
				request = false
			};
			if (fsmEventName != "none")
			{
				msg.FsmEventName = fsmEventName;
			}
			NetManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Request event sync from host.
		/// </summary>
		/// <param name="fsmId">FSM ID</param>
		public void RequestEventHookSync(int fsmId)
		{
			EventHookSyncMessage msg = new EventHookSyncMessage
			{
				fsmID = fsmId,
				fsmEventID = -1,
				request = true
			};
			NetManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Switches state of this player.
		/// </summary>
		/// <param name="newState">The state to switch to.</param>
		protected override void SwitchState(State newState)
		{
			if (state == newState)
			{
				return;
			}

			base.SwitchState(newState);

			// Force synchronization to be send on next frame.
			_timeToUpdate = 0.0f;
		}

		/// <summary>
		/// Get world position of the character.
		/// </summary>
		/// <returns>World position of the player character.</returns>
		public override Vector3 GetPosition()
		{
			GamePlayer player = GameWorld.Instance.Player;
			if (player == null)
			{
				return Vector3.zero;
			}
			GameObject playerObject = player.Object;
			if (playerObject == null)
			{
				return Vector3.zero;
			}
			return playerObject.transform.position;
		}

		/// <summary>
		/// Get world rotation of the character.
		/// </summary>
		/// <returns>World rotation of the player character.</returns>
		public override Quaternion GetRotation()
		{
			GamePlayer player = GameWorld.Instance.Player;
			if (player == null)
			{
				return Quaternion.identity;
			}
			GameObject playerObject = player.Object;
			if (playerObject == null)
			{
				return Quaternion.identity;
			}
			return playerObject.transform.rotation;
		}

		/// <summary>
		/// Get steam name of the player.
		/// </summary>
		/// <returns>Steam name of the player.</returns>
		public override string GetName()
		{
			return Steamworks.SteamFriends.GetPersonaName();
		}

		/// <summary>
		/// Teleport player to the given location.
		/// </summary>
		/// <param name="pos">The position to teleport to.</param>
		/// <param name="rot">The rotation to teleport to.</param>
		public override void Teleport(Vector3 pos, Quaternion rot)
		{
			GamePlayer player = GameWorld.Instance.Player;
			if (player == null)
			{
				return;
			}
			GameObject playerObject = player.Object;
			if (playerObject == null)
			{
				return;
			}
			playerObject.transform.position = pos;
			playerObject.transform.rotation = rot;
		}
	}
}
