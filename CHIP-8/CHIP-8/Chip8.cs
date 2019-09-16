using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Chip8
{
    public class Chip8
    {
        private byte[] Memory = new byte[4096];
        private byte[] Registers = new byte[16];
        private byte VF;
        private ushort I;

        // TODO: delay and sound timers
        private byte DelayTimer;
        private byte SoundTimer;

        private ushort PC = 0x200;
        private byte SP;
        private ushort[] Stack = new ushort[16];
        private byte[,] Display = new byte[64, 32];

        ushort Instruction;

        Random Random = new Random();

        private Dictionary<ConsoleKey, byte> KeyMap = new Dictionary<ConsoleKey, byte>()
        {
            { ConsoleKey.D1, 0x1 },
            { ConsoleKey.D2, 0x2 },
            { ConsoleKey.D3, 0x3 },
            { ConsoleKey.D4, 0xC },
            { ConsoleKey.Q, 0x4 },
            { ConsoleKey.W, 0x5 },
            { ConsoleKey.E, 0x6 },
            { ConsoleKey.R, 0xD },
            { ConsoleKey.A, 0x7 },
            { ConsoleKey.S, 0x8 },
            { ConsoleKey.D, 0x9 },
            { ConsoleKey.F, 0xE },
            { ConsoleKey.Z, 0xA },
            { ConsoleKey.X, 0x0 },
            { ConsoleKey.C, 0xB },
            { ConsoleKey.V, 0xF },
        };

        public void Initialize()
        {
            Console.WindowWidth = Display.GetLength(0);
            Console.WindowHeight = Display.GetLength(1) + 2;
            Console.CursorVisible = false;

            InitializeSprites();

            // start timer task
            Task.Run(() =>
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

                    Task.Delay(1000 / 60);
                }
            });
        }

        public void Load(string filename)
        {
            var rom = File.ReadAllBytes("../../../roms/" + filename);

            for (int i = 0; i < rom.Length; i++)
            {
                Memory[0x200 + i] = rom[i];
            }
        }

        public void Execute()
        {
            var blah = Memory[PC];
            var a = blah << 8;
            var b = Memory[PC + 1];

            Instruction = (ushort)(a + b);

            // 00E0 - CLS
            if (Instruction == 0x00E0)
            {
                ClearDisplay();
            }

            // 00EE RET
            if (Instruction == 0x00EE)
            {
                PC = Stack[SP--];
            }

            ushort nnn = (ushort)(Instruction & 0x0FFF);
            byte x = (byte)((Instruction & 0x0F00) >> 8);
            byte y = (byte)((Instruction & 0x00F0) >> 4);
            byte kk = (byte)(Instruction & 0x00FF);
            byte n = (byte)(Instruction & 0x000F);

            // 1nnn - JP addr
            if ((Instruction & 0xF000) == 0x1000)
            {
                PC = (ushort)(Instruction & 0x0FFF);
            }

            // 2nnn - CALL addr
            if ((Instruction & 0xF000) == 0x2000)
            {
                Stack[++SP] = PC;
            }

            // 3xkk - SE Vx, byte
            if ((Instruction & 0xF000) == 0x3000)
            {
                if (Registers[x] == kk)
                {
                    PC += 2;
                }
            }

            // 4xkk - SNE Vx, byte
            if ((Instruction & 0xF000) == 0x4000)
            {
                if (Registers[x] != kk)
                {
                    PC += 2;
                }
            }

            // 5xy0 - SE Vx, Vy
            if ((Instruction & 0xF00F) == 0x5000)
            {
                if (Registers[x] != Registers[y])
                {
                    PC += 2;
                }
            }

            // 6xkk - LD Vx, byte
            if ((Instruction & 0xF000) == 0x6000)
            {
                Registers[x] = kk;
            }

            // 7xkk - ADD Vx, byte
            if ((Instruction & 0xF000) == 0x7000)
            {
                Registers[x] += kk;
            }

            // 8xy0 - LD Vx, Vy
            if ((Instruction & 0xF00F) == 0x8000)
            {
                Registers[x] = Registers[y];
            }

            // 8xy1 - OR Vx, Vy
            if ((Instruction & 0xF00F) == 0x8001)
            {
                Registers[x] |= Registers[y];
            }

            // 8xy2 - AND Vx, Vy
            if ((Instruction & 0xF00F) == 0x8002)
            {
                Registers[x] &= Registers[y];
            }

            // 8xy3 - XOR Vx, Vy
            if ((Instruction & 0xF00F) == 0x8003)
            {
                Registers[x] ^= Registers[y];
            }

            // 8xy4 - ADD Vx, Vy
            if ((Instruction & 0xF00F) == 0x8004)
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
            if ((Instruction & 0xF00F) == 0x8005)
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
            if ((Instruction & 0xF00F) == 0x8006)
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
            if ((Instruction & 0xF00F) == 0x8007)
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
            if ((Instruction & 0xF00F) == 0x800E)
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
            if ((Instruction & 0xF00F) == 0x9000)
            {
                if (Registers[x] != Registers[y])
                {
                    PC += 2;
                }
            }

            // Annn - LD I, addr
            if ((Instruction & 0xF000) == 0xA000)
            {
                I = nnn;
            }

            // Bnnn - JP V0, addr
            if ((Instruction & 0xF000) == 0xB000)
            {
                PC = (ushort)(nnn + Registers[0]);
            }

            // Cxkk - RND Vx, byte
            if ((Instruction & 0xF000) == 0xC000)
            {
                Registers[x] = (byte)(Random.Next(256) & kk);
            }

            // Dxyn - DRW Vx, Vy, nibble
            if ((Instruction & 0xF000) == 0xD000)
            {
                bool collision = false;

                int xStart = Registers[x];
                int yStart = Registers[y];

                for (int yPos = 0; yPos < n; yPos++)
                {
                    byte row = Memory[I + yPos];
                    for (int xPos = 0; xPos < 8; xPos++)
                    {
                        byte bit = (byte)(row & 1);
                        row >>= 1;

                        int theX = xStart + (xPos % Display.GetLength(0));
                        int theY = yStart + (yPos % Display.GetLength(1));

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
            if ((Instruction & 0xF0FF) == 0xE09E)
            {
                var keyPress = Console.ReadKey();
                if (keyPress.Key.ToString() == Registers[x].ToString("X1"))
                {
                    PC += 2;
                }
            }

            // ExA1 - SKNP Vx
            if ((Instruction & 0xF0FF) == 0xE0A1)
            {
                var keyPress = Console.ReadKey();
                if (keyPress.Key.ToString() != Registers[x].ToString("X1"))
                {
                    PC += 2;
                }
            }

            // Fx07 - LD Vx, DT
            if ((Instruction & 0xF0FF) == 0xF007)
            {
                Registers[x] = DelayTimer;
            }

            // Fx0A - LD Vx, K
            if ((Instruction & 0xF0FF) == 0xF00A)
            {
                Registers[x] = ReadKey();
            }

            // Fx15 - LD DT, Vx
            if ((Instruction & 0xF0FF) == 0xF015)
            {
                DelayTimer = Registers[x];
            }

            // Fx18 - LD ST, Vx
            if ((Instruction & 0xF0FF) == 0xF018)
            {
                SoundTimer = Registers[x];
            }

            // Fx1E - ADD I, Vx
            if ((Instruction & 0xF0FF) == 0xF01E)
            {
                I += Registers[x];
            }

            // Fx29 - LD F, Vx
            if ((Instruction & 0xF0FF) == 0xF029)
            {
                I = (byte)(Registers[x] * 5);
            }

            // Fx33 - LD B, Vx
            if ((Instruction & 0xF0FF) == 0xF033)
            {
                byte bcd = Registers[x];

                Memory[I + 2] = (byte)(bcd % 10);
                bcd /= 10;
                Memory[I + 1] = (byte)(bcd % 10);
                bcd /= 10;
                Memory[I] = (byte)(bcd / 100);
            }

            // Fx55 - LD [I], Vx
            if ((Instruction & 0xF0FF) == 0xF055)
            {
                for (int idx = 0; idx < x; idx++)
                {
                    Memory[I + idx] = Registers[idx];
                }
            }

            // Fx65 - LD Vx, [I]
            if ((Instruction & 0xF0FF) == 0xF065)
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
            Console.Clear();

            for (int x = 0; x < Display.GetLength(0); x++)
            {
                for (int y = 0; y < Display.GetLength(1); y++)
                {
                    if (Display[x, y] == 1)
                    {
                        Console.SetCursorPosition(x, y);
                        Console.BackgroundColor = ConsoleColor.White;
                        Console.Write(' ');
                    }
                }
            }

            Console.BackgroundColor = ConsoleColor.Black;

            Console.SetCursorPosition(0, Display.GetLength(1));

            Console.WriteLine("PC: " + PC);
            Console.WriteLine("Instruction: 0x" + Instruction.ToString("X2"));
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

        private byte ReadKey()
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
                var keyPress = Console.ReadKey();
                if (KeyMap.ContainsKey(keyPress.Key))
                {
                    return KeyMap[keyPress.Key];
                }
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
