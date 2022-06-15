using System;
using System.Threading.Tasks;
using System.Windows;

namespace WinVolume
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        KeyboardHookManager keyboardHookManager = new KeyboardHookManager();
        public static bool isShowWindow = false;
        System.Windows.Forms.NotifyIcon icon = new System.Windows.Forms.NotifyIcon();
        System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
        MainWindow window = new MainWindow();
        protected override void OnStartup(StartupEventArgs e)
        {
            icon.Icon = new System.Drawing.Icon(@"C:\Users\GrapeApple\source\repos\WinVolume\icon.ico");
            icon.Visible = true;
            icon.Text = "WinVolume";
            System.Windows.Forms.ToolStripMenuItem menuItem = new System.Windows.Forms.ToolStripMenuItem();
            menuItem.Text = "&Exit";
            menuItem.Click += (s, e) =>
            {
                Current.Shutdown();
            };
            menu.Items.Add(menuItem);
            icon.ContextMenuStrip = menu;

            keyboardHookManager.Start();
            //F10
            keyboardHookManager.RegisterHotkey(ModifierKeys.Control, 0x79, () =>
            {
                if (!isShowWindow)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        window.Show();
                    });
                }
                AudioManager.ToggleMasterVolumeMute();
            });
            //F11
            keyboardHookManager.RegisterHotkey(ModifierKeys.Control, 0x7A, () =>
            {
                if (!isShowWindow)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        window.Show();
                    });
                }
                if (AudioManager.GetMasterVolumeMute())
                {
                    AudioManager.ToggleMasterVolumeMute();
                }
                AudioManager.StepMasterVolume(-2);
            });
            //F12
            keyboardHookManager.RegisterHotkey(ModifierKeys.Control, 0x7B, () =>
            {
                if (!isShowWindow)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        window.Show();
                    });
                }
                if (AudioManager.GetMasterVolumeMute())
                {
                    AudioManager.ToggleMasterVolumeMute();
                }
                AudioManager.StepMasterVolume(2);
            });
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            icon.Visible = false;
            keyboardHookManager.UnregisterAll();
        }
    }
}
