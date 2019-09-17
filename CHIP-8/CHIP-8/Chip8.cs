using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CHIP_8
{
    public class Chip8
    {
        private byte[] Memory = new byte[4096];
        private byte[] Registers = new byte[16];
        private byte VF;
        private ushort I;

        private byte DelayTimer;
        private byte SoundTimer;

        private ushort PC = 0x200;
        private byte SP;
        private ushort[] Stack = new ushort[16];
        private byte[,] Display = new byte[64, 32];

        // implementation-specific variables
        private Key? LastKeyPressed = null;
        private readonly Random Random = new Random();
        private readonly Canvas Canvas;

        private readonly Dictionary<Key, byte> KeyMap = new Dictionary<Key, byte>()
        {
            { Key.D1, 0x1 },
            { Key.D2, 0x2 },
            { Key.D3, 0x3 },
            { Key.D4, 0xC },
            { Key.Q, 0x4 },
            { Key.W, 0x5 },
            { Key.E, 0x6 },
            { Key.R, 0xD },
            { Key.A, 0x7 },
            { Key.S, 0x8 },
            { Key.D, 0x9 },
            { Key.F, 0xE },
            { Key.Z, 0xA },
            { Key.X, 0x0 },
            { Key.C, 0xB },
            { Key.V, 0xF },
        };

        public Chip8(Canvas canvas)
        {
            Canvas = canvas;
        }

        public void Initialize()
        {
            InitializeSprites();

            // start 60 Hz timer task
            Task.Run(async () =>
            {
                while (true)
                {
                    if (DelayTimer > 0)
                    {
                        DelayTimer -= 1;
                    }

                    if (SoundTimer > 0)
                    {
                        SoundTimer -= 1;
                        // Console.Beep();
                    }

                    await Task.Delay(1000 / 60);
                }
            });
        }

        public void Load(string filename)
        {
            var rom = File.ReadAllBytes("../../roms/" + filename);

            for (int i = 0; i < rom.Length; i++)
            {
                Memory[0x200 + i] = rom[i];
            }
        }

        public void KeyPressed(Key key)
        {
            LastKeyPressed = key;
        }

        public async Task Execute()
        {
            var blah = Memory[PC];
            var a = blah << 8;
            var b = Memory[PC + 1];

            ushort instruction = (ushort)(a + b);

            // 00E0 - CLS
            if (instruction == 0x00E0)
            {
                ClearDisplay();
            }

            // 00EE RET
            if (instruction == 0x00EE)
            {
                PC = Stack[SP--];
            }

            ushort nnn = (ushort)(instruction & 0x0FFF);
            byte x = (byte)((instruction & 0x0F00) >> 8);
            byte y = (byte)((instruction & 0x00F0) >> 4);
            byte kk = (byte)(instruction & 0x00FF);
            byte n = (byte)(instruction & 0x000F);

            // 1nnn - JP addr
            if ((instruction & 0xF000) == 0x1000)
            {
                PC = nnn;
                return;
            }

            // 2nnn - CALL addr
            if ((instruction & 0xF000) == 0x2000)
            {
                Stack[++SP] = PC;
            }

            // 3xkk - SE Vx, byte
            if ((instruction & 0xF000) == 0x3000)
            {
                if (Registers[x] == kk)
                {
                    PC += 2;
                }
            }

            // 4xkk - SNE Vx, byte
            if ((instruction & 0xF000) == 0x4000)
            {
                if (Registers[x] != kk)
                {
                    PC += 2;
                }
            }

            // 5xy0 - SE Vx, Vy
            if ((instruction & 0xF00F) == 0x5000)
            {
                if (Registers[x] != Registers[y])
                {
                    PC += 2;
                }
            }

            // 6xkk - LD Vx, byte
            if ((instruction & 0xF000) == 0x6000)
            {
                Registers[x] = kk;
            }

            // 7xkk - ADD Vx, byte
            if ((instruction & 0xF000) == 0x7000)
            {
                Registers[x] += kk;
            }

            // 8xy0 - LD Vx, Vy
            if ((instruction & 0xF00F) == 0x8000)
            {
                Registers[x] = Registers[y];
            }

            // 8xy1 - OR Vx, Vy
            if ((instruction & 0xF00F) == 0x8001)
            {
                Registers[x] |= Registers[y];
            }

            // 8xy2 - AND Vx, Vy
            if ((instruction & 0xF00F) == 0x8002)
            {
                Registers[x] &= Registers[y];
            }

            // 8xy3 - XOR Vx, Vy
            if ((instruction & 0xF00F) == 0x8003)
            {
                Registers[x] ^= Registers[y];
            }

            // 8xy4 - ADD Vx, Vy
            if ((instruction & 0xF00F) == 0x8004)
            {
                int result = Registers[x] + Registers[y];

                if (result > 256)
                {
                    VF = 1;
                }
                else
                {
                    VF = 0;
                }

                Registers[x] = (byte)result;
            }

            // 8xy5 - SUB Vx, Vy
            if ((instruction & 0xF00F) == 0x8005)
            {
                if (Registers[x] > Registers[y])
                {
                    VF = 1;
                }
                else
                {
                    VF = 0;
                }

                Registers[x] -= Registers[y];
            }

            // 8xy6 - SHR Vx {, Vy}
            if ((instruction & 0xF00F) == 0x8006)
            {
                if ((Registers[x] & 1) == 1)
                {
                    VF = 1;
                }
                else
                {
                    VF = 0;
                }

                Registers[x] /= 2;
            }

            // 8xy7 - SUBN Vx, Vy
            if ((instruction & 0xF00F) == 0x8007)
            {
                if (Registers[y] > Registers[x])
                {
                    VF = 1;
                }
                else
                {
                    VF = 0;
                }

                Registers[x] = (byte)(Registers[y] - Registers[x]);
            }

            // 8xyE - SHL Vx {, Vy}
            if ((instruction & 0xF00F) == 0x800E)
            {
                if ((Registers[x] & 0x8000) == 0x8000)
                {
                    VF = 1;
                }
                else
                {
                    VF = 0;
                }

                Registers[x] *= 2;
            }

            // 9xy0 - SNE Vx, Vy
            if ((instruction & 0xF00F) == 0x9000)
            {
                if (Registers[x] != Registers[y])
                {
                    PC += 2;
                }
            }

            // Annn - LD I, addr
            if ((instruction & 0xF000) == 0xA000)
            {
                I = nnn;
            }

            // Bnnn - JP V0, addr
            if ((instruction & 0xF000) == 0xB000)
            {
                PC = (ushort)(nnn + Registers[0]);
            }

            // Cxkk - RND Vx, byte
            if ((instruction & 0xF000) == 0xC000)
            {
                Registers[x] = (byte)(Random.Next(256) & kk);
            }

            // Dxyn - DRW Vx, Vy, nibble
            if ((instruction & 0xF000) == 0xD000)
            {
                bool collision = false;

                int xStart = Registers[x];
                int yStart = Registers[y];

                for (int yPos = 0; yPos < n; yPos++)
                {
                    byte row = Memory[I + yPos];
                    for (int xPos = 7; xPos >= 0; xPos--)
                    {
                        byte bit = (byte)(row & 1);
                        row >>= 1;

                        int theX = (xStart + xPos) % Display.GetLength(0);
                        int theY = (yStart + yPos) % Display.GetLength(1);

                        if (Display[theX, theY] != bit)
                        {
                            collision = true;
                        }

                        Display[theX, theY] = bit;
                    }
                }

                if (collision)
                {
                    VF = 1;
                }
                else
                {
                    VF = 0;
                }
            }

            // Ex9E - SKP Vx
            if ((instruction & 0xF0FF) == 0xE09E)
            {
                byte keyPress = await ReadKey();
                if (keyPress == Registers[x])
                {
                    PC += 2;
                }
            }

            // ExA1 - SKNP Vx
            if ((instruction & 0xF0FF) == 0xE0A1)
            {
                byte keyPress = await ReadKey();
                if (keyPress != Registers[x])
                {
                    PC += 2;
                }
            }

            // Fx07 - LD Vx, DT
            if ((instruction & 0xF0FF) == 0xF007)
            {
                Registers[x] = DelayTimer;
            }

            // Fx0A - LD Vx, K
            if ((instruction & 0xF0FF) == 0xF00A)
            {
                Registers[x] = await ReadKey();
            }

            // Fx15 - LD DT, Vx
            if ((instruction & 0xF0FF) == 0xF015)
            {
                DelayTimer = Registers[x];
            }

            // Fx18 - LD ST, Vx
            if ((instruction & 0xF0FF) == 0xF018)
            {
                SoundTimer = Registers[x];
            }

            // Fx1E - ADD I, Vx
            if ((instruction & 0xF0FF) == 0xF01E)
            {
                I += Registers[x];
            }

            // Fx29 - LD F, Vx
            if ((instruction & 0xF0FF) == 0xF029)
            {
                I = (byte)(Registers[x] * 5);
            }

            // Fx33 - LD B, Vx
            if ((instruction & 0xF0FF) == 0xF033)
            {
                byte bcd = Registers[x];

                Memory[I + 2] = (byte)(bcd % 10);
                bcd /= 10;
                Memory[I + 1] = (byte)(bcd % 10);
                bcd /= 10;
                Memory[I] = (byte)(bcd / 100);
            }

            // Fx55 - LD [I], Vx
            if ((instruction & 0xF0FF) == 0xF055)
            {
                for (int idx = 0; idx < x; idx++)
                {
                    Memory[I + idx] = Registers[idx];
                }
            }

            // Fx65 - LD Vx, [I]
            if ((instruction & 0xF0FF) == 0xF065)
            {
                for (int idx = 0; idx < x; idx++)
                {
                    Registers[idx] = Memory[I + idx];
                }
            }

            PC += 2;

            Draw();
        }

        private void Draw()
        {
            Canvas.Children.Clear();

            Canvas.Background = new SolidColorBrush(Colors.Black);

            for (int x = 0; x < Display.GetLength(0); x++)
            {
                for (int y = 0; y < Display.GetLength(1); y++)
                {
                    if (Display[x, y] == 1)
                    {
                        Rectangle rec = new Rectangle();

                        Canvas.SetTop(rec, y * 10);
                        Canvas.SetLeft(rec, x * 10);

                        rec.Width = 10;
                        rec.Height = 10;
                        rec.Fill = new SolidColorBrush(Colors.White);

                        Canvas.Children.Add(rec);
                    }
                }
            }
        }

        private void ClearDisplay()
        {
            for (int col = 0; col < Display.GetLength(0); col++)
            {
                for (int row = 0; row < Display.GetLength(1); row++)
                {
                    Display[col, row] = 0;
                }
            }
        }

        private async Task<byte> ReadKey()
        {
            /*
            Keypad                   Keyboard
            +-+-+-+-+                +-+-+-+-+
            |1|2|3|C|                |1|2|3|4|
            +-+-+-+-+                +-+-+-+-+
            |4|5|6|D|                |Q|W|E|R|
            +-+-+-+-+       =>       +-+-+-+-+
            |7|8|9|E|                |A|S|D|F|
            +-+-+-+-+                +-+-+-+-+
            |A|0|B|F|                |Z|X|C|V|
            +-+-+-+-+                +-+-+-+-+
            */

            while (true)
            {
                if (LastKeyPressed.HasValue && KeyMap.ContainsKey(LastKeyPressed.Value))
                {
                    var ret = KeyMap[LastKeyPressed.Value];
                    LastKeyPressed = null;
                    return ret;
                }

                await Task.Delay(1);
            }
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
