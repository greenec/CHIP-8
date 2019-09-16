namespace Chip8
{
    class Program
    {
        static void Main(string[] args)
        {
            var chip8 = new Chip8();

            chip8.Initialize();

            chip8.Load("helloworld.rom");

            while (true)
            {
                chip8.Execute();
            }
        }
    }
}
