using HutongGames.PlayMaker;
using UnityEngine;
using System;

namespace MSCMP.Game.Objects {
	/// <summary>
	/// Representation of game vehicle.
	/// </summary>
	class GameVehicle {

		GameObject gameObject = null;

		CarDynamics dynamics = null;
		Drivetrain driveTrain = null;

		bool isDriver = false;

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

		AxisCarController axisCarController = null;
		MPCarController mpCarController = null;

		public delegate void OnEnter(bool passenger);
		public delegate void OnLeave();
		public delegate void OnEngineStateChanged(EngineStates state, DashboardStates dashstate, float startTime);
		public delegate void OnVehicleSwitchChanged(SwitchIDs id, bool newValue, float newValueFloat);
		public OnEnter onEnter = (bool passenger) => {
			Logger.Log("On Enter");
		};
		public OnLeave onLeave = () => {
			Logger.Log("On Leave");
		};
		public OnEngineStateChanged onEngineStateChanged = (EngineStates state, DashboardStates dashstate, float startTime) => {
			Logger.Debug($"Engine state changed to: {state.ToString()}");
		};
		public OnVehicleSwitchChanged onVehicleSwitchChanges = (SwitchIDs id, bool newValue, float newValueFloat) => {
			Logger.Debug($"Switch {id.ToString()} changed to: {newValue} (Float: {newValueFloat})");
		};

		public string Name {
			get {
				return gameObject != null ? gameObject.name : "";
			}
		}

		public Transform VehicleTransform {
			get {
				return gameObject.transform;
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

		public bool StartEngineInput {
			get {
				return dynamics.carController.startEngineInput;
			}
			set {
				mpCarController.startEngineInput = value;
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

		public bool Range {
			get {
				return gearIndicatorFsm.Fsm.GetFsmBool("Range").Value;
			}
			set {
				if (hasRange == true) {
					rangeFsm.SendEvent(MP_RANGE_SWITCH_EVENT_NAME);
				}
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

		public float FrontHydraulic {
			get {
				return frontHydraulicFsm.Fsm.GetFsmFloat("LeverPos").Value;
			}
			set {
				frontHydraulicFsm.Fsm.GetFsmFloat("LeverPos").Value = value;
			}
		}


		GameObject seatGameObject = null;
		GameObject pSeatGameObject = null;
		GameObject starterGameObject = null;

		// General
		PlayMakerFSM starterFsm = null;
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

		// Truck specific
		PlayMakerFSM hydraulicPumpFsm = null;
		PlayMakerFSM diffLockFsm = null;
		PlayMakerFSM axleLiftFsm = null;
		PlayMakerFSM spillValveFsm = null;

		// Misc
		PlayMakerFSM waspNestFsm = null;

		public bool hasRange = false;
		bool hasLeverParkingBrake = false;
		bool hasPushParkingBrake = false;

		bool isTruck = false;
		public bool isTractor = false;

		bool hydraulicPumpFirstRun = true;
		bool diffLockFirstRun = true;
		bool axleLiftFirstRun = true;

		public Transform SeatTransform {
			get {
				return seatGameObject.transform;
			}
		}

		public Transform PassengerSeatTransform {
			get {
				return pSeatGameObject.transform;
			}
		}

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

		// Engine
		string MP_WAIT_FOR_START_EVENT_NAME = "MPWAITFORSTART";
		string MP_ACC_EVENT_NAME = "MPACC";
		string MP_TURN_KEY_EVENT_NAME = "MPTURNKEY";
		string MP_CHECK_CLUTCH_EVENT_NAME = "MPCHECKCLUTCH";
		string MP_STARTING_ENGINE_EVENT_NAME = "MPSTARTINGENGINE";
		string MP_START_ENGINE_EVENT_NAME = "MPSTARTENGINE";
		string MP_START_OR_NOT_EVENT_NAME = "MPSTARTORNOT";
		string MP_MOTOR_RUNNING_EVENT_NAME = "MPMOTORRUNNING";
		string MP_WAIT_EVENT_NAME = "MPWAIT";
		string MP_GLOWPLUG_EVENT_NAME = "MPGLOWPLUG";

		// Interior
		string MP_PBRAKE_INCREASE_EVENT_NAME = "MPPBRAKEINCREASE";
		string MP_PBRAKE_DECREASE_EVENT_NAME = "MPPBRAKEDECREASE";
		string MP_TRUCK_PBRAKE_FLIP_EVENT_NAME = "MPFLIPBRAKE";
		string MP_LIGHTS_EVENT_NAME = "MPLIGHTS";
		string MP_LIGHTS_SWITCH_EVENT_NAME = "MPLIGHTSSWITCH";
		string MP_WIPERS_EVENT_NAME = "MPWIPERS";
		string MP_HYDRAULIC_PUMP_EVENT_NAME = "MPHYDRAULICPUMP";
		string MP_AXLE_LIFT_EVENT_NAME = "MPAXLELIFT";
		string MP_INTERIOR_LIGHT_EVENT_NAME = "MPINTERIORLIGHT";
		string MP_DIFF_LOCK_EVENT_NAME = "MPDIFFLOCK";

		// Dashboard
		string MP_ACC_ON_EVENT_NAME = "MPACCON";
		string MP_TEST_EVENT_NAME = "MPTEST";
		string MP_ACC_ON_2_EVENT_NAME = "MPACCON2";
		string MP_MOTOR_STARTING_EVENT_NAME = "MPMOTORSTARTING";
		string MP_SHUT_OFF_EVENT_NAME = "MPSHUTOFF";
		string MP_MOTOR_OFF_EVENT_NAME = "MPMOTOROFF";
		string MP_WAIT_BUTTON_EVENT_NAME = "MPWAITBUTTON";
		string MP_WAIT_PLAYER_EVENT_NAME = "MPWAITPLAYER";

		// Misc
		string MP_RANGE_SWITCH_EVENT_NAME = "MPRANGE";
		string MP_FUEL_TAP_EVENT_NAME = "MPFUELTAP";
		string MP_SPILL_VALVE_EVENT_NAME = "MPSPILLVALVE";
		string MP_DESTROY_WASP_NEST_EVENT_NAME = "MPDESTROYNEST";

		/// <summary>
		/// PlayMaker state action executed when local player enters vehicle.
		/// </summary>
		private class OnEnterAction : FsmStateAction {
			private GameVehicle vehicle;

			public OnEnterAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				Utils.CallSafe("OnEnterHandler", () => {
					if (Fsm.PreviousActiveState != null && Fsm.PreviousActiveState.Name == "Death") {
						if (vehicle.onEnter != null) {
							vehicle.onEnter(false);
							vehicle.isDriver = true;

							if (vehicle.driveTrain != null) {
								vehicle.driveTrain.canStall = false;
							}
						}
					}
				});
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when local player leaves vehicle.
		/// </summary>
		private class OnLeaveAction : FsmStateAction {
			private GameVehicle vehicle;

			public OnLeaveAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				Utils.CallSafe("OnLeaveHandler", () => {
					if (Fsm.PreviousActiveState != null && Fsm.PreviousActiveState.Name == "Create player") {
						if (vehicle.onLeave != null) {
							vehicle.onLeave();
							vehicle.isDriver = false;

							if (vehicle.driveTrain != null) {
								vehicle.driveTrain.canStall = false;
							}
						}
					}
				});
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when vehicle enters Wait for start state.
		/// </summary>
		private class onWaitForStartAction : FsmStateAction {
			private GameVehicle vehicle;

			public onWaitForStartAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				//LastTransition is null on new vehicle spawn
				if (State.Fsm.LastTransition != null) {
					if (State.Fsm.LastTransition.EventName == vehicle.MP_WAIT_FOR_START_EVENT_NAME || vehicle.isDriver == false) {
						return;
					}
				}

				vehicle.onEngineStateChanged(EngineStates.WaitForStart, DashboardStates.MotorOff, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when vehicle enters ACC state.
		/// </summary>
		private class onACCAction : FsmStateAction {
			private GameVehicle vehicle;

			public onACCAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_ACC_EVENT_NAME || vehicle.isDriver == false) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.ACC, DashboardStates.Test, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when vehicle enters Turn key state.
		/// </summary>
		private class onTurnKeyAction : FsmStateAction {
			private GameVehicle vehicle;

			public onTurnKeyAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_TURN_KEY_EVENT_NAME || vehicle.isDriver == false) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.TurnKey, DashboardStates.ACCon2, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when vehicle enters Check clutch engine state.
		/// </summary>
		private class onCheckClutchAction : FsmStateAction {
			private GameVehicle vehicle;

			public onCheckClutchAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_CHECK_CLUTCH_EVENT_NAME || vehicle.isDriver == false) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.CheckClutch, DashboardStates.Null, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when vehicle enters Starting engine state.
		/// </summary>
		private class onStartingEngineAction : FsmStateAction {
			private GameVehicle vehicle;
			float startTime = 0;

			public onStartingEngineAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_STARTING_ENGINE_EVENT_NAME || vehicle.isDriver == false) {
					return;
				}

				startTime = vehicle.starterFsm.Fsm.GetFsmFloat("StartTime").Value;

				vehicle.onEngineStateChanged(EngineStates.StartingEngine, DashboardStates.MotorStarting, startTime);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when vehicle enters Start engine state.
		/// </summary>
		private class onStartEngineAction : FsmStateAction {
			private GameVehicle vehicle;

			public onStartEngineAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_START_ENGINE_EVENT_NAME || vehicle.isDriver == false) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.StartEngine, DashboardStates.Null, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when vehicle enters Motor running engine state.
		/// </summary>
		private class onMotorRunningAction : FsmStateAction {
			private GameVehicle vehicle;

			public onMotorRunningAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_MOTOR_RUNNING_EVENT_NAME || vehicle.isDriver == false) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.MotorRunning, DashboardStates.WaitPlayer, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when vehicle enters Wait engine state.
		/// </summary>
		private class onWaitAction : FsmStateAction {
			private GameVehicle vehicle;

			public onWaitAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_WAIT_EVENT_NAME || vehicle.isDriver == false) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.Wait, DashboardStates.Null, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when vehicle enters Start or not engine state.
		/// </summary>
		private class onAccGlowplugAction : FsmStateAction {
			private GameVehicle vehicle;

			public onAccGlowplugAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_GLOWPLUG_EVENT_NAME || vehicle.isDriver == false) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.Glowplug, DashboardStates.Null, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when vehicle enters Wait engine state.
		/// </summary>
		private class onStartOrNotAction : FsmStateAction {
			private GameVehicle vehicle;

			public onStartOrNotAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_START_OR_NOT_EVENT_NAME || vehicle.isDriver == false) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.StartOrNot, DashboardStates.Null, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when parking brake is pulled.
		/// </summary>
		private class onParkingBrakeIncreaseAction : FsmStateAction {
			private GameVehicle vehicle;

			public onParkingBrakeIncreaseAction(GameVehicle veh) {
				vehicle = veh;
			}

			// Called on exit so latest value is sent
			public override void OnExit() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_PBRAKE_INCREASE_EVENT_NAME) {
					return;
				}

				vehicle.onVehicleSwitchChanges(SwitchIDs.HandbrakePull, false, vehicle.handbrakeFsm.Fsm.GetFsmFloat("KnobPos").Value);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when parking brake is pushed.
		/// </summary>
		private class onParkingBrakeDecreaseAction : FsmStateAction {
			private GameVehicle vehicle;

			public onParkingBrakeDecreaseAction(GameVehicle veh) {
				vehicle = veh;
			}

			// Called on exit so latest value is sent
			public override void OnExit() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_PBRAKE_DECREASE_EVENT_NAME) {
					return;
				}

				vehicle.onVehicleSwitchChanges(SwitchIDs.HandbrakePull, false, vehicle.handbrakeFsm.Fsm.GetFsmFloat("KnobPos").Value);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when truck parking brake is used.
		/// </summary>
		private class onTruckPBrakeFlipAction : FsmStateAction {
			private GameVehicle vehicle;

			public onTruckPBrakeFlipAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_TRUCK_PBRAKE_FLIP_EVENT_NAME) {
					return;
				}

				vehicle.onVehicleSwitchChanges(SwitchIDs.HandbrakeLever, !vehicle.handbrakeFsm.Fsm.GetFsmBool("Brake").Value, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when bike fuel tap is used.
		/// </summary>
		private class onFuelTapUsedAction : FsmStateAction {
			private GameVehicle vehicle;

			public onFuelTapUsedAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_FUEL_TAP_EVENT_NAME) {
					return;
				}

				vehicle.onVehicleSwitchChanges(SwitchIDs.FuelTap, !vehicle.fuelTapFsm.Fsm.GetFsmBool("FuelOn").Value, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when lights in vehicles are used.
		/// </summary>
		private class onLightsUsedAction : FsmStateAction {
			private GameVehicle vehicle;

			public onLightsUsedAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_LIGHTS_EVENT_NAME) {
					return;
				}

				if (State.Fsm.LastTransition.EventName == "FINISHED") {
					if (vehicle.isDriver == false) {
						return;
					}
				}

				vehicle.onVehicleSwitchChanges(SwitchIDs.Lights, false, vehicle.lightsFsm.Fsm.GetFsmInt("Selection").Value);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when wipers are used.
		/// </summary>
		private class onWipersUsedAction : FsmStateAction {
			private GameVehicle vehicle;

			public onWipersUsedAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_WIPERS_EVENT_NAME) {
					return;
				}

				int selection = vehicle.wipersFsm.Fsm.GetFsmInt("Selection").Value;
				if (selection == 2) {
					selection = 0;
				}
				else {
					selection++;
				}

				vehicle.onVehicleSwitchChanges(SwitchIDs.Wipers, false, selection);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when interior light is used
		/// </summary>
		private class onInteriorLightUsedAction : FsmStateAction {
			private GameVehicle vehicle;

			public onInteriorLightUsedAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_INTERIOR_LIGHT_EVENT_NAME) {
					return;
				}

				vehicle.onVehicleSwitchChanges(SwitchIDs.InteriorLight, !vehicle.interiorLightFsm.Fsm.GetFsmBool("On").Value, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when hydraulic pump is used.
		/// </summary>
		private class onHydraulicPumpUsedAction : FsmStateAction {
			private GameVehicle vehicle;

			public onHydraulicPumpUsedAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (vehicle.hydraulicPumpFirstRun == false) {
					if (State.Fsm.LastTransition.EventName == vehicle.MP_HYDRAULIC_PUMP_EVENT_NAME) {
						return;
					}

					vehicle.onVehicleSwitchChanges(SwitchIDs.HydraulicPump, !vehicle.hydraulicPumpFsm.Fsm.GetFsmBool("On").Value, -1);
				}
				else {
					vehicle.hydraulicPumpFirstRun = false;
				}
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when spill valve is used
		/// </summary>
		private class onSpillValveUsedAction : FsmStateAction {
			private GameVehicle vehicle;

			public onSpillValveUsedAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_SPILL_VALVE_EVENT_NAME) {
					return;
				}

				vehicle.onVehicleSwitchChanges(SwitchIDs.SpillValve, !vehicle.spillValveFsm.Fsm.GetFsmBool("Open").Value, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when axle lift is used
		/// </summary>
		private class onAxleLiftUsedAction : FsmStateAction {
			private GameVehicle vehicle;

			public onAxleLiftUsedAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (vehicle.axleLiftFirstRun == false) {
					if (State.Fsm.LastTransition.EventName == vehicle.MP_AXLE_LIFT_EVENT_NAME) {
						return;
					}

					vehicle.onVehicleSwitchChanges(SwitchIDs.AxleLift, !vehicle.axleLiftFsm.Fsm.GetFsmBool("Up").Value, -1);
				}
				else {
					vehicle.axleLiftFirstRun = false;
				}
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when diff lock is used.
		/// </summary>
		private class onDiffLockUsedAction : FsmStateAction {
			private GameVehicle vehicle;

			public onDiffLockUsedAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (vehicle.diffLockFirstRun == false) {
					if (State.Fsm.LastTransition.EventName == vehicle.MP_DIFF_LOCK_EVENT_NAME) {
						return;
					}

					vehicle.onVehicleSwitchChanges(SwitchIDs.DiffLock, !vehicle.diffLockFsm.Fsm.GetFsmBool("Lock").Value, -1);
				}
				else {
					vehicle.diffLockFirstRun = false;
				}
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when wasp next is destroyed.
		/// </summary>
		private class onWaspNestDestroyedAction : FsmStateAction {
			private GameVehicle vehicle;

			public onWaspNestDestroyedAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_DESTROY_WASP_NEST_EVENT_NAME) {
					return;
				}

				vehicle.onVehicleSwitchChanges(SwitchIDs.DestroyWaspNest, false, -1);
				Logger.Debug("Wasp nest destroyed!");

				Finish();
			}
		}


		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="go">Vehicle game object.</param>
		public GameVehicle(GameObject go) {
			gameObject = go;

			dynamics = gameObject.GetComponent<CarDynamics>();
			Client.Assert(dynamics != null, "Missing car dynamics!");

			driveTrain = gameObject.GetComponent<Drivetrain>();

			if (driveTrain != null) {
				driveTrain.canStall = false;
			}

			axisCarController = gameObject.GetComponent<AxisCarController>();
			mpCarController = gameObject.AddComponent<MPCarController>();

			// Used for creating truck-specific events
			if (go.name.StartsWith("GIFU")) {
				isTruck = true;
			}

			PlayMakerFSM[] fsms = gameObject.GetComponentsInChildren<PlayMakerFSM>();

			foreach (var fsm in fsms) {
				if (fsm.FsmName == "PlayerTrigger") {
					SetupPlayerTriggerHooks(fsm);

					// Temp - use player trigger. (No idea what this comment meant, it's now many months later. :P)
					seatGameObject = fsm.gameObject;

					// Add Passenger seat.
					if (seatGameObject.name == "DriveTrigger" && !gameObject.name.StartsWith("JONNEZ") && !gameObject.name.StartsWith("KEKMET")) {
						GameObject passengerSeat = GameObject.CreatePrimitive(PrimitiveType.Cube);
						pSeatGameObject = passengerSeat;

						passengerSeat.transform.parent = fsm.gameObject.transform.parent;
						passengerSeat.transform.position = passengerSeat.transform.parent.position;
						passengerSeat.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);

						passengerSeat.transform.GetComponent<BoxCollider>().isTrigger = true;

						PassengerSeat pSeatScript = passengerSeat.AddComponent(typeof(PassengerSeat)) as PassengerSeat;
						pSeatScript.VehicleType = gameObject.name;
						pSeatScript.DriversSeat = fsm.gameObject;

						pSeatScript.onEnter = () => {
							this.onEnter(true);
						};
						pSeatScript.onLeave = () => {
							this.onLeave();
						};
					}
				}

				// Starter
				else if (fsm.FsmName == "Starter") {
					starterGameObject = fsm.gameObject;
					starterFsm = fsm;
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

				// Interior light
				else if (fsm.gameObject.name == "ButtonInteriorLight" && fsm.FsmName == "Use") {
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

				// Truck specific FSMs
					if (isTruck == true) {

					// Hydraulic pump
					if (fsm.gameObject.name == "Hydraulics" && fsm.FsmName == "Use") {
						hydraulicPumpFsm = fsm;
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
				}
			}

			if (starterFsm != null && dashboardFsm != null) {
				SetupVehicleHooks();
			}
		}

		public void SetRemoteSteering(bool enabled) {
			axisCarController.enabled = !enabled;
			mpCarController.enabled = enabled;
		}

		/// <summary>
		/// Setup player trigger related hooks.
		/// </summary>
		/// <param name="fsm">The fsm to hook.</param>
		private void SetupPlayerTriggerHooks(PlayMakerFSM fsm) {
			FsmState playerInCarState = fsm.Fsm.GetState("Player in car");
			FsmState waitForPlayerState = fsm.Fsm.GetState("Wait for player");

			if (waitForPlayerState != null) {
				PlayMakerUtils.AddNewAction(waitForPlayerState, new OnLeaveAction(this));
			}

			if (playerInCarState != null) {
				PlayMakerUtils.AddNewAction(playerInCarState, new OnEnterAction(this));
			}
		}

		/// <summary>
		/// Setup vehicle event hooks.
		/// </summary>
		private void SetupVehicleHooks() {
			FsmState waitForStartState = starterFsm.Fsm.GetState("Wait for start");
			FsmState accState = starterFsm.Fsm.GetState("ACC");
			FsmState turnKeyState = starterFsm.Fsm.GetState("Turn key");
			FsmState checkClutchState = starterFsm.Fsm.GetState("Check clutch");
			FsmState startingEngineState = starterFsm.Fsm.GetState("Starting engine");
			FsmState startEngineState = starterFsm.Fsm.GetState("Start engine");
			FsmState waitState = starterFsm.Fsm.GetState("Wait");
			FsmState startOrNotState = starterFsm.Fsm.GetState("Start or not");
			FsmState motorRunningState = starterFsm.Fsm.GetState("Motor running");
			FsmState accGlowplugState = starterFsm.Fsm.GetState("ACC / Glowplug");

			FsmState accOnState = dashboardFsm.Fsm.GetState("ACC on");
			FsmState testState = dashboardFsm.Fsm.GetState("Test");
			FsmState accOn2State = dashboardFsm.Fsm.GetState("ACC on 2");
			FsmState motorStartingState = dashboardFsm.Fsm.GetState("Motor starting");
			FsmState shutOffState = dashboardFsm.Fsm.GetState("Shut off");
			FsmState motorOffState = dashboardFsm.Fsm.GetState("Motor OFF");
			FsmState waitButtonState = dashboardFsm.Fsm.GetState("Wait button");
			FsmState waitPlayerState = dashboardFsm.Fsm.GetState("Wait player");

			FsmState pBrakeIncreaseState = null;
			FsmState pBrakeDecreaseState = null;
			if (hasPushParkingBrake == true) {
				pBrakeIncreaseState = handbrakeFsm.Fsm.GetState("INCREASE");
				pBrakeDecreaseState = handbrakeFsm.Fsm.GetState("DECREASE");
			}

			FsmState truckPBrakeFlipState = null;
			if (hasLeverParkingBrake == true) {
				truckPBrakeFlipState = handbrakeFsm.Fsm.GetState("Flip");
			}

			FsmState rangeSwitchState = null;
			if (hasRange == true) {
				if (isTruck == true) {
					rangeSwitchState = rangeFsm.Fsm.GetState("Switch");
				}
				else if (isTractor == true) {
					rangeSwitchState = rangeFsm.Fsm.GetState("Flip");
				}
			}

			FsmState fuelTapState = null;
			if (fuelTapFsm != null) {
				fuelTapState = fuelTapFsm.Fsm.GetState("Test");
			}

			FsmState lightsState = null;
			if (lightsFsm != null) {
				if (isTruck == true) {
					lightsState = lightsFsm.Fsm.GetState("Sound 2");
				}
				else {
					lightsState = lightsFsm.Fsm.GetState("Sound");
				}
			}

			FsmState wipersState = null;
			if (wipersFsm != null) {
				wipersState = wipersFsm.Fsm.GetState("Test 2");
			}

			FsmState interiorLightState = null;
			if (interiorLightFsm != null) {
				interiorLightState = interiorLightFsm.Fsm.GetState("Switch");
			}

			FsmState hydraulicPumpState = null;
			FsmState diffLockState = null;
			FsmState axleLiftState = null;
			FsmState spillValveState = null;
			if (isTruck == true) {
				hydraulicPumpState = hydraulicPumpFsm.Fsm.GetState("Test");
				diffLockState = diffLockFsm.Fsm.GetState("Test");
				axleLiftState = axleLiftFsm.Fsm.GetState("Test");
				spillValveState = spillValveFsm.Fsm.GetState("Switch");
			}

			FsmState waspNestDestroyState = null;
			if (waspNestFsm != null) {
				waspNestDestroyState = waspNestFsm.Fsm.GetState("State 2");
			}

			//Engine states
			if (waitForStartState != null) {
				PlayMakerUtils.AddNewAction(waitForStartState, new onWaitForStartAction(this));
				FsmEvent mpWaitForStartEvent = starterFsm.Fsm.GetEvent(MP_WAIT_FOR_START_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(starterFsm, mpWaitForStartEvent, "Wait for start");
			}

			if (accState != null) {
				EventHook.Add(starterFsm, "ACC", new Action(() => {
					Logger.Log($"Boop, this is a test from {this.gameObject.transform.name}!");

					if (starterFsm.Fsm.LastTransition.EventName == "MP_ACC" || this.isDriver == false) {
						return;
					}
					this.onEngineStateChanged(EngineStates.ACC, DashboardStates.Test, -1);
				}));
			}

			if (turnKeyState != null) {
				PlayMakerUtils.AddNewAction(turnKeyState, new onTurnKeyAction(this));
				FsmEvent mpTurnKeyEvent = starterFsm.Fsm.GetEvent(MP_TURN_KEY_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(starterFsm, mpTurnKeyEvent, "Turn key");
			}

			if (checkClutchState != null) {
				PlayMakerUtils.AddNewAction(checkClutchState, new onCheckClutchAction(this));
				FsmEvent mpCheckClutchState = starterFsm.Fsm.GetEvent(MP_CHECK_CLUTCH_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(starterFsm, mpCheckClutchState, "Check clutch");
			}

			if (startingEngineState != null) {
				PlayMakerUtils.AddNewAction(startingEngineState, new onStartingEngineAction(this));
				FsmEvent mpStartingEngineState = starterFsm.Fsm.GetEvent(MP_STARTING_ENGINE_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(starterFsm, mpStartingEngineState, "Starting engine");
			}

			if (startEngineState != null) {
				PlayMakerUtils.AddNewAction(startEngineState, new onStartEngineAction(this));
				FsmEvent mpStartEngineState = starterFsm.Fsm.GetEvent(MP_START_ENGINE_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(starterFsm, mpStartEngineState, "Start engine");
			}

			if (waitState != null) {
				PlayMakerUtils.AddNewAction(waitState, new onWaitAction(this));
				FsmEvent mpWaitState = starterFsm.Fsm.GetEvent(MP_WAIT_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(starterFsm, mpWaitState, "Wait");
			}

			if (startOrNotState != null) {
				PlayMakerUtils.AddNewAction(startOrNotState, new onStartOrNotAction(this));
				FsmEvent mpStartOrNotState = starterFsm.Fsm.GetEvent(MP_START_OR_NOT_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(starterFsm, mpStartOrNotState, "Start or not");
			}

			if (motorRunningState != null) {
				PlayMakerUtils.AddNewAction(motorRunningState, new onMotorRunningAction(this));
				FsmEvent mpMotorRunningState = starterFsm.Fsm.GetEvent(MP_MOTOR_RUNNING_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(starterFsm, mpMotorRunningState, "Motor running");
			}

			if (accGlowplugState != null) {
				PlayMakerUtils.AddNewAction(accGlowplugState, new onAccGlowplugAction(this));
				FsmEvent mpAccGlowplugState = starterFsm.Fsm.GetEvent(MP_GLOWPLUG_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(starterFsm, mpAccGlowplugState, "ACC / Glowplug");
			}

			// Dashboard
			if (accOnState != null) {
				FsmEvent mpAccOnState = dashboardFsm.Fsm.GetEvent(MP_ACC_ON_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(dashboardFsm, mpAccOnState, "ACC on");
			}

			if (testState != null) {
				FsmEvent mpTestState = dashboardFsm.Fsm.GetEvent(MP_TEST_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(dashboardFsm, mpTestState, "Test");
			}

			if (accOn2State != null) {
				FsmEvent mpAccOn2State = dashboardFsm.Fsm.GetEvent(MP_ACC_ON_2_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(dashboardFsm, mpAccOn2State, "ACC on 2");
			}

			if (motorStartingState != null) {
				FsmEvent mpMotorStartingState = dashboardFsm.Fsm.GetEvent(MP_MOTOR_STARTING_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(dashboardFsm, mpMotorStartingState, "Motor starting");
			}

			if (shutOffState != null) {
				FsmEvent mpShutOffState = dashboardFsm.Fsm.GetEvent(MP_SHUT_OFF_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(dashboardFsm, mpShutOffState, "Shut off");
			}

			if (motorOffState != null) {
				FsmEvent mpMotorOffState = dashboardFsm.Fsm.GetEvent(MP_MOTOR_OFF_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(dashboardFsm, mpMotorOffState, "Motor OFF");
			}

			if (waitButtonState != null) {
				FsmEvent mpWaitButtonState = dashboardFsm.Fsm.GetEvent(MP_WAIT_BUTTON_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(dashboardFsm, mpWaitButtonState, "Wait button");
			}

			if (waitPlayerState != null) {
				FsmEvent mpWaitPlayerState = dashboardFsm.Fsm.GetEvent(MP_WAIT_PLAYER_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(dashboardFsm, mpWaitPlayerState, "Wait player");
			}

			// Parking brake
			if (pBrakeDecreaseState != null) {
				PlayMakerUtils.AddNewAction(pBrakeDecreaseState, new onParkingBrakeDecreaseAction(this));
				FsmEvent mpParkingBrakeDecrease = handbrakeFsm.Fsm.GetEvent(MP_PBRAKE_DECREASE_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(handbrakeFsm, mpParkingBrakeDecrease, "DECREASE");
			}

			if (pBrakeIncreaseState != null) {
				PlayMakerUtils.AddNewAction(pBrakeIncreaseState, new onParkingBrakeIncreaseAction(this));
				FsmEvent mpParkingBrakeIncrease = handbrakeFsm.Fsm.GetEvent(MP_PBRAKE_INCREASE_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(handbrakeFsm, mpParkingBrakeIncrease, "INCREASE");
			}

			// Truck parking brake
			if (truckPBrakeFlipState != null) {
				PlayMakerUtils.AddNewAction(truckPBrakeFlipState, new onTruckPBrakeFlipAction(this));
				FsmEvent mpTruckPBrakeFlipState = handbrakeFsm.Fsm.GetEvent(MP_TRUCK_PBRAKE_FLIP_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(handbrakeFsm, mpTruckPBrakeFlipState, "Flip");
			}

			// Range selector
			if (rangeSwitchState != null) {
				FsmEvent mpRangeSwitchState = rangeFsm.Fsm.GetEvent(MP_RANGE_SWITCH_EVENT_NAME);
				if (isTractor == true) {
					PlayMakerUtils.AddNewGlobalTransition(rangeFsm, mpRangeSwitchState, "Flip");
				}
				else if (isTruck == true) {
					PlayMakerUtils.AddNewGlobalTransition(rangeFsm, mpRangeSwitchState, "Switch");
				}
			}

			// Fuel tap
			if (fuelTapState != null) {
				PlayMakerUtils.AddNewAction(fuelTapState, new onFuelTapUsedAction(this));
				FsmEvent mpFuelTapState = fuelTapFsm.Fsm.GetEvent(MP_FUEL_TAP_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(fuelTapFsm, mpFuelTapState, "Test");
			}

			// Lights
			if (lightsState != null) {
				PlayMakerUtils.AddNewAction(lightsState, new onLightsUsedAction(this));
				FsmEvent mpLightsState = lightsFsm.Fsm.GetEvent(MP_LIGHTS_EVENT_NAME);
				if (isTruck == true) {
					PlayMakerUtils.AddNewGlobalTransition(lightsFsm, mpLightsState, "Sound 2");
				}
				else {
					PlayMakerUtils.AddNewGlobalTransition(lightsFsm, mpLightsState, "Sound");
				}
			}

			if (lightsState != null) {
				FsmEvent mpLightsSwitchState = lightsFsm.Fsm.GetEvent(MP_LIGHTS_SWITCH_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(lightsFsm, mpLightsSwitchState, "Test");
			}

			// Wipers
			if (wipersState != null) {
				PlayMakerUtils.AddNewAction(wipersState, new onWipersUsedAction(this));
				FsmEvent mpWipersState = wipersFsm.Fsm.GetEvent(MP_WIPERS_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(wipersFsm, mpWipersState, "Test 2");
			}

			// Interior light
			if (interiorLightState != null) {
				PlayMakerUtils.AddNewAction(interiorLightState, new onInteriorLightUsedAction(this));
				FsmEvent mpInteriorLightState = interiorLightFsm.Fsm.GetEvent(MP_INTERIOR_LIGHT_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(interiorLightFsm, mpInteriorLightState, "Switch");
			}

			// Hydraulic pump
			if (hydraulicPumpState != null) {
				PlayMakerUtils.AddNewAction(hydraulicPumpState, new onHydraulicPumpUsedAction(this));
				FsmEvent mpHydraulicPumpState = hydraulicPumpFsm.Fsm.GetEvent(MP_HYDRAULIC_PUMP_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(hydraulicPumpFsm, mpHydraulicPumpState, "Test");
			}

			// Spill valve
			if (spillValveState != null) {
				PlayMakerUtils.AddNewAction(spillValveState, new onSpillValveUsedAction(this));
				FsmEvent mpSpillValveState = spillValveFsm.Fsm.GetEvent(MP_SPILL_VALVE_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(spillValveFsm, mpSpillValveState, "Switch");
			}

			// Axle lift
			if (axleLiftState != null) {
				PlayMakerUtils.AddNewAction(axleLiftState, new onAxleLiftUsedAction(this));
				FsmEvent mpAxleLiftState = axleLiftFsm.Fsm.GetEvent(MP_AXLE_LIFT_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(axleLiftFsm, mpAxleLiftState, "Test");
			}

			// Diff lock
			if (diffLockState != null) {
				PlayMakerUtils.AddNewAction(diffLockState, new onDiffLockUsedAction(this));
				FsmEvent mpDiffLockState = diffLockFsm.Fsm.GetEvent(MP_DIFF_LOCK_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(diffLockFsm, mpDiffLockState, "Test");
			}

			// Wasp nest
			if (waspNestDestroyState != null) {
				PlayMakerUtils.AddNewAction(waspNestDestroyState, new onWaspNestDestroyedAction(this));
				FsmEvent mpNestDestroyedState = waspNestFsm.Fsm.GetEvent(MP_DESTROY_WASP_NEST_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(waspNestFsm, mpNestDestroyedState, "State 2");
			}
		}

		public void SetPosAndRot(Vector3 pos, Quaternion rot) {
			Transform transform = gameObject.transform;
			transform.position = pos;
			transform.rotation = rot;
		}

		/// <summary>
		/// Set vehicle state
		/// </summary>
		public void SetEngineState(EngineStates state, DashboardStates dashstate, float startTime) {
			//Start time
			if (startTime != -1) {
				starterFsm.Fsm.GetFsmFloat("StartTime").Value = startTime;
			}

			// Engine states
			if (state == EngineStates.WaitForStart) {
				starterFsm.SendEvent(MP_WAIT_FOR_START_EVENT_NAME);
			}
			else if (state == EngineStates.ACC) {
				starterFsm.SendEvent(MP_ACC_EVENT_NAME);
			}
			else if (state == EngineStates.TurnKey) {
				starterFsm.SendEvent(MP_TURN_KEY_EVENT_NAME);
			}
			else if (state == EngineStates.StartingEngine) {
				starterFsm.SendEvent(MP_STARTING_ENGINE_EVENT_NAME);
			}
			else if (state == EngineStates.StartEngine) {
				starterFsm.SendEvent(MP_START_ENGINE_EVENT_NAME);
			}
			else if (state == EngineStates.MotorRunning) {
				starterFsm.SendEvent(MP_MOTOR_RUNNING_EVENT_NAME);
			}
			else if (state == EngineStates.Wait) {
				starterFsm.SendEvent(MP_WAIT_EVENT_NAME);
			}
			else if (state == EngineStates.CheckClutch) {
				starterFsm.SendEvent(MP_CHECK_CLUTCH_EVENT_NAME);
			}
			else if (state == EngineStates.StartOrNot) {
				starterFsm.SendEvent(MP_START_OR_NOT_EVENT_NAME);
			}
			else if (state == EngineStates.Glowplug) {
				starterFsm.SendEvent(MP_GLOWPLUG_EVENT_NAME);
			}

			// Dashboard states
			if (dashstate == DashboardStates.ACCon) {
				dashboardFsm.SendEvent(MP_ACC_ON_EVENT_NAME);
			}
			else if (dashstate == DashboardStates.Test) {
				dashboardFsm.SendEvent(MP_TEST_EVENT_NAME);
			}
			else if (dashstate == DashboardStates.ACCon2) {
				dashboardFsm.SendEvent(MP_ACC_ON_2_EVENT_NAME);
			}
			else if (dashstate == DashboardStates.MotorStarting) {
				dashboardFsm.SendEvent(MP_MOTOR_STARTING_EVENT_NAME);
			}
			else if (dashstate == DashboardStates.ShutOff) {
				dashboardFsm.SendEvent(MP_SHUT_OFF_EVENT_NAME);
			}
			else if (dashstate == DashboardStates.MotorOff) {
				dashboardFsm.SendEvent(MP_MOTOR_OFF_EVENT_NAME);
			}
			else if (dashstate == DashboardStates.WaitButton) {
				dashboardFsm.SendEvent(MP_WAIT_BUTTON_EVENT_NAME);
			}
			else if (dashstate == DashboardStates.WaitPlayer) {
				dashboardFsm.SendEvent(MP_WAIT_PLAYER_EVENT_NAME);
			}
		}

		public void SetVehicleSwitch(SwitchIDs state, bool newValue, float newValueFloat) {
			Logger.Debug($"Remote vehicle switch {state.ToString()} set on vehicle: {VehicleTransform.gameObject.name} (New value: {newValue} New value float: {newValueFloat})");

			// Parking brake
			if (state == SwitchIDs.HandbrakePull) {
				handbrakeFsm.Fsm.GetFsmFloat("KnobPos").Value = newValueFloat;
			}

			// Truck parking brake
			else if (state == SwitchIDs.HandbrakeLever) {
				if (handbrakeFsm.Fsm.GetFsmBool("Brake").Value != newValue) {
					handbrakeFsm.SendEvent(MP_TRUCK_PBRAKE_FLIP_EVENT_NAME);
				}
			}

			// Fuel tap
			else if (state == SwitchIDs.FuelTap) {
				if (fuelTapFsm.Fsm.GetFsmBool("FuelOn").Value != newValue) {
					fuelTapFsm.SendEvent(MP_FUEL_TAP_EVENT_NAME);
				}
			}

			// Lights
			else if (state == SwitchIDs.Lights) {
				if (lightsFsm.Fsm.GetFsmInt("Selection").Value != newValueFloat) {
					lightsFsm.SendEvent(MP_LIGHTS_SWITCH_EVENT_NAME);
				}
			}

			// Wipers
			else if (state == SwitchIDs.Wipers) {
				if (wipersFsm.Fsm.GetFsmInt("Selection").Value != newValueFloat) {
					wipersFsm.SendEvent(MP_WIPERS_EVENT_NAME);
				}
			}

			// Interior light
			else if (state == SwitchIDs.InteriorLight) {
				if (interiorLightFsm.Fsm.GetFsmBool("On").Value != newValue) {
					interiorLightFsm.SendEvent(MP_INTERIOR_LIGHT_EVENT_NAME);
				}
			}

			// Hydraulic pump
			else if (state == SwitchIDs.HydraulicPump) {
				if (hydraulicPumpFsm.Fsm.GetFsmBool("On").Value != newValue) {
					hydraulicPumpFsm.SendEvent(MP_HYDRAULIC_PUMP_EVENT_NAME);
				}
			}

			// Spill valve
			else if (state == SwitchIDs.SpillValve) {
				if (spillValveFsm.Fsm.GetFsmBool("Open").Value != newValue) {
					spillValveFsm.SendEvent(MP_SPILL_VALVE_EVENT_NAME);
				}
			}

			// Axle lift
			else if (state == SwitchIDs.AxleLift) {
				if (axleLiftFsm.Fsm.GetFsmBool("Up").Value != newValue) {
					axleLiftFsm.SendEvent(MP_AXLE_LIFT_EVENT_NAME);
				}
			}

			// Diff lock
			else if (state == SwitchIDs.DiffLock) {
				if (diffLockFsm.Fsm.GetFsmBool("Lock").Value != newValue) {
					diffLockFsm.SendEvent(MP_DIFF_LOCK_EVENT_NAME);
				}
			}

			// Destroy wasp nest
			else if (state == SwitchIDs.DestroyWaspNest) {
				waspNestFsm.SendEvent(MP_DESTROY_WASP_NEST_EVENT_NAME);
			}
		}

		public void UpdateIMGUI() {
			string vinfo = "Vehicle info:\n" +
				$"  Name: {gameObject.name}\n" +
				$"  Steering: {Steering}\n";

			if (starterFsm != null) {
				vinfo += "  > Starter\n";

				vinfo += $"     Active state: {starterFsm.Fsm.ActiveStateName}\n";
				if (starterFsm.Fsm.PreviousActiveState != null) {
					vinfo += $"     Prev Active state:  {starterFsm.Fsm.PreviousActiveState.Name}\n";
				}
				vinfo += $"     Start time: {starterFsm.Fsm.GetFsmFloat("StartTime").Value}\n";
			}

			if (dashboardFsm != null) {
				vinfo += "  > Dashboard:\n";
				vinfo += $"     Active state: {dashboardFsm.Fsm.ActiveStateName}\n";
				vinfo += $"     Prev Active state: {dashboardFsm.Fsm.PreviousActiveState.Name}\n";
			}

			if (lightsFsm != null) {
				vinfo += "  > Lights:\n";
				vinfo += $"     Active state: {lightsFsm.Fsm.ActiveStateName}\n";
				vinfo += $"     Prev Active state: {lightsFsm.Fsm.PreviousActiveState.Name}\n";
			}

			GUI.Label(new Rect(10, 200, 500, 500), vinfo);
		}
	}
}
