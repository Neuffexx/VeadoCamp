using System;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

// Veadotube API
using VeadoTube.BleatCan;

namespace VeadoCamp
{
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
        public required string Id { get; set; }

        [JsonPropertyName("payload")]
        public required Payload Payload { get; set; }
        //public required Dictionary<string, JsonElement> Payload { get; set; } // Using JsonElement to handle mixed types
    }

    // For handling JSON Payload
    public class Payload
    {
        [JsonPropertyName("event")]
        public required string Event { get; set; }

        // Recived when requesting all states, via 'list' event
        [JsonPropertyName("states")]
        public List<State> States { get; set; }

        // Recieved when setting the state, via 'peek' event
        [JsonPropertyName ("state")]
        public string ActiveState { get; set; }
    }

    // Veadotube States
    public class State
    {
        // Constructor
        public State(string id, string name, bool active = false)
        {
            ID = id;
            Name = name;

            Active = active;
        }

        // Recieved Data
        [JsonPropertyName("id")]
        public string ID { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        // Custom Data
        public bool Active { get; set; }
    }



    /// <summary>
    /// Pretty rushed implementation based on GitLab demo to just try and figure out how to send and recieve information from the websocket.
    /// Currently only connects to a single instance and is able to recieve and set the states (randomly for testing).
    /// 
    /// TODO:
    /// - Going to improve project structure and hook it up with the Mountain DisplayPad,
    /// so that it can be controlled from the Display Keys instead.
    /// - Also want to be able to connect to multiple instances, so that pupetering could be possible.
    /// i.e. Controlling seperate arms along with the face using the keys, without running the program multiple times.
    /// (no idea if that is going to be possilbe in a single instance once the non-mini version is released)
    /// - Better way to select which instance to connect to, in case more than one is running. (maybe process selection similar to cheat-engine)
    /// </summary>
    internal class VeadoCamp : IInstancesReceiver, IConnectionReceiver
    {
        // Main Entrance Point to application
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting VeadoCamp Integration...");

            

            var Integration = new VeadoCamp();
            
            // Get instance, last one if multiple
            Console.WriteLine("< active instances (will connect to last one, otherwise will quit)");
            Instance lastInstance = default;
            foreach (var i in Instances.Enumerate())
            {
                lastInstance = i;
                Console.WriteLine($"< ID: {i.id} | Server: {i.server}");
            }

            // No instaces found
            if (!lastInstance.id.isValid)
                return;
            // Server not enabled on instace
            if (lastInstance.server == "0")
                return;

            // Connect to instance
            using var instances = new Instances(Integration);
            Integration.m_Connection = new Connection(lastInstance.server, "VeadoCamp", Integration);

            // Manually send commands / events to websocket
            // For debugging purposes only...
            bool stop = false;
            bool initialized = false;
            Console.CancelKeyPress += (o, e) => stop = true;
            while(!stop)
            {
                Console.WriteLine("\nPress 1 to Refresh List of States, 2 to Set State, 3 to Exit:");
                var key = Console.ReadKey(intercept: true);

                // Initially Load the List of States - Doesnt work initially for some reason, need to manually request for now.
                if (!initialized)
                {
                    Integration.RequestAllStates();
                    initialized = true;
                }


                switch (key.Key)
                {
                    case ConsoleKey.D1:
                        Integration.RequestAllStates();
                        break;
                    case ConsoleKey.D2:
                        string stateId = Integration.GetRandomState().ID;

                        /*// Manually enter ID for debugging
                        Console.WriteLine("\nEnter state ID:");
                        string stateId = Console.ReadLine();*/

                        Integration.SetState(stateId);
                        break;
                    case ConsoleKey.D3:
                        stop = true;
                        break;
                    default:
                        Console.WriteLine("Invalid option. Try again.");
                        break;
                }
            }
        }



        /////////////////////////////////////////////////////////////////////////////////
        /// UTILITY FUNCTIONS
        /////////////////////////////////////////////////////////////////////////////////
        
        public State GetRandomState()
        {
            if (States.Count < 1)
                return null;

            Random random = new Random();
            return States[random.Next(States.Count)];
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



        /////////////////////////////////////////////////////////////////////////////////
        /// WebSocket FUNCTIONS
        /////////////////////////////////////////////////////////////////////////////////

        public void ListNodes()
        {
            string message = "{\"event\": \"list\"}";
            m_Connection.Send("nodes", Encoding.UTF8.GetBytes(message));
        }

        public void SetState(string stateId)
        {
            // Building the JSON string for the payload
            string message = "{\"event\":\"payload\",\"type\":\"stateEvents\",\"id\":\"mini\",\"payload\":{\"event\":\"set\",\"state\":\"" + stateId + "\"}}";
            // Assuming 'connection' is an instance of 'Connection' that has already been established
            m_Connection.Send("nodes", Encoding.UTF8.GetBytes(message));
            Console.WriteLine($"Attempting to set state to {stateId} in 'mini' node...");
        }

        public void RequestAllStates()
        {
            string message = "{\"event\":\"payload\",\"type\":\"stateEvents\",\"id\":\"mini\",\"payload\":{\"event\":\"list\"}}";
            m_Connection.Send("nodes", Encoding.UTF8.GetBytes(message));
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
                Console.WriteLine("From:    " + connection.server   );
                Console.WriteLine("Message: " + message             );
                Console.WriteLine("Channel: " + channel             );


                // Deserialize JSON to C# object
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
                                                States.Clear();  // Clear existing states
                                                foreach (var state in detailedPayload.Payload.States)
                                                {
                                                    States.Add(new State($"{state.ID}", state.Name));
                                                }
                                                Console.WriteLine("\n Updated States List.");
                                                break;
                                            }
                                            case "peek":    // Recieve the currently active state in instance
                                            {
                                                SetActiveState(detailedPayload.Payload.ActiveState);
                                                break;
                                            }
                                            case null:
                                            {
                                                Console.WriteLine("< reply UNKNOWN PAYLOAD TYPE.");
                                                break;
                                            }
                                        }
                                        break;
                                    }
                                    case null:
                                    {
                                        Console.WriteLine("< reply UNKNOWN COMMAND NAME.");
                                        break;
                                    }
                                }

                                break;
                            }
                            case null:
                            {
                                Console.WriteLine("< reply UNKNOWN COMMAND TYPE.");
                                break;
                            }
                        }

                        break;
                    }
                    case null:
                    {
                        Console.WriteLine("< reply UNKNOWN CHANNEL.");
                        break;
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
        private Connection m_Connection;

        public List<State> States = new List<State>();

    }
}
