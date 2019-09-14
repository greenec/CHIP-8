using System;

namespace Chip8
{
    public class Chip8
    {
        private byte[] Memory = new byte[4096];
        private byte[] Registers = new byte[16];
        private short I;

        // TODO: delay and sound timers

        private short PC;
        private byte SP;
        private short[] Stack = new short[16];
        private bool[,] Display = new bool[64, 32];

        public void Initialize()
        {
            InitializeSprites();
        }

        private void InitializeSprites()
        {
            var sprites = new string[]
            {
                "F0909090F0", // 0
                "2060202070", // 1
                "F010F080F0", // 2
                "F010F010F0", // 3
                "9090F01010", // 4
                "F080F010F0", // 5
                "F080F090F0", // 6
                "F010204040", // 7
                "F090F090F0", // 8
                "F090F010F0", // 9
                "F090F09090", // A
                "E090E090E0", // B
                "F0808080F0", // C
                "E0909090E0", // D
                "F080F080F0", // E
                "F080F08080", // F
            };

            int idx = 0;
            foreach (var spriteHex in sprites)
            {
                // load one byte at a time into memory
                for (int i = 0; i < spriteHex.Length; i += 2)
                {
                    Memory[idx++] = Convert.ToByte(spriteHex.Substring(i, 2), 16);
                }
            }
        }
    }
}
