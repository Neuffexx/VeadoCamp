using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// DisplayKeys SDK
using DisplayPad.SDK;

namespace VeadoCamp
{
    internal class DisplayPadInterface
    {
        

        //initialize the call back methods
        DisplayPadHelper helper = new DisplayPadHelper();



        /////////////////////////////////////////////////////////////////////////////////
        /// DisplayPad SDK INTERFACE        ---------------        TODO
        /////////////////////////////////////////////////////////////////////////////////

        ////////////////////////////////////////
        // DisplayPadHelper Interface Events


        // For some reason the
        // DisplayPadHelper. ... CallBack 
        // members are not recognized in this project, in the demo however this works without issues?
        // https://github.com/Mountain-BC/DisplayPad.SDK.Demo


        /*//Event will fire when the device is connected or disconnected
        DisplayPadHelper.DisplayPadPlugCallBack += DisplayPadHelper_DisplayPadPlugCallBack;

        //Event will fire when any key is pressed on the device
        DisplayPadHelper.DisplayPadKeyCallBack += DisplayPadHelper_DisplayPadKeyCallBack;

        //Event will fire when updating the firmware and uploading the images
        DisplayPadHelper.DisplayPadProgressCallBack += DisplayPadHelper_DisplayPadProgressCallBack;*/

        

        ////////////////////////////////////////
        // DisplayPadHelper Interface Functions
        

        void DisplayPadHelper_DisplayPadPlugCallBack(int Status, int DeviceId)
        {
            Console.WriteLine("Device status: " + Status + " for Device Id: " + DeviceId.ToString());

            bool PlugSatus = helper.DisplayPadIsDevicePlug(DeviceId);
        }

        void DisplayPadHelper_DisplayPadKeyCallBack(int KeyMatrix, int iPressed, int DeviceID)
        {
            Console.WriteLine("Key status: " + iPressed + " for Device Id: " + DeviceID.ToString());
        }

        void DisplayPadHelper_DisplayPadProgressCallBack(int Percentage)
        {
            Console.WriteLine("Device firmware update Progress status: " + Percentage);
        }
    }
}
