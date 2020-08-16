using System;
using System.Threading;
using Holo.GameConnectionSystem;
using Holo.Managers;
using Holo.Socketservers;
using Holo.Source.Managers;
using Ion.Storage;
using Holo.Source.Managers.RoomManager;

namespace Holo
{
    /// <summary>
    /// The core of Holograph Emulator codename "Eucalypt", contains Main() void for booting server, plus monitoring thread and shutdown void.
    /// </summary>
    public class Eucalypt
    {
        private static Thread serverMonitor = new Thread(new ThreadStart(monitorServer));
        public delegate void commonDelegate();
        public static bool printQueries = false;
        public static string serverVersion = "Sunnie Caching server";

        public static string dbHost;
        public static uint dbPort;
        public static string dbUsername;
        public static string dbPassword;
        public static string dbName;
        public static uint dbMaxConnections;
        public static int dbPool;
        public static DatabaseManager dbManager;
        public static DatabaseManager criticalManager;
        public static Ion.Storage.Database database;

        public static int serverInviteID;
        public static int serverInviteSenderID;
        public static string serverInviteName;
        public static int serverInviteAccepted;
        public static int serverInviteAnswered;
        public static int serverInviteSendTo;
        /// <summary>
        /// Starts up Holograph Emulator codename "Eucalypt".
        /// </summary>
        private static void Main()
        {
            Console.WindowHeight = Console.LargestWindowHeight - 10;
            Console.WindowWidth = Console.LargestWindowWidth - 10;
            Console.CursorVisible = false;
            Console.Title = "SunnieDay Emulator - " + serverVersion;
            Boot();
            //Out.WritePlain("DA" + "dd-MM-yyyy" + "\x02" + DateTime.Now + "\x02");
            while (true)
            {
                try
                {
                    string command = Console.ReadLine();

                    if (command.Equals("shutdown"))
                        Shutdown();
                    if (command.Contains("hotelbericht"))
                        userManager.sendData("BK" + stringManager.getString("scommand_hotelalert") + "\r" + command.Substring(12));
                }
                catch (Exception e) { Out.writeSeriousError(e.ToString()); }
            }

        }
        /// <summary>
        /// Boots the emulator.
        /// </summary>
        private static void Boot()
        {
            TimeSpan _TST;
            ThreadPool.SetMaxThreads(300,400);
            DateTime _START = DateTime.Now;
            string sqlConfigLocation = IO.workingDirectory + @"\bin\mysql.ini";
            if (System.IO.File.Exists(sqlConfigLocation) == false)
            {
                Out.WriteError("mysql.ini not found at " + sqlConfigLocation);
                Shutdown();
                return;
            }
           
            Out.WriteLine("mysql.ini found at " + sqlConfigLocation);

            dbHost = IO.readINI("mysql", "host", sqlConfigLocation);
            dbPort = uint.Parse(IO.readINI("mysql", "port", sqlConfigLocation));
            dbUsername = IO.readINI("mysql", "username", sqlConfigLocation);
            dbPassword = IO.readINI("mysql", "password", sqlConfigLocation);
            dbName = IO.readINI("mysql", "database", sqlConfigLocation);
            dbMaxConnections = uint.Parse(IO.readINI("mysql", "clientamount", sqlConfigLocation));
            Out.WriteBlank();
            dbManager = new DatabaseManager(dbHost, dbPort, dbUsername, dbPassword, dbName, 0, 10, true);
            dbManager.SetClientAmount(dbMaxConnections);
            dbManager.StartMonitor();
            criticalManager = new DatabaseManager(dbHost, dbPort, dbUsername, dbPassword, dbName, 0, 20, true);
            criticalManager.SetClientAmount(20);
            criticalManager.StartMonitor();
            int gamePort;
            int gameMaxConnections;
            int musPort;
            int musMaxConnections;
            string musHost;

            try
            {
                gamePort = int.Parse(Config.getTableEntry("server_game_port"));
                gameMaxConnections = 5000;// int.Parse(Config.getTableEntry("server_game_maxconnections"));
                musPort = int.Parse(Config.getTableEntry("server_mus_port"));
                musMaxConnections = int.Parse(Config.getTableEntry("server_mus_maxconnections"));
                musHost = Config.getTableEntry("server_mus_host");
            }
            catch
            {
                Out.WriteError("system_config table contains invalid values for socket server configuration!");
                Shutdown();
                return;
            }

            string langExt = Config.getTableEntry("lang");
            if (langExt == "")
            {
                Out.WriteError("No valid language extension [field: lang] was set in the system_config table!");
                Shutdown();
                return;
            }



            stringManager.Init(langExt, false);
            Out.WriteBlank();

            stringManager.initFilter(false);
            Out.WriteBlank();

            Config.Init(false);
            Out.WriteBlank();

            catalogueManager.Init();
            Out.WriteBlank();
            
            NavigatorHelper.init();
            Out.WriteBlank();

            navigatorManager.Init();
            Out.WriteBlank();

            recyclerManager.Init(false);
            Out.WriteBlank();

            rankManager.Init(false);
            Out.WriteBlank();

            roomManager.Init();
            Out.WriteBlank();

            userManager.Init();
            eventManager.Init();
            
            Out.WriteBlank();
            
            resetDynamics();

            Out.WriteBlank();

            gameSocketServer.SetupSocket();

            if (musSocketServer.Init(musPort, musHost) == false)
            {
                Shutdown();
                return;
            }

            Out.WriteBlank();

            
            _TST = DateTime.Now - _START;
            Out.WriteLine("Startup time in fixed milliseconds: " + _TST.TotalMilliseconds.ToString() + ".");
           

            GC.Collect();
            Out.WriteLine("Holograph Emulator is ready for Connections");
            Out.WriteBlank();
            
            Out.minimumImportance = Out.logFlags.MehAction; // All logs
            serverMonitor.Priority = ThreadPriority.Lowest;
            serverMonitor.Start();
        }
        /// <summary>
        /// Safely shutdowns Holograph Emulator, closing database and socket connections. Requires key press from user for final abort.
        /// </summary>
        public static void Shutdown()
        {
            Out.WriteBlank();
           
            gameSocketServer.AcceptConnections = false;
            userManager.disconnectAllUsers();
            gameSocketServer.Destroy(); 
            roomManager.saveAllRooms();
            navigatorManager.killAllActivity();
            try
            {
                dbManager.StopMonitor();
                dbManager.DestroyClients();
                dbManager.DestroyManager();
            }
            catch { }
            try
            {
                criticalManager.StopMonitor();
                criticalManager.DestroyClients();
                criticalManager.DestroyManager();
            }
            catch { }
            try
            {
                if (serverMonitor.IsAlive)
                    serverMonitor.Abort();
            }
            catch { }
            Out.WriteLine("Shutdown completed.");
            Console.ReadKey(true);
            Environment.Exit(2);
        }

        private static void resetDynamics()
        {
            int maxOnline = 0;
            using (DatabaseClient dbClient = criticalManager.GetClient())
            {
                maxOnline = dbClient.getInt("SELECT onlinecount_peak FROM system");
                dbClient.runQuery("UPDATE system SET onlinecount = '0',onlinecount_peak = '" + maxOnline + "',connections_accepted = '0',activerooms = '0'");
                dbClient.runQuery("UPDATE users SET ticket_sso = NULL");
                Out.WriteLine("Login tickets nulled.");
                dbClient.runQuery("UPDATE rooms SET visitors_now = '0'");
                Out.WriteLine("Room inside counts reset.");
                dbClient.runQuery("TRUNCATE TABLE `events`");
                Out.WriteLine("Events table truncated");
            }
            Out.WriteLine("Client connection statistics reset.");
        }
        /// <summary>
        /// Threaded void. Ran on background thread at lowest priority, interval = 3500 ms. Updates console title and online users count, active rooms count, peak connections count and peak online users count in database.
        /// </summary>
        private static void monitorServer()
        {
            int i = 0;
            try
            {
                while (true)
                {
                    int onlineCount = userManager.userCount;
                    int peakOnlineCount = userManager.peakUserCount;
                    int roomCount = roomManager.roomCount;
                    int peakRoomCount = roomManager.peakRoomCount;

                    int starvationNumber = dbManager.getStarvationNumber();
                    int acceptedConnections = gameSocketServer.acceptedConnections;
                    long memUsage = GC.GetTotalMemory(false) / 1024;
                    Console.Title = "Sunny Hotel: | online users: " + onlineCount + " | loaded rooms " + roomCount + " | RAM usage: " + memUsage + "KB | SQL Connections: | " + dbManager.databaseClients + " | Queries done: " + queries;
                    using (DatabaseClient dbClient = criticalManager.GetClient())
                    {
                        dbClient.runQuery("UPDATE system SET onlinecount = '" + onlineCount + "',onlinecount_peak = '" + peakOnlineCount + "',activerooms = '" + roomCount + "',activerooms_peak = '" + peakRoomCount + "',connections_accepted = '" + acceptedConnections + "'");
                    }
                    i++;
                    if (i == 6)
                    {
                        userManager.serverSave();
                        i = 0;
                    }
                    Thread.Sleep(10000);
                    //Out.WritePlain("Servermonitor loop");
                }
            }
            catch (ThreadAbortException) { } //nuthin' special
            catch (Exception e) { Out.writeSeriousError(e.ToString()); Eucalypt.Shutdown(); }
        }
        public static void addQuery()
        {
            queries++;
        }
        private static int queries;
    }
 
}