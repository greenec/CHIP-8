using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CHIP_8
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class EmulatorWindow : Window
    {
        private readonly Chip8 Chip8;

        private readonly WriteableBitmap WriteableBitmap;

        public EmulatorWindow()
        {
            InitializeComponent();

            WriteableBitmap = new WriteableBitmap(64, 32, 96, 96, PixelFormats.Bgra32, null);

            img.Source = WriteableBitmap;

            Chip8 = new Chip8(WriteableBitmap);
        }

        public async void Run()
        {
            Chip8.Initialize();

            Chip8.Load("space_invaders.ch8");

            while (true)
            {
                await Chip8.Execute();
            }
        }

        private void Canvas_Loaded(object sender, RoutedEventArgs e)
        {
            img.Focus();
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
