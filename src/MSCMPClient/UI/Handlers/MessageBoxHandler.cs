using UnityEngine;
using UnityEngine.UI;

namespace MSCMP.UI.Handlers
{
	/// <summary>
	/// Handle of the message box window.
	/// </summary>
	internal class MessageBoxHandler 
		: MonoBehaviour
	{
		/// <summary>
		/// Delegate of the callback called when window is closed.
		/// </summary>
		public delegate void OnMessageBoxCloseEvent();

		private OnMessageBoxCloseEvent _onMessageBoxClose;

		/// <summary>
		/// Setup the handler after message box spawn.
		/// </summary>
		private void Start()
		{
			Button btn = transform.FindChild("OKButton").GetComponent<Button>();
			btn.onClick.AddListener(() =>
			{
				Close();

				if (_onMessageBoxClose != null)
				{
					_onMessageBoxClose();
					_onMessageBoxClose = null;
				}
			});
		}

		/// <summary>
		/// Close currently active message box.
		/// </summary>
		public void Close()
		{
			gameObject.SetActive(false);
			Mpgui.Instance.ShowCursor(false);
		}

		/// <summary>
		/// Show message box with given text.
		/// </summary>
		/// <param name="text">The text to show.</param>
		/// <param name="onClose">The callback that will be closed when OK button is pressed.</param>
		/// <returns>true if message box was showed false otherwise</returns>
		public void Show(string text, OnMessageBoxCloseEvent onClose = null)
		{
			// Allow only one message box.
			if (gameObject.activeSelf)
			{
				return;
			}
			_onMessageBoxClose = onClose;
			transform.FindChild("Text").gameObject.GetComponent<Text>().text = text;
			gameObject.SetActive(true);
			Mpgui.Instance.ShowCursor(true);
		}
	}
}
