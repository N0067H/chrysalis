using System;
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
        Console.WriteLine("Status: running (press Ctrl+C to stop)");

        Shutdown.WaitOne();
        Console.WriteLine("Status: stopped");
    }
}
