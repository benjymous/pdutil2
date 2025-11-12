
using System.Drawing;
using System.IO.Ports;

#nullable disable
namespace pdutil;

internal class Playdate()
{

    public static readonly string ComPortName = GetComPortName();

    // playdate's filesystem doesn't seem to have second accuracy
    static bool TimesEquivalent(DateTime timea, DateTime timeb) => Math.Abs((timea - timeb).TotalSeconds) < 3;

    public static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
    {
        DirectoryInfo directoryInfo1 = new(sourceDirName);
        DirectoryInfo[] directoryInfoArray = directoryInfo1.Exists ? directoryInfo1.GetDirectories() : throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
        if (!Directory.Exists(destDirName))
            Directory.CreateDirectory(destDirName);
        foreach (FileInfo file in directoryInfo1.GetFiles())
        {
            string destFileName = Path.Combine(destDirName, file.Name);

            bool shouldCopy = true;

            if (File.Exists(destFileName))
            {
                FileInfo destFile = new(destFileName);

                shouldCopy = file.Length != destFile.Length || !TimesEquivalent(file.LastWriteTimeUtc, destFile.LastWriteTimeUtc);
            }

            if (shouldCopy)
            {
                file.CopyTo(destFileName, true);
                File.SetLastWriteTimeUtc(destFileName, file.LastWriteTimeUtc);
                Console.Write(".");
            }
            else
            {
                Console.Write("-");
            }
        }
        if (!copySubDirs)
            return;
        foreach (DirectoryInfo directoryInfo2 in directoryInfoArray)
        {
            string destDirName1 = Path.Combine(destDirName, directoryInfo2.Name);
            DirectoryCopy(directoryInfo2.FullName, destDirName1, copySubDirs);
        }
    }


    public static void Eject(string drive)
    {
        int num = PowerShell.RunScript("\r\n                function Eject-Drive {\r\n                    Param($DriveLetter)\r\n\t                $driveEject = New-Object -comObject Shell.Application\r\n                    $driveEject.Namespace(17).ParseName($DriveLetter).InvokeVerb('Eject')\r\n                }\r\n                Eject-Drive " + drive);
        if (num == 0)
            return;
        Console.WriteLine("PowerShell returned error: " + num.ToString());
    }

    public static string FindPlaydateDrive(string labelToFind, bool wait)
    {
        string playdateDrive = "";
        bool flag = false;
        while (!flag)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                string str = "";
                playdateDrive = "";
                try
                {
                    str = drive.VolumeLabel;
                    playdateDrive = drive.Name;
                }
                catch
                {
                }
                if (str == labelToFind)
                {
                    flag = true;
                    break;
                }
            }
            Thread.Sleep(500);
            if (!wait)
            {
                if (!flag)
                {
                    playdateDrive = "";
                    break;
                }
                break;
            }
        }
        return playdateDrive;
    }

    static string GetComPortName()
    {
        List<string> namesForIdentifier = ComPorts.GetComPortNamesForIdentifier("1331", "5740");
        if (namesForIdentifier.Count <= 0)
            return "";
        foreach (string portName in SerialPort.GetPortNames())
        {
            if (namesForIdentifier.Contains(portName))
                return portName;
        }
        return "";
    }

    public static void Install(string pdxPath, bool clean)
    {
        Console.WriteLine($"Installing \"{pdxPath}\"...");
        string drive = MountDataDisk();
        string path1 = drive;
        if (path1 == "")
        {
            Console.WriteLine("Could not mount data disk.");
        }
        else
        {
            string fileName = Path.GetFileName(pdxPath);
            string str = Path.Combine(Path.Combine(path1, "Games"), fileName);
            if (clean && Directory.Exists(str))
            {
                Console.WriteLine($"Removing existing \"{fileName}\"...");
                Directory.Delete(str, true);
            }
            Console.WriteLine("Copying files...");
            DirectoryCopy(pdxPath, str, true);
            Console.WriteLine("\nCopy complete.");
            Console.WriteLine($"Ejecting {drive}...");
            while (FindPlaydateDrive("PLAYDATE", false) != "")
            {
                Thread.Sleep(500);
                Eject(drive);
            }
            Console.WriteLine("Ejected.");
        }
    }

    public static string MountDataDisk()
    {
        string str = "";
        if (ComPortName == "")
            return str;
        SerialPort serialPort = ComPorts.Open(ComPortName);
        serialPort.WriteLine("datadisk");
        serialPort.Close();
        Console.WriteLine("Waiting for drive to appear...");
        string playdateDrive = FindPlaydateDrive("PLAYDATE", true);
        Console.WriteLine("Playdate data disk mounted as {0}", (object)playdateDrive);
        return playdateDrive;
    }

    public static string MountRecoveryDisk()
    {
        string str = "";
        if (ComPortName == "")
            return str;
        SerialPort serialPort = ComPorts.Open(ComPortName);
        serialPort.WriteLine("bootdisk");
        serialPort.Close();
        Console.WriteLine("Waiting for drive to appear...");
        string playdateDrive = FindPlaydateDrive("BOOTSY", true);
        Console.WriteLine("Playdate recovery disk mounted as {0}", (object)playdateDrive);
        return playdateDrive;
    }

    public static void Run(string appPath)
    {
        SerialPort serialPort = ComPorts.Open(ComPortName);
        serialPort.WriteLine("run " + appPath);
        serialPort.Close();
        Console.WriteLine("Command sent.");
    }

    public static string SendCmd(string serCommand)
    {
        SerialPort serialPort = ComPorts.Open(ComPortName);
        serialPort.WriteLine(serCommand);
        var data = "";
        while (serialPort.BytesToRead > 0)
        {
            Task.Delay(1000);
            data += serialPort.ReadExisting();

        }

        serialPort.Close();
        return data;
    }

    public static void Cmd(string serCommand)
    {
        Console.WriteLine(SendCmd(serCommand));
    }

    public static void Shell()
    {
        SerialPort serialPort = ComPorts.Open(ComPortName);
        Console.WriteLine("Ctrl+c to quit");
        serialPort.WriteLine("echo off\r\nhelp");
        try
        {
            while (true)
            {
                var str = serialPort.ReadExisting();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    Console.Write(str);
                }

                if (Console.KeyAvailable)
                {
                    var c = Console.ReadKey().KeyChar;
                    if (c == '\r')
                    {
                        serialPort.Write("\r\n");
                    }
                    else if (c == '\b')
                    {
                        Console.Write(" ");
                        Console.Write('\b');
                        serialPort.Write(c.ToString());
                    }
                    else
                    {
                        serialPort.Write(c.ToString());
                    }

                }
            }
        }
        finally
        {
            serialPort.Close();
        }
    }

    public static void Screen(string filename)
    {
        SerialPort serialPort = ComPorts.Open(ComPortName);
        serialPort.WriteLine("echo off\r\nscreen");
        while (true)
        {
            var line = serialPort.ReadLine();
            if (line.StartsWith("~screen:")) break;
        }
        Queue<int> data = [];
        while (serialPort.BytesToRead > 0)
        {
            data.Enqueue(serialPort.ReadByte());
        }

        serialPort.Close();

        var bmp = new Bitmap(400, 240);
        for (int y = 0; y < 240; y++)
        {
            int v = 0;
            int x1 = 0;
            int i = 7;
            for (int x = 0; x < 400; x++)
            {
                if (x % 8 == 0)
                {
                    v = data.Dequeue();
                    i = 7;
                    x1 = x;
                }
                bmp.SetPixel(x1+i, y, (v&1)==0 ? Color.Black : Color.White);
                v >>= 1;
                i--;
                
            }
        }

        bmp.Save(filename);
        Console.WriteLine($"Written screenshot to {filename}");

    }
}
