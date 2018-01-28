using HutongGames.PlayMaker;
using UnityEngine;

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

		public delegate void OnEnter();
		public delegate void OnLeave();
		public delegate void OnEngineStateChanged(EngineStates state, DashboardStates dashstate, float startTime);
		public delegate void OnVehicleSwitchChanged(SwitchIDs id, bool newValue, float newValueFloat);
		public OnEnter onEnter = () => {
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
				return dynamics.carController.throttle;
			}
			set {
				mpCarController.remoteThrottleInput = value;
			}
		}
		public float Brake {
			get {
				return dynamics.carController.brake;
			}
			set {
				mpCarController.remoteBrakeInput = value;
			}
		}
		public float HandbrakeInput {
			get {
				// Van, Tractor, Ruscko
				if (hasPushParkingBrake == true) {
					return handbrakeFsm.Fsm.GetFsmFloat("KnobPos").Value;
				}
				// Truck, Jonnez
				else {
					return 0;
				}
			}
			set {
				// Van, Tractor, Rucsko
				if (hasPushParkingBrake == true) {
					handbrakeFsm.Fsm.GetFsmFloat("KnobPos").Value = value;
				}
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
				return true;
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


		GameObject seatGameObject = null;
		GameObject starterGameObject = null;

		PlayMakerFSM starterFsm = null;
		PlayMakerFSM handbrakeFsm = null;
		PlayMakerFSM fuelTankFsm = null;
		PlayMakerFSM rangeFsm = null;
		PlayMakerFSM dashboardFsm = null;
		PlayMakerFSM fuelTapFsm = null;
		PlayMakerFSM lightsFsm = null;

		bool hasRange = false;
		bool hasLeverParkingBrake = false;
		bool hasPushParkingBrake = false;
		bool hasFuelTap = false;
		bool hasLights = false;


		public Transform SeatTransform {
			get {
				return seatGameObject.transform;
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
			Tailgate,
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
		string MP_TRUCK_PBRAKE_FLIP_EVENT_NAME = "MPFLIPBRAKE";
		string MP_LIGHTS_EVENT_NAME = "MPLIGHTS";

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
							vehicle.onEnter();
							vehicle.isDriver = true;
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

				// Not entirely sure why, but the parking brake bool gets inverted at some point.
				vehicle.onVehicleSwitchChanges(SwitchIDs.HandbrakeLever, vehicle.handbrakeFsm.Fsm.GetFsmBool("Brake").Value, -1);
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

				vehicle.onVehicleSwitchChanges(SwitchIDs.FuelTap, vehicle.fuelTapFsm.Fsm.GetFsmBool("FuelOn").Value, -1);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when lights switch in vehicles is used.
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

				int selection = vehicle.lightsFsm.Fsm.GetFsmInt("Selection").Value;
				if (selection == 2) {
					selection = 0;
				}
				else {
					selection++;
				}

				vehicle.onVehicleSwitchChanges(SwitchIDs.Lights, false, selection);
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

			if(driveTrain != null) {
				driveTrain.canStall = false;
			}

			axisCarController = gameObject.GetComponent<AxisCarController>();
			mpCarController = gameObject.AddComponent<MPCarController>();

			PlayMakerFSM[] fsms = gameObject.GetComponentsInChildren<PlayMakerFSM>();

			foreach (var fsm in fsms) {
				if (fsm.FsmName == "PlayerTrigger") {
					SetupPlayerTriggerHooks(fsm);

					// Temp - use player trigger..
					seatGameObject = fsm.gameObject;
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
					hasFuelTap = true;
				}

				// Lights
				else if (fsm.gameObject.name == "Lights" && fsm.FsmName == "Use") {
					lightsFsm = fsm;
					hasLights = true;
				}
			}

			if (starterFsm != null) {
				SetupVehicleEngineHooks();
			}
			else {
				if (starterFsm == null) {
					Logger.Log($"Missing vehicle starterFSM, vehicle: {gameObject.name}!");
				}
				if (handbrakeFsm == null) {
					Logger.Log($"Missing vehicle handbrakeFsm, vehicle: {gameObject.name}!");
				}
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
		/// Setup vehicle engine/dashboard related hooks.
		/// </summary>
		private void SetupVehicleEngineHooks() {
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

			FsmState truckPBrakeFlipState = null;
			if (hasLeverParkingBrake == true) {
				truckPBrakeFlipState = handbrakeFsm.Fsm.GetState("Flip");
			}

			FsmState rangeSwitchState = null;
			if (hasRange == true) {
				rangeSwitchState = rangeFsm.Fsm.GetState("Switch");
			}

			FsmState fuelTapState = null;
			if (hasFuelTap == true) {
				fuelTapState = fuelTapFsm.Fsm.GetState("Test");
			}

			FsmState lightsState = null;
			if (hasLights == true) {
				lightsState = lightsFsm.Fsm.GetState("Test");
			}

			FsmState accOnState = null;
			FsmState testState = null;
			FsmState accOn2State = null;
			FsmState motorStartingState = null;
			FsmState shutOffState = null;
			FsmState motorOffState = null;
			FsmState waitButtonState = null;
			FsmState waitPlayerState = null;
			if (dashboardFsm != null) {
				accOnState = dashboardFsm.Fsm.GetState("ACC on");
				testState = dashboardFsm.Fsm.GetState("Test");
				accOn2State = dashboardFsm.Fsm.GetState("ACC on 2");
				motorStartingState = dashboardFsm.Fsm.GetState("Motor starting");
				shutOffState = dashboardFsm.Fsm.GetState("Shut off");
				motorOffState = dashboardFsm.Fsm.GetState("Motor OFF");
				waitButtonState = dashboardFsm.Fsm.GetState("Wait button");
				waitPlayerState = dashboardFsm.Fsm.GetState("Wait player");
			}

			//Engine states
			if (waitForStartState != null) {
				PlayMakerUtils.AddNewAction(waitForStartState, new onWaitForStartAction(this));
				FsmEvent mpWaitForStartEvent = starterFsm.Fsm.GetEvent(MP_WAIT_FOR_START_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(starterFsm, mpWaitForStartEvent, "Wait for start");
			}

			if (accState != null) {
				PlayMakerUtils.AddNewAction(accState, new onACCAction(this));
				FsmEvent mpACCEvent = starterFsm.Fsm.GetEvent(MP_ACC_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(starterFsm, mpACCEvent, "ACC");
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
				PlayMakerUtils.AddNewAction(accGlowplugState, new onMotorRunningAction(this));
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

			// Truck parking brake
			if (truckPBrakeFlipState != null) {
				PlayMakerUtils.AddNewAction(truckPBrakeFlipState, new onTruckPBrakeFlipAction(this));
				FsmEvent mpTruckPBrakeFlipState = handbrakeFsm.Fsm.GetEvent(MP_TRUCK_PBRAKE_FLIP_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(handbrakeFsm, mpTruckPBrakeFlipState, "Flip");
			}

			// Range selector
			if (rangeSwitchState != null) {
				FsmEvent mpRangeSwitchState = rangeFsm.Fsm.GetEvent(MP_RANGE_SWITCH_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(rangeFsm, mpRangeSwitchState, "Switch");
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
				FsmEvent mpFuelTapState = lightsFsm.Fsm.GetEvent(MP_LIGHTS_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(lightsFsm, mpFuelTapState, "Test");
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
			Logger.Debug($"Remote dashboard state {dashstate.ToString()} set on vehicle: {VehicleTransform.gameObject.name}");
			//Start time
			if (startTime != -1) {
				starterFsm.Fsm.GetFsmFloat("StartTime").Value = startTime;
				Logger.Debug($"Start time set to: {startTime}");
			}
			Logger.Debug($"Engine state set to: {state.ToString()}");

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

			// Truck parking brake
			if (state == SwitchIDs.HandbrakeLever) {
				// Not sure why, but the parking brake value on the host is inverted compared to the remote.
				if (handbrakeFsm.Fsm.GetFsmBool("Brake").Value == newValue) {
					handbrakeFsm.SendEvent(MP_TRUCK_PBRAKE_FLIP_EVENT_NAME);
				}
			}

			// Fuel tap
			else if (state == SwitchIDs.FuelTap) {
				// If clicking too quickly, it takes too long to get the FsmBool and check it, so tap becomes desynced.
				//if (fuelTankFsm.Fsm.GetFsmBool("FuelOn").Value != newValue) {
					fuelTapFsm.SendEvent(MP_FUEL_TAP_EVENT_NAME);
				//}
			}

			// Lights
			else if (state == SwitchIDs.Lights) {
				if (lightsFsm.Fsm.GetFsmInt("Selection").Value != newValueFloat) {
					lightsFsm.SendEvent(MP_LIGHTS_EVENT_NAME);
				}
			}
		}

		public void UpdateIMGUI() {
			string vinfo = "Vehicle info:\n" +
				$"  Name: {gameObject.name}\n" +
				$"  Steering: {Steering}\n";

			if (starterFsm != null) {
				vinfo += "  > Starter\n";

				vinfo += "     Active state: " + starterFsm.Fsm.ActiveStateName + " \n";
				if (starterFsm.Fsm.PreviousActiveState != null) {
					vinfo += "     Prev Active state: " + starterFsm.Fsm.PreviousActiveState.Name + " \n";
				}
				vinfo += "     Start time: " + starterFsm.Fsm.GetFsmFloat("StartTime").Value + " \n";
			}

			if (dashboardFsm != null) {
				vinfo += "  > Dashboard:\n";
				vinfo += "     Active state: " + dashboardFsm.Fsm.ActiveStateName + " \n";
				vinfo += "     Prev Active state: " + dashboardFsm.Fsm.PreviousActiveState.Name + " \n";
				vinfo += "     Lights active state: " + lightsFsm.Fsm.ActiveStateName + " \n";
			}

			GUI.Label(new Rect(10, 200, 500, 500), vinfo);
		}



	}
}
