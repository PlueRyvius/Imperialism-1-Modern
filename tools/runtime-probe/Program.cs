using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Imperialism.RuntimeProbe;

/// <summary>
/// Read-only observer for the original 32-bit Imperialism process. It never writes
/// process memory, never changes a scenario, and deliberately samples only the
/// relation comparison value and a few raw relation-matrix entries recovered from
/// the EXE. See docs/disasm/wanted-values.md.
/// </summary>
internal static class Program
{
    private const string DefaultExecutable = @"E:\Imperialism\Imperialism.exe";
    private const uint OriginalImageBase = 0x0040_0000;
    private const uint GlobalStateAddress = 0x006A_20F8;
    private const uint CountryManagerAddress = 0x006A_43D0;
    private const int GlobalRelationComparisonOffset = 0x2C;
    private const int CountryRelationMatrixOffset = 0x79C;
    private const int CountryCount = 23;
    private const int PollMilliseconds = 250;

    private static readonly (int First, int Second)[] SamplePairs =
    [
        (0, 1),
        (0, 2),
        (1, 2),
    ];

    private static int Main(string[] args)
    {
        var options = ParseOptions(args);
        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        using var process = FindOrLaunch(options);
        if (process is null)
        {
            return 1;
        }

        Console.WriteLine($"Attached read-only monitor to PID {process.Id}: {process.MainModule?.FileName}");
        Console.WriteLine("No process memory will be written. Load a scenario, reach the terrain map, then issue/resolve one naval order.");

        using var handle = OpenProcess(
            ProcessAccessFlags.QueryInformation | ProcessAccessFlags.VirtualMemoryRead,
            false,
            process.Id);
        if (handle.IsInvalid)
        {
            Console.Error.WriteLine($"OpenProcess failed: {Marshal.GetLastWin32Error()}");
            return 1;
        }

        var moduleBase = checked((uint)process.MainModule!.BaseAddress.ToInt64());
        var globalStatePointerAddress = Rebase(moduleBase, GlobalStateAddress);
        var countryManagerPointerAddress = Rebase(moduleBase, CountryManagerAddress);
        Console.WriteLine(
            $"moduleBase=0x{moduleBase:X8}; global-state pointer=0x{globalStatePointerAddress:X8}; " +
            $"country-manager pointer=0x{countryManagerPointerAddress:X8}");

        ushort? previousComparison = null;
        uint? previousGlobalState = null;
        var previousPairs = new Dictionary<(int First, int Second), ushort>();
        var until = DateTimeOffset.UtcNow.AddMinutes(options.DurationMinutes);

        while (!process.HasExited && DateTimeOffset.UtcNow < until)
        {
            ObserveGlobalState(handle, globalStatePointerAddress, ref previousGlobalState, ref previousComparison);
            ObserveRelationMatrix(handle, countryManagerPointerAddress, previousPairs);
            Thread.Sleep(PollMilliseconds);
        }

        Console.WriteLine(process.HasExited
            ? "The game exited; monitor stopped."
            : $"Monitor duration ({options.DurationMinutes} minutes) elapsed; monitor stopped.");
        return 0;
    }

    private static void ObserveGlobalState(
        SafeProcessHandle process,
        uint globalStatePointerAddress,
        ref uint? previousGlobalState,
        ref ushort? previousComparison)
    {
        if (!TryReadUInt32(process, globalStatePointerAddress, out var globalState) || globalState == 0)
        {
            return;
        }

        if (previousGlobalState != globalState)
        {
            previousGlobalState = globalState;
            Console.WriteLine($"{Stamp()} global-state=0x{globalState:X8}");
        }

        if (!TryReadUInt16(process, checked(globalState + GlobalRelationComparisonOffset), out var comparison))
        {
            return;
        }

        if (previousComparison != comparison)
        {
            previousComparison = comparison;
            Console.WriteLine(
                $"{Stamp()} relation-comparison raw={comparison} signed={(short)comparison} " +
                $"(global+0x{GlobalRelationComparisonOffset:X})");
        }
    }

    private static void ObserveRelationMatrix(
        SafeProcessHandle process,
        uint countryManagerPointerAddress,
        IDictionary<(int First, int Second), ushort> previousPairs)
    {
        if (!TryReadUInt32(process, countryManagerPointerAddress, out var manager) || manager == 0)
        {
            return;
        }

        foreach (var pair in SamplePairs)
        {
            var index = checked(pair.First * CountryCount + pair.Second);
            var address = checked(manager + CountryRelationMatrixOffset + (uint)(index * sizeof(ushort)));
            if (!TryReadUInt16(process, address, out var value) ||
                (previousPairs.TryGetValue(pair, out var previous) && previous == value))
            {
                continue;
            }

            previousPairs[pair] = value;
            Console.WriteLine(
                $"{Stamp()} rela[{pair.First},{pair.Second}] raw={value} signed={(short)value} " +
                $"(country-manager+0x{CountryRelationMatrixOffset:X})");
        }
    }

    private static Process? FindOrLaunch(Options options)
    {
        var running = Process.GetProcessesByName("Imperialism")
            .OrderBy(static process => process.Id)
            .FirstOrDefault();
        if (running is not null)
        {
            return running;
        }

        if (!options.Launch)
        {
            Console.Error.WriteLine("Imperialism.exe is not running. Re-run with --launch or start it yourself.");
            return null;
        }

        if (!File.Exists(options.Executable))
        {
            Console.Error.WriteLine($"Original executable not found: {options.Executable}");
            return null;
        }

        var started = Process.Start(new ProcessStartInfo
        {
            FileName = options.Executable,
            WorkingDirectory = Path.GetDirectoryName(options.Executable)!,
            UseShellExecute = true,
        });
        if (started is null)
        {
            Console.Error.WriteLine("The original executable did not start.");
            return null;
        }

        Thread.Sleep(TimeSpan.FromSeconds(2));
        started.Refresh();
        return started;
    }

    private static uint Rebase(uint actualImageBase, uint originalVirtualAddress) =>
        checked(actualImageBase + (originalVirtualAddress - OriginalImageBase));

    private static bool TryReadUInt16(SafeProcessHandle process, uint address, out ushort value)
    {
        var buffer = new byte[sizeof(ushort)];
        value = 0;
        if (!ReadProcessMemory(process, (nint)address, buffer, (nuint)buffer.Length, out var bytesRead) ||
            bytesRead != (nuint)buffer.Length)
        {
            return false;
        }

        value = BitConverter.ToUInt16(buffer);
        return true;
    }

    private static bool TryReadUInt32(SafeProcessHandle process, uint address, out uint value)
    {
        var buffer = new byte[sizeof(uint)];
        value = 0;
        if (!ReadProcessMemory(process, (nint)address, buffer, (nuint)buffer.Length, out var bytesRead) ||
            bytesRead != (nuint)buffer.Length)
        {
            return false;
        }

        value = BitConverter.ToUInt32(buffer);
        return true;
    }

    private static Options ParseOptions(string[] args)
    {
        var launch = false;
        var executable = DefaultExecutable;
        var durationMinutes = 30;
        var showHelp = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--launch":
                    launch = true;
                    break;
                case "--exe" when index + 1 < args.Length:
                    executable = args[++index];
                    break;
                case "--minutes" when index + 1 < args.Length &&
                                      int.TryParse(args[++index], CultureInfo.InvariantCulture, out var minutes) &&
                                      minutes is > 0 and <= 120:
                    durationMinutes = minutes;
                    break;
                case "--help" or "-h":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown or incomplete option: {args[index]}");
            }
        }

        return new Options(launch, executable, durationMinutes, showHelp);
    }

    private static string Stamp() => DateTimeOffset.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static void PrintUsage() => Console.WriteLine(
        "Usage: dotnet run --project tools/runtime-probe -- [--launch] [--exe path] [--minutes 1..120]");

    private sealed record Options(bool Launch, string Executable, int DurationMinutes, bool ShowHelp);

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        QueryInformation = 0x0400,
        VirtualMemoryRead = 0x0010,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(
        ProcessAccessFlags desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(
        SafeProcessHandle process,
        nint baseAddress,
        [Out] byte[] buffer,
        nuint size,
        out nuint bytesRead);

    private sealed class SafeProcessHandle : SafeHandle
    {
        public SafeProcessHandle() : base(nint.Zero, ownsHandle: true)
        {
        }

        public override bool IsInvalid => handle == nint.Zero || handle == new nint(-1);

        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}
