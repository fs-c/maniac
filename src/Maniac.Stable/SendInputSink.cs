using System.Runtime.InteropServices;
using Maniac.Core.Input;

namespace Maniac.Stable;

sealed class SendInputSink : IInputSink
{
    readonly ushort[] keys;
    readonly bool[] held;

    public string Spec { get; }

    public SendInputSink(string spec) : this(ManiaKeys.Parse(spec), spec)
    {
    }

    public SendInputSink(ushort[] virtualKeys, string spec)
    {
        if (virtualKeys.Length == 0)
            throw new InvalidOperationException("key map is empty.");
        Spec = spec;
        keys = virtualKeys;
        held = new bool[keys.Length];
    }

    public int Columns => keys.Length;

    public void Press(int action) => Send(action, down: true);

    public void Release(int action) => Send(action, down: false);

    public void MoveMouse(float x, float y)
    {
        throw new InvalidOperationException("mouse move is not used for mania.");
    }

    public void ReleaseHeld()
    {
        for (int i = 0; i < held.Length; i++)
        {
            if (!held[i])
                continue;
            Send(i, down: false);
        }
    }

    void Send(int action, bool down)
    {
        if ((uint)action >= (uint)keys.Length)
            throw new InvalidOperationException($"column {action} is outside key map length {keys.Length}.");
        if (held[action] == down)
            return;
        held[action] = down;

        ushort vk = keys[action];
        ushort scan = (ushort)MapVirtualKey(vk, 0);
        uint flags = down ? 0u : 2u;
        if (vk is 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28)
            flags |= 1;

        var input = new INPUT
        {
            Type = 1,
            Union = new InputUnion
            {
                Keyboard = new KEYBDINPUT
                {
                    Vk = vk,
                    Scan = scan,
                    Flags = flags
                }
            }
        };
        uint sent = SendInput(1, [input], Marshal.SizeOf<INPUT>());
        if (sent != 1)
            throw new InvalidOperationException($"SendInput failed ({Marshal.GetLastWin32Error()}).");
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [DllImport("user32.dll")]
    static extern uint MapVirtualKey(uint code, uint mapType);

    [StructLayout(LayoutKind.Sequential, Size = 28)]
    struct INPUT
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }
}
