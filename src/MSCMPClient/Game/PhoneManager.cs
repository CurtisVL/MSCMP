using System;
using UnityEngine;

namespace MSCMP.Game
{
	/// <summary>
	/// Manages various events for the phone.
	/// </summary>
	class PhoneManager
	{
		GameObject gameObject;
		PlayMakerFSM ringFSM;
		public static PhoneManager Instance;

		bool callWaiting = false;
		int timer = 0;
		int cooldown = 0;

		bool answeredPhone = false;

		/// <summary>
		/// Setup the phone manager.
		/// </summary>
		/// <param name="go">Phone GameObject.</param>
		public void Setup(GameObject go) {
			Instance = this;
			gameObject = go;
			HookEvents();
		}

		/// <summary>
		/// Called once per frame.
		/// </summary>
		public void OnUpdate() {
			if (Input.GetKeyDown(KeyCode.K)) {
				Logger.Log("Phone call triggered!");
				PhoneCall("SHIT1", 10);
			}

			// Timer to allow the FSM to start up before sending phone call.
			if (callWaiting) {
				if (timer >= 5) {
					timer = 0;
					callWaiting = false;
					ringFSM.SendEvent("MP_State 4");
				}
				else {
					timer++;
				}
			}

			// Detect when phone call has been answered.
			if (gameObject.activeSelf) {
				if (ringFSM.Fsm.GetFsmBool("Answer").Value && !answeredPhone) {
					AnsweredPhoneCall();
					answeredPhone = true;
				}
			}
			else if (answeredPhone) {
				answeredPhone = false;
			}
		}

		/// <summary>
		/// Hook phone related events.
		/// </summary>
		void HookEvents() {
			gameObject.SetActive(true);

			ringFSM = Utils.GetPlaymakerScriptByName(gameObject, "Ring");
			EventHook.Add(ringFSM, "State 4", new Func<bool>(() => {
				if (Network.NetManager.Instance.IsHost) {
					WritePhoneCall();
				}
				return false;
			}));

			EventHook.Add(ringFSM, "Disable phone", new Func<bool>(() => {
				return false;
			}));

			EventHook.AddWithSync(ringFSM, "Thunder calls");

			gameObject.SetActive(false);
		}

		/// <summary>
		/// Send phone call sync to other clients.
		/// </summary>
		void WritePhoneCall() {
			// Cooldown required to stop event running multiple times causing pointless messages.
			if (cooldown == 0) {
				cooldown++;
				Network.Messages.PhoneMessage msg = new Network.Messages.PhoneMessage();
				msg.topic = ringFSM.Fsm.GetFsmString("Topic").Value;
				msg.timesToRing = ringFSM.Fsm.GetFsmInt("RandomTimes").Value;
				Network.NetManager.Instance.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			}
			else if (cooldown != 0) {
				cooldown++;
			}
			else if (cooldown > 10) {
				cooldown = 0;
			}
		}

		/// <summary>
		/// Called when a phone call is answered.
		/// </summary>
		void AnsweredPhoneCall() {
			Logger.Debug("Phone call answered!");
			Network.Messages.PhoneMessage msg = new Network.Messages.PhoneMessage();
			msg.topic = ringFSM.Fsm.GetFsmString("Topic").Value;
			msg.timesToRing = -1;
			Network.NetManager.Instance.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			MapManager.Instance.SyncDarts();
		}

		/// <summary>
		/// Set a phone call to trigger.
		/// </summary>
		/// <param name="topic">Topic of the call.</param>
		/// <param name="timesToRing">Amount of times the phone should ring.</param>
		public void PhoneCall(string topic, int timesToRing) {
			// Cancel call as it has been answered remotely.
			if (timesToRing == -1) {
				ringFSM.SendEvent("MP_Disable phone");
				//gameObject.SetActive(false);
				Logger.Debug("Disabling phone as remote client has answered it!");
			}

			// Start phone ringing.
			if (!gameObject.activeSelf) {
				gameObject.SetActive(true);
				ringFSM.Fsm.GetFsmString("Topic").Value = topic;
				ringFSM.Fsm.GetFsmInt("RandomTimes").Value = timesToRing;
				callWaiting = true;
			}
		}
	}
}
