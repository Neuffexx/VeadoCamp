﻿using System;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

// VT API
/// The 'BleatCan' Project was taken from GitLab by Olmewe: https://gitlab.com/veadotube/bleatcan
using VeadoTube.BleatCan;

namespace VeadoCamp.Integrations
{
    /////////////////////////////////////////////////////////////////////////////////
    /// VeadoTube object classes for Deserializing the Responses
    /////////////////////////////////////////////////////////////////////////////////

    // For handling JSON Reply
    public class RootObject
    {
        [JsonPropertyName("event")]
        public required string Event { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("id")]
        public required string Id { get; set; } // Currently always 'mini'

        [JsonPropertyName("payload")]
        public required Payload Payload { get; set; }
        //public required Dictionary<string, JsonElement> Payload { get; set; } // Using JsonElement to handle mixed types
    }

    // For handling JSON Payload
    public class Payload
    {
        // This will inform us of what type of Payload we actually received!
        [JsonPropertyName("event")]
        public required string Event { get; set; }

        // Recived when requesting all states, via 'list' event
        [JsonPropertyName("states")]
        public List<State> States { get; set; }

        // Recieved when setting the state, via 'peek' event
        [JsonPropertyName("state")]
        public string ActiveState { get; set; }

        // Recieved when requesting the Image of a state, via 'thumb' event
        [JsonPropertyName("width")]
        public int pngWidth { get; set; }

        [JsonPropertyName("height")]
        public int pngHeight { get; set; }

        [JsonPropertyName("png")]
        public string PNG { get; set; }
    }

    // VT States
    public class State
    {
        // Constructor
        public State(string id, string name, Image image = null, bool active = false)
        {
            ID = id;
            Name = name;
            Image = image;

            Active = active;
        }

        // Recieved Data
        [JsonPropertyName("id")]
        public string ID { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        public Image Image { get; set; }

        // Custom Data
        public bool Active { get; set; }
    }

    // VT State Images
    public class Image
    {
        public Image(int width, int height, string imageData64)
        {
            Width = width;

            Height = height;

            Image64 = imageData64;
        }

        [JsonPropertyName("width")]
        public int Width;

        [JsonPropertyName("height")]
        public int Height;

        [JsonPropertyName("png")]
        public string Image64;
    }



    // Functionality class to access all of the required data/functionality to communicate with
    // VeadoTube, as well as what will be used by the DisplayPad to interact with the software.
    internal class VT : IInstancesReceiver, IConnectionReceiver
    {
        /////////////////////////////////////////////////////////////////////////////////
        /// UTILITY FUNCTIONS
        /////////////////////////////////////////////////////////////////////////////////

        // Gets a randomk State from the 'States' array
        public State GetRandomState()
        {
            if (States.Count < 1)
                return null;

            Random random = new Random();
            return States[random.Next(States.Count)];
        }

        // Gets a State based on its ID
        // ID is an Int simply to allow override based on name as they are both string
        public State GetState(int ID) 
        {
            foreach(var state in States)
            {
                if(state.ID == ID.ToString())
                    return state;
            }

            return null;
        }

        //Gets a State based on its Name
        public State GetState(string Name)
        {
            foreach (var state in States)
            {
                if (state.Name == Name)
                    return state;
            }

            return null;
        }

        // Done here instead of the 'SetState' function,
        // because the request might fail for whatever reason desyncinc state representation with Instance.
        public void SetActiveState(string ActiveID)
        {
            // Invalidate all States
            foreach (var state in States)
            {
                state.Active = false;

                // Set wanted state to Active
                if (state.ID == ActiveID)
                    state.Active = true;
            }
        }

        // DEBUGGING ONLY
        // Converts the Base64 Encoded version from VeadoTube to a .png
        // and saves it to disk (desktop)
        public void SaveImageFromBase64(string base64Image, string filename)
        {
            // Get the path to the user's desktop
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            // Combine the desktop path with the filename parameter to create the full path
            string filePath = Path.Combine(desktopPath, filename);

            // Ensure the filename ends with '.png' to save as a PNG file, since they will likely be taken directly from state
            if (!filePath.EndsWith(".png"))
            {
                filePath += ".png";
            }

            try
            {
                // Convert Base64 String to byte[]
                byte[] imageBytes = Convert.FromBase64String(base64Image);

                // Write the bytes to a file
                using (var imageFile = new FileStream(filePath, FileMode.Create))
                {
                    imageFile.Write(imageBytes, 0, imageBytes.Length);
                    imageFile.Flush(); // Ensure all bytes are written to the file
                }

                Console.WriteLine($"Image saved to {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving image: " + ex.Message);
            }
        }



        /////////////////////////////////////////////////////////////////////////////////
        /// WebSocket FUNCTIONS
        /////////////////////////////////////////////////////////////////////////////////

        public void ListNodes()
        {
            string message = "{\"event\": \"list\"}";
            m_Connection.Send("nodes", Encoding.UTF8.GetBytes(message));
        }

        public void SetState(string stateID)
        {
            // Building the JSON string for the payload
            string message = "{\"event\":\"payload\",\"type\":\"stateEvents\",\"id\":\"mini\",\"payload\":{\"event\":\"set\",\"state\":\"" + stateID + "\"}}";
            // Assuming 'connection' is an instance of 'Connection' that has already been established
            m_Connection.Send("nodes", Encoding.UTF8.GetBytes(message));
            Console.WriteLine($"Attempting to set state to [ {stateID} | {GetState(int.Parse(stateID)).Name} ] in 'mini' node...");
        }

        public void RequestAllStates()
        {
            string message = "{\"event\":\"payload\",\"type\":\"stateEvents\",\"id\":\"mini\",\"payload\":{\"event\":\"list\"}}";
            m_Connection.Send("nodes", Encoding.UTF8.GetBytes(message));
            Console.WriteLine($"Requesting List of Current State");
        }

        public void RequestStateImage(string stateID)
        {
            string message = "{\"event\":\"payload\",\"type\":\"stateEvents\",\"id\":\"mini\",\"payload\":{\"event\":\"thumb\",\"state\":\"" + stateID + "\"}}";
            m_Connection.Send("nodes", Encoding.UTF8.GetBytes(message));
            Console.WriteLine($"Requesting State Image for StateID: {stateID}");
        }

        public void StartStateMonitor()
        {
            string message = "{\"event\": \"listen\", \"token\": \"uniqueID\"}"; // Replace "uniqueID" with an actual token if needed
            m_Connection.Send("stateEvents", Encoding.UTF8.GetBytes(message));
        }

        public void EndStateMonitor()
        {
            string message = "{\"event\": \"unlisten\", \"token\": \"uniqueID\"}"; // Use the same ID that was used for listening
            m_Connection.Send("stateEvents", Encoding.UTF8.GetBytes(message));
        }



        /////////////////////////////////////////////////////////////////////////////////
        /// Websocket INTERFACE FUNCTIONS
        /////////////////////////////////////////////////////////////////////////////////


        ////////////////////////////////////////
        // IInstanceReciever

        public void OnStart(Instance instance)
        {
            Console.WriteLine($"< started instance: {instance.id} ({instance.server})");
        }

        public void OnChange(Instance instance)
        {
            Console.WriteLine($"< changed instance: {instance.id} ({instance.server})");
        }

        public void OnEnd(InstanceID id)
        {
            Console.WriteLine($"< ended instance: {id}");
        }


        ////////////////////////////////////////
        // IConnectionReciever

        public void OnError(Connection connection, ConnectionError error)
        {
            Console.WriteLine($"connection error: {error}");
        }

        public void OnConnect(Connection connection, bool active)
        {
            Console.WriteLine($"connected: {active} | to {connection.server}");
        }

        public void OnReceive(Connection connection, string channel, ReadOnlySpan<byte> data)
        {
            Console.WriteLine($"Received on channel: {channel}");
            try
            {
                // Grab JSON Reply
                string message = Encoding.UTF8.GetString(data).TrimEnd('\0');

                // Debugging
                Console.WriteLine("From:                " + connection.server);
                Console.WriteLine("Message:             " + message);
                Console.WriteLine("Channel:             " + channel);


                // Deserialize JSON to temporary C# object
                RootObject detailedPayload = JsonSerializer.Deserialize<RootObject>(message);


                // Do stuff with reply
                // Yes I used switches for everything, because I felt like it... I work with collapsed code
                // Reply Channel
                switch (channel)
                {
                    case "nodes":
                        {
                            // Command Type 
                            switch (detailedPayload.Type)
                            {
                                case "stateEvents":
                                    {
                                        // Command Name
                                        switch (detailedPayload.Name)
                                        {
                                            case "avatar state":
                                                {
                                                    // Payload Type
                                                    switch (detailedPayload.Payload.Event)
                                                    {
                                                        case "list":    // Recieve a list of states that the instance holds
                                                            {
                                                                States.Clear();  // Clear existing States
                                                                foreach (var state in detailedPayload.Payload.States)
                                                                {
                                                                    // Create State
                                                                    States.Add(new State($"{state.ID}", state.Name));
                                                                    // Get the Image of the State
                                                                    RequestStateImage(state.ID);
                                                                }
                                                                Console.WriteLine("\n Updated States List.");
                                                                break;
                                                            }
                                                        case "peek":    // Recieve the currently ACTIVE State in Instance
                                                            {
                                                                SetActiveState(detailedPayload.Payload.ActiveState);
                                                                break;
                                                            }
                                                        case "thumb":   // Recieve the Image of a State
                                                            {
                                                                // Check if image was recieved / deserialized correctly
                                                                if (string.IsNullOrEmpty(detailedPayload.Payload.PNG) ||
                                                                    detailedPayload.Payload.pngWidth < 0 ||
                                                                    detailedPayload.Payload.pngHeight < 0)
                                                                    throw new Exception("Image was NULL!");

                                                                // Save Image
                                                                foreach (var state in States)
                                                                {
                                                                    if (state.ID == detailedPayload.Payload.ActiveState)
                                                                    {
                                                                        state.Image = new Image(detailedPayload.Payload.pngWidth, detailedPayload.Payload.pngHeight, detailedPayload.Payload.PNG);
                                                                        // DEBUG: Save them to the Desktop in order to view them
                                                                        //SaveImageFromBase64(state.Image.Image64, state.Name);
                                                                    }
                                                                }
                                                                break;
                                                            }
                                                        case null:
                                                            {
                                                                throw new Exception("UNKNOWN PAYLOAD TYPE!");
                                                            }
                                                    } // End Event
                                                    break;
                                                }
                                            case null:
                                                {
                                                    throw new Exception("UNKNOWN COMMAND NAME!");
                                                }
                                        } // End Name

                                        break;
                                    }
                                case null:
                                    {
                                        throw new Exception("UNKNOWN COMMAND TYPE!");
                                    }
                            } // End Type

                            break;
                        }
                    case null:
                        {
                            throw new Exception("reply UNKNOWN CHANNEL!");
                        }
                } // End of Switch


            }
            catch (Exception ex)
            {
                Console.WriteLine($"< ERROR: Failed to parse message: {ex.Message}");
                Console.WriteLine($"< ERROR: Failed Instance: {ex.InnerException}");
            }
        }



        /////////////////////////////////////////////////////////////////////////////////
        /// MEMBER VARIABLES
        /////////////////////////////////////////////////////////////////////////////////
        public Connection m_Connection;

        public List<State> States = new List<State>();

    }
}
