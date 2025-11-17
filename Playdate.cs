
using System.Drawing;
using System.Drawing.Printing;
using System.IO.Compression;
using System.IO.Ports;

namespace pdutil;

internal class Playdate()
{
    public static readonly string ComPortName = GetComPortName();
    public static SerialPort OpenComPort() => ComPorts.Open(ComPortName);

    // Playdate's filesystem (FAT32) only has accuracy to 2 seconds for timestamps
    static bool TimesEquivalent(DateTime timea, DateTime timeb) => Math.Abs((timea - timeb).TotalSeconds) < 3;

    public static void DirectoryCopy(string sourceDirName, string destDirName)
    {
        DirectoryInfo sourceDirInfo = new(sourceDirName);
        if (!Directory.Exists(destDirName)) Directory.CreateDirectory(destDirName);
        foreach (FileInfo file in sourceDirInfo.GetFiles())
        {
            string destFileName = Path.Combine(destDirName, file.Name);

            if (NewerOrModified(file, destFileName))
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

        // Remove files that exist on target but not in source
        DirectoryInfo destDirInfo = new(destDirName);
        foreach (FileInfo file in destDirInfo.GetFiles())
        {
            string sourceFileName = Path.Combine(sourceDirName, file.Name);

            if (!File.Exists(sourceFileName))
            {
                File.Delete(file.FullName);
                Console.Write("x");
            }
        }

        DirectoryInfo[] sourceSubDirs = sourceDirInfo.Exists ? sourceDirInfo.GetDirectories() : throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
        foreach (DirectoryInfo subDirInfo in sourceSubDirs)
        {
            DirectoryCopy(subDirInfo.FullName, Path.Combine(destDirName, subDirInfo.Name));
        }

        //Console.WriteLine("---");
        //// Remove subdirs that exist on target but not in source
        //DirectoryInfo[] destSubDirs = destDirInfo.Exists ? destDirInfo.GetDirectories() : throw new DirectoryNotFoundException("Destination directory does not exist or could not be found:" + destDirName);
        //foreach (DirectoryInfo subDirInfo in destSubDirs)
        //{
        //    sourceDirName = Path.Combine(sourceDirName, subDirInfo.Name);
        //    Console.WriteLine($"{subDirInfo.FullName} : {sourceDirName} ?");
        //    if (!Directory.Exists(sourceDirName))
        //    {
        //        Directory.Delete(subDirInfo.FullName, true);
        //        Console.WriteLine("X");
        //    }
        //}
       
    }

    private static bool NewerOrModified(FileInfo file, string destFileName)
    {
        bool shouldCopy = true;

        if (File.Exists(destFileName))
        {
            FileInfo destFile = new(destFileName);

            shouldCopy = file.Length != destFile.Length || !TimesEquivalent(file.LastWriteTimeUtc, destFile.LastWriteTimeUtc);
        }

        return shouldCopy;
    }

    public static void Eject(string drive)
    {
        int num = PowerShell.RunScript("\r\n                function Eject-Drive {\r\n                    Param($DriveLetter)\r\n\t                $driveEject = New-Object -comObject Shell.Application\r\n                    $driveEject.Namespace(17).ParseName($DriveLetter).InvokeVerb('Eject')\r\n                }\r\n                Eject-Drive " + drive);
        if (num != 0)
            Console.WriteLine($"PowerShell returned error: {num}");
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
#pragma warning disable CA1031 // Don't catch generic exception types
                catch { }
#pragma warning restore CA1031
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
        if (namesForIdentifier.Count > 0)
        {
            foreach (string portName in SerialPort.GetPortNames())
            {
                if (namesForIdentifier.Contains(portName))
                    return portName;
            }
            return "";
        }

        return "";
    }

    public static void Install(string pdxPath, bool clean)
    {
        if (pdxPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            InstallPdxZip(pdxPath, clean);

            return;
        }

        Console.WriteLine($"Installing \"{pdxPath}\"...");
        string playdateDrive = MountDataDisk();
        if (string.IsNullOrWhiteSpace(playdateDrive))
        {
            Console.WriteLine("Could not mount data disk.");
        }
        else
        {
            try
            {
                string fileName = Path.GetFileName(pdxPath);
                string str = Path.Combine(playdateDrive, "Games", fileName);
                if (clean && Directory.Exists(str))
                {
                    Console.WriteLine($"Removing existing \"{fileName}\"...");
                    Directory.Delete(str, true);
                }
                Console.WriteLine("Copying files...");
                DirectoryCopy(pdxPath, str);
                Console.WriteLine("\nCopy complete.");
            }
            finally
            {
                Console.WriteLine($"Ejecting {playdateDrive}...");
                while (!string.IsNullOrEmpty(FindPlaydateDrive("PLAYDATE", false)))
                {
                    Thread.Sleep(500);
                    Eject(playdateDrive);
                }
                Console.WriteLine("Ejected.");
            }
        }
    }

    private static void InstallPdxZip(string pdxPath, bool clean)
    {
        Console.WriteLine($"Opening {pdxPath}...");
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(tempDirectory);
            ZipFile.ExtractToDirectory(pdxPath, tempDirectory);

            DirectoryInfo di = new(tempDirectory);
            var pdx = di.GetDirectories().FirstOrDefault(d => d.FullName.EndsWith("pdx", StringComparison.OrdinalIgnoreCase));
            if (pdx != null)
            {
                Install(pdx.FullName, clean);
            }
            else
            {
                Console.WriteLine($"{pdxPath} doesn't seem to be a zipped pdx");
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Console.WriteLine($"Deleting temp directory {tempDirectory}");
                Directory.Delete(tempDirectory, true);
            }
        }
    }

    public static string MountDataDisk() => Mount("datadisk", "PLAYDATE", "data");

    public static string MountRecoveryDisk() => Mount("bootdisk", "BOOTSY", "recovery");

    private static string Mount(string command, string expectedName, string description)
    {
        if (string.IsNullOrWhiteSpace(ComPortName)) return "";
        SendCmd(command, false);
        Console.WriteLine("Waiting for drive to appear...");
        string playdateDrive = FindPlaydateDrive(expectedName, true);
        Console.WriteLine($"Playdate {description} disk mounted as {playdateDrive}");
        return playdateDrive;
    }

    public static void Run(string appPath)
    {
        SendCmd($"run {appPath}", false);
        Console.WriteLine("Command sent.");
    }

    public static string SendCmd(string serCommand, bool readResponse)
    {
        using SerialPort serialPort = OpenComPort();
        serialPort.WriteLine(serCommand);
        var data = "";
        if (readResponse)
        {
            while (serialPort.BytesToRead > 0)
            {
                Thread.Sleep(1000);
                data += serialPort.ReadExisting();
            }
        }
        return data;
    }

    public static void Cmd(string serCommand)
    {
        Console.WriteLine(SendCmd(serCommand, true));
    }

    public static void Shell()
    {
        using SerialPort serialPort = OpenComPort();
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
                        Console.Write(" \b"); ;
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
            Console.WriteLine();
            serialPort.Close();
        }
    }

    public static void Screen(string filename)
    {
        using SerialPort serialPort = OpenComPort();
        serialPort.WriteLine("echo off\r\nscreen");
        while (true)
        {
            // wait for response - starts with ~screen:
            var line = serialPort.ReadLine();
            if (line.StartsWith("~screen:", StringComparison.Ordinal)) break;
        }
        Queue<int> data = [];
        while (serialPort.BytesToRead > 0)
        {
            data.Enqueue(serialPort.ReadByte());
        }

        serialPort.Close();

        using (Bitmap bmp = DecodeRawPlaydateBitmap(data))
        {
            bmp.Save(filename);
        }

        Console.WriteLine($"Written screenshot to {filename}");
    }

    private static Bitmap DecodeRawPlaydateBitmap(Queue<int> data)
    {
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
                bmp.SetPixel(x1 + i, y, (v & 1) == 0 ? Color.Black : Color.White);
                v >>= 1;
                i--;
            }
        }

        return bmp;
    }
}
