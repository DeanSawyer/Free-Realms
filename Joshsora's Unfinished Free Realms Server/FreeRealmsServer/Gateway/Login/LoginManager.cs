using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading;
using log4net;
using Newtonsoft.Json.Linq;
using System.IO;

using SOE;
using SOE.Core;
using SOE.Interfaces;

using FreeRealms;
using FreeRealms.Accounts;

namespace Gateway.Login
{
    public static class LoginManager
    {
        // Components
        private static SOEServer Server;
        private static ILog Log;

        // Login queue
        private static ConcurrentQueue<LoginRequest> LoginQueue = new ConcurrentQueue<LoginRequest>();

        public static void Start(SOEServer server=null)
        {
            // Set our server
            if (server == null)
            {
                Server = Gateway.Server;
            }
            else
            {
                Server = server;
            }

            // Thread
            int threadSleep = Server.Configuration["ServerThreadSleep"];
            Thread loginThread = new Thread((threadStart) =>
            {
                while (Server.Running)
                {
                    LoginRequest request;

                    // Do a cycle
                    if (LoginQueue.TryDequeue(out request))
                    {
                        HandleLoginRequest(request);
                    }

                    // Sleep
                    Thread.Sleep(threadSleep);
                }
            });
            loginThread.Start();

            // Logging
            Log = Server.Logger.GetLogger("LoginManager");
            Log.Info("Service constructed");
        }

            // Login Handler
        [SOEMessageHandler("LoginRequest", (ushort)LoginMessages.LoginRequest, "CGAPI_527")]
        public static void HandleLoginRequest(SOEClient sender, SOEMessage message)
        {
            // Setup a reader
            SOEReader reader = new SOEReader(message);

            // Get the login details
            string clientToken = reader.ReadASCIIString();
            reader.ReadUInt64();
            string clientVersion = reader.ReadASCIIString();

            // Correct client version?
            Log.DebugFormat("Received login request from client {0}, token: {1}, version: {2}", sender.GetClientID(), clientToken, clientVersion);
            string expectedClientVersion = Server.ApplicationConfiguration["ClientVersion"];
            if (clientVersion != expectedClientVersion)
            {
                // Error!
                Log.ErrorFormat("Received login request from outdated client: {0}", clientVersion);

                SendLoginResponse(sender, false);
                sender.Disconnect((ushort)SOEDisconnectReasons.Application);
                return;
            }

            // Create a login request and enqueue it
            LoginRequest request = new LoginRequest(sender, clientToken, clientVersion);
            LoginQueue.Enqueue(request);
        }

        public static void HandleLoginRequest(LoginRequest request)
        {
            // Get variables
            // Local development mode deliberately bypasses the retired WebAPI.
            // Keep this enabled only for a server bound to localhost.
            if ((bool)Server.ApplicationConfiguration["LocalMode"])
            {
                if (request.Token != "LOCAL-ONLY")
                {
                    Log.ErrorFormat("Rejected invalid local login ticket from client {0}!", request.Client.GetClientID());
                    SendLoginResponse(request.Client, false);
                    request.Client.Disconnect((ushort)SOEDisconnectReasons.Application);
                    return;
                }

                Account localAccount = new Account
                {
                    ID = 1,
                    Username = "LocalPlayer",
                    AccessLevel = 200,
                    Banned = false
                };

                Log.Info("Successful local development login!");
                SendLoginResponse(request.Client, true);
                AccountManager.AddAccount(request.Client, localAccount);
                SendInitialWorldState(request.Client);
                return;
            }

            string loginServer = Server.ApplicationConfiguration["LoginServer"];
            string apiRequest = loginServer + "consumeToken/";
            JObject response = null;

            using (var client = new WebClient { Proxy = null })
            {
                var values = new NameValueCollection();
                values["token"] = request.Token;

                var responseRaw = client.UploadValues(apiRequest, values);
                var responseString = Encoding.ASCII.GetString(responseRaw);
                try
                {
                    response = JObject.Parse(responseString);
                }
                catch (Exception)
                {
                    Log.Fatal("Received bad response from login server!");
                    Environment.Exit(0);
                }
            }

            if (response != null)
            {
                bool success = response["success"].ToObject<bool>();

                if (success)
                {
                    // Get the account!
                    Account userAccount = response["account"].ToObject<Account>();

                    // Is the user banned?
                    if (userAccount.Banned)
                    {
                        // Report the attempt and disconnect them
                        Log.ErrorFormat("Received successful login for client {0} but this user is banned!",
                            request.Client.GetClientID());
                        SendLoginResponse(request.Client, false);

                        request.Client.Disconnect((ushort) SOEDisconnectReasons.Application);
                    }
                    else
                    {
                        // Check if they're allowed to be on this server
                        if (userAccount.AccessLevel < (int)Server.ApplicationConfiguration["MinimumAccessLevel"])
                        {
                            // Log the attempt
                            Log.ErrorFormat("Received successful login for client {0} but this user doesn't meet the required minimum access level!",
                                request.Client.GetClientID());
                            SendLoginResponse(request.Client, false);

                            // Disconnect them
                            request.Client.Disconnect((ushort)SOEDisconnectReasons.Application);
                            return;
                        }

                        // Send the succesful login response!
                        Log.Info("Successful login!");
                        SendLoginResponse(request.Client, true);

                        // Manage the account on the gateway
                        AccountManager.AddAccount(request.Client, userAccount);

                        SendInitialWorldState(request.Client);
                    }
                }
                else
                {
                    // Send an unsucessful login response
                    Log.ErrorFormat("Received invalid token from client {0}!", request.Client.GetClientID());
                    SendLoginResponse(request.Client, false);

                    // Disconnect the requesting Client
                    request.Client.Disconnect((ushort)SOEDisconnectReasons.Application);
                }
            }
        }

        private static void SendInitialWorldState(SOEClient client)
        {
            // The world-routing handshake is not implemented yet. Send the same
            // environment and zone announcements the original gateway used.
            SOEWriter commandWriter = new SOEWriter(0xA6, true);
            byte[] environmentName = Encoding.ASCII.GetBytes("live");
            commandWriter.AddHostUInt32((uint)environmentName.Length);
            commandWriter.AddBytes(environmentName);
            byte[] environmentCommand = commandWriter.GetRaw();

            SOEWriter writer = new SOEWriter((ushort)GatewayMessages.EnqueueCommand, true);
            writer.AddBoolean(true);
            writer.AddHostUInt32((uint)environmentCommand.Length);
            writer.AddBytes(environmentCommand);
            SOEMessage environmentMessage = writer.GetFinalSOEMessage(client);

            commandWriter = new SOEWriter(0x2B, true);
            byte[] worldName = Encoding.ASCII.GetBytes("FabledRealms");
            commandWriter.AddHostUInt32((uint)worldName.Length);
            commandWriter.AddBytes(worldName);
            commandWriter.AddHostUInt32(2);
            commandWriter.AddHostUInt32(0);
            commandWriter.AddHostUInt32(0);
            commandWriter.AddUInt32(5);
            commandWriter.AddHostUInt32(0);
            commandWriter.AddBoolean(false);
            byte[] worldCommand = commandWriter.GetRaw();

            writer = new SOEWriter((ushort)GatewayMessages.EnqueueCommand, true);
            writer.AddBoolean(true);
            writer.AddHostUInt32((uint)worldCommand.Length);
            writer.AddBytes(worldCommand);
            SOEMessage worldMessage = writer.GetFinalSOEMessage(client);

            client.SendMessage(environmentMessage);
            client.SendMessage(worldMessage);
        }

        public static void SendLoginResponse(SOEClient client, bool success)
        {
            // Login Reply
            SOEWriter writer = new SOEWriter((ushort)LoginMessages.LoginResponse, true);

            // Success?
            writer.AddBoolean(success);

            // Send the reply!
            SOEMessage reply = writer.GetFinalSOEMessage(client);
            client.SendMessage(reply);
        }
    }
}
