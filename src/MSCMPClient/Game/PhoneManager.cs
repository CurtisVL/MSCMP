using UnityEngine;

namespace MSCMP.Game
{
	/// <summary>
	/// Manages various events for the phone.
	/// </summary>
	internal class PhoneManager
	{
		public static PhoneManager Instance;

		private GameObject _gameObject;
		private PlayMakerFSM _ringFsm;
		private bool _callWaiting;
		private int _timer;
		private int _cooldown;
		private bool _answeredPhone;

		/// <summary>
		/// Setup the phone manager.
		/// </summary>
		/// <param name="go">Phone GameObject.</param>
		public void Setup(GameObject go)
		{
			Instance = this;
			_gameObject = go;
			HookEvents();
		}

		/// <summary>
		/// Called once per frame.
		/// </summary>
		public void OnUpdate()
		{
			if (Input.GetKeyDown(KeyCode.K))
			{
				Logger.Log("Phone call triggered!");
				PhoneCall("SHIT1", 10);
			}

			// Timer to allow the FSM to start up before sending phone call.
			if (_callWaiting)
			{
				if (_timer >= 5)
				{
					_timer = 0;
					_callWaiting = false;
					_ringFsm.SendEvent("MP_State 4");
				}
				else
				{
					_timer++;
				}
			}

			// Detect when phone call has been answered.
			if (_gameObject.activeSelf)
			{
				if (_ringFsm.Fsm.GetFsmBool("Answer").Value && !_answeredPhone)
				{
					AnsweredPhoneCall();
					_answeredPhone = true;
				}
			}
			else if (_answeredPhone)
			{
				_answeredPhone = false;
			}
		}

		/// <summary>
		/// Hook phone related events.
		/// </summary>
		private void HookEvents()
		{
			_gameObject.SetActive(true);

			_ringFsm = Utils.GetPlaymakerScriptByName(_gameObject, "Ring");
			EventHook.Add(_ringFsm, "State 4", () =>
			{
				if (Network.NetManager.Instance.IsHost)
				{
					WritePhoneCall();
				}
				return false;
			});

			EventHook.Add(_ringFsm, "Disable phone", () => false);

			EventHook.AddWithSync(_ringFsm, "Thunder calls");

			_gameObject.SetActive(false);
		}

		/// <summary>
		/// Send phone call sync to other clients.
		/// </summary>
		private void WritePhoneCall()
		{
			// Cooldown required to stop event running multiple times causing pointless messages.
			if (_cooldown == 0)
			{
				_cooldown++;
				Network.Messages.PhoneMessage msg = new Network.Messages.PhoneMessage
				{
					topic = _ringFsm.Fsm.GetFsmString("Topic").Value,
					timesToRing = _ringFsm.Fsm.GetFsmInt("RandomTimes").Value
				};
				Network.NetManager.Instance.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			}
			else if (_cooldown != 0)
			{
				_cooldown++;
			}
			else if (_cooldown > 10)
			{
				_cooldown = 0;
			}
		}

		/// <summary>
		/// Called when a phone call is answered.
		/// </summary>
		private void AnsweredPhoneCall()
		{
			Logger.Debug("Phone call answered!");
			Network.Messages.PhoneMessage msg = new Network.Messages.PhoneMessage
			{
				topic = _ringFsm.Fsm.GetFsmString("Topic").Value,
				timesToRing = -1
			};
			Network.NetManager.Instance.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			MapManager.Instance.SyncDarts();
		}

		/// <summary>
		/// Set a phone call to trigger.
		/// </summary>
		/// <param name="topic">Topic of the call.</param>
		/// <param name="timesToRing">Amount of times the phone should ring.</param>
		public void PhoneCall(string topic, int timesToRing)
		{
			// Cancel call as it has been answered remotely.
			if (timesToRing == -1)
			{
				_ringFsm.SendEvent("MP_Disable phone");
				//gameObject.SetActive(false);
				Logger.Debug("Disabling phone as remote client has answered it!");
			}

			// Start phone ringing.
			if (!_gameObject.activeSelf)
			{
				_gameObject.SetActive(true);
				_ringFsm.Fsm.GetFsmString("Topic").Value = topic;
				_ringFsm.Fsm.GetFsmInt("RandomTimes").Value = timesToRing;
				_callWaiting = true;
			}
		}
	}
}
