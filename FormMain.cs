using Microsoft.Win32;
using ScreenBoundaries.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenBoundaries
{
	public partial class FormMain : Form
	{
		private NotifyIcon m_tray;

		private bool m_firstStartup;
		private bool m_quit;

		public FormMain()
		{
			InitializeComponent();

			m_tray = new NotifyIcon();
			m_tray.Icon = Icon;
			m_tray.Text = "Nimble Screen Boundaries";
			m_tray.Visible = true;
			m_tray.DoubleClick += (o, e) => Show();

			LoadSettings();
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);

			if (!m_firstStartup) {
				m_firstStartup = true;
				foreach (var arg in Environment.GetCommandLineArgs()) {
					if (arg == "/startup") {
						Hide();
					}
				}
			} else {
				LoadSettings();
			}
		}

		void LoadSettings()
		{
			numSafeArea.Value = Settings.Default.SafeArea;

			var runRegKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
			checkStartWithWindows.Checked = runRegKey.GetValue("NimbleScreenBoundaries") != null;
		}

		void SaveSettings()
		{
			Settings.Default.SafeArea = (int)numSafeArea.Value;
			Settings.Default.Save();

			if (checkStartWithWindows.Checked) {
				var runRegKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
				runRegKey.SetValue("NimbleScreenBoundaries", "\"" + Application.ExecutablePath + "\" /startup");
			}
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);

			Application.AddMessageFilter(new MouseFlicker(Handle));
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			if (!m_quit) {
				e.Cancel = true;
				SaveSettings();
				Hide();
			}

			base.OnClosing(e);
		}

		private void buttonOK_Click(object sender, EventArgs e)
		{
			SaveSettings();
			Hide();
		}

		private void buttonQuit_Click(object sender, EventArgs e)
		{
			m_tray.Visible = false;
			m_tray.Dispose();
			m_tray = null;

			m_quit = true;

			Close();
		}
	}
}
