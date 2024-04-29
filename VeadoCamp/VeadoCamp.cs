using DisplayPad.SDK;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using static System.Runtime.InteropServices.JavaScript.JSType;

using VeadoTube.BleatCan;
using VeadoCamp.Integrations;

////////////////////////////////////////////
// Exit Codes Overview:
///
//  General System
/// 0 for Successful completion
/// 1 for general errors
/// 2 for incorrect usage of the application(e.g., wrong number of arguments)
/// 3 for specific failures (e.g., unable to read a file)
/// 255 for unexpected errors
/// 
//  Examples
/// 30 = VeadoTube instance not found
/// 31 = DisplayPad not found / disconnected
////////////////////////////////////////////


/// <summary>
/// Pretty rushed implementation based on GitLab demo to just try and figure out how to send and recieve information from the websocket.
/// Currently only connects to a single instance and is able to recieve and set the states (randomly for testing).
/// 
/// TODO:
/// - Going to improve project structure and hook it up with the Mountain DP,
/// so that it can be controlled from the Display Keys instead.
/// - Also want to be able to connect to multiple instances, so that pupetering could be possible.
/// i.e. Controlling seperate arms along with the face using the keys, without running the program multiple times.
/// (no idea if that is going to be possilbe in a single instance once the non-mini version is released)
/// - Better way to select which instance to connect to, in case more than one is running. (maybe process selection similar to cheat-engine)
/// </summary>
namespace VeadoCamp
{
    internal class VeadoCamp
    {
        // Main Entrance Point to application
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting VeadoCamp Integration...");


            // Create veadotube api interface
            VT VeadoIntegration = new VT();
            connectToVTInstance(VeadoIntegration);

            //Connect to DP Device
            DP DPIntegration = new DP();
            connectToDevice(DPIntegration, VeadoIntegration);

            // Manually send commands / events to websocket
            // For debugging purposes only...
            bool stop = false;
            bool VTStatesInitialized = false;
            Console.CancelKeyPress += (o, e) => stop = true;
            while (!stop)
            {
                // Initially Load the List of States - Doesnt work initially for some reason, need to manually request for now.
                if (!VTStatesInitialized)
                {
                    VeadoIntegration.RequestAllStates();
                    VTStatesInitialized = true;
                }

                // Only Print this line if a non-displayPad button is Pressed
                Console.WriteLine(">>>>>>> Waiting on Input from DisplayPad......");
                Console.ReadKey(intercept: true);

                /*
                // Manual Debugging without DP
                Console.WriteLine("\nPress 1 to Refresh List of States, 2 to Set State, 3 to Exit:");
                var key = Console.ReadKey(intercept: true);


                switch (key.Key)
                {
                    case ConsoleKey.D1:
                        VeadoIntegration.RequestAllStates();
                        break;
                    case ConsoleKey.D2:
                        string stateId = VeadoIntegration.GetRandomState().ID;

                        // Manually enter ID for debugging
                        //Console.WriteLine("\nEnter state ID:");
                        //string stateId = Console.ReadLine();

                        VeadoIntegration.SetState(stateId);
                        break;
                    case ConsoleKey.D3:
                        stop = true;
                        break;
                    default:
                        Console.WriteLine("Invalid option. Try again.");
                        break;
                }
                */
            }
        }

        static void connectToVTInstance(VT VeadoIntegration)
        {
            // Get instance, last one if multiple
            Console.WriteLine("< active instances (will connect to last one, otherwise will quit)");
            Instance lastInstance = default;
            foreach (var i in Instances.Enumerate())
            {
                lastInstance = i;
                Console.WriteLine($"< ID: {i.id} | Server: {i.server}");
            }

            // No instaces found         || Server not enabled on instace
            if (!lastInstance.id.isValid || lastInstance.server == "0")
                System.Environment.Exit(30);

            // Connect to instance
            using var instances = new Instances(VeadoIntegration);
            VeadoIntegration.m_Connection = new Connection(lastInstance.server, "VeadoCamp", VeadoIntegration);
        }

        static void connectToDevice(DP DPIntegration, VT VeadoIntegration)
        {
            //Check if device is connected
            Console.WriteLine("< connected DisplayPad (checks for DisplayPad, otherwise will quit)");

            DPIntegration.RegisterCallbacks();

            // No device found, close program
            if (!DPIntegration.bIsDevicePlugged)
                System.Environment.Exit(31);

            // DEBUG: Register a function to all keys
            DPIntegration.CreateKeyList(() => VeadoIntegration.SetState(VeadoIntegration.GetRandomState().ID));
        }
    }
}
