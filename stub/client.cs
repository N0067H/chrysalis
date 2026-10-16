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
                    if (!connection.Client.Poll(500000, SelectMode.SelectRead))
                        continue;
                    int next = connection.GetStream().ReadByte();
                    if (next < 0)
                    {
                        Console.WriteLine("Server closed the connection.");
                        break;
                    }
                    if (next == '\n')
                    {
                        RunDiagnostic(request.ToString(), connection.GetStream());
                        request.Length = 0;
                    }
                    else
                    {
                        if (request.Length >= 128)
                            throw new IOException("Diagnostic request is too long.");
                        request.Append((char)next);
                    }
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

    private static void RunDiagnostic(string request, Stream connection)
    {
        string executable;
        switch (request)
        {
            case "RUN\thostname": executable = "hostname.exe"; break;
            case "RUN\twhoami": executable = "whoami.exe"; break;
            case "RUN\tipconfig": executable = "ipconfig.exe"; break;
            default:
                Console.Error.WriteLine("Rejected unsupported diagnostic request.");
                return;
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
                // Drain both pipes concurrently so a full stderr pipe cannot deadlock stdout.
                Thread errorReader = new Thread(delegate() { errors = process.StandardError.ReadToEnd(); });
                errorReader.Start();
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                errorReader.Join();
                exitCode = process.ExitCode;
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
