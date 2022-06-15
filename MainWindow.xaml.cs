using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WinVolume
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public void ChangeVolumeBar()
        {

        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            var anim = new DoubleAnimation(0, (Duration)TimeSpan.FromSeconds(0.5));
            anim.Completed += (s, _) => thisClose();
            this.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void thisClose()
        {
            App.isShowWindow = false;
            this.Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var anim = new DoubleAnimation(0, 0.7, (Duration)TimeSpan.FromSeconds(0.5));
            this.BeginAnimation(UIElement.OpacityProperty, anim);
            App.isShowWindow = true;
            await Task.Delay(1500);
            this.Close();
        }
    }
}
