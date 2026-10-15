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
                        RunDiagnostic(request.ToString());
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

    private static void RunDiagnostic(string request)
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
        try
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = Path.Combine(Environment.SystemDirectory, executable);
            start.UseShellExecute = false;
            using (Process process = Process.Start(start))
            {
                process.WaitForExit();
            }
            Console.WriteLine("Diagnostic finished: " + executable);
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            Console.Error.WriteLine("Diagnostic failed: " + error.Message);
        }
    }

    private static string Clean(string value)
    {
        return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }
}
