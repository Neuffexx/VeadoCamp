using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;

// DisplayKeys SDK
using DisplayPad.SDK;

namespace VeadoCamp.Integrations
{
    /////////////////////////////////////////////////////////////////////////////////
    /// VeadoTube object classes for Deserializing the Responses
    /////////////////////////////////////////////////////////////////////////////////

    // C# Placeholder(?) Representation of the DisplayPad Keys
    public class Key 
    {
        /// DisplayPad KeyMatrix ID's 
        /// (2 Rows of 6 Keys)
        /// Key 1 ID: 8     | Key 2 ID: 17  | Key 3 ID: 26  | Key 4 ID: 35  | Key 5 ID: 44  | Key 6 ID: 53  | 
        /// Key 7 ID: 62    | Key 8 ID: 71  | Key 9 ID: 80  | Key 10 ID: 89 | Key 11 ID: 98 | Key 12 ID: 125| 

        public Key(int KeyID, Action CallBackFunction) 
        {
            ID = KeyID;
            Action = CallBackFunction;
        }

        public int ID { get; set; }

        public Action Action { get; set; }
    }



    internal class DP
    {
        /////////////////////////////////////////////////////////////////////////////////
        /// UTILITY FUNCTIONS
        /////////////////////////////////////////////////////////////////////////////////

        // Takes the Key ID and performs the KeysRegistered function, if any.
        /// For now just calls a specific function independantly, still
        /// need to figure out how to assign Keys and manage folders on the DisplayPad without destroying profiles...?
        public void PerformVeadoCampAction(int KeyID)
        {
            Console.WriteLine($"Key: {KeyID} | Performing Action: RandomStatus");

            foreach (Key key in Keys)
            {
                if (key.ID == KeyID)
                    key.Action();
            }
        }

        // Debug Only
        public void CreateKeyList(Action CallBack)
        {
            for (int i = 1; i <= 12; i++) 
            {
                if (i == 12)
                {
                    Action Close = new Action(() => System.Environment.Exit(0));
                    Keys.Add(new Key(125, Close));
                }
                else
                    Keys.Add(new Key((i * 9) - 1, CallBack));
            }
        }



        /////////////////////////////////////////////////////////////////////////////////
        /// DisplayPad FUNCTIONS
        /////////////////////////////////////////////////////////////////////////////////

        // Assign Key - To Folder?

        // Assign Function to Key

        // Upload Image to Key

        // Delete Key

        // ...



        /////////////////////////////////////////////////////////////////////////////////
        /// DisplayPad SDK INTERFACE
        /////////////////////////////////////////////////////////////////////////////////

        // Access functions of the SDK directly through the helper Object
        // helper.FooFunction()
        // Register Callbacks through the DisplayPadHelper Class instead
        // DisplayPadHelper.FooCallBack

        ////////////////////////////////////////
        // DisplayPadHelper

        public void RegisterCallbacks() 
        {
            ////////////////////////////////////////
            // DisplayPadHelper CallBack Events

            //Event will fire when the device is connected or disconnected
            DisplayPadHelper.DisplayPadPlugCallBack += DisplayPadHelper_DisplayPadPlugCallBack;

            //Event will fire when any key is pressed on the device
            DisplayPadHelper.DisplayPadKeyCallBack += DisplayPadHelper_DisplayPadKeyCallBack;

            //Event will fire when updating the firmware and uploading the images
            DisplayPadHelper.DisplayPadProgressCallBack += DisplayPadHelper_DisplayPadProgressCallBack;

            // Initial Check, in case Device is connected before program is ran
            if (helper.DisplayPadIsDevicePlug(1))   // If connected before it should be the First (only) ID assigned
                bIsDevicePlugged = true;
            else                                    // If connected mid startup, or after, it will recieve some other ID larger than 1.     TODO: Handle better in future to allow connecting mid run...
                bIsDevicePlugged = false;
        }


        void DisplayPadHelper_DisplayPadPlugCallBack(int Status, int DeviceId)
        {
            Console.WriteLine($"Device status: {Status} for Device Id: {DeviceId.ToString()}");

            // Disconnected
            if (Status == 0) 
            {
                Console.WriteLine("< DisplayPad has been disconnected!");
                System.Environment.Exit(31);

                //bIsDevicePlugged = helper.DisplayPadIsDevicePlug(DeviceId);
            }
            // Connected
            else if(Status == 1) 
            {
                //bIsDevicePlugged = helper.DisplayPadIsDevicePlug(DeviceId);
            }            
        }

        void DisplayPadHelper_DisplayPadKeyCallBack(int KeyMatrix, int iPressed, int DeviceID)
        {
            // Only Perform a Key action on Press, not Release
            if (iPressed == 1)
            {
                // Debug: Remove previous messages
                Console.Clear();

                Console.WriteLine($"KeyMatrix ID: {KeyMatrix} | Key status: {iPressed} for Device Id: {DeviceID.ToString()}");

                // Perform function bound to Key, if Any
                PerformVeadoCampAction(KeyMatrix);
            }
        }

        void DisplayPadHelper_DisplayPadProgressCallBack(int Percentage)
        {
            Console.WriteLine($"Device firmware update Progress status: {Percentage}");
        }



        /////////////////////////////////////////////////////////////////////////////////
        /// MEMBER VARIABLES
        /////////////////////////////////////////////////////////////////////////////////

        public DisplayPadHelper helper = new DisplayPadHelper();

        public bool bIsDevicePlugged = false; // Used to check on Application startup if the Device is connected or not


        // List of all keys 12 (at max in 1 folder)
        /// If more keys are needed, then a folder structure needs to be created as well.
        /// Then each Folder needs to hold its own Key List.
        /// Each Key may will need to be be registered a callback function _OnPressed, to register custom functionality?
        public List<Key> Keys = new List<Key>();
    }
}
