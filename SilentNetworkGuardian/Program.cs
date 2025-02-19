using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Timers;
using Timer = System.Timers.Timer;

class Program
{
    static HashSet<string> knownConnections = new HashSet<string>();
    static Dictionary<string, string> resolvedIPs = new Dictionary<string, string>(); // Cache des résolutions DNS
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
            string resolvedDns = ResolveDns(conn);
            Console.WriteLine($"[Nouvelle Connexion] {conn} | {resolvedDns}");
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
            Arguments = "-an",
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

            if (state == "LISTENING")
            {
                return $"{protocol} {localAddress} (INBOUND)";
            }
            return $"{protocol} {localAddress} -> {remoteAddress} ({state})";
        }
        return null;
    }

    static string ResolveDns(string connection)
    {
        try
        {
            var match = Regex.Match(connection, @"->\s+(\S+)");
            if (match.Success)
            {
                string ipAddress = match.Groups[1].Value.Split(':')[0];

                // Vérifier si l'IP est déjà résolue dans le cache
                if (resolvedIPs.ContainsKey(ipAddress))
                {
                    return resolvedIPs[ipAddress];
                }

                // Sinon, résoudre et stocker dans le cache
                string hostName = GetHostName(ipAddress);
                resolvedIPs[ipAddress] = hostName;
                return hostName;
            }
        }
        catch (Exception)
        {
            return "Impossible de résoudre le DNS.";
        }
        return "Non résolu";
    }

    static string GetHostName(string ipAddress)
    {
        try
        {
            IPHostEntry hostEntry = Dns.GetHostEntry(ipAddress);
            return hostEntry.HostName;
        }
        catch
        {
            return "Non résolu";
        }
    }
}
