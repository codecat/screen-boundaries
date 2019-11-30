using Microsoft.Win32;
using ScreenBoundaries.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenBoundaries
{
	public class MouseFlicker : IMessageFilter
	{
		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		private struct ScreenPair
		{
			public Rectangle m_realBounds;
			public float m_scale;
		}

		private List<ScreenPair> m_screens = new List<ScreenPair>();

		public MouseFlicker(IntPtr handle)
		{
			var devices = new Native.RAWINPUTDEVICE[1];
			devices[0].UsagePage = Native.HIDUsagePage.Generic;
			devices[0].Usage = Native.HIDUsage.Mouse;
			devices[0].Flags = Native.RawInputDeviceFlags.InputSink;
			devices[0].WindowHandle = handle;

			if (!Native.RegisterRawInputDevices(devices, devices.Length, Marshal.SizeOf(devices[0]))) {
				Logger.Error("Failed to register raw input device");
			} else {
				Logger.Info("Raw input device registered");
			}

			UpdateScreens();

			SystemEvents.DisplaySettingsChanged += (o, e) => {
				Logger.Warn("Display settings changed");
				UpdateScreens();
			};
		}

		void UpdateScreens()
		{
			Logger.Info("Updating screens");

			m_screens.Clear();

			var screens = Screen.AllScreens.OrderBy(s => s.Bounds.X).ToArray();
			for (int i = 0; i < screens.Length; i++) {
				Screen prevScreen = i == 0 ? null : screens[i - 1];
				Screen screen = screens[i];
				Screen nextScreen = i == screens.Length - 1 ? null : screens[i + 1];

				var dm = new Native.DEVMODE();
				dm.dmSize = (short)Marshal.SizeOf<Native.DEVMODE>();
				Native.EnumDisplaySettings(screen.DeviceName, -1, ref dm);

				float dpiScale = dm.dmPelsHeight / (float)screen.Bounds.Height;

				Logger.Debug("Screen {0}, \"{1}\", {5} x {6} -> {2} x {3} (@ {4}):", i, screen.DeviceName, screen.Bounds.Width, screen.Bounds.Height, dpiScale, screen.Bounds.X / dpiScale, screen.Bounds.Y / dpiScale);

				if (prevScreen != null && screen.Bounds.Height > prevScreen.Bounds.Height) {
					Logger.Debug("  Screen height {0} bigger than left screen height {1}", screen.Bounds.Height, prevScreen.Bounds.Height);
				}

				if (nextScreen != null && screen.Bounds.Height > nextScreen.Bounds.Height) {
					Logger.Debug("  Screen height {0} bigger than right screen height {1}", screen.Bounds.Height, nextScreen.Bounds.Height);
				}

				m_screens.Add(new ScreenPair() {
					m_scale = dpiScale,
					m_realBounds = new Rectangle(screen.Bounds.X, screen.Bounds.Y, dm.dmPelsWidth, dm.dmPelsHeight)
				});
			}
		}

		int GetScreenIndex(Point p)
		{
			for (int i = 0; i < m_screens.Count; i++) {
				var screen = m_screens[i];
				if (screen.m_realBounds.Contains(p)) {
					return i;
				}
			}
			return -1;
		}

		int GetNearestScreenIndexHorizontal(Point p)
		{
			for (int i = 0; i < m_screens.Count; i++) {
				var screen = m_screens[i];
				if (p.X < screen.m_realBounds.X + screen.m_realBounds.Width) {
					return i;
				}
			}
			return m_screens.Count - 1;
		}

		public bool PreFilterMessage(ref Message m)
		{
			if (m.Msg != 0x00FF) {
				return false;
			}

			Native.RawInput input;
			int size = Marshal.SizeOf<Native.RawInput>();

			Native.GetRawInputData(m.LParam, Native.RawInputCommand.Input, out input, ref size, Marshal.SizeOf<Native.RawInputHeader>());

			if (input.Mouse.Flags != Native.RawMouseFlags.MoveRelative) {
				return false;
			}

			var pos = Cursor.Position;

			int screenIndex = GetScreenIndex(pos);
			Debug.Assert(screenIndex != -1);
			var screen = m_screens[screenIndex];

			var relativePos = new Point(pos.X - screen.m_realBounds.X, pos.Y - screen.m_realBounds.Y);
			relativePos.X = (int)(relativePos.X * screen.m_scale);
			relativePos.Y = (int)(relativePos.Y * screen.m_scale);

			pos.X = screen.m_realBounds.X + relativePos.X;
			pos.Y = screen.m_realBounds.Y + relativePos.Y;

			var newPos = new Point(pos.X + input.Mouse.LastX, pos.Y + input.Mouse.LastY);

			int newScreenIndex = GetScreenIndex(newPos);
			if (newScreenIndex != -1) {
				return false;
			}

			newScreenIndex = GetNearestScreenIndexHorizontal(newPos);
			if (newScreenIndex != screenIndex) {
				var newScreen = m_screens[newScreenIndex];
				var newScreenBounds = newScreen.m_realBounds;

				int safeArea = Settings.Default.SafeArea;

				if (newPos.Y < newScreenBounds.Y) {
					newPos.Y = newScreenBounds.Y + safeArea;
				} else if (newPos.Y > newScreenBounds.Y + newScreenBounds.Height) {
					newPos.Y = newScreenBounds.Y + newScreenBounds.Height - 1 - safeArea;
				}

				Cursor.Position = newPos;

				Logger.Debug("Trying to go off-screen from {0} to {1}, teleport to {2}, {3}!", screenIndex, newScreenIndex, newPos.X, newPos.Y);
			}

			return false;
		}
	}
}
