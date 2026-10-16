using System;
using System.IO;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;

internal static class Client
{
    private const string ServerAddress = "{{SERVER_ADDRESS}}";
    private static readonly ManualResetEvent Shutdown = new ManualResetEvent(false);

    private static void Main()
    {
        Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Shutdown.Set();
        };

        Console.WriteLine("Client: " + Environment.MachineName);
        Console.WriteLine("OS: " + Environment.OSVersion);
        Console.WriteLine(".NET Framework: " + Environment.Version);
        Console.WriteLine("Configured server: " + ServerAddress);

        Uri server = new Uri("tcp://" + ServerAddress);
        using (TcpClient connection = new TcpClient())
        {
            bool connected = false;
            Console.WriteLine("Status: connecting");
            try
            {
                IAsyncResult attempt = connection.BeginConnect(server.Host, server.Port, null, null);
                if (!attempt.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5)))
                {
                    Console.WriteLine("Status: connection timed out");
                    return;
                }

                connection.EndConnect(attempt);

                string identity = Clean(Environment.MachineName) + "\t" + Clean(Environment.OSVersion.ToString()) + "\n";
                byte[] message = Encoding.UTF8.GetBytes(identity);
                connection.GetStream().Write(message, 0, message.Length);
                connected = true;
                Console.WriteLine("Status: connected (press Ctrl+C to stop)");
                
                StringBuilder request = new StringBuilder();
                while (!Shutdown.WaitOne(0))
                {
                    string frame;
                    if (ReceiveRequest(connection, request, out frame))
                        RunDiagnostic(frame, connection, request);
                    else
                        Shutdown.WaitOne(50);
                }
            }
            catch (SocketException error)
            {
                Console.Error.WriteLine("Connection failed: " + error.Message);
            }
            catch (IOException error)
            {
                Console.Error.WriteLine("Connection failed: " + error.Message);
            }
            finally
            {
                Console.WriteLine(connected ? "Status: disconnected" : "Status: stopped");
            }
        }
    }

    private static bool ReceiveRequest(TcpClient connection, StringBuilder pending, out string frame)
    {
        frame = null;
        while (connection.Client.Poll(0, SelectMode.SelectRead))
        {
            int next = connection.GetStream().ReadByte();
            if (next < 0)
                throw new IOException("Server closed the connection.");
            if (next == '\n')
            {
                frame = pending.ToString();
                pending.Length = 0;
                return true;
            }
            if (pending.Length >= 128)
                throw new IOException("Diagnostic request is too long.");
            pending.Append((char)next);
        }
        return false;
    }

    private static void RunDiagnostic(string request, TcpClient client, StringBuilder pending)
    {
        // Late cancellations are harmless, including when the same command runs again.
        if (request.StartsWith("CANCEL\t", StringComparison.Ordinal))
            return;
        string[] fields = request.Split('\t');
        int id;
        if (fields.Length != 3 || fields[0] != "RUN" ||
            !Int32.TryParse(fields[1], out id) || id < 0)
            throw new IOException("Invalid diagnostic request.");
        string executable;
        switch (fields[2])
        {
            case "hostname": executable = "hostname.exe"; break;
            case "whoami": executable = "whoami.exe"; break;
            case "ipconfig": executable = "ipconfig.exe"; break;
            default:
                throw new IOException("Rejected unsupported diagnostic request.");
        }

        Console.WriteLine("Running diagnostic: " + executable);
        string output = "";
        string errors = "";
        int exitCode = -1;
        try
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = Path.Combine(Environment.SystemDirectory, executable);
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            using (Process process = Process.Start(start))
            {
                // Keep both pipes draining while the main thread handles timeout/cancel.
                Thread errorReader = new Thread(delegate() { errors = process.StandardError.ReadToEnd(); });
                Thread outputReader = new Thread(delegate() { output = process.StandardOutput.ReadToEnd(); });
                errorReader.Start();
                outputReader.Start();
                Stopwatch elapsed = Stopwatch.StartNew();
                string stopped = null;
                try
                {
                    while (!process.WaitForExit(50))
                    {
                        string frame;
                        if (Shutdown.WaitOne(0))
                            stopped = "Diagnostic cancelled by client.";
                        else if (elapsed.Elapsed >= TimeSpan.FromSeconds(30))
                            stopped = "Diagnostic timed out after 30 seconds.";
                        else if (ReceiveRequest(client, pending, out frame) &&
                            frame == "CANCEL\t" + fields[1])
                            stopped = "Diagnostic cancelled by server.";

                        if (stopped != null)
                        {
                            exitCode = stopped.Contains("timed out") ? 124 : 130;
                            break;
                        }
                    }
                }
                finally
                {
                    // Also clean up when the socket closes or a malformed frame arrives.
                    try
                    {
                        if (!process.HasExited)
                        {
                            try { process.Kill(); }
                            catch (InvalidOperationException) { }
                        }
                        process.WaitForExit();
                    }
                    finally
                    {
                        outputReader.Join();
                        errorReader.Join();
                    }
                }
                if (stopped == null)
                    exitCode = process.ExitCode;
                else
                    errors += Environment.NewLine + stopped + Environment.NewLine;
            }
            Console.WriteLine("Diagnostic finished: " + executable);
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            errors = "Diagnostic failed: " + error.Message;
        }
        Console.Write(output);
        Console.Error.Write(errors);
        Console.WriteLine("Exit status: " + exitCode);
        Stream connection = client.GetStream();
        WriteNumber(connection, exitCode);
        WriteField(connection, output);
        WriteField(connection, errors);
        connection.Flush();
    }

    private static void WriteNumber(Stream connection, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        connection.Write(bytes, 0, bytes.Length);
    }

    private static void WriteField(Stream connection, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > 1024 * 1024)
            throw new IOException("Diagnostic output exceeds 1 MiB.");
        WriteNumber(connection, bytes.Length);
        connection.Write(bytes, 0, bytes.Length);
    }

    private static string Clean(string value)
    {
        return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }
}
