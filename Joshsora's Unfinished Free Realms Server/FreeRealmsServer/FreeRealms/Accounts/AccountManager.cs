using System.Collections.Generic;
using System.Linq;
using System.Threading;
using log4net;
using SOE;
using SOE.Core;

namespace FreeRealms.Accounts
{
    public static class AccountManager
    {
        private static readonly object Sync = new object();

        // Components
        private static SOEServer Server;
        private static ILog Log;

        // Account maps
        private static Dictionary<SOEClient, Account> Client2Account;
        private static Dictionary<uint, SOEClient> ID2Client;
        private static Dictionary<uint, Account> ID2Account;
        private static Dictionary<string, Account> Username2Account;

        public static void Start(SOEServer server)
        {
            lock (Sync)
            {
                Server = server;
                Client2Account = new Dictionary<SOEClient, Account>();
                ID2Client = new Dictionary<uint, SOEClient>();
                ID2Account = new Dictionary<uint, Account>();
                Username2Account = new Dictionary<string, Account>();

                Log = Server.Logger.GetLogger("AccountManager");
                Log.Info("Service constructed");
            }

            // LibSOE's bundled ConnectionManager has no OnDisconnect event.
            // Watch its live client slots and remove accounts whose client was
            // disconnected or whose slot has since been reused.
            Thread cleanupThread = new Thread(CleanupDisconnectedClients);
            cleanupThread.Name = "AccountManager::DisconnectCleanup";
            cleanupThread.IsBackground = true;
            cleanupThread.Start();
        }

        private static void CleanupDisconnectedClients()
        {
            while (Server.Running)
            {
                lock (Sync)
                {
                    if (Client2Account != null)
                    {
                        foreach (KeyValuePair<SOEClient, Account> entry in Client2Account.ToArray())
                        {
                            SOEClient client = entry.Key;
                            if (Server.ConnectionManager.GetClient(client.GetClientID()) != client)
                            {
                                LogoutInternal(entry.Value);
                            }
                        }
                    }
                }

                Thread.Sleep(1000);
            }
        }

        public static void OnClientDisconnect(SOEClient client)
        {
            lock (Sync)
            {
                Account account;
                if (Client2Account.TryGetValue(client, out account))
                {
                    LogoutInternal(account);
                }
            }
        }

        public static void AddAccount(SOEClient owner, Account account)
        {
            lock (Sync)
            {
                // Remove a stale session or log out an existing session before
                // inserting the replacement, since LibSOE emits no disconnect event.
                Account existingAccount;
                if (ID2Account.TryGetValue(account.ID, out existingAccount))
                {
                    Log.Warn("A user has logged into an account that is already in use! Disconnecting old client!");
                    SOEClient oldClient = ID2Client[account.ID];
                    oldClient.Disconnect((ushort)SOEDisconnectReasons.NewConnection);
                    LogoutInternal(existingAccount);
                }

                // Avoid stale username entries as well.
                Account existingUsername;
                if (Username2Account.TryGetValue(account.Username, out existingUsername))
                {
                    LogoutInternal(existingUsername);
                }

                Log.InfoFormat("Account '{0}' has successfully logged in!", account.Username);

                ID2Client.Add(account.ID, owner);
                Client2Account.Add(owner, account);
                ID2Account.Add(account.ID, account);
                Username2Account.Add(account.Username, account);
            }
        }

        public static void Logout(Account account)
        {
            lock (Sync)
            {
                LogoutInternal(account);
            }
        }

        private static void LogoutInternal(Account account)
        {
            if (account == null)
            {
                return;
            }

            if (Log != null)
            {
                Log.InfoFormat("Logging out '{0}'!", account.Username);
            }

            SOEClient client;
            if (ID2Account.TryGetValue(account.ID, out Account currentAccount) && currentAccount == account)
            {
                if (ID2Client.TryGetValue(account.ID, out client))
                {
                    Client2Account.Remove(client);
                }

                ID2Client.Remove(account.ID);
                ID2Account.Remove(account.ID);
            }

            if (Username2Account.TryGetValue(account.Username, out Account usernameAccount) && usernameAccount == account)
            {
                Username2Account.Remove(account.Username);
            }
        }

        public static Account GetAccount(SOEClient client)
        {
            lock (Sync)
            {
                Account account;
                return Client2Account.TryGetValue(client, out account) ? account : null;
            }
        }

        public static Account GetAccount(uint id)
        {
            lock (Sync)
            {
                Account account;
                return ID2Account.TryGetValue(id, out account) ? account : null;
            }
        }

        public static Account GetAccount(string username)
        {
            lock (Sync)
            {
                Account account;
                return Username2Account.TryGetValue(username, out account) ? account : null;
            }
        }

        public static SOEClient GetClient(uint id)
        {
            lock (Sync)
            {
                SOEClient client;
                return ID2Client.TryGetValue(id, out client) ? client : null;
            }
        }

        public static bool IsAuthorized(SOEClient client)
        {
            lock (Sync)
            {
                return Client2Account.ContainsKey(client);
            }
        }
    }
}
