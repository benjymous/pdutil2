#nullable disable
namespace pdutil;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: pdutil2 <action> [options]");
            Console.WriteLine("  datadisk           - Mount Playdate data partition");
            Console.WriteLine("  install <pdx-path> - Install .pdx to device (only copying newer)");
            Console.WriteLine("  clean <pdx-path>   - Clean install .pdx to device");
            Console.WriteLine("  recoverydisk       - Mount Playdate recovery partition");
            Console.WriteLine("  run <path>         - Run .pdx from device's data partition");
            Console.WriteLine("  cmd <command>      - Send command to the device");
            Console.WriteLine("  shell              - Connect to an interactive serial shell");
            Console.WriteLine("  screen <filename>  - Save a screenshot to filename");
            Environment.Exit(0);
        }
        string arg0 = args[0];
        string arg1 = "";
        if (args.Length >= 2)
            arg1 = args[1];
 

        if (Playdate.ComPortName == "")
        {
            Console.WriteLine("No Playdate device detected.");
            Environment.Exit(1);
        }
        else
            Console.WriteLine("Playdate device detected on " + Playdate.ComPortName);

        switch (arg0)
        {
            case "datadisk":
                Playdate.MountDataDisk();
                break;
            case "install":
            case "clean":
                if (arg1 == "")
                {
                    Console.WriteLine("Please provide the name or path to a .pdx to install.");
                    Environment.Exit(1);
                }
                Playdate.Install(arg1, arg0 == "clean");
                break;


            case "recoverydisk":
                Playdate.MountRecoveryDisk();
                break;
            case "run":
                if (arg1 == "")
                {
                    Console.WriteLine("Please provide the path to an application on device, such as Games/MyGame.pdx");
                    Environment.Exit(1);
                }
                Playdate.Run(arg1);
                break;
            case "cmd":
                if (arg1 == "")
                {
                    Console.WriteLine("Please provide a command to send to the device, such as help");
                    Environment.Exit(1);
                }
                Playdate.Cmd(arg1);
                break;
            case "shell":
                Playdate.Shell();
                break;
            case "screen":
                Playdate.Screen(arg1 ?? "out.png");
                break;
            default:
                Console.WriteLine($"Unknown action \"{arg0}\".");
                break;
        }
    }
}
