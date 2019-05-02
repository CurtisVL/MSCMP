using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using MSCMP.Game.Components;
using MSCMP.Network;
using HutongGames.PlayMaker;

namespace MSCMP.Game.Objects {
	class PlayerVehicle : ISyncedObject {

		ObjectSyncComponent syncComponent;
		bool isSyncing = false;

		GameObject gameObject;
		Rigidbody rigidbody;

		public GameObject ParentGameObject;
		public Transform SeatTransform;
		public Transform PassengerSeatTransform;

		public enum DrivingStates {
			Driver,
			Passenger,
			None
		}

		public bool DriverIsLocal = false;
		public DrivingStates CurrentDrivingState = DrivingStates.None;

		CarDynamics dynamics;
		Drivetrain driveTrain;
		GameObject starterGameObject = null;

		// General
		PlayMakerFSM starterFsm = null;
		PlayMakerFSM ignitionFsm = null;
		PlayMakerFSM handbrakeFsm = null;
		PlayMakerFSM fuelTankFsm = null;
		PlayMakerFSM rangeFsm = null;
		PlayMakerFSM gearIndicatorFsm = null;
		PlayMakerFSM dashboardFsm = null;
		PlayMakerFSM fuelTapFsm = null;
		PlayMakerFSM lightsFsm = null;
		PlayMakerFSM wipersFsm = null;
		PlayMakerFSM interiorLightFsm = null;
		PlayMakerFSM frontHydraulicFsm = null;
		PlayMakerFSM indicatorsFsm = null;

		// Truck specific
		PlayMakerFSM hydraulicPumpFsm = null;
		PlayMakerFSM diffLockFsm = null;
		PlayMakerFSM axleLiftFsm = null;
		PlayMakerFSM spillValveFsm = null;
		PlayMakerFSM beaconFsm = null;

		// Misc
		PlayMakerFSM waspNestFsm = null;

		// Vehicle specifics
		public bool hasRange = false;
		bool hasLeverParkingBrake = false;
		bool hasPushParkingBrake = false;

		bool isTruck = false;
		bool isTractor = false;
		bool isBike = false;

		bool hydraulicPumpFirstRun = true;
		bool axleLiftFirstRun = true;
		bool diffLockFirstRun = true;

		GameObject steeringPivot;

		AxisCarController axisCarController = null;
		MPCarController mpCarController = null;

		public enum EngineStates {
			WaitForStart,
			ACC,
			Glowplug,
			TurnKey,
			CheckClutch,
			StartingEngine,
			StartEngine,
			StartOrNot,
			MotorRunning,
			Wait,
			Null,
		}

		public enum DashboardStates {
			ACCon,
			Test,
			ACCon2,
			MotorStarting,
			ShutOff,
			MotorOff,
			WaitButton,
			WaitPlayer,
			Null,
		}

		public enum SwitchIDs {
			HandbrakePull,
			HandbrakeLever,
			Lights,
			Wipers,
			HydraulicPump,
			DiffLock,
			AxleLift,
			InteriorLight,
			SpillValve,
			FuelTap,
			TractorHydraulics,
			DestroyWaspNest,
			FlatbedHatch,
		}

		class MPCarController : AxisCarController {
			public float remoteThrottleInput = 0.0f;
			public float remoteBrakeInput = 0.0f;
			public float remoteSteerInput = 0.0f;
			public float remoteHandbrakeInput = 0.0f;
			public float remoteClutchInput = 0.0f;
			public bool remoteStartEngineInput = false;
			public int remoteTargetGear = 0;

			protected override void GetInput(out float throttleInput, out float brakeInput, out float steerInput, out float handbrakeInput, out float clutchInput, out bool startEngineInput, out int targetGear) {
				throttleInput = remoteThrottleInput;
				brakeInput = remoteBrakeInput;
				steerInput = remoteSteerInput;
				handbrakeInput = remoteHandbrakeInput;
				clutchInput = remoteClutchInput;
				startEngineInput = remoteStartEngineInput;
				targetGear = remoteTargetGear;
			}
		}

		public float Steering {
			get {
				return dynamics.carController.steering;
			}
			set {
				mpCarController.remoteSteerInput = value;
			}
		}

		public float Throttle {
			get {
				return dynamics.carController.throttleInput;
			}
			set {
				mpCarController.remoteThrottleInput = value;
			}
		}

		public float Brake {
			get {
				return dynamics.carController.brakeInput;
			}
			set {
				mpCarController.remoteBrakeInput = value;
				dynamics.carController.brakeInput = value;
			}
		}

		public float ClutchInput {
			get {
				return driveTrain.clutch.GetClutchPosition();
			}
			set {
				driveTrain.clutch.SetClutchPosition(value);
			}
		}

		public int Gear {
			get {
				return driveTrain.gear;
			}
			set {
				mpCarController.remoteTargetGear = value;
			}
		}

		public float Fuel {
			get {
				return fuelTankFsm.Fsm.GetFsmFloat("FuelLevel").Value;
			}
			set {
				fuelTankFsm.Fsm.GetFsmFloat("FuelLevel").Value = value;
			}
		}

		float steamID = Steamworks.SteamUser.GetSteamID().m_SteamID;


		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="go"></param>
		public PlayerVehicle(GameObject go, ObjectSyncComponent osc) {
			gameObject = go;
			syncComponent = osc;
			ParentGameObject = go.transform.parent.parent.gameObject;

			if (ParentGameObject.name.StartsWith("JONNEZ")) {
				isBike = true;
				steeringPivot = ParentGameObject.transform.FindChild("LOD/Suspension/Steering/SteeringPivot").gameObject;
			}

			rigidbody = ParentGameObject.GetComponent<Rigidbody>();
			dynamics = ParentGameObject.GetComponent<CarDynamics>();
			driveTrain = ParentGameObject.GetComponent<Drivetrain>();

			axisCarController = ParentGameObject.GetComponent<AxisCarController>();
			mpCarController = ParentGameObject.AddComponent<MPCarController>();

			FindFSMs();
		}

		/// <summary>
		/// Specifics for syncing this object.
		/// </summary>
		/// <returns>What should be synced for this object.</returns>
		public ObjectSyncManager.Flags flags() {
			return ObjectSyncManager.Flags.Full;
		}

		/// <summary>
		/// Adds a passenger seat to the vehicle.
		/// </summary>
		void AddPassengerSeat(PlayMakerFSM fsm) {
			GameObject passengerSeat = GameObject.CreatePrimitive(PrimitiveType.Cube);
			PassengerSeatTransform = passengerSeat.transform;

			passengerSeat.transform.parent = fsm.gameObject.transform.parent;
			passengerSeat.transform.position = passengerSeat.transform.parent.position;
			passengerSeat.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);

			passengerSeat.transform.GetComponent<BoxCollider>().isTrigger = true;

			PassengerSeat pSeatScript = passengerSeat.AddComponent(typeof(PassengerSeat)) as PassengerSeat;
			pSeatScript.VehicleType = ParentGameObject.name;
			pSeatScript.DriversSeat = fsm.gameObject;
		}

		/// <summary>
		/// Enable or disable remote steering.
		/// </summary>
		/// <param name="enabled"></param>
		public void SetRemoteSteering(bool enabled) {
			axisCarController.enabled = !enabled;
			mpCarController.enabled = enabled;
		}

		/// <summary>
		/// Get object's Transform.
		/// </summary>
		/// <returns>Object's Transform.</returns>
		public Transform ObjectTransform() {
			return ParentGameObject.transform;
		}

		/// <summary>
		/// Check is periodic sync of the object is enabled.
		/// </summary>
		/// <returns>Periodic sync enabled or disabled.</returns>
		public bool PeriodicSyncEnabled() {
			return true;
		}

		/// <summary>
		/// Determines if the object should be synced.
		/// </summary>
		/// <returns>True if object should be synced, false if it shouldn't.</returns>
		public bool CanSync() {
			if (CurrentDrivingState == DrivingStates.Driver && DriverIsLocal) {
				isSyncing = true;
				return true;
			}
			else if (rigidbody.velocity.sqrMagnitude >= 0.01f) {
				isSyncing = true;
				return true;
			}
			else {
				isSyncing = false;
				return false;
			}
		}

		/// <summary>
		/// Called when a player enters range of an object.
		/// </summary>
		/// <returns>True if the player should tkae ownership of the object.</returns>
		public bool ShouldTakeOwnership() {
			return true;
		}

		/// <summary>
		/// Returns variables to be sent to the remote client.
		/// </summary>
		/// <returns>Variables to be sent to the remote client.</returns>
		public float[] ReturnSyncedVariables(bool sendAllVariables) {
			if (isSyncing == true) {
				// Removed fuel from this due to an error, maybe an update broke it?
				if (isBike) {
					float[] variables = { Steering, Throttle, Brake, ClutchInput, Gear, steeringPivot.transform.localRotation.z };
					return variables;
				}
				else {
					float[] variables = { Steering, Throttle, Brake, ClutchInput, Gear };
					return variables;
				}
			}
			else {
				return null;
			}
		}

		/// <summary>
		/// Handle variables sent from the remote client.
		/// </summary>
		public void HandleSyncedVariables(float[] variables) {
			if (variables != null) {
				Steering = variables[0];
				Throttle = variables[1];
				Brake = variables[2];
				ClutchInput = variables[3];
				Gear = (int)variables[4];
				//Fuel = variables[5];
				if (isBike) {
					Quaternion rot = steeringPivot.transform.localRotation;
					rot.z = variables[5];
					steeringPivot.transform.localRotation = rot;
				}
			}
		}

		}

		/// <summary>
		/// Called when owner is set to the remote client.
		/// </summary>
		public void OwnerSetToRemote() {
			SetRemoteSteering(true);
		}

		/// <summary>
		/// Called when owner is removed.
		/// </summary>
		public void OwnerRemoved() {
			SetRemoteSteering(false);
		}

		/// <summary>
		/// Called when an object is constantly syncing. (Usually when a pickupable is picked up, or when a vehicle is being driven)
		/// </summary>
		/// <param name="newValue">If object is being constantly synced.</param>
		public void ConstantSyncChanged(bool newValue) {

		}

		/// <summary>
		/// Find required FSMs in the game object.
		/// </summary>
		public void FindFSMs() {
			PlayMakerFSM[] fsms = ParentGameObject.GetComponentsInChildren<PlayMakerFSM>();

			foreach (var fsm in fsms) {
				if (fsm.FsmName == "PlayerTrigger") {
					PlayerEventHooks(fsm);
				}

				// Starter
				else if (fsm.FsmName == "Starter") {
					starterGameObject = fsm.gameObject;
					starterFsm = fsm;
				}

				// Ignitionn
				else if (fsm.FsmName == "Use" && fsm.gameObject.name == "Ignition") {
					ignitionFsm = fsm;
				}

				// Handbrake for Van, Ferndale, Tractor, Ruscko
				else if (fsm.gameObject.name == "ParkingBrake" && fsm.FsmName == "Use") {
					handbrakeFsm = fsm;
					hasPushParkingBrake = true;
				}

				// Handbrake for Truck
				else if (fsm.gameObject.name == "Parking Brake" && fsm.FsmName == "Use") {
					handbrakeFsm = fsm;
					hasLeverParkingBrake = true;
				}

				// Range selector
				else if (fsm.gameObject.name == "Range" && fsm.FsmName == "Use") {
					rangeFsm = fsm;
					hasRange = true;
				}

				// Fuel tank
				else if (fsm.gameObject.name == "FuelTank" && fsm.FsmName == "Data") {
					fuelTankFsm = fsm;
				}

				// Dashboard
				else if (fsm.gameObject.name == "Ignition" && fsm.FsmName == "Use") {
					dashboardFsm = fsm;
				}

				// Fuel tap
				else if (fsm.gameObject.name == "FuelTap" && fsm.FsmName == "Use") {
					fuelTapFsm = fsm;
				}

				// Lights
				else if (fsm.gameObject.name == "Lights" && fsm.FsmName == "Use" || fsm.gameObject.name == "ButtonLights" && fsm.FsmName == "Use" || fsm.gameObject.name == "knob" && fsm.FsmName == "Use") {
					lightsFsm = fsm;
				}

				// Wipers
				else if (fsm.gameObject.name == "Wipers" && fsm.FsmName == "Use" || fsm.gameObject.name == "ButtonWipers" && fsm.FsmName == "Use") {
					wipersFsm = fsm;
				}

				// Interior light Truck
				else if (fsm.gameObject.name == "ButtonInteriorLight" && fsm.FsmName == "Use") {
					interiorLightFsm = fsm;
				}

				// Interior light Van/Ferndale
				else if (fsm.gameObject.name == "Use" && fsm.FsmName == "Use" && fsm.Fsm.GetState("Flip 2") != null) {
					interiorLightFsm = fsm;
				}

				// Gear indicator - Used to get Range position
				else if (fsm.FsmName == "GearIndicator") {
					gearIndicatorFsm = fsm;
				}

				// Tractor front hydraulic
				else if (fsm.gameObject.name == "FrontHyd" && fsm.FsmName == "Use") {
					frontHydraulicFsm = fsm;
					isTractor = true;
				}

				// Wasp nest
				else if (fsm.gameObject.name == "WaspHive" && fsm.FsmName == "Data") {
					waspNestFsm = fsm;
				}

				// Indicators
				else if (fsm.gameObject.name == "TurnSignals" && fsm.FsmName == "Usage") {
					indicatorsFsm = fsm;
				}

				// Truck specific FSMs

				// Hydraulic pump
				if (fsm.gameObject.name == "Hydraulics" && fsm.FsmName == "Use") {
					hydraulicPumpFsm = fsm;
					isTruck = true;
				}

				// Diff lock
				if (fsm.gameObject.name == "Differential lock" && fsm.FsmName == "Use") {
					diffLockFsm = fsm;
				}

				// Axle lift
				if (fsm.gameObject.name == "Liftaxle" && fsm.FsmName == "Use") {
					axleLiftFsm = fsm;
				}

				// Spill valve
				if (fsm.gameObject.name == "OpenSpill" && fsm.FsmName == "Use") {
					spillValveFsm = fsm;
				}

				// Beacon
				if (fsm.gameObject.name == "KnobBeacon" && fsm.FsmName == "Use") {
					beaconFsm = fsm;
				}
			}

			// Finished finding FSMs, now hook the events.
			HookEvents();
		}

		/// <summary>
		/// Hook vehicle events.
		/// </summary>
		void HookEvents() {
			// Engine states
			string[] ignitionStateNames = { "Wait button", "Motor starting", "Motor OFF", "Test", "Shut off", "ACC on", "ACC on 2" };
			string[] ignitionStateNamesBike = { "Wait button", "Motor starting", "Motor OFF", "Test" };

			if (isBike) {
				foreach (string name in ignitionStateNamesBike) {
					EventHook.AddWithSync(ignitionFsm, name);
				}

				EventHook.AddWithSync(starterFsm, "Wait for start");
				EventHook.AddWithSync(starterFsm, "Start or not");
				EventHook.AddWithSync(starterFsm, "Start or not 2");
				EventHook.AddWithSync(starterFsm, "Start engine");
			}
			else {
				foreach (string name in ignitionStateNames) {
					EventHook.AddWithSync(ignitionFsm, name);
				}
			}


			// Dashboard states
			string[] dashboardStateNames = { "ACC on", "Test", "ACC on 2", "Motor starting", "Shut off", "Motor OFF", "Wait button", "Wait player" };

			foreach (string name in dashboardStateNames) {
				EventHook.Add(dashboardFsm, name, new Func<bool>(() => {
					return false;
				}));
			}

			// Range
			if (hasRange) {
				if (isTruck) {
					EventHook.AddWithSync(rangeFsm, "Switch");
				}
				else if (isTractor) {
					EventHook.AddWithSync(rangeFsm, "Flip");
				}
			}

			// Push parking brake
			if (hasPushParkingBrake) {
				EventHook.Add(handbrakeFsm, "DECREASE", new Func<bool>(() => {
					WriteVehicleSwitchMessage(syncComponent, SwitchIDs.HandbrakePull, false, this.handbrakeFsm.Fsm.GetFsmFloat("KnobPos").Value);
					return false;
				}), actionOnExit: true);
				EventHook.Add(handbrakeFsm, "INCREASE", new Func<bool>(() => {
					WriteVehicleSwitchMessage(syncComponent, SwitchIDs.HandbrakePull, false, this.handbrakeFsm.Fsm.GetFsmFloat("KnobPos").Value);
					return false;
				}), actionOnExit: true);
			}

			// Truck parking brake
			if (hasLeverParkingBrake) {
				EventHook.Add(handbrakeFsm, "Flip", new Func<bool>(() => {
					WriteVehicleSwitchMessage(syncComponent, SwitchIDs.HandbrakeLever, !this.handbrakeFsm.Fsm.GetFsmBool("Brake").Value, -1);
					return false;
				}));
			}

			// Fuel tap
			if (fuelTapFsm != null) {
				EventHook.Add(fuelTapFsm, "Test", new Func<bool>(() => {
					WriteVehicleSwitchMessage(syncComponent, SwitchIDs.FuelTap, !this.fuelTapFsm.Fsm.GetFsmBool("FuelOn").Value, -1);
					return false;
				}));
			}

			// Lights
			if (lightsFsm != null) {
				EventHook.AddWithSync(lightsFsm, "Off");
				EventHook.AddWithSync(lightsFsm, "Shorts");
				if (!isBike) {
					EventHook.AddWithSync(lightsFsm, "Longs");
				}
			}

			// Indicators
			if (indicatorsFsm != null) {
				EventHook.AddWithSync(indicatorsFsm, "Activate dash");
				EventHook.AddWithSync(indicatorsFsm, "Activate dash 2");
				EventHook.AddWithSync(indicatorsFsm, "On", action: new Func<bool>(() => {
					if (this.DriverIsLocal == false) {
						return true;
					}
					return false;
				}));
				EventHook.AddWithSync(indicatorsFsm, "On 2", action: new Func<bool>(() => {
					if (this.DriverIsLocal == false) {
						return true;
					}
					return false;
				}));
				EventHook.AddWithSync(indicatorsFsm, "Off", action: new Func<bool>(() => {
					if (this.DriverIsLocal == false) {
						return true;
					}
					return false;
				}));
				EventHook.AddWithSync(indicatorsFsm, "Off 2", action: new Func<bool>(() => {
					if (this.DriverIsLocal == false) {
						return true;
					}
					return false;
				}));

				EventHook.AddWithSync(indicatorsFsm, "State 3", action: new Func<bool>(() => {
					if (this.DriverIsLocal == false) {
						GameObject left;
						GameObject right;
						left = this.gameObject.transform.FindChild("LOD/Electricity/PowerON/Blinkers/Left").gameObject;
						right = this.gameObject.transform.FindChild("LOD/Electricity/PowerON/Blinkers/Right").gameObject;
						// Ferndale has a different hierarchy. Why not, right?
						if (left == null) {
							left = this.gameObject.transform.FindChild("LOD/Electricity 1/PowerON/Blinkers/Left").gameObject;
							right = this.gameObject.transform.FindChild("LOD/Electricity 1/PowerON/Blinkers/Right").gameObject;
						}
						left.SetActive(false);
						right.SetActive(false);
					}
					return false;
				}));
			}

			// Wipers
			if (wipersFsm != null) {
				EventHook.Add(wipersFsm, "Test 2", new Func<bool>(() => {
					int selection = this.wipersFsm.Fsm.GetFsmInt("Selection").Value;
					if (selection == 2) {
						selection = 0;
					}
					else {
						selection++;
					}

					WriteVehicleSwitchMessage(syncComponent, SwitchIDs.Wipers, false, selection);
					return false;
				}));
			}

			// Interior light
			if (interiorLightFsm != null) {
				if (isTruck) {
					EventHook.Add(interiorLightFsm, "Switch", new Func<bool>(() => {
						WriteVehicleSwitchMessage(syncComponent, SwitchIDs.InteriorLight, !this.interiorLightFsm.Fsm.GetFsmBool("On").Value, -1);
						return false;
					}));
				}
				else {
					EventHook.Add(interiorLightFsm, "Flip 2", new Func<bool>(() => {
						WriteVehicleSwitchMessage(syncComponent, SwitchIDs.InteriorLight, !this.interiorLightFsm.Fsm.GetFsmBool("LightON").Value, -1);
						return false;
					}));
				}
			}

			// Truck related events
			if (isTruck) {
				// Hydraulic pump
				EventHook.Add(hydraulicPumpFsm, "Test", new Func<bool>(() => {
					if (this.hydraulicPumpFirstRun == false) {
						WriteVehicleSwitchMessage(syncComponent, SwitchIDs.HydraulicPump, !this.hydraulicPumpFsm.Fsm.GetFsmBool("On").Value, -1);
					}
					else {
						this.hydraulicPumpFirstRun = false;
					}
					return false;
				}));

				// Spill valve
				EventHook.Add(spillValveFsm, "Switch", new Func<bool>(() => {
					WriteVehicleSwitchMessage(syncComponent, SwitchIDs.SpillValve, !this.spillValveFsm.Fsm.GetFsmBool("Open").Value, -1);
					return false;
				}));

				// Axle lift
				EventHook.Add(axleLiftFsm, "Test", new Func<bool>(() => {
					if (this.axleLiftFirstRun == false) {
						WriteVehicleSwitchMessage(syncComponent, SwitchIDs.AxleLift, !this.axleLiftFsm.Fsm.GetFsmBool("Up").Value, -1);
					}
					else {
						this.axleLiftFirstRun = false;
					}
					return false;
				}));

				// Diff lock
				EventHook.Add(diffLockFsm, "Test", new Func<bool>(() => {
					if (this.diffLockFirstRun == false) {
						WriteVehicleSwitchMessage(syncComponent, SwitchIDs.DiffLock, !this.diffLockFsm.Fsm.GetFsmBool("Lock").Value, -1);
					}
					else {
						this.diffLockFirstRun = false;
					}
					return false;
				}));

				// Beacon
				EventHook.AddWithSync(beaconFsm, "ON");
				EventHook.AddWithSync(beaconFsm, "OFF");
			}

			// Wasp nest
			if (waspNestFsm != null) {
				EventHook.AddWithSync(waspNestFsm, "State 2");
			}

			// Sync vehicle data with the host on spawn.
			if (Network.NetManager.Instance.IsOnline && !Network.NetManager.Instance.IsHost) {
				syncComponent.RequestObjectSync();
			}
		}

		/// <summary>
		/// Hook player trigger events.
		/// </summary>
		/// <param name="fsm">Player trigger FSM on vehicle drivers seat.</param>
		void PlayerEventHooks(PlayMakerFSM fsm) {
			// Temp - use player trigger. (No idea what this comment meant, it's now many months later. :P)
			// It's now 6+ months later, no idea what this means at all now. :p -Curtis
			EventHook.Add(fsm, "Player in car", new Func<bool>(() => {
				if (CurrentDrivingState == DrivingStates.Driver && !DriverIsLocal) {
					return true;
				}
				else {
					CurrentDrivingState = DrivingStates.Driver;
					syncComponent.TakeSyncControl();
					DriverIsLocal = true;
					SetRemoteSteering(false);
					Network.NetLocalPlayer.Instance.EnterVehicle(syncComponent, false);
					return false;
				}
			}));
			EventHook.Add(fsm, "Wait for player", new Func<bool>(() => {
				if (CurrentDrivingState == DrivingStates.Driver && DriverIsLocal) {
					CurrentDrivingState = DrivingStates.None;
					DriverIsLocal = false;
					syncComponent.SendConstantSync(false);
					NetLocalPlayer.Instance.LeaveVehicle();
				}
				return false;
			}));
			if (isBike) {
				EventHook.Add(fsm, "Press return", new Func<bool>(() => {
					syncComponent.TakeSyncControl();
					syncComponent.SendConstantSync(true);
					return false;
				}));
			}
			SeatTransform = fsm.gameObject.transform;

			if (SeatTransform.gameObject.name == "DriveTrigger" && !isBike && !ParentGameObject.name.StartsWith("KEKMET")) {
				AddPassengerSeat(fsm);
			}
		}

		/// <summary>
		/// Set value of switches within a vehicle.
		/// </summary>
		/// <param name="state">Switch to change.</param>
		/// <param name="newValue">New value as a bool.</param>
		/// <param name="newValueFloat">New value as a float.</param>
		public void SetVehicleSwitch(SwitchIDs state, bool newValue, float newValueFloat) {
			switch (state) {
				// Parking brake
				case SwitchIDs.HandbrakePull:
					handbrakeFsm.Fsm.GetFsmFloat("KnobPos").Value = newValueFloat;
					break;

				// Truck parking brake
				case SwitchIDs.HandbrakeLever:
					if (handbrakeFsm.Fsm.GetFsmBool("Brake").Value != newValue) {
						handbrakeFsm.SendEvent("MP_Flip");
					}
					break;

				// Fuel tap
				case SwitchIDs.FuelTap:
					if (fuelTapFsm.Fsm.GetFsmBool("FuelOn").Value != newValue) {
						fuelTapFsm.SendEvent("MP_Test");
					}
					break;

				// Lights
				case SwitchIDs.Lights:
					while (lightsFsm.Fsm.GetFsmInt("Selection").Value != newValueFloat) {
						lightsFsm.SendEvent("MP_Test");
					}
					break;

				// Wipers
				case SwitchIDs.Wipers:
					if (wipersFsm.Fsm.GetFsmInt("Selection").Value != newValueFloat) {
						wipersFsm.SendEvent("MP_Test 2");
					}
					break;
				
				// Interior light
				case SwitchIDs.InteriorLight:
					if (isTruck) {
						if (interiorLightFsm.Fsm.GetFsmBool("On").Value != newValue) {
							interiorLightFsm.SendEvent("MP_Switch");
						}
					}
					else {
						if (interiorLightFsm.Fsm.GetFsmBool("LightON").Value != newValue) {
							interiorLightFsm.SendEvent("MP_Flip 2");
						}
					}
					break;
				
				// Hydraulic pump
				case SwitchIDs.HydraulicPump:
					if (hydraulicPumpFsm.Fsm.GetFsmBool("On").Value != newValue) {
						hydraulicPumpFsm.SendEvent("MP_Test");
					}
					break;

				// Spill valve
				case SwitchIDs.SpillValve:
					if (spillValveFsm.Fsm.GetFsmBool("Open").Value != newValue) {
						spillValveFsm.SendEvent("MP_Switch");
					}
					break;

				// Axle lift
				case SwitchIDs.AxleLift:
					if (axleLiftFsm.Fsm.GetFsmBool("Up").Value != newValue) {
						axleLiftFsm.SendEvent("MP_Test");
					}
					break;

				// Diff lock
				case SwitchIDs.DiffLock:
					if (diffLockFsm.Fsm.GetFsmBool("Lock").Value != newValue) {
						diffLockFsm.SendEvent("MP_Test");
					}
					break;
			}
		}

		/// <summary>
		/// Write vehicle switch changes into vehicle switch message.
		/// </summary>
		/// <param name="state">The engine state to write.</param>
		public void WriteVehicleSwitchMessage(ObjectSyncComponent vehicle, PlayerVehicle.SwitchIDs switchID, bool newValue, float newValueFloat) {
			Network.Messages.VehicleSwitchMessage msg = new Network.Messages.VehicleSwitchMessage();
			msg.objectID = vehicle.ObjectID;
			msg.switchID = (int)switchID;
			msg.switchValue = newValue;
			if (newValueFloat != -1) {
				msg.SwitchValueFloat = newValueFloat;
			}
			NetManager.Instance.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
		}
	}
}
