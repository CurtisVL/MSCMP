using UnityEngine;

namespace MSCMP.Utilities
{
	internal static class ImguiUtils
	{
		/// <summary>
		/// The 1x1 plain white pixel texture.
		/// </summary>
		private static readonly Texture2D FillText = new Texture2D(1, 1);

		/// <summary>
		/// Small label style.
		/// </summary>
		private static readonly GUIStyle SmallLabelStyle = new GUIStyle();

		/// <summary>
		/// Setup all rendering objects.
		/// </summary>
		public static void Setup()
		{
			SmallLabelStyle.fontSize = 11;

			FillText.SetPixel(0, 0, Color.white);
			FillText.wrapMode = TextureWrapMode.Repeat;
			FillText.Apply();
		}

		/// <summary>
		/// Draw plain color rectangle.
		/// </summary>
		/// <param name="rct">Where rectangle should be drawn.</param>
		public static void DrawPlainColorRect(Rect rct)
		{
			if (FillText != null)
			{
				GUI.DrawTexture(rct, FillText);
			}
		}

		/// <summary>
		/// Draw small label.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="rct">The rectangle where label should be drawn.</param>
		/// <param name="color">Color of the label.</param>
		/// <param name="shadow">Should the method also draw shadow?</param>
		public static void DrawSmallLabel(string text, Rect rct, Color color, bool shadow = false)
		{
			if (shadow)
			{
				rct.y += 1;
				rct.x += 1;
				SmallLabelStyle.normal.textColor = Color.black;
				GUI.Label(rct, text, SmallLabelStyle);
				rct.y -= 1;
				rct.x -= 1;
			}
			SmallLabelStyle.normal.textColor = color;
			GUI.Label(rct, text, SmallLabelStyle);
		}
	}
}
