using MSCMP.Game.Components;
using MSCMP.Network;
using UnityEngine;

namespace MSCMP.Game.Objects
{
	internal class PlayerVehicle 
		: ISyncedObject
	{
		private readonly ObjectSyncComponent _syncComponent;
		private bool _isSyncing;

		private readonly GameObject _gameObject;
		private readonly Rigidbody _rigidbody;

		public GameObject ParentGameObject;
		public Transform SeatTransform;
		public Transform PassengerSeatTransform;

		public enum DrivingStates
		{
			Driver,
			Passenger,
			None
		}

		public bool DriverIsLocal;
		public DrivingStates CurrentDrivingState = DrivingStates.None;

		private readonly CarDynamics _dynamics;
		private readonly Drivetrain _driveTrain;
		private GameObject _starterGameObject;

		// General
		private PlayMakerFSM _starterFsm;
		private PlayMakerFSM _ignitionFsm;
		private PlayMakerFSM _handbrakeFsm;
		private PlayMakerFSM _fuelTankFsm;
		private PlayMakerFSM _rangeFsm;
		private PlayMakerFSM _gearIndicatorFsm;
		private PlayMakerFSM _dashboardFsm;
		private PlayMakerFSM _fuelTapFsm;
		private PlayMakerFSM _lightsFsm;
		private PlayMakerFSM _wipersFsm;
		private PlayMakerFSM _interiorLightFsm;
		private PlayMakerFSM _frontHydraulicFsm;
		private PlayMakerFSM _indicatorsFsm;

		// Truck specific
		private PlayMakerFSM _hydraulicPumpFsm;
		private PlayMakerFSM _diffLockFsm;
		private PlayMakerFSM _axleLiftFsm;
		private PlayMakerFSM _spillValveFsm;
		private PlayMakerFSM _beaconFsm;

		// Misc
		private PlayMakerFSM _waspNestFsm;

		// Vehicle specifics
		public bool HasRange;
		private bool _hasLeverParkingBrake;
		private bool _hasPushParkingBrake;

		private bool _isTruck;
		private bool _isTractor;
		private readonly bool _isBike;

		private bool _hydraulicPumpFirstRun = true;
		private bool _axleLiftFirstRun = true;
		private bool _diffLockFirstRun = true;

		private readonly GameObject _steeringPivot;

		private readonly AxisCarController _axisCarController;
		private readonly MpCarController _mpCarController;

		public enum EngineStates
		{
			WaitForStart,
			Acc,
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

		public enum DashboardStates
		{
			AcCon,
			Test,
			AcCon2,
			MotorStarting,
			ShutOff,
			MotorOff,
			WaitButton,
			WaitPlayer,
			Null,
		}

		public enum SwitchIDs
		{
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

		private class MpCarController : AxisCarController
		{
			public float RemoteThrottleInput;
			public float RemoteBrakeInput;
			public float RemoteSteerInput;
			public readonly float RemoteHandbrakeInput = 0.0f;
			public readonly float RemoteClutchInput = 0.0f;
			public readonly bool RemoteStartEngineInput = false;
			public int RemoteTargetGear;

			protected override void GetInput(out float throttleInput, out float brakeInput, out float steerInput, out float handbrakeInput, out float clutchInput, out bool startEngineInput, out int targetGear)
			{
				throttleInput = RemoteThrottleInput;
				brakeInput = RemoteBrakeInput;
				steerInput = RemoteSteerInput;
				handbrakeInput = RemoteHandbrakeInput;
				clutchInput = RemoteClutchInput;
				startEngineInput = RemoteStartEngineInput;
				targetGear = RemoteTargetGear;
			}
		}

		public float Steering
		{
			get => _dynamics.carController.steering;
			set => _mpCarController.RemoteSteerInput = value;
		}

		public float Throttle
		{
			get => _dynamics.carController.throttleInput;
			set => _mpCarController.RemoteThrottleInput = value;
		}

		public float Brake
		{
			get => _dynamics.carController.brakeInput;
			set
			{
				_mpCarController.RemoteBrakeInput = value;
				_dynamics.carController.brakeInput = value;
			}
		}

		public float ClutchInput
		{
			get => _driveTrain.clutch.GetClutchPosition();
			set => _driveTrain.clutch.SetClutchPosition(value);
		}

		public int Gear
		{
			get => _driveTrain.gear;
			set => _mpCarController.RemoteTargetGear = value;
		}

		public float Fuel
		{
			get => _fuelTankFsm.Fsm.GetFsmFloat("FuelLevel").Value;
			set => _fuelTankFsm.Fsm.GetFsmFloat("FuelLevel").Value = value;
		}

		private float _steamId = Steamworks.SteamUser.GetSteamID().m_SteamID;


		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="go"></param>
		public PlayerVehicle(GameObject go, ObjectSyncComponent osc)
		{
			_gameObject = go;
			_syncComponent = osc;
			ParentGameObject = go.transform.parent.parent.gameObject;

			if (ParentGameObject.name.StartsWith("JONNEZ"))
			{
				_isBike = true;
				_steeringPivot = ParentGameObject.transform.FindChild("LOD/Suspension/Steering/SteeringPivot").gameObject;
			}

			_rigidbody = ParentGameObject.GetComponent<Rigidbody>();
			_dynamics = ParentGameObject.GetComponent<CarDynamics>();
			_driveTrain = ParentGameObject.GetComponent<Drivetrain>();

			_axisCarController = ParentGameObject.GetComponent<AxisCarController>();
			_mpCarController = ParentGameObject.AddComponent<MpCarController>();

			AddVehicleDoorSync();
			FindFsMs();
		}

		/// <summary>
		/// Specifics for syncing this object.
		/// </summary>
		/// <returns>What should be synced for this object.</returns>
		public ObjectSyncManager.Flags Flags()
		{
			return ObjectSyncManager.Flags.Full;
		}

		/// <summary>
		/// Add sync for doors on the vehicle.
		/// </summary>
		private void AddVehicleDoorSync()
		{
			Transform[] children = ParentGameObject.GetComponentsInChildren<Transform>();
			foreach (Transform child in children)
			{
				// Rear door of van, trunk of old car and Ferndale.
				if (child.name == "RearDoor")
				{
					child.gameObject.AddComponent<ObjectSyncComponent>().Setup(ObjectSyncManager.ObjectTypes.VehicleDoor, ObjectSyncManager.AutomaticId);
				}
				else if (child.name == "Bootlid")
				{
					child.gameObject.AddComponent<ObjectSyncComponent>().Setup(ObjectSyncManager.ObjectTypes.VehicleDoor, ObjectSyncManager.AutomaticId);
				}
				// Side door of van
				else if (child.name == "SideDoor")
				{
					GameObject fsmGo = child.FindChild("door").FindChild("Collider").gameObject;
					PlayMakerFSM fsm = Utils.GetPlaymakerScriptByName(fsmGo, "Use");
					if (fsm != null)
					{
						EventHook.AddWithSync(fsm, "Open door");
						EventHook.AddWithSync(fsm, "Close door");
					}
				}
				// Object containing drivers doors
				else if (child.name == "DriverDoors")
				{
					// Van, Truck.
					if (child.FindChild("doorl"))
					{
						child.FindChild("doorl").gameObject.AddComponent<ObjectSyncComponent>().Setup(ObjectSyncManager.ObjectTypes.VehicleDoor, ObjectSyncManager.AutomaticId);
						child.FindChild("doorr").gameObject.AddComponent<ObjectSyncComponent>().Setup(ObjectSyncManager.ObjectTypes.VehicleDoor, ObjectSyncManager.AutomaticId);
					}
					// Ferndale.
					else if (child.FindChild("door(leftx)"))
					{
						child.FindChild("door(leftx)").gameObject.AddComponent<ObjectSyncComponent>().Setup(ObjectSyncManager.ObjectTypes.VehicleDoor, ObjectSyncManager.AutomaticId);
						child.FindChild("door(right)").gameObject.AddComponent<ObjectSyncComponent>().Setup(ObjectSyncManager.ObjectTypes.VehicleDoor, ObjectSyncManager.AutomaticId);
					}
				}
			}
		}

		/// <summary>
		/// Adds a passenger seat to the vehicle.
		/// </summary>
		private void AddPassengerSeat(PlayMakerFSM fsm)
		{
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
		public void SetRemoteSteering(bool enabled)
		{
			_axisCarController.enabled = !enabled;
			_mpCarController.enabled = enabled;
		}

		/// <summary>
		/// Get object's Transform.
		/// </summary>
		/// <returns>Object's Transform.</returns>
		public Transform ObjectTransform()
		{
			return ParentGameObject.transform;
		}

		/// <summary>
		/// Check is periodic sync of the object is enabled.
		/// </summary>
		/// <returns>Periodic sync enabled or disabled.</returns>
		public bool PeriodicSyncEnabled()
		{
			return true;
		}

		/// <summary>
		/// Determines if the object should be synced.
		/// </summary>
		/// <returns>True if object should be synced, false if it shouldn't.</returns>
		public bool CanSync()
		{
			if (CurrentDrivingState == DrivingStates.Driver && DriverIsLocal)
			{
				_isSyncing = true;
				return true;
			}

			if (_rigidbody.velocity.sqrMagnitude >= 0.01f)
			{
				_isSyncing = true;
				return true;
			}
			_isSyncing = false;
			return false;
		}

		/// <summary>
		/// Called when a player enters range of an object.
		/// </summary>
		/// <returns>True if the player should tkae ownership of the object.</returns>
		public bool ShouldTakeOwnership()
		{
			return true;
		}

		/// <summary>
		/// Called when sync control is taken by force. 
		/// </summary> 
		public void SyncTakenByForce()
		{
			SetRemoteSteering(true);
		}

		/// <summary>
		/// Returns variables to be sent to the remote client.
		/// </summary>
		/// <returns>Variables to be sent to the remote client.</returns>
		public float[] ReturnSyncedVariables(bool sendAllVariables)
		{
			if (_isSyncing)
			{
				// Removed fuel from this due to an error, maybe an update broke it?
				if (_isBike)
				{
					float[] variables = { Steering, Throttle, Brake, ClutchInput, Gear, _steeringPivot.transform.localRotation.z };
					return variables;
				}
				else
				{
					float[] variables = { Steering, Throttle, Brake, ClutchInput, Gear };
					return variables;
				}
			}

			return null;
		}

		/// <summary>
		/// Handle variables sent from the remote client.
		/// </summary>
		public void HandleSyncedVariables(float[] variables)
		{
			if (variables != null)
			{
				Steering = variables[0];
				Throttle = variables[1];
				Brake = variables[2];
				ClutchInput = variables[3];
				Gear = (int)variables[4];
				//Fuel = variables[5];
				if (_isBike)
				{
					Quaternion rot = _steeringPivot.transform.localRotation;
					rot.z = variables[5];
					_steeringPivot.transform.localRotation = rot;
				}
			}
		}

		/// <summary>
		/// Called when owner is set to the remote client.
		/// </summary>
		public void OwnerSetToRemote()
		{
			SetRemoteSteering(true);
		}

		/// <summary>
		/// Called when owner is removed.
		/// </summary>
		public void OwnerRemoved()
		{
			SetRemoteSteering(false);
		}

		/// <summary>
		/// Called when an object is constantly syncing. (Usually when a pickupable is picked up, or when a vehicle is being driven)
		/// </summary>
		/// <param name="newValue">If object is being constantly synced.</param>
		public void ConstantSyncChanged(bool newValue)
		{

		}

		/// <summary>
		/// Find required FSMs in the game object.
		/// </summary>
		public void FindFsMs()
		{
			PlayMakerFSM[] fsms = ParentGameObject.GetComponentsInChildren<PlayMakerFSM>();

			foreach (PlayMakerFSM fsm in fsms)
			{
				if (fsm.FsmName == "PlayerTrigger")
				{
					PlayerEventHooks(fsm);
				}

				// Starter
				else if (fsm.FsmName == "Starter")
				{
					_starterGameObject = fsm.gameObject;
					_starterFsm = fsm;
				}

				// Ignitionn
				else if (fsm.FsmName == "Use" && fsm.gameObject.name == "Ignition")
				{
					_ignitionFsm = fsm;
				}

				// Handbrake for Van, Ferndale, Tractor, Ruscko
				else if (fsm.gameObject.name == "ParkingBrake" && fsm.FsmName == "Use")
				{
					_handbrakeFsm = fsm;
					_hasPushParkingBrake = true;
				}

				// Handbrake for Truck
				else if (fsm.gameObject.name == "Parking Brake" && fsm.FsmName == "Use")
				{
					_handbrakeFsm = fsm;
					_hasLeverParkingBrake = true;
				}

				// Range selector
				else if (fsm.gameObject.name == "Range" && fsm.FsmName == "Use")
				{
					_rangeFsm = fsm;
					HasRange = true;
				}

				// Fuel tank
				else if (fsm.gameObject.name == "FuelTank" && fsm.FsmName == "Data")
				{
					_fuelTankFsm = fsm;
				}

				// Dashboard
				else if (fsm.gameObject.name == "Ignition" && fsm.FsmName == "Use")
				{
					_dashboardFsm = fsm;
				}

				// Fuel tap
				else if (fsm.gameObject.name == "FuelTap" && fsm.FsmName == "Use")
				{
					_fuelTapFsm = fsm;
				}

				// Lights
				else if (fsm.gameObject.name == "Lights" && fsm.FsmName == "Use" || fsm.gameObject.name == "ButtonLights" && fsm.FsmName == "Use" || fsm.gameObject.name == "knob" && fsm.FsmName == "Use")
				{
					_lightsFsm = fsm;
				}

				// Wipers
				else if (fsm.gameObject.name == "Wipers" && fsm.FsmName == "Use" || fsm.gameObject.name == "ButtonWipers" && fsm.FsmName == "Use")
				{
					_wipersFsm = fsm;
				}

				// Interior light Truck
				else if (fsm.gameObject.name == "ButtonInteriorLight" && fsm.FsmName == "Use")
				{
					_interiorLightFsm = fsm;
				}

				// Interior light Van/Ferndale
				else if (fsm.gameObject.name == "Use" && fsm.FsmName == "Use" && fsm.Fsm.GetState("Flip 2") != null)
				{
					_interiorLightFsm = fsm;
				}

				// Gear indicator - Used to get Range position
				else if (fsm.FsmName == "GearIndicator")
				{
					_gearIndicatorFsm = fsm;
				}

				// Tractor front hydraulic
				else if (fsm.gameObject.name == "FrontHyd" && fsm.FsmName == "Use")
				{
					_frontHydraulicFsm = fsm;
					_isTractor = true;
				}

				// Wasp nest
				else if (fsm.gameObject.name == "WaspHive" && fsm.FsmName == "Data")
				{
					_waspNestFsm = fsm;
				}

				// Indicators
				else if (fsm.gameObject.name == "TurnSignals" && fsm.FsmName == "Usage")
				{
					_indicatorsFsm = fsm;
				}

				// Truck specific FSMs

				// Hydraulic pump
				if (fsm.gameObject.name == "Hydraulics" && fsm.FsmName == "Use")
				{
					_hydraulicPumpFsm = fsm;
					_isTruck = true;
				}

				// Diff lock
				if (fsm.gameObject.name == "Differential lock" && fsm.FsmName == "Use")
				{
					_diffLockFsm = fsm;
				}

				// Axle lift
				if (fsm.gameObject.name == "Liftaxle" && fsm.FsmName == "Use")
				{
					_axleLiftFsm = fsm;
				}

				// Spill valve
				if (fsm.gameObject.name == "OpenSpill" && fsm.FsmName == "Use")
				{
					_spillValveFsm = fsm;
				}

				// Beacon
				if (fsm.gameObject.name == "KnobBeacon" && fsm.FsmName == "Use")
				{
					_beaconFsm = fsm;
				}
			}

			// Finished finding FSMs, now hook the events.
			HookEvents();
		}

		/// <summary>
		/// Hook vehicle events.
		/// </summary>
		private void HookEvents()
		{
			// Engine states
			string[] ignitionStateNames = { "Wait button", "Motor starting", "Motor OFF", "Test", "Shut off", "ACC on", "ACC on 2" };
			string[] ignitionStateNamesBike = { "Wait button", "Motor starting", "Motor OFF", "Test" };

			if (_isBike)
			{
				foreach (string name in ignitionStateNamesBike)
				{
					EventHook.AddWithSync(_ignitionFsm, name);
				}

				EventHook.AddWithSync(_starterFsm, "Wait for start");
				EventHook.AddWithSync(_starterFsm, "Start or not");
				EventHook.AddWithSync(_starterFsm, "Start or not 2");
				EventHook.AddWithSync(_starterFsm, "Start engine");
			}
			else
			{
				foreach (string name in ignitionStateNames)
				{
					EventHook.AddWithSync(_ignitionFsm, name);
				}
			}


			// Dashboard states
			string[] dashboardStateNames = { "ACC on", "Test", "ACC on 2", "Motor starting", "Shut off", "Motor OFF", "Wait button", "Wait player" };

			foreach (string name in dashboardStateNames)
			{
				EventHook.Add(_dashboardFsm, name, () =>
				{
					return false;
				});
			}

			// Range
			if (HasRange)
			{
				if (_isTruck)
				{
					EventHook.AddWithSync(_rangeFsm, "Switch");
				}
				else if (_isTractor)
				{
					EventHook.AddWithSync(_rangeFsm, "Flip");
				}
			}

			// Push parking brake
			if (_hasPushParkingBrake)
			{
				EventHook.Add(_handbrakeFsm, "DECREASE", () =>
				{
					WriteVehicleSwitchMessage(_syncComponent, SwitchIDs.HandbrakePull, false, _handbrakeFsm.Fsm.GetFsmFloat("KnobPos").Value);
					return false;
				}, actionOnExit: true);
				EventHook.Add(_handbrakeFsm, "INCREASE", () =>
				{
					WriteVehicleSwitchMessage(_syncComponent, SwitchIDs.HandbrakePull, false, _handbrakeFsm.Fsm.GetFsmFloat("KnobPos").Value);
					return false;
				}, actionOnExit: true);
			}

			// Truck parking brake
			if (_hasLeverParkingBrake)
			{
				EventHook.Add(_handbrakeFsm, "Flip", () =>
				{
					WriteVehicleSwitchMessage(_syncComponent, SwitchIDs.HandbrakeLever, !_handbrakeFsm.Fsm.GetFsmBool("Brake").Value, -1);
					return false;
				});
			}

			// Fuel tap
			if (_fuelTapFsm != null)
			{
				EventHook.Add(_fuelTapFsm, "Test", () =>
				{
					WriteVehicleSwitchMessage(_syncComponent, SwitchIDs.FuelTap, !_fuelTapFsm.Fsm.GetFsmBool("FuelOn").Value, -1);
					return false;
				});
			}

			// Lights
			if (_lightsFsm != null)
			{
				EventHook.AddWithSync(_lightsFsm, "Off");
				EventHook.AddWithSync(_lightsFsm, "Shorts");
				if (!_isBike)
				{
					EventHook.AddWithSync(_lightsFsm, "Longs");
				}
			}

			// Indicators
			if (_indicatorsFsm != null)
			{
				EventHook.AddWithSync(_indicatorsFsm, "Activate dash");
				EventHook.AddWithSync(_indicatorsFsm, "Activate dash 2");
				EventHook.AddWithSync(_indicatorsFsm, "On", action: () =>
				{
					if (DriverIsLocal == false)
					{
						return true;
					}
					return false;
				});
				EventHook.AddWithSync(_indicatorsFsm, "On 2", action: () =>
				{
					if (DriverIsLocal == false)
					{
						return true;
					}
					return false;
				});
				EventHook.AddWithSync(_indicatorsFsm, "Off", action: () =>
				{
					if (DriverIsLocal == false)
					{
						return true;
					}
					return false;
				});
				EventHook.AddWithSync(_indicatorsFsm, "Off 2", action: () =>
				{
					if (DriverIsLocal == false)
					{
						return true;
					}
					return false;
				});

				EventHook.AddWithSync(_indicatorsFsm, "State 3", action: () =>
				{
					if (DriverIsLocal == false)
					{
						GameObject left;
						GameObject right;
						left = _gameObject.transform.FindChild("LOD/Electricity/PowerON/Blinkers/Left").gameObject;
						right = _gameObject.transform.FindChild("LOD/Electricity/PowerON/Blinkers/Right").gameObject;
						// Ferndale has a different hierarchy. Why not, right?
						if (left == null)
						{
							left = _gameObject.transform.FindChild("LOD/Electricity 1/PowerON/Blinkers/Left").gameObject;
							right = _gameObject.transform.FindChild("LOD/Electricity 1/PowerON/Blinkers/Right").gameObject;
						}
						left.SetActive(false);
						right.SetActive(false);
					}
					return false;
				});
			}

			// Wipers
			if (_wipersFsm != null)
			{
				EventHook.Add(_wipersFsm, "Test 2", () =>
				{
					int selection = _wipersFsm.Fsm.GetFsmInt("Selection").Value;
					if (selection == 2)
					{
						selection = 0;
					}
					else
					{
						selection++;
					}

					WriteVehicleSwitchMessage(_syncComponent, SwitchIDs.Wipers, false, selection);
					return false;
				});
			}

			// Interior light
			if (_interiorLightFsm != null)
			{
				if (_isTruck)
				{
					EventHook.Add(_interiorLightFsm, "Switch", () =>
					{
						WriteVehicleSwitchMessage(_syncComponent, SwitchIDs.InteriorLight, !_interiorLightFsm.Fsm.GetFsmBool("On").Value, -1);
						return false;
					});
				}
				else
				{
					EventHook.Add(_interiorLightFsm, "Flip 2", () =>
					{
						WriteVehicleSwitchMessage(_syncComponent, SwitchIDs.InteriorLight, !_interiorLightFsm.Fsm.GetFsmBool("LightON").Value, -1);
						return false;
					});
				}
			}

			// Truck related events
			if (_isTruck)
			{
				// Hydraulic pump
				EventHook.Add(_hydraulicPumpFsm, "Test", () =>
				{
					if (_hydraulicPumpFirstRun == false)
					{
						WriteVehicleSwitchMessage(_syncComponent, SwitchIDs.HydraulicPump, !_hydraulicPumpFsm.Fsm.GetFsmBool("On").Value, -1);
					}
					else
					{
						_hydraulicPumpFirstRun = false;
					}
					return false;
				});

				// Spill valve
				EventHook.Add(_spillValveFsm, "Switch", () =>
				{
					WriteVehicleSwitchMessage(_syncComponent, SwitchIDs.SpillValve, !_spillValveFsm.Fsm.GetFsmBool("Open").Value, -1);
					return false;
				});

				// Axle lift
				EventHook.Add(_axleLiftFsm, "Test", () =>
				{
					if (_axleLiftFirstRun == false)
					{
						WriteVehicleSwitchMessage(_syncComponent, SwitchIDs.AxleLift, !_axleLiftFsm.Fsm.GetFsmBool("Up").Value, -1);
					}
					else
					{
						_axleLiftFirstRun = false;
					}
					return false;
				});

				// Diff lock
				EventHook.Add(_diffLockFsm, "Test", () =>
				{
					if (_diffLockFirstRun == false)
					{
						WriteVehicleSwitchMessage(_syncComponent, SwitchIDs.DiffLock, !_diffLockFsm.Fsm.GetFsmBool("Lock").Value, -1);
					}
					else
					{
						_diffLockFirstRun = false;
					}
					return false;
				});

				// Beacon
				EventHook.AddWithSync(_beaconFsm, "ON");
				EventHook.AddWithSync(_beaconFsm, "OFF");
			}

			// Wasp nest
			if (_waspNestFsm != null)
			{
				EventHook.AddWithSync(_waspNestFsm, "State 2");
			}

			// Sync vehicle data with the host on spawn.
			if (NetManager.Instance.IsOnline && !NetManager.Instance.IsHost)
			{
				_syncComponent.RequestObjectSync();
			}
		}

		/// <summary>
		/// Hook player trigger events.
		/// </summary>
		/// <param name="fsm">Player trigger FSM on vehicle drivers seat.</param>
		private void PlayerEventHooks(PlayMakerFSM fsm)
		{
			// Temp - use player trigger. (No idea what this comment meant, it's now many months later. :P)
			// It's now 6+ months later, no idea what this means at all now. :p -Curtis
			EventHook.Add(fsm, "Player in car", () =>
			{
				if (CurrentDrivingState == DrivingStates.Driver && !DriverIsLocal)
				{
					return true;
				}

				CurrentDrivingState = DrivingStates.Driver;
				_syncComponent.TakeSyncControl();
				DriverIsLocal = true;
				SetRemoteSteering(false);
				NetLocalPlayer.Instance.EnterVehicle(_syncComponent, false);
				return false;
			});
			EventHook.Add(fsm, "Wait for player", () =>
			{
				if (CurrentDrivingState == DrivingStates.Driver && DriverIsLocal)
				{
					CurrentDrivingState = DrivingStates.None;
					DriverIsLocal = false;
					_syncComponent.SendConstantSync(false);
					NetLocalPlayer.Instance.LeaveVehicle();
				}
				return false;
			});
			if (_isBike)
			{
				EventHook.Add(fsm, "Press return", () =>
				{
					_syncComponent.TakeSyncControl();
					_syncComponent.SendConstantSync(true);
					return false;
				});
			}
			SeatTransform = fsm.gameObject.transform;

			if (SeatTransform.gameObject.name == "DriveTrigger" && !_isBike && !ParentGameObject.name.StartsWith("KEKMET"))
			{
				AddPassengerSeat(fsm);
			}
		}

		/// <summary>
		/// Set value of switches within a vehicle.
		/// </summary>
		/// <param name="state">Switch to change.</param>
		/// <param name="newValue">New value as a bool.</param>
		/// <param name="newValueFloat">New value as a float.</param>
		public void SetVehicleSwitch(SwitchIDs state, bool newValue, float newValueFloat)
		{
			switch (state)
			{
				// Parking brake
				case SwitchIDs.HandbrakePull:
					_handbrakeFsm.Fsm.GetFsmFloat("KnobPos").Value = newValueFloat;
					break;

				// Truck parking brake
				case SwitchIDs.HandbrakeLever:
					if (_handbrakeFsm.Fsm.GetFsmBool("Brake").Value != newValue)
					{
						_handbrakeFsm.SendEvent("MP_Flip");
					}
					break;

				// Fuel tap
				case SwitchIDs.FuelTap:
					if (_fuelTapFsm.Fsm.GetFsmBool("FuelOn").Value != newValue)
					{
						_fuelTapFsm.SendEvent("MP_Test");
					}
					break;

				// Lights
				case SwitchIDs.Lights:
					while (_lightsFsm.Fsm.GetFsmInt("Selection").Value != newValueFloat)
					{
						_lightsFsm.SendEvent("MP_Test");
					}
					break;

				// Wipers
				case SwitchIDs.Wipers:
					if (_wipersFsm.Fsm.GetFsmInt("Selection").Value != newValueFloat)
					{
						_wipersFsm.SendEvent("MP_Test 2");
					}
					break;

				// Interior light
				case SwitchIDs.InteriorLight:
					if (_isTruck)
					{
						if (_interiorLightFsm.Fsm.GetFsmBool("On").Value != newValue)
						{
							_interiorLightFsm.SendEvent("MP_Switch");
						}
					}
					else
					{
						if (_interiorLightFsm.Fsm.GetFsmBool("LightON").Value != newValue)
						{
							_interiorLightFsm.SendEvent("MP_Flip 2");
						}
					}
					break;

				// Hydraulic pump
				case SwitchIDs.HydraulicPump:
					if (_hydraulicPumpFsm.Fsm.GetFsmBool("On").Value != newValue)
					{
						_hydraulicPumpFsm.SendEvent("MP_Test");
					}
					break;

				// Spill valve
				case SwitchIDs.SpillValve:
					if (_spillValveFsm.Fsm.GetFsmBool("Open").Value != newValue)
					{
						_spillValveFsm.SendEvent("MP_Switch");
					}
					break;

				// Axle lift
				case SwitchIDs.AxleLift:
					if (_axleLiftFsm.Fsm.GetFsmBool("Up").Value != newValue)
					{
						_axleLiftFsm.SendEvent("MP_Test");
					}
					break;

				// Diff lock
				case SwitchIDs.DiffLock:
					if (_diffLockFsm.Fsm.GetFsmBool("Lock").Value != newValue)
					{
						_diffLockFsm.SendEvent("MP_Test");
					}
					break;
			}
		}

		/// <summary>
		/// Write vehicle switch changes into vehicle switch message.
		/// </summary>
		/// <param name="vehicle"></param>
		/// <param name="switchId"></param>
		/// <param name="newValue"></param>
		/// <param name="newValueFloat"></param>
		public void WriteVehicleSwitchMessage(ObjectSyncComponent vehicle, SwitchIDs switchId, bool newValue, float newValueFloat)
		{
			Network.Messages.VehicleSwitchMessage msg = new Network.Messages.VehicleSwitchMessage();
			msg.objectID = vehicle.ObjectId;
			msg.switchID = (int)switchId;
			msg.switchValue = newValue;
			if (newValueFloat != -1)
			{
				msg.SwitchValueFloat = newValueFloat;
			}
			NetManager.Instance.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
		}
	}
}
