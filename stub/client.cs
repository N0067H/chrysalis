using System;
using System.Net.Sockets;
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
                connected = true;
                Console.WriteLine("Status: connected (press Ctrl+C to stop)");
                while (!Shutdown.WaitOne(0))
                {
                    if (connection.Client.Poll(500000, SelectMode.SelectRead) && connection.Client.Available == 0)
                    {
                        Console.WriteLine("Server closed the connection.");
                        break;
                    }
                }
            }
            catch (SocketException error)
            {
                Console.Error.WriteLine("Connection failed: " + error.Message);
            }
            finally
            {
                Console.WriteLine(connected ? "Status: disconnected" : "Status: stopped");
            }
        }
    }
}
