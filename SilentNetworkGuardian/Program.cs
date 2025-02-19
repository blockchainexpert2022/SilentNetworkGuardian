using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using Timer = System.Timers.Timer;

class Program
{
    static HashSet<string> knownConnections = new HashSet<string>();
    static Timer timer;

    static void Main()
    {
        Console.WriteLine("Surveillance des connexions réseau en cours...");
        timer = new Timer(5000);
        timer.Elapsed += (sender, e) => MonitorConnections();
        timer.Start();
        Console.WriteLine("Appuyez sur Entrée pour quitter.");
        Console.ReadLine();
    }

    static void MonitorConnections()
    {
        HashSet<string> currentConnections = GetActiveConnections();

        var newConnections = currentConnections.Except(knownConnections).ToList();
        var closedConnections = knownConnections.Except(currentConnections).ToList();

        foreach (var conn in newConnections)
        {
            Console.WriteLine($"[Nouvelle Connexion] {conn}");
            ResolveDns(conn);
        }

        foreach (var conn in closedConnections)
        {
            Console.WriteLine($"[Connexion Fermée] {conn}");
        }

        knownConnections = currentConnections;
    }

    static HashSet<string> GetActiveConnections()
    {
        HashSet<string> connections = new HashSet<string>();
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "netstat",
            Arguments = "-n",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(psi))
        {
            using (System.IO.StreamReader reader = process.StandardOutput)
            {
                string output = reader.ReadToEnd();
                string[] lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (line.Contains("TCP") || line.Contains("UDP"))
                    {
                        string connection = ParseConnection(line);
                        if (!string.IsNullOrEmpty(connection))
                        {
                            connections.Add(connection);
                        }
                    }
                }
            }
        }
        return connections;
    }

    static string ParseConnection(string line)
    {
        var match = Regex.Match(line, @"(TCP|UDP)\s+(\S+)\s+(\S+)\s+(\S+)");
        if (match.Success)
        {
            string protocol = match.Groups[1].Value;
            string localAddress = match.Groups[2].Value;
            string remoteAddress = match.Groups[3].Value;
            string state = match.Groups[4].Value;
            return $"{protocol} {localAddress} -> {remoteAddress} ({state})";
        }
        return null;
    }

    static void ResolveDns(string connection)
    {
        try
        {
            var match = Regex.Match(connection, @"->\s+(\S+)");
            if (match.Success)
            {
                string ipAddress = match.Groups[1].Value.Split(':')[0];
                string hostName = ExecuteNsLookup(ipAddress);
                Console.WriteLine($"Résolution DNS : {ipAddress} -> {hostName}");
            }
        }
        catch (Exception)
        {
            Console.WriteLine("Impossible de résoudre le DNS.");
        }
    }

    static string ExecuteNsLookup(string ipAddress)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "nslookup",
                Arguments = ipAddress,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(psi))
            {
                using (System.IO.StreamReader reader = process.StandardOutput)
                {
                    string output = reader.ReadToEnd();
                    var match = Regex.Match(output, @"Nom :\s+(\S+)");
                    return match.Success ? match.Groups[1].Value : "Non résolu";
                }
            }
        }
        catch
        {
            return "Erreur NsLookup";
        }
    }
}
