
using Microsoft.Win32;
using System.IO.Ports;
using System.Text.RegularExpressions;

namespace pdutil;

internal class ComPorts
{
    public static List<string> GetComPortNamesForIdentifier(string VID, string PID)
    {
        Regex regex = new($"^VID_{VID}.PID_{PID}", RegexOptions.IgnoreCase);
        List<string> namesForIdentifier = [];
        RegistryKey registryKey1 = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Enum");
        foreach (string subKeyName1 in registryKey1.GetSubKeyNames())
        {
            RegistryKey registryKey2 = registryKey1.OpenSubKey(subKeyName1);
            foreach (string subKeyName2 in registryKey2.GetSubKeyNames())
            {
                if (regex.Match(subKeyName2).Success)
                {
                    RegistryKey registryKey3 = registryKey2.OpenSubKey(subKeyName2);
                    foreach (string subKeyName3 in registryKey3.GetSubKeyNames())
                    {
                        RegistryKey registryKey4 = registryKey3.OpenSubKey(subKeyName3).OpenSubKey("Device Parameters");
                        namesForIdentifier.Add((string)registryKey4.GetValue("PortName"));
                    }
                }
            }
        }
        return namesForIdentifier;
    }

    public static SerialPort Open(string comPortName)
    {
        SerialPort serialPort = new()
        {
            PortName = comPortName,
            BaudRate = 115200,
            Handshake = Handshake.RequestToSend
        };
        serialPort.Open();
        return serialPort;
    }
}
