using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LazerInspect;

sealed class ProcessMemory : IDisposable
{
    const int ProcessVmRead = 0x0010;
    const int ProcessQueryInformation = 0x0400;
    const int ProcessQueryLimitedInformation = 0x1000;
    const int MemCommit = 0x1000;
    const int PageReadWrite = 0x04;
    const int PageExecuteReadWrite = 0x40;

    readonly IntPtr _handle;

    public int ProcessId { get; }
    public string ProcessPath { get; }

    ProcessMemory(IntPtr handle, int pid, string path)
    {
        _handle = handle;
        ProcessId = pid;
        ProcessPath = path;
    }

    public static ProcessMemory Attach(int? pid = null)
    {
        var process = FindOsu(pid);
        int access = ProcessVmRead | ProcessQueryInformation | ProcessQueryLimitedInformation;
        var handle = OpenProcess(access, false, process.Id);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"OpenProcess failed (pid {process.Id}, err {Marshal.GetLastPInvokeError()}).");

        string path = QueryImagePath(handle);
        if (string.IsNullOrEmpty(path))
        {
            try { path = process.MainModule?.FileName ?? ""; }
            catch { /* access denied on MainModule is fine */ }
        }

        if (string.IsNullOrEmpty(path))
        {
            CloseHandle(handle);
            throw new InvalidOperationException($"Could not resolve the path of pid {process.Id}.");
        }

        return new ProcessMemory(handle, process.Id, path);
    }

    public byte[] ReadBytes(ulong address, int size)
    {
        var buffer = new byte[size];
        if (!ReadProcessMemory(_handle, (IntPtr)address, buffer, size, out int read) || read != size)
            throw new InvalidOperationException($"RPM 0x{address:X} size {size} failed (err {Marshal.GetLastPInvokeError()}).");
        return buffer;
    }

    public bool TryReadBytes(ulong address, Span<byte> dest)
    {
        byte[] tmp = new byte[dest.Length];
        if (!ReadProcessMemory(_handle, (IntPtr)address, tmp, tmp.Length, out int read) || read != tmp.Length)
            return false;
        tmp.CopyTo(dest);
        return true;
    }

    public ulong ReadPtr(ulong address) => BitConverter.ToUInt64(ReadBytes(address, 8));
    public int ReadInt(ulong address) => BitConverter.ToInt32(ReadBytes(address, 4));
    public long ReadLong(ulong address) => BitConverter.ToInt64(ReadBytes(address, 8));
    public float ReadFloat(ulong address) => BitConverter.ToSingle(ReadBytes(address, 4));
    public double ReadDouble(ulong address) => BitConverter.ToDouble(ReadBytes(address, 8));
    public byte ReadByte(ulong address) => ReadBytes(address, 1)[0];

    public ulong ReadPtrOrZero(ulong address)
    {
        Span<byte> buf = stackalloc byte[8];
        return TryReadBytes(address, buf) ? BitConverter.ToUInt64(buf) : 0;
    }

    public bool TryReadInt(ulong address, out int value)
    {
        Span<byte> buf = stackalloc byte[4];
        if (!TryReadBytes(address, buf))
        {
            value = 0;
            return false;
        }
        value = BitConverter.ToInt32(buf);
        return true;
    }

    public bool TryReadFloat(ulong address, out float value)
    {
        Span<byte> buf = stackalloc byte[4];
        if (!TryReadBytes(address, buf))
        {
            value = 0;
            return false;
        }
        value = BitConverter.ToSingle(buf);
        return true;
    }

    public bool TryReadDouble(ulong address, out double value)
    {
        Span<byte> buf = stackalloc byte[8];
        if (!TryReadBytes(address, buf))
        {
            value = 0;
            return false;
        }
        value = BitConverter.ToDouble(buf);
        return true;
    }

    /// <summary> plausibly a managed heap pointer on x64 Windows. </summary>
    public static bool SeemsPtr(ulong p) =>
        p is >= 0x1_0000 and < 0x7FF0_0000_0000 && (p & 7) == 0;

    public string ReadClrString(ulong address)
    {
        if (address == 0)
            return "";

        int length = ReadInt(address + 8);
        if (length <= 0 || length > 4096)
            return "";

        var bytes = ReadBytes(address + 12, length * 2);
        return Encoding.Unicode.GetString(bytes);
    }

    public string ReadClrStringPtr(ulong address)
    {
        ulong ptr = ReadPtrOrZero(address);
        return ptr == 0 ? "" : ReadClrString(ptr);
    }

    /// <summary>.NET x64 List&lt;T&gt; / Stack&lt;T&gt; of references.</summary>
    public ulong[] ReadRefList(ulong list)
    {
        if (list == 0)
            return [];

        int size = ReadInt(list + 0x10);
        if (size is < 0 or > 1_000_000)
        {
            // maybe it's a raw array
            size = ReadInt(list + 0x8);
            if (size is > 0 and < 1_000_000)
                return ReadRefArray(list, size);
            return [];
        }

        ulong items = ReadPtr(list + 0x8);
        return ReadRefArray(items, size);
    }

    public bool TryReadRefList(ulong list, out ulong[] items)
    {
        items = [];
        if (list == 0 || !TryReadInt(list + 0x10, out int size) || size is < 0 or > 1_000_000)
            return false;

        ulong array = ReadPtrOrZero(list + 0x8);
        if (array == 0)
            return size == 0;

        if (!TryReadInt(array + 8, out int length) || length < size)
            return false;

        try
        {
            items = ReadRefArray(array, size);
            return true;
        }
        catch
        {
            items = [];
            return false;
        }
    }

    /// <summary>.NET x64 <c>List&lt;T&gt;</c> of 4-byte values (int / enum).</summary>
    public bool TryReadIntList(ulong list, out int[] items)
    {
        items = [];
        if (list == 0 || !TryReadInt(list + 0x10, out int size) || size is < 0 or > 1_000_000)
            return false;
        if (size == 0)
            return true;

        ulong array = ReadPtrOrZero(list + 0x8);
        if (array == 0 || !TryReadInt(array + 8, out int length) || length < size)
            return false;

        var bytes = new byte[size * 4];
        if (!TryReadBytes(array + 0x10, bytes))
            return false;

        items = new int[size];
        for (int i = 0; i < size; i++)
            items[i] = BitConverter.ToInt32(bytes, i * 4);
        return true;
    }

    public ulong[] ReadRefArray(ulong array, int size)
    {
        if (array == 0 || size <= 0)
            return [];

        var result = new ulong[size];
        var bytes = ReadBytes(array + 0x10, size * 8);
        for (int i = 0; i < size; i++)
            result[i] = BitConverter.ToUInt64(bytes, i * 8);
        return result;
    }

    public ulong BindableObject(ulong bindable) => bindable == 0 ? 0 : ReadPtrOrZero(bindable + 0x20);
    public int BindableInt(ulong bindable) => bindable == 0 ? 0 : ReadInt(bindable + 0x40);
    public double BindableDouble(ulong bindable) => bindable == 0 ? 0 : ReadDouble(bindable + 0x40);
    public bool BindableBool(ulong bindable) => bindable == 0 ? false : ReadByte(bindable + 0x40) != 0;

    public IEnumerable<ulong> Scan(byte[] pattern)
    {
        foreach (var (baseAddr, size) in QueryWritableRegions())
        {
            const int chunk = 4 * 1024 * 1024;
            int overlap = pattern.Length - 1;
            ulong offset = 0;
            while (offset < size)
            {
                int take = (int)Math.Min((ulong)chunk, size - offset);
                if (take < pattern.Length)
                    break;

                var buf = new byte[take];
                if (TryReadBytes(baseAddr + offset, buf))
                {
                    int idx = 0;
                    while (idx + pattern.Length <= buf.Length)
                    {
                        int found = buf.AsSpan(idx).IndexOf(pattern);
                        if (found < 0)
                            break;
                        yield return baseAddr + offset + (ulong)idx + (ulong)found;
                        idx += found + pattern.Length;
                    }
                }

                if ((ulong)take >= size - offset)
                    break;
                offset += (ulong)(take - overlap);
            }
        }
    }

    IEnumerable<(ulong Base, ulong Size)> QueryWritableRegions()
    {
        ulong address = 0;
        while (VirtualQueryEx(_handle, (IntPtr)address, out var info, (uint)Marshal.SizeOf<MemoryBasicInformation>()) != 0)
        {
            ulong size = info.RegionSize;
            if (size == 0)
                break;

            if ((info.State & MemCommit) != 0 &&
                (info.Protect & (PageReadWrite | PageExecuteReadWrite)) != 0)
            {
                yield return (info.BaseAddress, size);
            }

            ulong next = info.BaseAddress + size;
            if (next <= address)
                break;
            address = next;
        }
    }

    public void Dispose() => CloseHandle(_handle);

    static Process FindOsu(int? pid)
    {
        if (pid is int requested)
            return Process.GetProcessById(requested);

        var windowPids = FindOsuWindowPids();
        if (windowPids.Count == 1)
            return Process.GetProcessById(windowPids[0]);
        if (windowPids.Count > 1)
            throw new InvalidOperationException($"Multiple osu! windows: pids {string.Join(", ", windowPids)}. Pass --pid.");

        var matches = Process.GetProcesses()
            .Where(p =>
            {
                try
                {
                    return p.ProcessName.Equals("osu!", StringComparison.OrdinalIgnoreCase)
                        || p.ProcessName.Equals("osu", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            })
            .ToList();

        if (matches.Count == 0)
            throw new InvalidOperationException("No osu! window or process found. Start osu!lazer and retry.");
        if (matches.Count > 1)
            throw new InvalidOperationException($"Multiple osu! processes: {string.Join(", ", matches.Select(p => p.Id))}. Pass --pid.");

        return matches[0];
    }

    static List<int> FindOsuWindowPids()
    {
        var pids = new HashSet<int>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0)
                return true;

            try
            {
                using var process = Process.GetProcessById((int)pid);
                if (process.ProcessName.Equals("osu!", StringComparison.OrdinalIgnoreCase)
                    || process.ProcessName.Equals("osu", StringComparison.OrdinalIgnoreCase))
                    pids.Add((int)pid);
            }
            catch
            {
                // process exited
            }

            return true;
        }, IntPtr.Zero);

        return pids.OrderBy(id => id).ToList();
    }

    static string QueryImagePath(IntPtr handle)
    {
        var buffer = new StringBuilder(32768);
        int size = buffer.Capacity;
        return QueryFullProcessImageName(handle, 0, buffer, ref size)
            ? buffer.ToString()
            : "";
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(int access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(IntPtr process, IntPtr address, [Out] byte[] buffer, int size, out int read);

    [DllImport("kernel32.dll")]
    static extern nint VirtualQueryEx(IntPtr process, IntPtr address, out MemoryBasicInformation info, uint length);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder path, ref int size);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc callback, IntPtr extra);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr extra);

    [StructLayout(LayoutKind.Sequential)]
    struct MemoryBasicInformation
    {
        public ulong BaseAddress;
        public ulong AllocationBase;
        public int AllocationProtect;
        public int Alignment1;
        public ulong RegionSize;
        public int State;
        public int Protect;
        public int Type;
        public int Alignment2;
    }
}
