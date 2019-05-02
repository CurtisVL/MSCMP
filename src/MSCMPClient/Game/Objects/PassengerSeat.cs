using UnityEngine;

namespace MSCMP.Game.Objects
{
	/// <summary>
	/// Handles passenger seats.
	/// 
	/// This method appears to be easier than modifying current passenger seats of vehicles.
	/// </summary>
	public class PassengerSeat
		: MonoBehaviour
	{
		public string VehicleType = null;
		public GameObject DriversSeat = null;

		private GameObject _player;
		private GameObject _trigger;

		private CapsuleCollider _playerCollider;

		private bool _canSit;
		private bool _isSitting;
		private bool _showGui;

		private CharacterMotor _motor;

		private GameObject _guiGameObject;
		private PlayMakerFSM _iconsFsm;
		private PlayMakerFSM _textFsm;

		public delegate void OnEnterPassengerSeatEvent();
		public delegate void OnLeavePassengerSeatEvent();
		public OnEnterPassengerSeatEvent OnEnterPassengerSeat = () =>
		{

		};
		public OnLeavePassengerSeatEvent OnLeavePassengerSeat = () =>
		{

		};

		private Vector3 _currentPosition;

		/// <summary>
		/// Initialise passenger seat
		/// </summary>
		private void Start()
		{
			if (Network.NetWorld.DisplayObjectRegisteringDebug)
			{
				Logger.Debug($"Passenger seat added, vehicle: {VehicleType}");
			}

			_guiGameObject = GameObject.Find("GUI");

			PlayMakerFSM[] fsms = _guiGameObject.GetComponentsInChildren<PlayMakerFSM>();
			foreach (PlayMakerFSM fsm in fsms)
			{
				if (fsm.FsmName == "Logic")
				{
					_iconsFsm = fsm;
					continue;
				}

				if (fsm.FsmName == "SetText" && fsm.gameObject.name == "Interaction")
				{
					_textFsm = fsm;
					continue;
				}
				if (_iconsFsm != null && _textFsm != null)
				{
					break;
				}
			}

			// Set seat position and size based on vehicle
			_trigger = gameObject;

			// Van
			if (VehicleType.StartsWith("HAYOSIKO"))
			{
				_trigger.transform.localPosition = new Vector3(-DriversSeat.transform.localPosition.x + 0.1f, -DriversSeat.transform.localPosition.y + 0.35f, -DriversSeat.transform.localPosition.z - 0.7f);
			}

			// Truck
			if (VehicleType.StartsWith("GIFU"))
			{
				_trigger.transform.localPosition = new Vector3(-DriversSeat.transform.localPosition.x, -DriversSeat.transform.localPosition.y, -DriversSeat.transform.localPosition.z + 0.15f);
			}

			// Old car
			if (VehicleType.StartsWith("RCO_RUSCKO"))
			{
				_trigger.transform.localPosition = new Vector3(-DriversSeat.transform.localPosition.x, -DriversSeat.transform.localPosition.y, -DriversSeat.transform.localPosition.z - 0.15f);
			}

			// The impossible to drive car
			if (VehicleType.StartsWith("FERNDALE"))
			{
				_trigger.transform.localPosition = new Vector3(-DriversSeat.transform.localPosition.x + 0.1f, -DriversSeat.transform.localPosition.y, -DriversSeat.transform.localPosition.z - 0.6f);
			}

			// Satsuma
			if (VehicleType.StartsWith("SATSUMA"))
			{
				_trigger.transform.localPosition = new Vector3(-DriversSeat.transform.localPosition.x, -DriversSeat.transform.localPosition.y, -DriversSeat.transform.localPosition.z + 0.15f);
			}

			// Destroys the cube mesh render
			Destroy(_trigger.GetComponentInChildren<MeshRenderer>());
		}

		/// <summary>
		/// Triggered on entering the passenger seat.
		/// </summary>
		/// <param name="other"></param>
		private void OnTriggerEnter(Collider other)
		{
			if (other.gameObject.name != "PLAYER") return;

			_canSit = true;
			if (_isSitting == false)
			{
				_showGui = true;
			}

			if (_player == null)
			{
				_player = other.gameObject;
				_motor = _player.GetComponentInChildren<CharacterMotor>();
			}

			if (_playerCollider == null)
			{
				_playerCollider = _player.GetComponentInChildren<CapsuleCollider>();
				_playerCollider.enabled = false;
			}
		}

		/// <summary>
		/// Triggered on leaving the passenger seat.
		/// </summary>
		/// <param name="other"></param>
		private void OnTriggerExit(Collider other)
		{
			if (other.gameObject.name != "PLAYER") return;

			_canSit = false;
			_showGui = false;
			_iconsFsm.Fsm.GetFsmBool("GUIpassenger").Value = false;
			_textFsm.Fsm.GetFsmString("GUIinteraction").Value = "";
		}

		/// <summary>
		/// Called every frame.
		/// </summary>
		private void Update()
		{
			if (_showGui)
			{
				// Yep, this needs to be called on update. Thanks MSC.
				_textFsm.Fsm.GetFsmString("GUIinteraction").Value = "ENTER PASSENGER MODE";
				_iconsFsm.Fsm.GetFsmBool("GUIpassenger").Value = true;
			}

			if (Input.GetKeyDown(KeyCode.Return))
			{
				// Enter seat
				if (_isSitting == false && _canSit)
				{
					_isSitting = true;

					_player.transform.parent = gameObject.transform;
					_currentPosition = _player.transform.localPosition;
					_motor.enabled = false;

					_player.transform.FindChild("Sphere").GetComponentInChildren<SphereCollider>().enabled = false;

					_showGui = false;
					_iconsFsm.Fsm.GetFsmBool("GUIpassenger").Value = false;
					_textFsm.Fsm.GetFsmString("GUIinteraction").Value = "";

					Collider[] colliders = _player.GetComponents<Collider>();
					foreach (Collider col in colliders)
					{
						col.enabled = false;
					}

					OnEnterPassengerSeat();
				}
				// Leave seat
				else if (_isSitting)
				{
					_isSitting = false;

					Collider[] colliders = _player.GetComponents<Collider>();
					foreach (Collider col in colliders)
					{
						col.enabled = true;
					}

					_player.transform.parent = null;

					// Resets player rotation on leaving the seat
					_player.transform.rotation = new Quaternion(0, _player.transform.rotation.y, 0, _player.transform.rotation.w);

					_motor.enabled = true;

					_showGui = true;

					OnLeavePassengerSeat();
				}
			}

			// Keep player in the correct position in the seat.
			if (_isSitting)
			{
				_player.transform.localPosition = _currentPosition;
			}
		}
	}
}
