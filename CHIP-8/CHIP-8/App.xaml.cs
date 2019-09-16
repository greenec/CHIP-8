using System.Windows;

namespace CHIP_8
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void OnStartup(object sender, StartupEventArgs e)
        {
            EmulatorWindow emulatorWindow = new EmulatorWindow();

            this.MainWindow = emulatorWindow;

            emulatorWindow.Show();
            emulatorWindow.Run();
        }
    }
}
