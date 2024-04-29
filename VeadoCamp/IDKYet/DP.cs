using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;

// DisplayKeys SDK
using DisplayPad.SDK;

namespace VeadoCamp.IDKYet
{
    internal class DP
    {
        /////////////////////////////////////////////////////////////////////////////////
        /// DP SDK INTERFACE        ---------------        TODO
        /////////////////////////////////////////////////////////////////////////////////

        // Access functions of the SDK directly through the helper Object
        // helper.FooFunction()
        // Register Callbacks through the DisplayPadHelper Class instead
        // DisplayPadHelper.FooCallBack

        // Constructor
        /*public DP()
        { 

        }*/


        ////////////////////////////////////////
        // DisplayPadHelper Interface Functions

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
            if (helper.DisplayPadIsDevicePlug(1)) // If connected before it should be the First (only) ID assigned
                bIsDevicePlugged = true;
            else if (helper.DisplayPadIsDevicePlug(2)) // If connected mid startup, edge-case, but it will recieve second ID
                bIsDevicePlugged = true;
            else
                bIsDevicePlugged = false;
        }


        void DisplayPadHelper_DisplayPadPlugCallBack(int Status, int DeviceId)
        {
            Console.WriteLine($"Device status: {Status} for Device Id: {DeviceId.ToString()}");

            bIsDevicePlugged = helper.DisplayPadIsDevicePlug(DeviceId);
        }

        void DisplayPadHelper_DisplayPadKeyCallBack(int KeyMatrix, int iPressed, int DeviceID)
        {
            Console.WriteLine($"KeyMatrix ID: {KeyMatrix} | Key status: {iPressed} for Device Id: {DeviceID.ToString()}");
        }

        void DisplayPadHelper_DisplayPadProgressCallBack(int Percentage)
        {
            Console.WriteLine($"Device firmware update Progress status: {Percentage}");
        }



        /////////////////////////////////////////////////////////////////////////////////
        /// MEMBER VARIABLES
        /////////////////////////////////////////////////////////////////////////////////

        public DisplayPadHelper helper = new DisplayPadHelper();

        public bool bIsDevicePlugged = false;
    }
}
