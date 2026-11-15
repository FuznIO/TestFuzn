using System.Diagnostics;

namespace Fuzn.TestFuzn.Worker;

// A load or controller child: the user's test exe started with dotnet. The worker app holds its stdin
// open; if the worker app dies, stdin closes and the child stops by itself.
internal sealed class ChildProcess : IDisposable
{
    private const int TailLines = 20;

    private readonly Process _process;
    private readonly StreamWriter _outputLog;
    private readonly Queue<string> _tail = new();
    private readonly object _lock = new();

    private ChildProcess(Process process, StreamWriter outputLog)
    {
        _process = process;
        _outputLog = outputLog;
    }

    public bool HasExited => _process.HasExited;

    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

    public Task WaitForExit(CancellationToken cancellationToken) => _process.WaitForExitAsync(cancellationToken);

    public static ChildProcess Start(string packageDirectory, string entryAssembly, string argument,
        string outputLogPath, IReadOnlyDictionary<string, string> environment)
    {
        // dotnet MyTests.dll, not MyTests.exe: an .exe from a Windows build does not run elsewhere.
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = packageDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(entryAssembly);
        startInfo.ArgumentList.Add(argument);
        foreach (var variable in environment)
            startInfo.Environment[variable.Key] = variable.Value;

        Directory.CreateDirectory(Path.GetDirectoryName(outputLogPath)!);
        var outputLog = new StreamWriter(new FileStream(outputLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var child = new ChildProcess(process, outputLog);
        process.OutputDataReceived += (_, e) => child.Append(e.Data);
        process.ErrorDataReceived += (_, e) => child.Append(e.Data);

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            outputLog.Dispose();
            throw new InvalidOperationException($"Could not start 'dotnet {entryAssembly}': {ex.Message}. Is the .NET runtime installed on this worker?", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return child;
    }

    public void Send(string command)
    {
        try
        {
            if (_process.HasExited)
                return;

            _process.StandardInput.WriteLine(command);
            _process.StandardInput.Flush();
        }
        catch (IOException)
        {
            // The child is exiting; it no longer reads its input.
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Kill()
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    // The last lines of output, e.g. .NET's own message when the test project's runtime is missing.
    public string Tail()
    {
        lock (_lock)
            return string.Join(Environment.NewLine, _tail);
    }

    private void Append(string? line)
    {
        if (line == null)
            return;

        lock (_lock)
        {
            _outputLog.WriteLine(line);
            _tail.Enqueue(line);
            while (_tail.Count > TailLines)
                _tail.Dequeue();
        }
    }

    public void Dispose()
    {
        Kill();
        try
        {
            _process.StandardInput.Close();
        }
        catch (Exception)
        {
        }

        lock (_lock)
            _outputLog.Dispose();

        _process.Dispose();
    }
}
