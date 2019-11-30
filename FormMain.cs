using Microsoft.Win32;
using ScreenBoundaries.Properties;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace ScreenBoundaries
{
	public partial class FormMain : Form
	{
		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		private NotifyIcon m_tray;

		private bool m_firstStartup;
		private bool m_quit;

		public FormMain()
		{
			InitializeComponent();

			Logger.Info("Initializing");

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
			Logger.Info("Loading settings");

			numSafeArea.Value = Settings.Default.SafeArea;

			var runRegKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
			checkStartWithWindows.Checked = runRegKey.GetValue("NimbleScreenBoundaries") != null;
		}

		void SaveSettings()
		{
			Logger.Info("Saving settings");

			Settings.Default.SafeArea = (int)numSafeArea.Value;
			Settings.Default.Save();

			var runRegKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
			if (checkStartWithWindows.Checked) {
				runRegKey.SetValue("NimbleScreenBoundaries", "\"" + Application.ExecutablePath + "\" /startup");
			} else {
				runRegKey.DeleteValue("NimbleScreenBoundaries", false);
			}
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);

			Logger.Debug("Creating message filter");

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
			SaveSettings();

			m_tray.Visible = false;
			m_tray.Dispose();
			m_tray = null;

			m_quit = true;

			Logger.Info("Quitting");
			Close();
		}
	}
}
