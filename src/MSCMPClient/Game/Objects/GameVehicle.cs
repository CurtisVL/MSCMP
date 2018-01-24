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
		public delegate void OnEngineStateChanged(EngineStates state);
		public OnEnter onEnter = () => {
			Logger.Log("On Enter");
		};
		public OnLeave onLeave = () => {
			Logger.Log("On Leave");
		};
		public OnEngineStateChanged onEngineStateChanged = (EngineStates state) => {
			Logger.Debug($"Engine state changed to: {state.ToString()}");
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
				return dynamics.carController.handbrakeInput;
			}
			set {
				mpCarController.remoteHandbrakeInput = value;
			}
		}
		public float ClutchInput {
			get {
				return dynamics.carController.clutchInput;
			}
			set {
				mpCarController.remoteClutchInput = value;
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
				driveTrain.gear = value;
			}
		}


		GameObject seatGameObject = null;
		GameObject starterGameObject = null;

		PlayMakerFSM starterFsm = null;

		public Transform SeatTransform {
			get {
				return seatGameObject.transform;
			}
		}

		public enum EngineStates {
			WaitForStart,
			ACC,
			TurnKey,
			StartingEngine,
			StartEngine,
			MotorRunning,
			Wait,
		}

		string MP_WAIT_FOR_START_EVENT_NAME = "MPWAITFORSTART";
		string MP_ACC_EVENT_NAME = "MPACC";
		string MP_TURN_KEY_EVENT_NAME = "MPTURNKEY";
		string MP_STARTING_ENGINE_EVENT_NAME = "MPSTARTINGENGINE";
		string MP_START_ENGINE_EVENT_NAME = "MPSTARTENGINE";
		string MP_MOTOR_RUNNING_EVENT_NAME = "MPMOTORRUNNING";
		string MP_WAIT_EVENT_NAME = "MPWAIT";

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
					if (State.Fsm.LastTransition.EventName == vehicle.MP_WAIT_FOR_START_EVENT_NAME) {
						return;
					}
				}

				vehicle.onEngineStateChanged(EngineStates.WaitForStart);
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
				if (State.Fsm.LastTransition.EventName == vehicle.MP_ACC_EVENT_NAME) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.ACC);
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
				if (State.Fsm.LastTransition.EventName == vehicle.MP_TURN_KEY_EVENT_NAME) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.TurnKey);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when vehicle enters Starting engine state.
		/// </summary>
		private class onStartingEngineAction : FsmStateAction {
			private GameVehicle vehicle;

			public onStartingEngineAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_STARTING_ENGINE_EVENT_NAME) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.StartingEngine);
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
				if (State.Fsm.LastTransition.EventName == vehicle.MP_START_ENGINE_EVENT_NAME) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.StartEngine);
				Finish();
			}
		}

		/// <summary>
		/// PlayMaker state action executed when vehicle enters Start engine state.
		/// </summary>
		private class onMotorRunningAction : FsmStateAction {
			private GameVehicle vehicle;

			public onMotorRunningAction(GameVehicle veh) {
				vehicle = veh;
			}

			public override void OnEnter() {
				if (State.Fsm.LastTransition.EventName == vehicle.MP_MOTOR_RUNNING_EVENT_NAME) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.MotorRunning);
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
				if (State.Fsm.LastTransition.EventName == vehicle.MP_WAIT_EVENT_NAME) {
					return;
				}

				vehicle.onEngineStateChanged(EngineStates.Wait);
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

			axisCarController = gameObject.GetComponent<AxisCarController>();
			mpCarController = gameObject.AddComponent<MPCarController>();

			PlayMakerFSM[] fsms = gameObject.GetComponentsInChildren<PlayMakerFSM>();

			foreach (var fsm in fsms) {
				if (fsm.FsmName == "PlayerTrigger") {
					SetupPlayerTriggerHooks(fsm);

					// Temp - use player trigger..
					seatGameObject = fsm.gameObject;
				}
				else if (fsm.FsmName == "Starter") {
					SetupVehicleEngineHooks(fsm);
					starterGameObject = fsm.gameObject;
					starterFsm = fsm;
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
		/// Setup vehicle engine related hooks.
		/// </summary>
		private void SetupVehicleEngineHooks(PlayMakerFSM fsm) {
			FsmState waitForStartState = fsm.Fsm.GetState("Wait for start");
			FsmState accState = fsm.Fsm.GetState("ACC");
			FsmState turnKeyState = fsm.Fsm.GetState("Turn key");
			FsmState startingEngineState = fsm.Fsm.GetState("Starting engine");
			FsmState startEngineState = fsm.Fsm.GetState("Start engine");
			FsmState motorRunningState = fsm.Fsm.GetState("Motor running");

			if (waitForStartState != null) {
				PlayMakerUtils.AddNewAction(waitForStartState, new onWaitForStartAction(this));
				FsmEvent mpWaitForStartEvent = fsm.Fsm.GetEvent(MP_WAIT_FOR_START_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(fsm, mpWaitForStartEvent, "Wait for start");
			}

			if (accState != null) {
				PlayMakerUtils.AddNewAction(accState, new onACCAction(this));
				FsmEvent mpACCEvent = fsm.Fsm.GetEvent(MP_ACC_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(fsm, mpACCEvent, "ACC");
			}

			if(turnKeyState != null) {
				PlayMakerUtils.AddNewAction(turnKeyState, new onTurnKeyAction(this));
				FsmEvent mpTurnKeyEvent = fsm.Fsm.GetEvent(MP_TURN_KEY_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(fsm, mpTurnKeyEvent, "Turn key");
			}

			if (startingEngineState != null) {
				PlayMakerUtils.AddNewAction(startingEngineState, new onStartingEngineAction(this));
				FsmEvent mpStartingEngineState = fsm.Fsm.GetEvent(MP_STARTING_ENGINE_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(fsm, mpStartingEngineState, "Starting engine");
			}

			if (startEngineState != null) {
				PlayMakerUtils.AddNewAction(startEngineState, new onStartEngineAction(this));
				FsmEvent mpStartEngineState = fsm.Fsm.GetEvent(MP_START_ENGINE_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(fsm, mpStartEngineState, "Start engine");
			}

			if (motorRunningState != null) {
				PlayMakerUtils.AddNewAction(motorRunningState, new onMotorRunningAction(this));
				FsmEvent mpMotorRunningState = fsm.Fsm.GetEvent(MP_MOTOR_RUNNING_EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(fsm, mpMotorRunningState, "Motor running");
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
		public void SetEngineState(EngineStates state) {
			Logger.Debug($"Remote engine state {state.ToString()} set on vehicle: {VehicleTransform.gameObject.name}");
			if(state == EngineStates.WaitForStart) {
				starterFsm.SendEvent(MP_WAIT_FOR_START_EVENT_NAME);
			}
			if (state == EngineStates.ACC) {
				starterFsm.SendEvent(MP_ACC_EVENT_NAME);
			}
			if (state == EngineStates.TurnKey) {
				starterFsm.SendEvent(MP_TURN_KEY_EVENT_NAME);
			}
			if (state == EngineStates.StartingEngine) {
				starterFsm.SendEvent(MP_STARTING_ENGINE_EVENT_NAME);
			}
			if (state == EngineStates.StartEngine) {
				starterFsm.SendEvent(MP_START_ENGINE_EVENT_NAME);
			}
			if (state == EngineStates.MotorRunning) {
				starterFsm.SendEvent(MP_MOTOR_RUNNING_EVENT_NAME);
			}
			if (state == EngineStates.Wait) {
				starterFsm.SendEvent(MP_WAIT_EVENT_NAME);
			}
		}

		public void UpdateIMGUI() {
			string vinfo = "Vehicle info:\n" +
				$"  Name: {gameObject.name}\n" +
				$"  Steering: {Steering}\n";

			Transform ignitionTransform = gameObject.transform.Find("LOD/Dashboard/Ignition");
			if (ignitionTransform != null) {
				GameObject ignition = ignitionTransform.gameObject;
				PlayMakerFSM use = Utils.GetPlaymakerScriptByName(ignition, "Use");
				if (use != null) {
					vinfo += "  > Use:\n";

					vinfo += "     Active state: " + use.Fsm.ActiveStateName + " \n";
					if (use.Fsm.PreviousActiveState != null) {
						vinfo += "     Prev Active state: " + use.Fsm.PreviousActiveState.Name + " \n";
					}

				}
				else {
					vinfo += "  > Use missing!\n";
				}
			}
			else {
				vinfo += "  > Ignition missing\n";
			}

			Transform starter = gameObject.transform.Find("Starter");
			if (starter != null) {
				vinfo += "  > Starter\n";

				PlayMakerFSM starterFsm = Utils.GetPlaymakerScriptByName(starter.gameObject, "Starter");
				vinfo += "     Active state: " + starterFsm.Fsm.ActiveStateName + " \n";
				if (starterFsm.Fsm.PreviousActiveState != null) {
					vinfo += "     Prev Active state: " + starterFsm.Fsm.PreviousActiveState.Name + " \n";
				}
			}


			GUI.Label(new Rect(10, 200, 500, 500), vinfo);
		}



	}
}
