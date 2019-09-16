using System.Windows;
using System.Windows.Controls;

namespace CHIP_8
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class EmulatorWindow : Window
    {
        private Chip8 Chip8;

        public EmulatorWindow()
        {
            InitializeComponent();

            Chip8 = new Chip8(Canvas);
        }

        public async void Run()
        {
            Chip8.Initialize();

            Chip8.Load("helloworld.rom");

            while (true)
            {
                await Chip8.Execute();
            }
        }

        private void Canvas_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Chip8.KeyPressed(e.Key);
        }

        private void Canvas_Loaded(object sender, RoutedEventArgs e)
        {
            Canvas.Focus();
        }
    }
}
