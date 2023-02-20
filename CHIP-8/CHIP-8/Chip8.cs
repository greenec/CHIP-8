using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CHIP_8
{
    public class Chip8
    {
        private enum PixelColor
        {
            Black = 0,
            White = 1
        }

        private const int _initialPC = 0x200;

        public readonly WriteableBitmap WriteableBitmap = new WriteableBitmap(pixelWidth: 64, pixelHeight: 32, dpiX: 96, dpiY: 96, PixelFormats.BlackWhite, palette: null);

        // CHIP-8 emulation variables
        private readonly byte[] _memory = new byte[4096];
        private readonly byte[] _registers = new byte[16];
        private ushort _I;

        private byte _delayTimer;
        private byte _soundTimer;

        private ushort _PC = _initialPC;
        private byte _SP;
        private readonly ushort[] _stack = new ushort[16];
        private readonly byte[,] _display = new byte[64, 32];

        // implementation-specific variables
        private bool _waitingForInput;
        private byte? _lastKeyPressed = null;
        private string _lastFilename;
        private Task _timerTask;
        private readonly byte[] _keys = new byte[16];
        private readonly Random _random = new Random();

        private readonly Dictionary<Key, byte> _keyMap = new Dictionary<Key, byte>()
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

        private readonly byte[] _whitePixel = { 255 };
        private readonly byte[] _blackPixel = { 0 };

        public Chip8()
        {

        }

        public void Initialize()
        {
            // set the screen to all black pixels
            ClearDisplay();

            InitializeSprites();

            if (_timerTask == null)
            {
                // start 60 Hz timer task
                _timerTask = Task.Run(async () =>
                {
                    while (true)
                    {
                        if (_delayTimer > 0)
                        {
                            _delayTimer -= 1;
                        }

                        if (_soundTimer > 0)
                        {
                            _soundTimer -= 1;
                            Dispatcher.CurrentDispatcher.Invoke(() => SystemSounds.Beep.Play());
                        }

                        await Task.Delay(1000 / 60);
                    }
                });
            }
        }

        public void LoadRom(string filename)
        {
            var rom = File.ReadAllBytes("../../roms/" + filename);

            for (int i = 0; i < rom.Length; i++)
            {
                _memory[_initialPC + i] = rom[i];
            }

            _lastFilename = filename;
        }

        public async Task Run()
        {
            while (true)
            {
                for (int i = 0; i < 10; i++)
                {
                    ushort instruction = Fetch();
                    DecodeAndExecute(instruction);
                }

                // yield for the UI thread to handle key events and rendering
                await Task.Delay(1);
            }
        }

        public void KeyDown(Key key)
        {
            if (_keyMap.TryGetValue(key, out byte keyValue))
            {
                _keys[keyValue] = 1;

                if (_waitingForInput)
                {
                    _lastKeyPressed = keyValue;
                }
            }
            else if (key == Key.Escape)
            {
                Reset();
            }
        }

        public void KeyUp(Key key)
        {
            if (_keyMap.TryGetValue(key, out byte keyValue))
            {
                _keys[keyValue] = 0;
            }
        }

        private void Reset()
        {
            Array.Clear(_memory, 0, _memory.Length);
            Array.Clear(_registers, 0, _registers.Length);
            Array.Clear(_stack, 0, _stack.Length);

            _I = 0;
            _PC = _initialPC;
            _SP = 0;
            _delayTimer = 0;
            _soundTimer = 0;

            // reset implementation-specific variables
            Array.Clear(_keys, 0, _keys.Length);

            _waitingForInput = false;
            _lastKeyPressed = null;

            // reinitialize sprites and clear display 
            Initialize();

            if (_lastFilename != null)
            {
                LoadRom(_lastFilename);
            }
        }

        private ushort Fetch()
        {
            return (ushort)((_memory[_PC] << 8) | _memory[_PC + 1]);
        }

        // see http://devernay.free.fr/hacks/chip8/C8TECH10.HTM and https://en.wikipedia.org/wiki/CHIP-8#Opcode_table
        private void DecodeAndExecute(ushort instruction)
        {
            byte instructionType = (byte)((instruction & 0xF000) >> 12);

            ushort nnn = (ushort)(instruction & 0x0FFF);
            byte x = (byte)((instruction & 0x0F00) >> 8);
            byte y = (byte)((instruction & 0x00F0) >> 4);
            byte kk = (byte)(instruction & 0x00FF);
            byte n = (byte)(instruction & 0x000F);

            switch (instructionType)
            {
                // 00kk
                case 0x0:
                    switch (kk)
                    {
                        // 00E0 - CLS
                        case 0xE0:
                            ClearDisplay();
                            break;

                        // 00EE RET
                        case 0xEE:
                            _PC = _stack[_SP--];
                            break;
                    }
                    break;

                // 1nnn - JP addr
                case 0x1:
                    _PC = nnn;
                    return;

                // 2nnn - CALL addr
                case 0x2:
                    _stack[++_SP] = _PC;
                    _PC = nnn;
                    return;

                // 3xkk - SE Vx, byte
                case 0x3:
                    if (_registers[x] == kk)
                    {
                        _PC += 2;
                    }
                    break;

                // 4xkk - SNE Vx, byte
                case 0x4:
                    if (_registers[x] != kk)
                    {
                        _PC += 2;
                    }
                    break;

                // 5xy0 - SE Vx, Vy
                case 0x5:
                    if (_registers[x] == _registers[y])
                    {
                        _PC += 2;
                    }
                    break;

                // 6xkk - LD Vx, byte
                case 0x6:
                    _registers[x] = kk;
                    break;

                // 7xkk - ADD Vx, byte
                case 0x7:
                    _registers[x] += kk;
                    break;

                // 8xyn - math and assignment
                case 0x8:
                    switch (n)
                    {
                        // 8xy0 - LD Vx, Vy
                        case 0x0:
                            _registers[x] = _registers[y];
                            break;

                        // 8xy1 - OR Vx, Vy
                        case 0x1:
                            _registers[x] |= _registers[y];
                            break;

                        // 8xy2 - AND Vx, Vy
                        case 0x2:
                            _registers[x] &= _registers[y];
                            break;

                        // 8xy3 - XOR Vx, Vy
                        case 0x3:
                            _registers[x] ^= _registers[y];
                            break;

                        // 8xy4 - ADD Vx, Vy
                        case 0x4:
                            int result = _registers[x] + _registers[y];

                            if (result > 256)
                            {
                                _registers[0xF] = 1;
                            }
                            else
                            {
                                _registers[0xF] = 0;
                            }

                            _registers[x] = (byte)result;
                            break;

                        // 8xy5 - SUB Vx, Vy
                        case 0x5:
                            if (_registers[x] > _registers[y])
                            {
                                _registers[0xF] = 1;
                            }
                            else
                            {
                                _registers[0xF] = 0;
                            }

                            _registers[x] -= _registers[y];
                            break;

                        // 8xy6 - SHR Vx {, Vy}
                        case 0x6:
                            if ((_registers[x] & 1) == 1)
                            {
                                _registers[0xF] = 1;
                            }
                            else
                            {
                                _registers[0xF] = 0;
                            }

                            _registers[x] /= 2;
                            break;

                        // 8xy7 - SUBN Vx, Vy
                        case 0x7:
                            if (_registers[y] > _registers[x])
                            {
                                _registers[0xF] = 1;
                            }
                            else
                            {
                                _registers[0xF] = 0;
                            }

                            _registers[x] = (byte)(_registers[y] - _registers[x]);
                            break;

                        // 8xyE - SHL Vx {, Vy}
                        case 0xE:
                            if ((_registers[x] & 0x8000) == 0x8000)
                            {
                                _registers[0xF] = 1;
                            }
                            else
                            {
                                _registers[0xF] = 0;
                            }

                            _registers[x] *= 2;
                            break;
                    }
                    break;

                // 9xy0 - SNE Vx, Vy
                case 0x9:
                    if (_registers[x] != _registers[y])
                    {
                        _PC += 2;
                    }
                    break;

                // Annn - LD I, addr
                case 0xA:
                    _I = nnn;
                    break;

                // Bnnn - JP V0, addr
                case 0xB:
                    _PC = (ushort)(nnn + _registers[0]);
                    break;

                // Cxkk - RND Vx, byte
                case 0xC:
                    _registers[x] = (byte)(_random.Next(256) & kk);
                    break;

                // Dxyn - DRW Vx, Vy, nibble
                case 0xD:
                    bool collision = false;

                    int xStart = _registers[x];
                    int yStart = _registers[y];

                    for (int yPos = 0; yPos < n; yPos++)
                    {
                        byte row = _memory[_I + yPos];
                        for (int xPos = 7; xPos >= 0; xPos--)
                        {
                            byte bit = (byte)(row & 1);
                            row >>= 1;

                            int xCoord = (xStart + xPos) % _display.GetLength(0);
                            int yCoord = (yStart + yPos) % _display.GetLength(1);

                            byte initial = _display[xCoord, yCoord];

                            if (initial != 0 && (initial ^ bit) == 0)
                            {
                                collision = true;
                            }

                            _display[xCoord, yCoord] = (byte)(initial ^ bit);

                            SetPixel(xCoord, yCoord, (_display[xCoord, yCoord] == 1) ? PixelColor.White : PixelColor.Black);
                        }
                    }

                    if (collision)
                    {
                        _registers[0xF] = 1;
                    }
                    else
                    {
                        _registers[0xF] = 0;
                    }
                    break;

                // Exkk
                case 0xE:
                    byte key = _registers[x];
                    switch (kk)
                    {
                        // Ex9E - SKP Vx
                        case 0x9E:
                            if (_keys[key] == 1)
                            {
                                _PC += 2;
                            }
                            break;

                        // ExA1 - SKNP Vx
                        case 0xA1:
                            if (_keys[key] == 0)
                            {
                                _PC += 2;
                            }
                            break;
                    }
                    break;

                // Fxkk
                case 0xF:
                    switch (kk)
                    {
                        // Fx07 - LD Vx, DT
                        case 0x07:
                            _registers[x] = _delayTimer;
                            break;

                        // Fx0A - LD Vx, K
                        case 0x0A:
                            if (!MaybeReadKey(out byte lastPressedKey))
                            {
                                return;
                            }

                            _registers[x] = lastPressedKey;
                            break;

                        // Fx15 - LD DT, Vx
                        case 0x15:
                            _delayTimer = _registers[x];
                            break;

                        // Fx18 - LD ST, Vx
                        case 0x18:
                            _soundTimer = _registers[x];
                            break;

                        // Fx1E - ADD I, Vx
                        case 0x1E:
                            _I += _registers[x];
                            break;

                        // Fx29 - LD F, Vx
                        case 0x29:
                            _I = (byte)(_registers[x] * 5);
                            break;

                        // Fx33 - LD B, Vx
                        case 0x33:
                            byte bcd = _registers[x];

                            _memory[_I + 2] = (byte)(bcd % 10);
                            bcd /= 10;
                            _memory[_I + 1] = (byte)(bcd % 10);
                            bcd /= 10;
                            _memory[_I] = (byte)(bcd / 100);
                            break;

                        // Fx55 - LD [I], Vx
                        case 0x55:
                            for (int idx = 0; idx <= x; idx++)
                            {
                                _memory[_I + idx] = _registers[idx];
                            }
                            break;

                        // Fx65 - LD Vx, [I]
                        case 0x65:
                            for (int idx = 0; idx <= x; idx++)
                            {
                                _registers[idx] = _memory[_I + idx];
                            }
                            break;
                    }
                    break;
            }

            _PC += 2;
        }

        private void SetPixel(int x, int y, PixelColor pixel)
        {
            Int32Rect rect = new Int32Rect(x, y, width: 1, height: 1);
            byte[] pixelArray = pixel == PixelColor.White ? _whitePixel : _blackPixel;

            WriteableBitmap.WritePixels(rect, pixelArray, pixelArray.Length, 0);
        }

        private void ClearDisplay()
        {
            for (int col = 0; col < _display.GetLength(0); col++)
            {
                for (int row = 0; row < _display.GetLength(1); row++)
                {
                    _display[col, row] = 0;
                    SetPixel(col, row, PixelColor.Black);
                }
            }
        }

        private bool MaybeReadKey(out byte keyValue)
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

            if (!_lastKeyPressed.HasValue)
            {
                _waitingForInput = true;

                keyValue = 0;
                return false;
            }

            keyValue = _lastKeyPressed.Value;

            _lastKeyPressed = null;
            _waitingForInput = false;

            return true;
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
                    _memory[idx++] = Convert.ToByte(spriteHex.Substring(i, 2), 16);
                }
            }
        }
    }
}
