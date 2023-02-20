using System.Windows;

namespace CHIP_8
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class EmulatorWindow : Window
    {
        private readonly Chip8 Chip8;

        public EmulatorWindow()
        {
            InitializeComponent();

            Chip8 = new Chip8();

            Canvas.Source = Chip8.WriteableBitmap;
        }

        public async void Run()
        {
            Chip8.Initialize();

            Chip8.LoadRom("space_invaders.ch8");

            await Chip8.Run();
        }

        private void Canvas_Loaded(object sender, RoutedEventArgs e)
        {
            Canvas.Focus();
        }

        private void Canvas_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Chip8.KeyDown(e.Key);
        }

        private void Canvas_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Chip8.KeyUp(e.Key);
        }
    }
}
