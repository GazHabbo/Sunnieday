using Ion.Storage;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading;

using Holo.Managers;
using Holo.Virtual.Item;
using Holo.Virtual.Rooms;
using Holo.Source.Managers;
using Holo.Managers.catalogue;
using Holo.Virtual.Users.Items;
using Holo.Virtual.Rooms.Games;
using Holo.Source.Virtual.Users;
using Holo.Virtual.Users.Messenger;
using Holo.Source.Virtual.Rooms.Pets;
using Holo.Source.Managers.RoomManager;
using Holo.Source.GameConnectionSystem;
using Holo.Source.Managers.Catalogue_manager;
using Holo.Source.Virtual.Users.ItemManagement;
using Holo.Virtual.Rooms.Games.Wobble_Squabble;
using Holo.Source.Virtual.Rooms;

namespace Holo.Virtual.Users
{

    /// <summary>
    /// Represents a virtual user, with connection and packet handling, access management etc etc. The details about the user are kept separate in a different class.
    /// </summary>
    public class virtualUser
    {
        #region declares
        internal bool isGeest = false;
        /// <summary>
        /// magicTile information
        /// </summary>
        internal Rooms.Pathfinding.Coord[] spawnLoc = new Rooms.Pathfinding.Coord[2] { new Rooms.Pathfinding.Coord(-1, -1), new Rooms.Pathfinding.Coord(-1, -1) };
        internal string spawnStatus = "";
        /// <summary>
        /// The ID of the connection for this virtual user. Assigned by the game socket server.
        /// </summary>
        private gameConnection _ConnectionManager;

        private string currentPacket
        {
            get
            {
                return this._ConnectionManager.getPacket();
            }
        }
        public string connectionRemoteIP
        {
            get
            {
                return _ConnectionManager.connectionRemoteIP;
            }
        }
        /// <summary>
        /// The byte array where the data is saved in while receiving the data asynchronously.
        /// </summary>
        private byte[] dataBuffer = new byte[1024];
        /// <summary>
        /// Specifies if the client has sent the 'CD' packet on time. Being checked by the user manager every minute.
        /// </summary>
        internal bool pingOK;
        /// <summary>
        /// Specifies if the client has logged in and the user details are loaded. If false, then the user is just a connected client and shouldn't be able to send 'logged in' packets.
        /// </summary>
        public bool _isLoggedIn;
        /// <summary>        
        /// The room user ID (rUID) of the virtual user where this virtual user is currently trading items with. If not trading, then this value is -1.
        /// </summary>
        internal virtualUser _tradePartner;
        /// Specifies if the user has received the sprite index packet (Dg) already. This packet only requires being sent once, and since it's a BIG packet, we limit it to send it once.
        /// </summary>
        private bool _receivedSpriteIndex;
        /// <summary>
        /// The number of the page of the Hand (item inventory) the user is currently on.
        /// </summary>
        private int _handPage;
        /// <summary>
        /// The v26 badge system. 
        /// </summary>
        internal List<string> _Badges = new List<string>();
        internal List<int> _badgeSlotIDs = new List<int>();

        /// <summary>
        /// The virtual room the user is in.
        /// </summary>
        internal virtualRoom Room;

        internal UserVariables _WSVariables = null;
        /// <summary>
        /// The virtualRoomUser that represents this virtual user in room. Contains in-room only objects such as position, rotation and walk related objects.
        /// </summary>
        internal virtualRoomUser roomUser;
        /// <summary>
        /// The status manager that keeps status strings for the user in room.
        /// </summary>
        internal virtualRoomStatusManager statusManager;
        /// <summary>
        /// The messenger that provides instant messaging, friendlist etc for this virtual user.
        /// </summary>
        internal virtualMessenger Messenger;
        /// <summary>
        /// Variant of virtualRoomUser object. Represents this virtual user in a game arena, aswell as in a game team in the navigator.
        /// </summary>
        internal gamePlayer gamePlayer;
        /// <summary>
        /// holds information about the items the user currently has
        /// </summary>
        internal HandItemManager handItem;
        /// <summary>
        /// Holds information about the rooms this user currently has
        /// </summary>
        internal ownRoomManager ownRooms;

        #endregion

        #region Personal
        internal bool warning = false;
        internal int userID;
        internal string _Username;
        internal string _Figure;
        internal char _Sex;
        internal string _Mission;
        internal string _consoleMission;
        internal byte _Rank;
        private int credits;
        internal int _Credits
        {
            get
            {
                return credits;
            }
            set
            {
                using (DatabaseClient dbCient = Eucalypt.dbManager.GetClient())
                {
                    dbCient.runQuery("UPDATE users SET credits = '" + value + "' WHERE id  = " + this.userID);
                }
                credits = value;
            }
        }

        internal int _BelCredits
        {
            get
            {
                using (DatabaseClient dbCient = Eucalypt.dbManager.GetClient())
                {
                    return dbCient.getInt("SELECT belcredits FROM users WHERE id = " + this.userID);
                }
            }
            set
            {
                using (DatabaseClient dbCient = Eucalypt.dbManager.GetClient())
                {
                    dbCient.runQuery("UPDATE users SET belcredits = '" + value + "' WHERE id  = " + this.userID);
                }
            }

        }
        internal int _Tickets;

        internal List<string> _fuserights;

        //internal Holo.Virtual.Rooms.Games.Wobble_Squabble.userVariables _WSVariables = null;

        internal bool _clubMember;


        internal int _amountOfFavourites;

        internal int _roomID;
        internal bool _inPublicroom;
        internal bool _ROOMACCESS_PRIMARY_OK;
        internal bool _ROOMACCESS_SECONDARY_OK;
        internal bool _isOwner;
        internal bool _hasRights;
        internal bool _isMuted;
        internal int belcreditsWarningItemTemplateId;

        internal int _groupID;
        internal int _groupMemberRank;

        internal int _tradePartnerUID = -1;
        internal bool _tradeAccept;
        internal bool abortingTrade;
        internal List<genericItem> _tradeItems = new List<genericItem>();

        internal int _teleporterID;
        internal int _teleportRoomID;
        internal bool _hostsEvent;

        private virtualSongEditor songEditor;
        #endregion

        #region Constructors/destructors
        /// <summary>
        /// Initializes a new virtual user, and starts packet transfer between client and asynchronous socket server.
        /// </summary>
        /// <param name="connectionID">The ID of the new connection.</param>
        /// <param name="connectionSocket">The socket of the new connection.</param>
        public virtualUser(gameConnection _ConnectionManager)
        {
            this._ConnectionManager = _ConnectionManager;
        }
        #endregion

        #region Connection management

        /// <summary>
        /// Immediately completes the current data transfer [if any], disconnects the client and flags the connection slot as free.
        /// </summary>
        public void Reset()
        {
            try
            {

                leaveCurrentRoom();
                if (Messenger != null)
                {
                    Messenger.Clear();
                    Messenger = null;
                }
                if (this.handItem != null)
                {
                    handItem.destroy();
                    handItem = null;
                }
                if (this.ownRooms != null)
                {
                    ownRooms.destroy();
                    ownRooms = null;
                }
                if (songEditor != null)
                    unloadSongMachine();
                userManager.removeUser(userID);
            }
            catch (Exception e) { Out.writeSeriousError(e.ToString()); }
        }
        private void unloadSongMachine()
        {
            songEditor.destroy();
            songEditor = null;
        }


        /// <summary>
        /// Immediately completes the current data transfer [if any], disconnects the client and flags the connection slot as free.
        /// </summary>
        internal void Disconnect()
        {
            _ConnectionManager.Close();
        }
        /// <summary>
        /// Disables receiving on the socket, sleeps for a specified amount of time [ms] and disconnects via normal Disconnect() void. Asynchronous.
        /// </summary>
        /// <param name="ms"></param>
        internal void Disconnect(int ms)
        {
            Thread.Sleep(ms);
            Disconnect();
        }
        /// <summary>
        /// Sends an error to the user
        /// </summary>
        /// <param name="error">The error ID</param>
        private void sendError(int error, string additionalInformation)
        {
            this._ConnectionManager.sendError(error, additionalInformation);
        }

        #endregion

        #region Data Checking

        private void validatePacket(string currentPacket)
        {
            if (currentPacket.Contains("\x02") || currentPacket.Contains("\x05") || currentPacket.Contains("\x09"))
            {
                Out.WriteSpecialLine("User " + this.userID + " Used an exploit packet", Out.logFlags.MehAction, ConsoleColor.White, ConsoleColor.DarkCyan, "> [" + Thread.GetDomainID() + "]", 2, ConsoleColor.Cyan);
                Disconnect();
                return;
            }
        }
        #endregion

        #region Data sending
        /// <summary>
        /// Sends a single packet to the client via an asynchronous BeginSend action.
        /// </summary>
        /// <param name="Data">The string of data to send. char[01] is added.</param>
        internal void sendData(string packet)
        {
            _ConnectionManager.sendData(packet);

        }



        #endregion

        #region Packet processing

        #region Non-logged in packet processing


        public void cnReceived()
        {
            sendData("DUIH");
            //Random RND = new Random();
            //sendData("DU" + Encoding.encodeVL64(RND.Next()) + "\x02" + Encoding.encodeVL64(RND.Next()));
        }

        public void packetSSO()
        {
            sendData("DA" + "QBHIIIKHJIPAIQAdd-MM-yyyy" + Convert.ToChar(2) + "SAHPB/client" + Convert.ToChar(2) + "QBH" + "IJWVVVSNKQCFUBJASMSLKUUOJCOLJQPNSBIRSVQBRXZQOTGPMNJIHLVJCRRULBLUO"); // V25+ SSO LOGIN BY vista4life
            //sendData("DA" + "dd-MM-yyyy" + "\x02" + DateTime.Now );
        }

        public void initializePlayer()
        {
            validatePacket(currentPacket);
            int myID;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.AddParamWithValue("sso", currentPacket.Substring(4));
                myID = dbClient.getInt("SELECT id FROM users WHERE ticket_sso = @sso AND ipaddress_last = '" + connectionRemoteIP + "' LIMIT 1");
                //dbClient.runQuery("UPDATE users SET ticket_sso = NULL WHERE ticket_sso = @sso AND ipaddress_last = '" + connectionRemoteIP + "' LIMIT 1");

            }
            this._fuserights = new List<string>();
            if (myID == 0) //|| !(myID == 280812 || myID == 1)) // No user found for this sso ticket and/or IP address
            {
                Disconnect();
                return;
            }


            string banReason = userManager.getBanReason(myID);
            if (banReason != "")
            {
                sendData("@c" + banReason);
                Disconnect(2000);
                return;
            }

            this.userID = myID;

            DataRow dRow;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dRow = dbClient.getRow("SELECT name,figure,sex,mission,rank,consolemission,lastvisit FROM users WHERE id = '" + this.userID + "'");
                _amountOfFavourites = dbClient.getInt("SELECT COUNT(userid) FROM users_favourites WHERE userid = '" + this.userID + "'");

            }
            _Username = Convert.ToString(dRow[0]);

            _Figure = Convert.ToString(dRow[1]);
            _Sex = Convert.ToChar(dRow[2]);
            _Mission = Convert.ToString(dRow[3]);
            _Rank = Convert.ToByte(dRow[4]);
            _consoleMission = Convert.ToString(dRow[5]);
            ownRooms = new ownRoomManager(this._Username);

            userManager.addUser(myID, this);
            _isLoggedIn = true;

            generateHandItems();
            DataColumn dCol;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dCol = dbClient.getColumn("SELECT fuseright FROM users_fuserights WHERE userid = " + this.userID);
            }

            foreach (DataRow dbRow in dCol.Table.Rows)
                _fuserights.Add(Convert.ToString(dbRow[0]));
            sendData("@B" + rankManager.fuseRights(_Rank, this));
            sendData("DbIH");
            sendData("@C");

            if (Config.enableWelcomeMessage)
                sendNotify(stringManager.getString("welcomemessage_text"));
            this._ConnectionManager.RegisterLoggedInPackets();

        }
        #endregion

        #region Logged-in packet processing

        #region Misc

        /// <summary>
        /// A request of the current date by the user
        /// </summary>
        public void dateRequest()
        {
            sendData("Bc" + DateTime.Today.ToShortDateString());
        }
        /// <summary>
        /// Response to the ping packet
        /// </summary>
        public void pingReceived()
        {
            pingOK = true;
        }
        /// <summary>
        /// Redeems a credit code, if there was any
        /// </summary>
        public void redeemCreditCode()
        {
            validatePacket(currentPacket);
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                int voucherAmount;
                dbClient.AddParamWithValue("code", currentPacket.Substring(4));
                if ((voucherAmount = dbClient.getInt("SELECT credits FROM vouchers WHERE voucher = @code")) > 0)
                {
                    dbClient.runQuery("DELETE FROM vouchers WHERE voucher = @code LIMIT 1");

                    _Credits += voucherAmount;
                    sendData("@F" + _Credits);
                    sendData("CT");
                    dbClient.runQuery("UPDATE users SET credits = '" + voucherAmount + "' WHERE id = '" + userID + "' LIMIT 1");
                }
                else
                    sendData("CU1");
            }
        }
        #endregion

        #region Login
        /// <summary>
        /// Login - initialize messenger
        /// </summary>
        public void initializeMessenger()
        {
            Messenger = new Messenger.virtualMessenger(userID);
            sendData("@L" + Messenger.friendList());
            sendData("Dz" + Messenger.friendRequests());
        }

        /// <summary>
        /// Login - initialize Club subscription status
        /// </summary>
        public void refreshClubPacket()
        {
            refreshClub();
        }

        /// <summary>
        /// Login - initialize/refresh appearance
        /// </summary>
        public void refreshAppearance()
        {
            refreshAppearance(false, true, false);
        }
        /// <summary>
        /// Login - initialize/refresh valueables [credits, tickets, etc]
        /// </summary>
        public void refreshValuables()
        {
            refreshValueables(true, true, false, true);
        }

        /// <summary>
        /// Login - initialize/refresh badges
        /// </summary>
        public void initializeBadges()
        {
            refreshBadges();

        }

        /// <summary>
        /// Login - initialize/refresh group status
        /// </summary>
        public void refreshGroup()
        {
            refreshGroupStatus();
        }

        /// <summary>
        /// Recycler - receive recycler setup
        /// </summary>
        public void recyclerSetup()
        {
            sendData("Do" + recyclerManager.setupString);
        }
        /// <summary>
        /// Recycler - receive recycler session status
        /// </summary>
        public void recyclerSessionStatus()
        {
            sendData("Dp" + recyclerManager.sessionString(userID));
        }
        #endregion

        #region Messenger
        /// <summary>
        /// Messenger - request user as friend
        /// </summary>
        public void messengerFriendRequest()
        {
            validatePacket(currentPacket);
            if (Messenger != null)
            {
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.AddParamWithValue("username", currentPacket.Substring(4));
                    int toID = dbClient.getInt("SELECT id FROM users WHERE name = @username");
                    if (toID != this.userID && toID > 0 && Messenger.hasFriendRequests(toID) == false && Messenger.hasFriendship(toID) == false)
                    {
                        int requestID = dbClient.getInt("SELECT MAX(requestid) FROM messenger_friendrequests WHERE userid_to = '" + toID + "'") + 1;
                        dbClient.runQuery("INSERT INTO messenger_friendrequests(userid_to,userid_from,requestid) VALUES ('" + toID + "','" + userID + "','" + requestID + "')");
                        if (userManager.getUser(toID) != null)
                            userManager.getUser(toID).sendData("BD" + "I" + _Username + Convert.ToChar(2) + userID + Convert.ToChar(2));
                    }
                }
            }
        }

        /// <summary>
        /// Search in console 
        /// </summary>
        public void searchInConsole()
        {
            validatePacket(currentPacket);
            // Variables 
            string PacketFriends = "";
            string PacketOthers = "";
            string PacketAdd = "";
            int CountFriends = 0;
            int CountOthers = 0;

            // Database 
            string[] IDs;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                string Search = currentPacket.Substring(4);
                Search = Search.Replace(@"\", "\\").Replace("'", @"\'");
                dbClient.AddParamWithValue("search", Search);
                IDs = dataHandling.dColToArray((dbClient.getColumn("SELECT id FROM users WHERE name LIKE '" + Search + "%' LIMIT 20 ")));
            }

            // Loop through results 
            for (int i = 0; i < IDs.Length; i++)
            {

                int thisID = Convert.ToInt32(IDs[i]);
                bool online = userManager.containsUser(thisID);
                string onlineStr = online ? "I" : "H";

                DataRow row;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    row = dbClient.getRow("SELECT name, mission, lastvisit, figure FROM users WHERE id = " + thisID.ToString());
                }
                PacketAdd = Encoding.encodeVL64(thisID)
                             + row[0] + ""
                             + row[1] + ""
                             + onlineStr + onlineStr + ""
                             + onlineStr + (online ? row[3] : "") + ""
                             + (online ? "" : row[2]) + "";

                // Friend or not? 
                if (Messenger.hasFriendship(thisID))
                {
                    CountFriends += 1;
                    PacketFriends += PacketAdd;
                }
                else
                {
                    CountOthers += 1;
                    PacketOthers += PacketAdd;
                }


            }

            // Add count headers 
            PacketFriends = Encoding.encodeVL64(CountFriends) + PacketFriends;
            PacketOthers = Encoding.encodeVL64(CountOthers) + PacketOthers;

            // Merge & send packets 
            sendData("Fs" + PacketFriends + PacketOthers);

        }

        /// <summary>
        /// Messenger - accept friendrequest(s)
        /// </summary>
        public void acceptFriendRequests()
        {
            validatePacket(this.currentPacket);
            if (Messenger != null)
            {
                int Amount = Encoding.decodeVL64(this.currentPacket.Substring(2));
                string currentPacket = this.currentPacket.Substring(Encoding.encodeVL64(Amount).Length + 2);

                int updateAmount = 0;
                StringBuilder Updates = new StringBuilder();
                virtualBuddy Me = new virtualBuddy(userID);

                for (int i = 0; i < Amount; i++)
                {
                    if (currentPacket == "")
                    {
                        return;
                    }
                    int requestID = Encoding.decodeVL64(currentPacket);
                    int fromUserID;
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        fromUserID = dbClient.getInt("SELECT userid_from FROM messenger_friendrequests WHERE userid_to = '" + this.userID + "' AND requestid = '" + requestID + "'");
                    }
                    if (fromUserID == 0) // Corrupt data
                    {
                        return;
                    }

                    virtualBuddy Buddy = new virtualBuddy(fromUserID);
                    Updates.Append(Buddy.newUser(true));
                    updateAmount++;

                    Messenger.addBuddy(Buddy, false);
                    if (userManager.containsUser(fromUserID))
                    {
                        if (userManager.getUser(fromUserID).Messenger != null)
                            userManager.getUser(fromUserID).Messenger.addBuddy(Me, true);
                    }
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        dbClient.runQuery("INSERT INTO messenger_friendships(userid,friendid) VALUES ('" + fromUserID + "','" + this.userID + "')");
                        dbClient.runQuery("DELETE FROM messenger_friendrequests WHERE userid_to = '" + this.userID + "' AND requestid = '" + requestID + "' LIMIT 1");
                    }
                    currentPacket = currentPacket.Substring(Encoding.encodeVL64(requestID).Length);
                }
                if (updateAmount > 0)
                    sendData("@M" + "HI" + Encoding.encodeVL64(updateAmount) + Updates.ToString());
            }
        }
        /// <summary>
        /// Messenger - decline friendrequests
        /// </summary>
        public void declineFriendRequests()
        {
            validatePacket(this.currentPacket);
            if (Messenger != null)
            {
                int Amount = Encoding.decodeVL64(this.currentPacket.Substring(3));
                string currentPacket = this.currentPacket.Substring(Encoding.encodeVL64(Amount).Length + 3);
                for (int i = 0; i < Amount; i++)
                {
                    if (currentPacket == "")
                    {
                        return;
                    }
                    int requestID = Encoding.decodeVL64(currentPacket);
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        dbClient.runQuery("DELETE FROM messenger_friendrequests WHERE userid_to = '" + this.userID + "' AND requestid = '" + requestID + "' LIMIT 1");
                    }
                    currentPacket = currentPacket.Substring(Encoding.encodeVL64(requestID).Length);
                }
            }
        }

        /// <summary>
        /// Messenger - remove buddy from friendlist
        /// </summary>
        public void removeBuddyFromFriendlist()
        {
            validatePacket(currentPacket);
            if (Messenger != null)
            {
                int buddyID = Encoding.decodeVL64(currentPacket.Substring(3));
                Messenger.removeBuddy(buddyID);
                if (userManager.containsUser(buddyID))
                {
                    if (userManager.getUser(buddyID).Messenger != null)
                        userManager.getUser(buddyID).Messenger.removeBuddy(userID);
                }
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.runQuery("DELETE FROM messenger_friendships WHERE (userid = '" + userID + "' AND friendid = '" + buddyID + "') OR (userid = '" + buddyID + "' AND friendid = '" + userID + "') LIMIT 1");
                }
            }
        }

        /// <summary>
        /// Messenger - send instant message to buddy
        /// </summary>
        public void sendInstantMessagetoBuddy()
        {
            validatePacket(currentPacket);
            if (Messenger != null)
            {
                int buddyID = Encoding.decodeVL64(currentPacket.Substring(2));
                string Message = currentPacket.Substring(Encoding.encodeVL64(buddyID).Length + 4);
                Message = stringManager.filterSwearWords(Message); // Filter swearwords

                if (Messenger.containsOnlineBuddy(buddyID)) // Buddy online
                    userManager.getUser(buddyID).sendData("BF" + Encoding.encodeVL64(userID) + Message + Convert.ToChar(2));
                else // Buddy offline (or user doesn't has user in buddylist)
                    sendData("DE" + Encoding.encodeVL64(5) + Encoding.encodeVL64(userID));
            }
        }

        /// <summary>
        /// Messenger - refresh friendlist
        /// </summary>
        public void refreshFriendlist()
        {
            if (Messenger != null)
                sendData(Messenger.getUpdates());
        }

        /// <summary>
        /// // Messenger - follow buddy to a room
        /// </summary>
        public void stalkButtonMessenger()
        {
            validatePacket(currentPacket);
            if (Messenger != null)
            {
                int ID = Encoding.decodeVL64(currentPacket.Substring(2));
                int errorID = -1;
                if (Messenger.hasFriendship(ID)) // Has friendship with user
                {
                    if (userManager.containsUser(ID)) // User is online
                    {
                        virtualUser _User = userManager.getUser(ID);
                        if (_User._roomID > 0) // User is in room
                        {
                            if (_User._inPublicroom)
                                sendData("D^" + "I" + Encoding.encodeVL64(_User._roomID));
                            else
                                sendData("D^" + "H" + Encoding.encodeVL64(_User._roomID));
                        }
                        else // User is not in a room
                            errorID = 2;
                    }
                    else // User is offline
                        errorID = 1;
                }
                else // User is not this virtual user's friend
                    errorID = 0;

                if (errorID != -1) // Error occured
                    sendData("E]" + Encoding.encodeVL64(errorID));
            }
        }

        /// <summary>
        /// Messenger - invite buddies to your room 
        /// </summary>
        public void massInvite()
        {
            validatePacket(this.currentPacket);
            try
            {
                if (Messenger != null && roomUser != null)
                {
                    int Amount = Encoding.decodeVL64(this.currentPacket.Substring(2));
                    int[] IDs = new int[Amount];
                    string currentPacket = this.currentPacket.Substring(Encoding.encodeVL64(Amount).Length + 2);

                    for (int i = 0; i < Amount; i++)
                    {
                        if (currentPacket == "")
                            return;

                        int ID = Encoding.decodeVL64(currentPacket);
                        if (Messenger.hasFriendship(ID) && userManager.containsUser(ID))
                            IDs[i] = ID;

                        currentPacket = currentPacket.Substring(Encoding.encodeVL64(ID).Length);
                    }

                    string Message = currentPacket.Substring(2);
                    string Data = "BG" + Encoding.encodeVL64(userID) + Message + Convert.ToChar(2);
                    for (int i = 0; i < Amount; i++)
                        if (userManager.containsUser(IDs[i]))
                            userManager.getUser(IDs[i]).sendData(Data);
                }
            }
            catch
            {
                sendData("BKSorry maar er is iets mis gegaan met het versturen\rvan berichten naar je vrienden.\r\rJe krijgt geen FM maar je bericht is niet verstuurd naar\ralle sunnie's");
            }

        }
        #endregion

        #region Navigator actions

        #region caching done

        /// <summary>
        /// Navigator - navigate through rooms and categories
        /// </summary>
        public void navigateThroughCategorie()
        {
            validatePacket(currentPacket);
            int hideFull = Encoding.decodeVL64(currentPacket.Substring(2, 1));
            int cataID = Encoding.decodeVL64(currentPacket.Substring(3));
            sendData(navigatorManager.getCategoryDetails(hideFull, cataID, _Rank));
        }

        /// <summary>
        /// Navigator - request index of categories to place guestroom on
        /// </summary>
        public void requestNavigatorIndex()
        {
            sendData(navigatorManager.getCategoryIndex(_Rank));
        }

        /// <summary>
        /// Navigator - refresh recommended rooms (random guestrooms)
        /// </summary>
        public void requestRandomRooms()
        {
            sendData("E_" + "K" + navigatorManager.getRandomRooms());
        }

        /// <summary>
        /// Navigator - view user's own guestrooms
        /// </summary>
        public void getOwnRooms()
        {
            sendData(ownRooms.getOwnRoomPacket());
        }
        #endregion

        #region un-cache able
        /// <summary>
        /// Navigator - perform guestroom search on name/owner with a given criticeria
        /// </summary>
        public void guestroomSearch()
        {
            validatePacket(currentPacket);
            bool seeAllRoomOwners = rankManager.containsRight(_Rank, "fuse_see_all_roomowners", _fuserights);
            DataTable dTable;
            if (!(currentPacket.Length > 3))
                return;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.AddParamWithValue("search", currentPacket.Substring(2));
                dbClient.AddParamWithValue("max", Config.Navigator_roomSearch_maxResults);
                dTable = dbClient.getTable("SELECT id,name,owner,description,state,showname,visitors_now,visitors_max FROM rooms WHERE `owner` = @search  LIMIT @max");
            }
            if (dTable.Rows.Count > 0)
            {
                StringBuilder Rooms = new StringBuilder();
                string nameString;
                foreach (DataRow dRow in dTable.Rows)
                {
                    nameString = Convert.ToString(dRow["owner"]);
                    if (Convert.ToString(dRow["showname"]) == "0" && Convert.ToString(dRow["owner"]) != _Username && seeAllRoomOwners == false)// The room owner has hidden his name at the guestroom and this user hasn't got the fuseright to see all room owners
                        nameString = "-";
                    Rooms.Append(Convert.ToString(dRow["id"]) + Convert.ToChar(9) + Convert.ToString(dRow["name"]) + Convert.ToChar(9) + Convert.ToString(dRow["owner"]) + Convert.ToChar(9) + roomManager.getRoomState(Convert.ToInt32(dRow["state"])) + Convert.ToChar(9) + "x" + Convert.ToChar(9) + Convert.ToString(dRow["visitors_now"]) + Convert.ToChar(9) + Convert.ToString(dRow["visitors_max"]) + Convert.ToChar(9) + "null" + Convert.ToChar(9) + Convert.ToString(dRow["description"]) + Convert.ToChar(9) + Convert.ToChar(13));
                }
                sendData("@w" + Rooms.ToString());
            }
            else
                sendData("@z");
        }
        #endregion

        /// <summary>
        /// Navigator - get guestroom details
        /// </summary>
        public void getGuestroomDetails()
        {
            validatePacket(currentPacket);
            int roomID = int.Parse(currentPacket.Substring(2));
            bool hideDetails = rankManager.containsRight(_Rank, "fuse_see_all_roomowners", _fuserights);
            string packet = navigatorManager.getGuestRoomDetails(roomID, hideDetails);
            if (packet != "")
                sendData(packet);
        }

        #region needs caching
        /// <summary>
        /// Navigator - initialize user's favorite rooms
        /// </summary>
        public void getFavoriteRooms()
        {
            validatePacket(currentPacket);
            DataColumn dCol;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dCol = dbClient.getColumn("SELECT roomid FROM users_favourites WHERE userid = '" + userID + "'  LIMIT " + Config.Navigator_Favourites_maxRooms);
            }

            List<RoomStructure> roomList = new List<RoomStructure>();
            foreach (DataRow dRow in dCol.Table.Rows)
            {
                RoomStructure room = NavigatorHelper.getRoom(Convert.ToInt32(dRow["roomid"]));
                if (room.getID() != 0)
                    roomList.Add(room);
                else
                {
                    roomManager.deleteRoom(Convert.ToInt32(dRow["roomid"]), "");
                }
            }

            if (roomList.Count > 0)
            {
                int guestRoomAmount = 0;
                string nameString;
                bool seeHiddenRoomOwners = rankManager.containsRight(_Rank, "fuse_enter_locked_rooms", _fuserights);
                StringBuilder Rooms = new StringBuilder();

                foreach (RoomStructure room in roomList)
                {
                    if (room.getName() == "" || room.getName() == null)
                        Rooms.Append(Encoding.encodeVL64(room.getID()) + "I" + room.getName() + Convert.ToChar(2) + Encoding.encodeVL64(room.getVisitorsNow()) + Encoding.encodeVL64(room.getMaxInside()) + Encoding.encodeVL64(room.getCategory()) + room.getDescription() + Convert.ToChar(2) + Encoding.encodeVL64(room.getID()) + "H" + room.getCCTs() + Convert.ToChar(2) + "HI");

                    else // Guestroom
                    {
                        nameString = room.getName();
                        if (room.getShowName() == 0 && !this.ownRooms.hasRoom(room.getID()) && seeHiddenRoomOwners == false) // Room owner doesn't wish to show his name, and this user isn't the room owner and this user doesn't has the right to see hidden room owners, change room owner to '-'
                            nameString = "-";
                        Rooms.Append(Encoding.encodeVL64(room.getID()) + nameString + Convert.ToChar(2) + room.getOwner() + Convert.ToChar(2) + roomManager.getRoomState(room.getState()) + Convert.ToChar(2) + Encoding.encodeVL64(room.getVisitorsNow()) + Encoding.encodeVL64(room.getMaxInside()) + Convert.ToString(room.getDescription()) + Convert.ToChar(2));
                        guestRoomAmount++;
                    }
                }
                sendData("@}" + "HHJ" + Convert.ToChar(2) + "HHH" + Encoding.encodeVL64(roomList.Count) + Rooms.ToString());
            }
        }

        /// <summary>
        /// Navigator - add room to favourite rooms list
        /// </summary>
        public void addFavouriteRooms()
        {
            validatePacket(currentPacket);
            int roomID = Encoding.decodeVL64(currentPacket.Substring(3));
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                if (roomManager.roomExists(roomID) && dbClient.findsResult("SELECT userid FROM users_favourites WHERE userid = '" + userID + "' AND roomid = '" + roomID + "'") == false) // The virtual room does exist, and the virtual user hasn't got it in the list already
                {
                    if (_amountOfFavourites < Config.Navigator_Favourites_maxRooms)
                    {
                        dbClient.runQuery("INSERT INTO users_favourites (userid,roomid) VALUES ('" + userID + "','" + roomID + "')");
                        _amountOfFavourites++;
                    }
                    else
                        sendData("@a" + "nav_error_toomanyfavrooms");
                }
            }
        }
        /// <summary>
        /// Navigator - remove room from favourite rooms list
        /// </summary>
        public void removeFavouriteRoom()
        {
            validatePacket(currentPacket);
            int roomID = Encoding.decodeVL64(currentPacket.Substring(3));
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.runQuery("DELETE FROM users_favourites WHERE userid = '" + userID + "' AND roomid = '" + roomID + "' LIMIT 1");
            }
        }
        #endregion

        #endregion

        #region Room event actions

        /// <summary>
        /// Events - get setup
        /// </summary>
        public void getEventsData()
        {
            sendData("Ep" + Encoding.encodeVL64(eventManager.categoryAmount));
        }

        /// <summary>
        /// Events - show/hide 'Host event' button
        /// </summary>
        public void showEventButton()
        {
            if (_inPublicroom || roomUser == null || _hostsEvent) // In publicroom, not in room at all or already hosting event
                sendData("Eo" + "H"); // Hide
            else
                sendData("Eo" + "I"); // Show
        }

        /// <summary>
        /// Events - check if event category is OK
        /// </summary>
        public void checkEventCategory()
        {
            validatePacket(currentPacket);
            int categoryID = Encoding.decodeVL64(currentPacket.Substring(2));
            if (eventManager.categoryOK(categoryID))
                sendData("Eb" + Encoding.encodeVL64(categoryID));
        }
        /// <summary>
        /// Events - open category
        /// </summary>
        public void openEventCategory()
        {
            validatePacket(currentPacket);
            int categoryID = Encoding.decodeVL64(currentPacket.Substring(2));
            if (categoryID >= 1 && categoryID <= 11)
                sendData("Eq" + Encoding.encodeVL64(categoryID) + eventManager.getEvents(categoryID));
        }

        /// <summary>
        /// Events - create event
        /// </summary>
        public void createNewEvent()
        {
            validatePacket(currentPacket);
            if (_isOwner && _hostsEvent == false && _inPublicroom == false && roomUser != null)
            {
                int categoryID = Encoding.decodeVL64(currentPacket.Substring(2));
                if (eventManager.categoryOK(categoryID))
                {
                    int categoryLength = Encoding.encodeVL64(categoryID).Length;
                    int nameLength = Encoding.decodeB64(currentPacket.Substring(categoryLength + 2, 2));
                    string Name = currentPacket.Substring(categoryLength + 4, nameLength);
                    string Description = currentPacket.Substring(categoryLength + nameLength + 6);

                    _hostsEvent = true;
                    eventManager.createEvent(categoryID, userID, _roomID, Name, Description);
                    Room.sendData("Er" + eventManager.getEvent(_roomID));
                }
            }
        }

        /// <summary>
        /// Events - edit event
        /// </summary>
        public void editCurrentEvent()
        {
            validatePacket(currentPacket);
            if (_hostsEvent && _isOwner && _inPublicroom == false && roomUser != null)
            {
                int categoryID = Encoding.decodeVL64(currentPacket.Substring(2));
                if (eventManager.categoryOK(categoryID))
                {
                    int categoryLength = Encoding.encodeVL64(categoryID).Length;
                    int nameLength = Encoding.decodeB64(currentPacket.Substring(categoryLength + 2, 2));
                    string Name = currentPacket.Substring(categoryLength + 4, nameLength);
                    string Description = currentPacket.Substring(categoryLength + nameLength + 6);
                    eventManager.editEvent(categoryID, _roomID, Name, Description);
                    Room.sendData("Er" + eventManager.getEvent(_roomID));
                }
            }
        }

        /// <summary>
        /// Events - end event
        /// </summary>
        public void endEvent()
        {
            if (_hostsEvent && _isOwner && _inPublicroom == false && roomUser != null)
            {
                _hostsEvent = false;
                eventManager.removeEvent(_roomID);
                Room.sendData("Er" + "-1");
            }
        }
        #endregion

        #region Guestroom create and modify

        /// <summary>
        /// Create guestroom - phase 1
        /// </summary>
        public void createGuestroomPhaseOne()
        {
            validatePacket(currentPacket);
            if (ownRooms.getCount() < Config.Navigator_createRoom_maxRooms)
            {
                string[] roomSettings = currentPacket.Split('/');
                roomSettings[2] = stringManager.filterSwearWords(roomSettings[2]);
                roomSettings[3] = roomSettings[3].Substring(6, 1);
                roomSettings[4] = roomManager.getRoomState(roomSettings[4]).ToString();
                if (roomSettings[5] != "0" && roomSettings[5] != "1")
                    return;
                int roomID;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.AddParamWithValue("rs2", roomSettings[2]);
                    dbClient.AddParamWithValue("user", _Username);
                    dbClient.AddParamWithValue("rs3", roomSettings[3]);
                    dbClient.AddParamWithValue("rs4", roomSettings[4]);
                    dbClient.AddParamWithValue("rs5", roomSettings[5]);
                    roomID = (int)dbClient.insertQuery("INSERT INTO rooms (name, owner, model, state, showname) VALUES (@rs2,@user,@rs3,@rs4,@rs5)");
                }
                ownRooms.addOwnRoom(roomID, roomSettings[2], int.Parse(roomSettings[4]), 25, "", 0, roomSettings[3], int.Parse(roomSettings[5]), 0, null);

                sendData("@{" + roomID + Convert.ToChar(13) + roomSettings[2]);
            }

            else
                sendData("@a" + "Error creating a private room");
        }

        /// <summary>
        /// Create guestroom - phase 2 / modify guestroom
        /// </summary>
        public void createGuestroomPhaseTwo()
        {
            validatePacket(currentPacket);
            int roomID = 0;
            if (currentPacket.Substring(2, 1) == "/")
                roomID = int.Parse(currentPacket.Split('/')[1]);
            else
                roomID = int.Parse(currentPacket.Substring(2).Split('/')[0]);
            if (!ownRooms.hasRoom(roomID))
                return;
            string superUsers = "0";
            int maxVisitors = 25;
            string[] packetContent = currentPacket.Split(Convert.ToChar(13));
            string roomDescription = "";
            string roomPassword = "";

            for (int i = 1; i < packetContent.Length; i++) // More proper way, thanks Jeax
            {
                string updHeader = packetContent[i].Split('=')[0];
                string updValue = packetContent[i].Substring(updHeader.Length + 1);
                switch (updHeader)
                {
                    case "description":
                        roomDescription = stringManager.filterSwearWords(updValue);
                        break;

                    case "allsuperuser":
                        superUsers = updValue;
                        if (superUsers != "0" && superUsers != "1")
                            superUsers = "0";

                        break;

                    case "maxvisitors":
                        maxVisitors = int.Parse(updValue);
                        if (maxVisitors < 10)
                            maxVisitors = 10;
                        else if (maxVisitors > 50)
                            maxVisitors = 50;
                        break;

                    case "password":
                        roomPassword = updValue;
                        break;

                    default:
                        return;
                }
            }


            if (roomPassword == "")
                roomPassword = null;
            ownRooms.modifyRoom(roomID, "", maxVisitors, -1, roomDescription, true, -1, roomPassword, superUsers);
        }

        /// <summary>
        /// Modify guestroom, save name, state and show/hide ownername
        /// </summary>
        public void modifyRoomNameDetails()
        {
            validatePacket(currentPacket);
            string[] packetContent = currentPacket.Substring(2).Split('/');
            if (!ownRooms.hasRoom(int.Parse(packetContent[0])))
                return;
            if (packetContent[3] != "1" && packetContent[2] != "0")
                packetContent[2] = "1";

            ownRooms.modifyRoom(int.Parse(packetContent[0]), stringManager.filterSwearWords(packetContent[1]), -1, roomManager.getRoomState(packetContent[2]), "", false, -1, null, null);
        }

        /// <summary>
        /// Navigator - trigger guestroom modify
        /// </summary>
        public void triggerGuestroomModify()
        {
            validatePacket(currentPacket);
            int roomID = Encoding.decodeVL64(currentPacket.Substring(2));
            if (!ownRooms.hasRoom(roomID))
                return;
            sendData("C^" + Encoding.encodeVL64(roomID) + Encoding.encodeVL64(ownRooms.getRoomCategory(roomID)));
        }

        /// <summary>
        /// Navigator - edit category of a guestroom
        /// </summary>
        public void modifyRoomCategory()
        {
            validatePacket(currentPacket);
            int roomID = Encoding.decodeVL64(currentPacket.Substring(2));
            if (!ownRooms.hasRoom(roomID))
                return;
            int cataID = Encoding.decodeVL64(currentPacket.Substring(Encoding.encodeVL64(roomID).Length + 2));
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                if (dbClient.findsResult("SELECT id FROM room_categories WHERE id = '" + cataID + "' AND type = '2' AND parent > 0 AND access_rank_min <= " + _Rank)) // Category is valid for this user
                {
                    ownRooms.modifyRoom(roomID, "", -1, -1, "", false, cataID, null, null);
                }
            }
        }

        /// <summary>
        /// Guestroom - Delete
        /// </summary>
        public void deleteGuestroom()
        {
            validatePacket(currentPacket);
            int roomID = int.Parse(currentPacket.Substring(2));

            if (ownRooms.hasRoom(roomID))
                roomManager.deleteRoom(roomID, _Username);

        }

        /// <summary>
        /// Navigator - 'Who's in here' feature for public rooms
        /// </summary>
        public void getUserlistInsideRoom()
        {
            validatePacket(currentPacket);
            int roomID = Encoding.decodeVL64(currentPacket.Substring(2));
            if (roomManager.containsRoom(roomID))
                sendData("C_" + roomManager.getRoom(roomID).Userlist);
            else
                sendData("C_");
        }
        #endregion

        #region Enter/leave room
        /// <summary>
        /// Leaves a room
        /// </summary>
        /// <param name="message">The message (mod kick)</param>
        /// <param name="kick">wheter it's a kick or not</param>
        public void leaveCurrentRoom(string message, bool kick)
        {
            if (Room != null && roomUser != null)
            {
                if (isGeest)
                    makeGenie(false);
                if (roomUser.Diving)
                    Room.stopDiving();
                Room.removeUser(roomUser.roomUID, kick, message, true);

            }
            leaveGame();
            stopWSgame();
            abortTrade();

            _roomID = 0;
            _inPublicroom = false;
            _ROOMACCESS_PRIMARY_OK = false;
            _ROOMACCESS_SECONDARY_OK = false;
            _isOwner = false;
            _hasRights = false;
            Room = null;
            if (statusManager != null)
            {
                statusManager.Clear();
                statusManager = null;
            }
            roomUser = null;
        }

        /// <summary>
        /// Rooms - leave room
        /// </summary>
        public void leaveCurrentRoom()
        {
            leaveCurrentRoom("", false);
        }

        /// <summary>
        /// Enter room - loading screen advertisement
        /// </summary>
        public void loadAdvertisement()
        {
            Config.Rooms_LoadAvertisement_img = "";
            if (Config.Rooms_LoadAvertisement_img == "")
                sendData("DB0");
            else
                sendData("DB" + Config.Rooms_LoadAvertisement_img + Convert.ToChar(9) + Config.Rooms_LoadAvertisement_uri);
        }

        /// <summary>
        /// Enter room - determine room and check state + max visitors override
        /// </summary>
        public void getRoomState()
        {
            validatePacket(currentPacket);
            int roomID = Encoding.decodeVL64(currentPacket.Substring(3));
            if (roomID == 0)
                return;

            bool isPublicroom = (currentPacket.Substring(2, 1) == "A");
            sendData("@S");
            sendData("Bf" + "http://www.sunnieday.net/me.php");

            if (gamePlayer != null && gamePlayer.Game != null)
            {
                if (gamePlayer.enteringGame)
                {
                    leaveCurrentRoom();
                    sendData("AE" + gamePlayer.Game.Lobby.Type + "_arena_" + gamePlayer.Game.mapID + " " + roomID);
                    sendData("Cs" + gamePlayer.Game.getMap());
                    string s = gamePlayer.Game.getMap();
                }
                else
                    leaveGame();
            }
            else
            {
                if (Room != null && roomUser != null)
                    leaveCurrentRoom();

                RoomStructure room = NavigatorHelper.getRoom(roomID);

                if (_teleporterID == 0 || _teleportRoomID != roomID)
                {
                    if (_teleporterID != 0)
                    {
                        _teleportRoomID = 0;
                        _teleporterID = 0;
                    }

                    if (room.getState() == 3 && !_clubMember && !rankManager.containsRight(_Rank, "fuse_enter_locked_rooms", _fuserights)) // Room is only for club subscribers and the user isn't club and hasn't got the fuseright for entering all rooms nomatter the state
                    {
                        sendData("C`" + "Kc");
                        return;
                    }
                    else if (room.getState() == 4 && !rankManager.containsRight(_Rank, "fuse_enter_locked_rooms", _fuserights)) // The room is only for staff and the user hasn't got the fuseright for entering all rooms nomatter the state
                    {
                        sendNotify(stringManager.getString("room_stafflocked"));
                        return;
                    }

                    if (room.isFull() && rankManager.containsRight(_Rank, "fuse_enter_full_rooms", _fuserights) == false)
                    {
                        if (isPublicroom == false)
                            sendData("C`" + "I");
                        else
                            sendNotify(stringManager.getString("room_full"));
                        return;
                    }
                }

                _roomID = roomID;
                _inPublicroom = isPublicroom;
                _ROOMACCESS_PRIMARY_OK = true;

                if (isPublicroom)
                {
                    sendData("AE" + room.getModel() + " " + roomID);
                    _ROOMACCESS_SECONDARY_OK = true;
                }
            }
        }

        /// <summary>
        ///  Enter room - guestroom - enter room by using a teleporter
        /// </summary>
        public void enterRoomUsingTeleporter()
        {
            sendData("@S");
        }

        /// <summary>
        /// Enter room - guestroom - check roomban/password/doorbell
        /// </summary>
        public void enterCheckRoom()
        {
            validatePacket(currentPacket);
            if (_inPublicroom == false)
            {
                RoomStructure room = NavigatorHelper.getRoom(_roomID);
                _isOwner = this.ownRooms.hasRoom(_roomID);
                bool isBannedFromRoom = false;
                if (_isOwner == false)
                {
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        _hasRights = dbClient.findsResult("SELECT userid FROM room_rights WHERE roomid = '" + _roomID + "' AND userid = '" + userID + "'");
                        isBannedFromRoom = dbClient.findsResult("SELECT roomid FROM room_bans WHERE roomid = '" + _roomID + "' AND userid = '" + userID + "'");
                    }
                }
                if (_hasRights == false)
                    _hasRights = room.getSuperusers() == 1;

                if (!(_teleporterID != 0 && _teleportRoomID == _roomID) && _isOwner == false && rankManager.containsRight(_Rank, "fuse_enter_locked_rooms", _fuserights) == false)
                {
                    if (_teleporterID != 0)
                    {
                        _teleporterID = 0;
                        _teleportRoomID = 0;
                    }

                    if (_ROOMACCESS_PRIMARY_OK == false && room.getState() != 2)
                    {
                        return;
                    }
                    // Check for roombans

                    if (isBannedFromRoom)
                    {
                        using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                        {
                            DateTime banExpireMoment;
                            bool succes = DateTime.TryParse(dbClient.getString("SELECT ban_expire FROM room_bans WHERE roomid = '" + _roomID + "' AND userid = '" + userID + "'"), out banExpireMoment);
                            if (succes)
                            {
                                if (DateTime.Compare(banExpireMoment, DateTime.Now) > 0)
                                {
                                    sendData("C`" + "PA");
                                    sendData("@R");
                                    return;
                                }
                                else
                                    dbClient.runQuery("DELETE FROM room_bans WHERE roomid = '" + _roomID + "' AND userid = '" + userID + "' LIMIT 1");
                            }
                            else
                            {
                                dbClient.runQuery("DELETE FROM room_bans WHERE roomid = '" + _roomID + "' AND userid = '" + userID + "' LIMIT 1");
                            }
                        }
                    }

                    if (room.getState() == 1) // Doorbell
                    {
                        if (roomManager.containsRoom(_roomID) == false)
                        {
                            sendData("BC");
                            return;
                        }
                        else
                        {
                            roomManager.getRoom(_roomID).sendDataToRights("A[" + _Username);
                            sendData("A[");
                            return;
                        }
                    }
                    else if (room.getState() == 2) // Password
                    {
                        string givenPassword = "";
                        try { givenPassword = currentPacket.Split('/')[1]; }
                        catch { }

                        if (givenPassword != room.getPassword()) { sendData("@a" + "Incorrect flat password"); return; }
                    }

                }

                _ROOMACCESS_SECONDARY_OK = true;
                sendData("@i");
            }

        }

        /// <summary>
        /// Answer guestroom doorbell
        /// </summary>
        public void awnserDoorbell()
        {
            validatePacket(currentPacket);
            if (_hasRights == false && rankManager.containsRight(roomUser.User._Rank, "fuse_enter_locked_rooms", _fuserights))
                return;

            string ringer = currentPacket.Substring(4, Encoding.decodeB64(currentPacket.Substring(2, 2)));
            bool letIn = currentPacket.Substring(currentPacket.Length - 1) == "A";

            virtualUser ringerData = userManager.getUser(ringer);
            if (ringerData == null)
                return;
            if (ringerData._roomID != _roomID)
                return;

            if (letIn)
            {
                ringerData._ROOMACCESS_SECONDARY_OK = true;
                Room.sendDataToRights("@i" + ringer + Convert.ToChar(2));
                ringerData.sendData("@i");
            }
            else
            {
                ringerData.sendData("BC");
                ringerData._roomID = 0;
                ringerData._inPublicroom = false;
                ringerData._ROOMACCESS_PRIMARY_OK = false;
                ringerData._ROOMACCESS_SECONDARY_OK = false;
                ringerData._isOwner = false;
                ringerData._hasRights = false;
                ringerData.Room = null;
                ringerData.roomUser = null;
            }
        }

        /// <summary>
        /// Enter room - guestroom - guestroom only data: model, landscape, wallpaper, rights, room votes
        /// </summary>
        public void getGuestroomData()
        {
            validatePacket(currentPacket);
            if (_ROOMACCESS_SECONDARY_OK)
            {
                if (_roomID == 0)
                    return;
                if (roomManager.containsRoom(_roomID))
                    Room = roomManager.getRoom(_roomID);
                else
                {
                    Room = new virtualRoom(_roomID);
                    roomManager.addRoom(_roomID, Room);
                }
                if (!_inPublicroom)
                {

                    DataRow dRow;
                    string Landscape;
                    string Model;
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        dRow = dbClient.getRow("SELECT wallpaper, floor, landscape FROM rooms WHERE id = '" + _roomID + "'");
                    }
                    try
                    {
                        Model = "model_" + NavigatorHelper.getRoom(_roomID).getModel();
                        Landscape = dRow["landscape"].ToString();
                    }
                    catch
                    {
                        return;
                    }
                    sendData("AE" + Model + " " + _roomID);
                    int Wallpaper = Convert.ToInt32(dRow["wallpaper"]);
                    int Floor = Convert.ToInt32(dRow["floor"]);
                    sendData("@n" + "landscape/" + Landscape.Replace(",", "."));
                    if (Wallpaper > 0)
                        sendData("@n" + "wallpaper/" + Wallpaper);
                    if (Floor > 0)
                        sendData("@n" + "floor/" + Floor);


                    if (_isOwner == false)
                    {
                        _isOwner = rankManager.containsRight(_Rank, "fuse_any_room_controller", _fuserights);
                    }
                    if (_isOwner)
                    {
                        _hasRights = true;
                        sendData("@o");
                    }
                    if (_hasRights)
                        sendData("@j");

                    int voteAmount;
                    if (Room.hasUserVoted(userID))
                    {
                        voteAmount = Room.getVoteCount();
                    }
                    else
                    {
                        voteAmount = -1;
                    }

                    sendData("EY" + Encoding.encodeVL64(voteAmount) + "\x01" + "Er" + eventManager.getEvent(_roomID));
                }
            }
        }

        /// <summary>
        /// Enter room - get room advertisement
        /// </summary>
        public void getRoomAdvertise()
        {
            //using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            //{
            //if (_inPublicroom && dbClient.findsResult("SELECT roomid FROM room_ads WHERE roomid = '" + _roomID + "'"))
            //{
            //    DataRow dRow = dbClient.getRow("SELECT img, uri FROM room_ads WHERE roomid = '" + _roomID + "'");
            //    string advImg = Convert.ToString(dRow["img"]);
            //    string advUri = Convert.ToString(dRow["uri"]);
            //    sendData("CP" + advImg + Convert.ToChar(9) + advUri);
            //}
            //else
            sendData("CP" + "0");
            //}
        }

        /// <summary>
        /// Enter room - get roomclass + get heightmap
        /// </summary>
        public void getRoomClass()
        {

            if (Room == null)
            {
                if (roomManager.containsRoom(_roomID))
                    Room = roomManager.getRoom(_roomID);
                else
                {
                    Room = new virtualRoom(_roomID);
                    roomManager.addRoom(_roomID, Room);
                }
            }

            if (_ROOMACCESS_SECONDARY_OK && Room != null)
            {

                sendData("@_" + Room.Heightmap + "\x01" + @"@\" + Room.dynamicUnits);
                //sendData();
            }
            else
            {
                if (gamePlayer != null && gamePlayer.enteringGame && gamePlayer.teamID != -1 && gamePlayer.Game != null)
                {
                    sendData("@_" + gamePlayer.Game.Heightmap + "\x01" + "Cs" + gamePlayer.Game.getPlayers());
                    //sendData();
                    string s = gamePlayer.Game.getPlayers();
                    gamePlayer.enteringGame = false;
                }

            }
        }

        /// <summary>
        /// Enter room - get items
        /// </summary>
        public void getRoomItems()
        {
            if (_ROOMACCESS_SECONDARY_OK && Room != null)
            {
                sendData("@^" + Room.PublicroomItems + "\x01" + "@`" + Room.Flooritems);
                //sendData();
            }
        }

        /// <summary>
        /// Enter room - get group badges, optional skill levels in game lobbies and sprite index
        /// </summary>
        public void getGroupSkillLevelsAndGroupBadges()
        {
            validatePacket(currentPacket);
            if (_ROOMACCESS_SECONDARY_OK && Room != null)
            {
                StringBuilder sb = new StringBuilder("Du" + Room.Groups);

                if (Room.Lobby != null)
                {
                    sb.Append("\x01" + "Cg" + "H" + Room.Lobby.Rank.Title + Convert.ToChar(2) + Encoding.encodeVL64(Room.Lobby.Rank.minPoints) + Encoding.encodeVL64(Room.Lobby.Rank.maxPoints) + "\x01" + "Cz" + Room.Lobby.playerRanks);

                }

                sb.Append("\x01" + "DiH");
                if (_receivedSpriteIndex == false)
                {
                    sb.Append("\x01" + "Dg");
                    _receivedSpriteIndex = true;
                }
                sendData(sb.ToString());
            }
        }

        /// <summary>
        /// Enter room - guestroom - get wallitems
        /// </summary>
        public void getGuestroomWallItems()
        {
            if (_ROOMACCESS_SECONDARY_OK && Room != null)
                sendData("@m" + Room.Wallitems);
        }

        /// <summary>
        /// Enter room - add this user to room
        /// </summary>
        public void enterRoom()
        {
            if (_ROOMACCESS_SECONDARY_OK && Room != null && roomUser == null)
            {
                sendData("@b" + Room.dynamicStatuses);
                Room.addUser(this);
                if (Room.hasSpecialCasts("cam1"))
                {
                    sendData("AGcam1 targetcamera " + roomUser.roomUID);
                }
            }
        }

        #endregion

        #region Moderation

        #region MOD-Tool
        /// <summary>
        /// MOD-Tool
        /// </summary>
        public void useModTool()
        {
            validatePacket(currentPacket);
            int messageLength = 0;
            string Message = "";
            int staffNoteLength = 0;
            string staffNote = "";
            string targetUser = "";

            switch (currentPacket.Substring(2, 2)) // Select the action
            {
                #region Alert single user
                case "HH": // Alert single user
                    {
                        if (rankManager.containsRight(_Rank, "fuse_alert", _fuserights) == false) { sendNotify(stringManager.getString("modtool_accesserror")); return; }

                        messageLength = Encoding.decodeB64(currentPacket.Substring(4, 2));
                        Message = currentPacket.Substring(6, messageLength).Replace(Convert.ToChar(1).ToString(), " ");
                        staffNoteLength = Encoding.decodeB64(currentPacket.Substring(messageLength + 6, 2));
                        staffNote = currentPacket.Substring(messageLength + 8, staffNoteLength);
                        targetUser = currentPacket.Substring(messageLength + staffNoteLength + 10);

                        if (Message == "" || targetUser == "")
                            return;

                        virtualUser _targetUser = userManager.getUser(targetUser);
                        if (_targetUser == null)
                            sendNotify(stringManager.getString("modtool_actionfail") + "\r" + stringManager.getString("modtool_usernotfound"));
                        else
                        {
                            _targetUser.sendData("B!" + Message + Convert.ToChar(2));
                            staffManager.addStaffMessage("alert", userID, _targetUser.userID, Message, staffNote);
                        }
                        break;
                    }
                #endregion

                #region Kick single user from room
                case "HI": // Kick single user from room
                    {
                        if (rankManager.containsRight(_Rank, "fuse_kick", _fuserights) == false) { sendNotify(stringManager.getString("modtool_accesserror")); return; }

                        messageLength = Encoding.decodeB64(currentPacket.Substring(4, 2));
                        Message = currentPacket.Substring(6, messageLength).Replace(Convert.ToChar(1).ToString(), " ");
                        staffNoteLength = Encoding.decodeB64(currentPacket.Substring(messageLength + 6, 2));
                        staffNote = currentPacket.Substring(messageLength + 8, staffNoteLength);
                        targetUser = currentPacket.Substring(messageLength + staffNoteLength + 10);

                        if (Message == "" || targetUser == "")
                            return;

                        virtualUser _targetUser = userManager.getUser(targetUser);
                        if (_targetUser == null)
                            sendNotify(stringManager.getString("modtool_actionfail") + "\r" + stringManager.getString("modtool_usernotfound"));
                        else
                        {
                            if (_targetUser.Room != null && _targetUser.roomUser != null)
                            {
                                if (_targetUser._Rank <= _Rank)
                                {
                                    _targetUser.leaveCurrentRoom(Message, true);
                                    staffManager.addStaffMessage("kick", userID, _targetUser.userID, Message, staffNote);
                                }
                                else
                                    sendNotify(stringManager.getString("modtool_actionfail") + "\r" + stringManager.getString("modtool_rankerror"));
                            }
                        }
                        break;
                    }
                #endregion

                #region Ban single user
                case "HJ": // Ban single user / IP
                    {
                        if (rankManager.containsRight(_Rank, "fuse_ban", _fuserights) == false) { sendNotify(stringManager.getString("modtool_accesserror")); return; }

                        int targetUserLength = 0;
                        int banHours = 0;
                        bool banIP = (currentPacket.Substring(currentPacket.Length - 1, 1) == "I");

                        messageLength = Encoding.decodeB64(currentPacket.Substring(4, 2));
                        Message = currentPacket.Substring(6, messageLength).Replace(Convert.ToChar(1).ToString(), " ");
                        staffNoteLength = Encoding.decodeB64(currentPacket.Substring(messageLength + 6, 2));
                        staffNote = currentPacket.Substring(messageLength + 8, staffNoteLength);
                        targetUserLength = Encoding.decodeB64(currentPacket.Substring(messageLength + staffNoteLength + 8, 2));
                        targetUser = currentPacket.Substring(messageLength + staffNoteLength + 10, targetUserLength);
                        banHours = Encoding.decodeVL64(currentPacket.Substring(messageLength + staffNoteLength + targetUserLength + 10));

                        if (Message == "" || targetUser == "" || banHours == 0)
                            return;
                        else
                        {
                            DataRow dRow;
                            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                            {
                                dbClient.AddParamWithValue("name", targetUser);
                                dRow = dbClient.getRow("SELECT id,rank,ipaddress_last FROM users WHERE name = @name");
                            }
                            if (dRow.Table.Rows.Count == 0)
                            {
                                sendNotify(stringManager.getString("modtool_actionfail") + "\r" + stringManager.getString("modtool_usernotfound"));
                                return;
                            }
                            else if (Convert.ToByte(dRow["rank"]) >= _Rank)
                            {
                                sendNotify(stringManager.getString("modtool_actionfail") + "\r" + stringManager.getString("modtool_rankerror"));
                                return;
                            }

                            int targetID = Convert.ToInt32(dRow["id"]);
                            //string Report = "";
                            staffManager.addStaffMessage("ban", userID, targetID, Message, staffNote);
                            if (banIP && rankManager.containsRight(_Rank, "fuse_superban", _fuserights)) // IP ban is chosen and allowed for this staff member
                            {
                                userManager.setBan(Convert.ToString(dRow["ipaddress_last"]), banHours, Message, targetID);
                                //Report = userManager.generateBanReport(Convert.ToString(dRow["ipaddress_last"]));
                            }
                            else
                            {
                                userManager.setBan(targetID, banHours, Message);
                                //Report = userManager.generateBanReport(targetID);
                            }

                            //sendNotify(Report);
                        }
                        break;
                    }
                #endregion

                #region Room alert
                case "IH": // Alert all users in current room
                    {
                        if (rankManager.containsRight(_Rank, "fuse_room_alert", _fuserights) == false) { sendNotify(stringManager.getString("modtool_accesserror")); return; }
                        if (Room == null || roomUser == null) { return; }

                        messageLength = Encoding.decodeB64(currentPacket.Substring(4, 2));
                        Message = currentPacket.Substring(6, messageLength).Replace(Convert.ToChar(1).ToString(), " ");
                        staffNoteLength = Encoding.decodeB64(currentPacket.Substring(messageLength + 6, 2));
                        staffNote = currentPacket.Substring(messageLength + 8, staffNoteLength);

                        if (Message != "")
                        {
                            Room.sendData("B!" + Message + Convert.ToChar(2));
                            staffManager.addStaffMessage("ralert", userID, _roomID, Message, staffNote);
                        }
                        break;
                    }
                #endregion

                #region Room kick
                case "II": // Kick all users below users rank from room
                    {
                        if (rankManager.containsRight(_Rank, "fuse_room_kick", _fuserights) == false) { sendNotify(stringManager.getString("modtool_accesserror")); return; }
                        if (Room == null || roomUser == null) { return; }

                        messageLength = Encoding.decodeB64(currentPacket.Substring(4, 2));
                        Message = currentPacket.Substring(6, messageLength).Replace(Convert.ToChar(1).ToString(), " ");
                        staffNoteLength = Encoding.decodeB64(currentPacket.Substring(messageLength + 6, 2));
                        staffNote = currentPacket.Substring(messageLength + 8, staffNoteLength);

                        if (Message != "")
                        {
                            Room.kickUsers(_Rank, Message);
                            staffManager.addStaffMessage("rkick", userID, _roomID, Message, staffNote);
                        }
                        break;
                    }
                #endregion
            }

        }
        #endregion

        #region Call For Help

        #region User Side
        /// <summary>
        /// User wants to send a CFH message 
        /// </summary>
        public void receivedCFH()
        {

            DataRow dRow;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dRow = dbClient.getRow("SELECT id, date, message FROM cms_help WHERE username = '" + _Username + "' AND picked_up = '0'");
            }
            if (dRow.Table.Rows.Count == 0)
                sendData("D" + "H");
            else
                sendData("D" + "I" + Convert.ToString(dRow[0]) + Convert.ToChar(2) + Convert.ToString(dRow[1]) + Convert.ToChar(2) + Convert.ToString(dRow[2]) + Convert.ToChar(2));
        }

        /// <summary>
        /// User deletes his pending CFH message
        /// </summary>
        public void deleteCFH()
        {
            int cfhID;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                cfhID = dbClient.getInt("SELECT id FROM cms_help WHERE username = '" + _Username + "' AND picked_up = '0'");
                dbClient.runQuery("DELETE FROM cms_help WHERE picked_up = '0' AND username = '" + _Username + "' LIMIT 1");
            }
            sendData("DH");
            userManager.sendToRank(Config.Minimum_CFH_Rank, true, "BT" + Encoding.encodeVL64(cfhID) + Convert.ToChar(2) + "I" + "User Deleted!" + Convert.ToChar(2) + "User Deleted!" + Convert.ToChar(2) + "User Deleted!" + Convert.ToChar(2) + Encoding.encodeVL64(0) + Convert.ToChar(2) + "" + Convert.ToChar(2) + "H" + Convert.ToChar(2) + Encoding.encodeVL64(0));
        }

        /// <summary>
        /// User sends CFH message
        /// </summary>
        public void sendCFH()
        {
            validatePacket(currentPacket);
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                if (dbClient.findsResult("SELECT id FROM cms_help WHERE username = '" + _Username + "' AND picked_up = '0'") == true)
                    return;
            }
            int messageLength = Encoding.decodeB64(currentPacket.Substring(2, 2));
            if (messageLength == 0)
                return;
            string cfhMessage = currentPacket.Substring(4, messageLength);
            int cfhID;
            string roomName;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.AddParamWithValue("username", _Username);
                dbClient.AddParamWithValue("ip", connectionRemoteIP);
                dbClient.AddParamWithValue("message", cfhMessage);
                dbClient.AddParamWithValue("date", DateTime.Now);
                dbClient.AddParamWithValue("roomid", _roomID.ToString());
                dbClient.runQuery("INSERT INTO cms_help (username,ip,message,date,picked_up,subject,roomid) VALUES (@username,@ip,@message,@date,'0','CFH message [hotel]',@roomid)");
                cfhID = dbClient.getInt("SELECT id FROM cms_help WHERE username = @username AND picked_up = '0'");
            }
            roomName = NavigatorHelper.getRoom(_roomID).getName();
            sendData("EAH"); //                                                                                           \_/                                                                                                                                                                                                    \_/
            userManager.sendToRank(Config.Minimum_CFH_Rank, true, "BT" + Encoding.encodeVL64(cfhID) + Convert.ToChar(2) + "I" + "Sent: " + DateTime.Now + Convert.ToChar(2) + _Username + Convert.ToChar(2) + cfhMessage + Convert.ToChar(2) + Encoding.encodeVL64(_roomID) + Convert.ToChar(2) + roomName + Convert.ToChar(2) + "I" + Convert.ToChar(2) + Encoding.encodeVL64(_roomID));
        }
        #endregion

        #region Staff Side
        /// <summary>
        /// CFH center - reply call
        /// </summary>
        public void replyToCFH()
        {
            validatePacket(currentPacket);
            if (rankManager.containsRight(_Rank, "fuse_receive_calls_for_help", _fuserights) == false)
                return;
            int cfhID = Encoding.decodeVL64(currentPacket.Substring(4, Encoding.decodeB64(currentPacket.Substring(2, 2))));
            string cfhReply = currentPacket.Substring(Encoding.decodeB64(currentPacket.Substring(2, 2)) + 6);

            //Database dbClient = new Database(true, false, 92);
            string toUserName;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                toUserName = dbClient.getString("SELECT username FROM cms_help WHERE id = '" + cfhID + "'");
            }
            if (toUserName == null)
                sendNotify(stringManager.getString("cfh_fail"));
            else
            {
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.runQuery("UPDATE cms_help SET picked_up = '" + _Username + "' WHERE id = '" + cfhID + "' LIMIT 1");
                }
                int toUserID = userManager.getUserID(toUserName);
                virtualUser toVirtualUser = userManager.getUser(toUserID);
                if (toVirtualUser == null)
                    return;
                if (toVirtualUser._isLoggedIn)
                {
                    toVirtualUser.sendData("DR" + cfhReply + Convert.ToChar(2));

                }
            }
        }

        /// <summary>
        /// CFH center - Delete (Downgrade)
        /// </summary>
        public void moderatorDeleteCFH()
        {
            validatePacket(currentPacket);
            if (rankManager.containsRight(_Rank, "fuse_receive_calls_for_help", _fuserights) == false)
                return;
            int cfhID = Encoding.decodeVL64(currentPacket.Substring(4, Encoding.decodeB64(currentPacket.Substring(2, 2))));
            DataRow dRow;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dRow = dbClient.getRow("SELECT picked_up FROM cms_help WHERE id = '" + cfhID + "'");
            }
            if (dRow.Table.Columns.Count == 0)
            {
                return;
            }
            else
            {
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.runQuery("DELETE FROM cms_help WHERE id = '" + cfhID + "' LIMIT 1");
                }
                userManager.sendToRank(Config.Minimum_CFH_Rank, true, "BT" + Encoding.encodeVL64(cfhID) + Convert.ToChar(2) + "H" + "Staff Deleted!" + Convert.ToChar(2) + "Staff Deleted!" + Convert.ToChar(2) + "Staff Deleted!" + Convert.ToChar(2) + "H" + Convert.ToChar(2) + Convert.ToChar(2) + "H" + Convert.ToChar(2) + "H");
            }
        }

        /// <summary>
        /// CFH center - Pickup
        /// </summary>
        public void pickupCFH()
        {
            validatePacket(currentPacket);
            int cfhID = Encoding.decodeVL64(currentPacket.Substring(4));
            bool result;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                result = dbClient.findsResult("SELECT id FROM cms_help WHERE id = '" + cfhID + "'");
            }
            if (result == false)
            {
                sendNotify(stringManager.getString("cfh_deleted"));
                return;
            }
            DataRow dRow;
            string roomName;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dRow = dbClient.getRow("SELECT picked_up,username,message,roomid FROM cms_help WHERE id = '" + cfhID + "'");
            }
            roomName = NavigatorHelper.getRoom(int.Parse(Convert.ToString(dRow[3]))).getName();
            if (Convert.ToString(dRow[0]) == "1")
                sendNotify(stringManager.getString("cfh_picked_up"));
            else
                userManager.sendToRank(Config.Minimum_CFH_Rank, true, "BT" + Encoding.encodeVL64(cfhID) + Convert.ToChar(2) + "I" + "Picked up: " + DateTime.Now + Convert.ToChar(2) + Convert.ToString(dRow[1]) + Convert.ToChar(2) + Convert.ToString(dRow[2]) + Convert.ToChar(2) + Encoding.encodeVL64(Convert.ToInt32(dRow[3])) + Convert.ToChar(2) + roomName + Convert.ToChar(2) + "I" + Convert.ToChar(2) + Encoding.encodeVL64(Convert.ToInt32(dRow[3])));
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.runQuery("UPDATE cms_help SET picked_up = '1' WHERE id = '" + cfhID + "' LIMIT 1");
            }
        }

        /// <summary>
        /// Go to the room that the call for help was sent from
        /// </summary>
        public void getCFHroomID()
        {
            validatePacket(currentPacket);
            if (rankManager.containsRight(_Rank, "fuse_receive_calls_for_help", _fuserights) == false)
                return;
            int idLength = Encoding.decodeB64(currentPacket.Substring(2, 2));
            int cfhID = Encoding.decodeVL64(currentPacket.Substring(4, idLength));
            int roomID;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                roomID = dbClient.getInt("SELECT roomid FROM cms_help WHERE id = '" + cfhID + "'");
            }
            if (roomID == 0 || roomManager.getRoom(roomID) == null)
            {
                sendData("BKSorry maar de kamer waar de persoon was is niet meer ingeladen\rDe persoon is misschien naar een andere kamer gegaan.");
                return;
            }
            virtualRoom room = roomManager.getRoom(roomID);
            if (room.isPublicroom)
                sendData("D^" + "I" + Encoding.encodeVL64(roomID));
            else
                sendData("D^" + "H" + Encoding.encodeVL64(roomID));
        }
        #endregion
        #endregion

        #endregion

        #region In-room actions

        #region Misc
        /// <summary>
        /// Room - rotate user
        /// </summary>
        public void rotateUser()
        {
            validatePacket(currentPacket);
            if (Room != null && roomUser != null && statusManager.containsStatus("sit") == false && statusManager.containsStatus("lay") == false)
            {
                int X = int.Parse(currentPacket.Substring(2).Split(' ')[0]);
                int Y = int.Parse(currentPacket.Split(' ')[1]);
                roomUser.Z1 = Rooms.Pathfinding.Rotation.Calculate(roomUser.X, roomUser.Y, X, Y);
                roomUser.Z2 = roomUser.Z1;
            }
        }

        /// <summary>
        /// Room - walk to a new square
        /// </summary>
        public void walkToNewSquare()
        {
            validatePacket(currentPacket);
            if (Room != null && roomUser != null && roomUser.walkLock == false)
            {
                int goalX = Encoding.decodeB64(currentPacket.Substring(2, 2));
                int goalY = Encoding.decodeB64(currentPacket.Substring(4, 2));

                if (roomUser.SPECIAL_TELEPORTABLE)
                {
                    roomUser.X = goalX;
                    roomUser.Y = goalY;
                    roomUser.goalX = -1;
                    refreshAppearance(false, false, true);
                }
                else if (_WSVariables != null)
                {
                    _WSVariables.WaitingToJoinQueue = false;
                    if (_WSVariables.QueuePosition != -1)
                        Room._WSQueueManager.LeaveQueue(_WSVariables.QueuePosition, _WSVariables.Side);
                    _WSVariables = null;
                    Room.moveUser(roomUser, roomUser.X - 1, roomUser.Y, false);
                    statusManager.addStatus("swim", null, 0, null, 0, 0);
                }
                else
                {
                    roomUser.goalX = goalX;
                    roomUser.goalY = goalY;
                }
            }
        }

        /// <summary>
        /// Room - click door to exit room
        /// </summary>
        public void walkToDoor()
        {
            if (Room != null && roomUser != null && roomUser.walkDoor == false)
            {
                roomUser.walkLock = true;
                roomUser.walkDoor = true;
                roomUser.goalX = Room.doorX;
                roomUser.goalY = Room.doorY;
            }
        }

        /// <summary>
        /// Room - select swimming outfit
        /// </summary>
        public void newSwimmingOutfit()
        {
            validatePacket(currentPacket);
            if (Room != null || roomUser != null && Room.hasSwimmingPool)
            {
                squareTrigger Trigger = Room.getTrigger(roomUser.X, roomUser.Y);
                if (Trigger.Object == "curtains1" || Trigger.Object == "curtains2")
                {

                    Room.sendSpecialCast(Trigger.Object, "open");
                    //if(!checkSwimOutfit(currentPacket.Substring(2)))
                    //    return;
                    roomUser.swimOutfit = currentPacket.Substring(2);
                    Room.sendData(@"@\" + roomUser.detailsString);
                    //refreshAppearance(false, true, true);
                    roomUser.walkLock = false;
                    roomUser.goalX = Trigger.goalX;
                    roomUser.goalY = Trigger.goalY;

                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        dbClient.AddParamWithValue("figure_swim", currentPacket.Substring(2));
                        dbClient.AddParamWithValue("id", userID);
                        dbClient.runQuery("UPDATE users SET figure_swim = @figure_swim WHERE id = @id LIMIT 1");
                    }
                }
            }
        }

        private bool checkSwimOutfit(string swimOutfit)
        {
            if (stringManager.getStringPart(swimOutfit, 0, 5) == "ch=s01=/" && stringManager.getStringPart(swimOutfit, 0, 5) != "ch=s01=/")
                return false;
            swimOutfit = swimOutfit.Remove(0, 5);
            string[] x = swimOutfit.Split(",".ToCharArray());
            int testInt;
            if(x.Length == 3)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (!int.TryParse(x[i], out testInt))
                        return false;
                }
                return true;
            }
            return false;

        }

        /// <summary>
        /// Badges - switch or toggle on/off badge
        /// </summary>
        public void badgeOnOff()
        {
            validatePacket(currentPacket);
            if (Room != null && roomUser != null)
            {
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.runQuery("UPDATE users_badges SET slotid = '0' , iscurrent = '0' WHERE userid = '" + this.userID + "'");
                }

                int enabledBadgeAmount = 0;
                string szWorkData = currentPacket.Substring(2);
                while (szWorkData != "")
                {
                    int slotID = Encoding.decodeVL64(szWorkData);
                    szWorkData = szWorkData.Substring(Encoding.encodeVL64(slotID).Length);

                    int badgeNameLength = Encoding.decodeB64(szWorkData.Substring(0, 2));

                    if (badgeNameLength > 0)
                    {
                        string Badge = szWorkData.Substring(2, badgeNameLength);
                        if (Badge.Length != 3)
                        {
                            Disconnect();
                            return;
                        }
                        using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                        {
                            dbClient.AddParamWithValue("badge", Badge);
                            dbClient.runQuery("UPDATE users_badges SET slotid = '" + slotID + "', iscurrent = '1' WHERE userid = '" + this.userID + "' AND badgeid = @badge LIMIT 1"); // update slot
                        }

                        enabledBadgeAmount++;
                    }

                    szWorkData = szWorkData.Substring(badgeNameLength + 2);
                }
                // Active badges have their badge slot set now, other ones have '0'

                this.refreshBadges();

                string szNotify = this.userID + Convert.ToChar(2).ToString() + Encoding.encodeVL64(enabledBadgeAmount);
                for (int x = 0; x < _Badges.Count; x++)
                {
                    if (_badgeSlotIDs[x] > 0) // Badge enabled
                    {
                        szNotify += Encoding.encodeVL64(_badgeSlotIDs[x]);
                        szNotify += _Badges[x];
                        szNotify += Convert.ToChar(2);
                    }
                }

                this.Room.sendData("Cd" + szNotify);
            }
        }



        /// <summary>
        /// Tags - get tags of virtual user
        /// </summary>
        public void getTagsOfUser()
        {
            validatePacket(currentPacket);
            int ownerID = Encoding.decodeVL64(currentPacket.Substring(2));
            DataColumn dCol;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dCol = dbClient.getColumn("SELECT tag FROM cms_tags WHERE ownerid = '" + ownerID + "' LIMIT 20");
            }
            StringBuilder List = new StringBuilder(Encoding.encodeVL64(ownerID) + Encoding.encodeVL64(dCol.Table.Rows.Count));
            foreach (DataRow dRow in dCol.Table.Rows)
                List.Append(Convert.ToString(dRow["tag"]) + Convert.ToChar(2));
            sendData("E^" + List.ToString());
        }

        /// <summary>
        /// Group badges - get details about a group [click badge]
        /// </summary>
        public void getGroupDetails()
        {
            validatePacket(currentPacket);
            if (Room != null && roomUser != null)
            {
                int groupID = Encoding.decodeVL64(currentPacket.Substring(2));
                DataRow dRow;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dRow = dbClient.getRow("SELECT name,description,roomid FROM groups_details WHERE id = '" + groupID + "'");
                }
                if (dRow.Table.Rows.Count == 1)
                {
                    string roomName = "";
                    int roomID = Convert.ToInt32(dRow["roomid"]);
                    if (roomID > 0)
                    {
                        roomName = roomManager.getRoomName(roomID);
                    }
                    else
                        roomID = -1;

                    sendData("Dw" + Encoding.encodeVL64(groupID) + Convert.ToString(dRow["name"]) + Convert.ToChar(2) + Convert.ToString(dRow["description"]) + Convert.ToChar(2) + Encoding.encodeVL64(Convert.ToInt32(dRow["roomid"])) + roomName + Convert.ToChar(2));
                }
            }
        }

        /// <summary>
        /// Statuses - stop status
        /// </summary>
        public void removeStatus()
        {
            validatePacket(currentPacket);
            if (statusManager != null)
            {
                string Status = currentPacket.Substring(2);
                if (Status == "CarryItem")
                    statusManager.removeStatus("carryd");
                else if (Status == "Dance")
                {
                    statusManager.removeStatus("dance");
                }
            }
        }
        /// <summary>
        /// Statuses - wave
        /// </summary>
        public void statusWave()
        {
            if (Room != null && roomUser != null && statusManager.containsStatus("wave") == false)
            {
                statusManager.removeStatus("dance");
                statusManager.addStatus("wave", null, Config.Statuses_Wave_waveDuration, null, 0, 0);
            }
        }

        /// <summary>
        /// Statuses - dance
        /// </summary>
        public void doNewDance()
        {
            validatePacket(currentPacket);
            if (Room != null && roomUser != null && statusManager.containsStatus("sit") == false && statusManager.containsStatus("lay") == false)
            {
                statusManager.removeStatus("carryd");
                if (currentPacket.Length == 2)
                    statusManager.addStatus("dance", null, 0, null, 0, 0);
                else
                {
                    if (rankManager.containsRight(_Rank, "fuse_use_club_dance", _fuserights) == false) { return; }
                    int danceID = Encoding.decodeVL64(currentPacket.Substring(2));
                    if (danceID < 0 || danceID > 4) { return; }
                    statusManager.addStatus("dance", danceID.ToString(), 0, null, 0, 0);
                }
            }
        }
        /// <summary>
        /// Statuses - carry item
        /// </summary>
        public void carryNewItem()
        {
            validatePacket(currentPacket);
            if (Room != null && roomUser != null)
            {
                string Item = currentPacket.Substring(2);
                if (statusManager.containsStatus("lay") || Item.Contains("/"))
                    return; // THE HAX! \o/
                try
                {
                    int nItem = int.Parse(Item);
                    if (nItem < 1 || nItem > 26)
                        return;
                }
                catch
                {

                    if (_inPublicroom == false && Item.ToLower() != "water" && Item.ToLower() != "milk" && Item.ToLower() != "juice") // Not a drink that can be retrieved from the infobus minibar
                    {
                        reportExploiter("status-hack: " + Item);
                        return; }
                    else if (_inPublicroom && Item.ToLower() != "water" && Item != "melk" && Item != "Juice" && Item != "sap")
                    {
                        reportExploiter("status-hack: " + Item);
                        return;
                    }
                        
                }
                statusManager.addStatus("carryd", Item, Config.Statuses_itemCarrying_SipAmount, "drink", Config.Statuses_itemCarrying_SipInterval, Config.Statuses_itemCarrying_SipDuration);
            }
        }

        

        /// <summary>
        /// Statuses - Lido Voting
        /// </summary>
        public void voteForLido()
        {
            validatePacket(currentPacket);
            if (Room != null && roomUser != null && statusManager.containsStatus("sit") == false && statusManager.containsStatus("lay") == false)
            {
                if (currentPacket.Length == 2)
                    statusManager.addStatus("sign", null, 0, null, 0, 0);
                else
                {
                    string signID = currentPacket.Substring(2);
                    statusManager.addStatus("sign", signID, Config.Statuses_Wave_waveDuration, null, 0, 0);
                }
            }
        }

        #region hotelvote?
        //\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//
        // I never released the full poll code and I do not have it anymore (I lost it). I can not be bothered to code it again. This code has been disabled and skiped \\
        //\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//\\//
        //case "Cl": // Room Poll - answer
        //    {
        //        if (Room == null || roomUser == null)
        //            return;
        //        int subStringSkip = 2;
        //        int pollID = Encoding.decodeVL64(currentPacket.Substring(subStringSkip));
        //        if (DB.checkExists("SELECT aid FROM poll_results WHERE uid = '" + userID + "' AND pid = '" + pollID + "'"))
        //            return;
        //        subStringSkip += Encoding.encodeVL64(pollID).Length;
        //        int questionID = Encoding.decodeVL64(currentPacket.Substring(subStringSkip));
        //        subStringSkip += Encoding.encodeVL64(questionID).Length;
        //        bool typeThree = DB.checkExists("SELECT type FROM poll_questions WHERE qid = '" + questionID + "' AND type = '3'");
        //        if (typeThree)
        //        {
        //            int countAnswers = Encoding.decodeB64(currentPacket.Substring(subStringSkip, 2));
        //            subStringSkip += 2;
        //            string Answer = DB.Stripslash(currentPacket.Substring(subStringSkip, countAnswers));
        //            DB.runQuery("INSERT INTO poll_results (pid,qid,aid,answers,uid) VALUES ('" + pollID + "','" + questionID + "','0','" + Answer + "','" + userID + "')");
        //        }
        //        else
        //        {
        //            int countAnswers = Encoding.decodeVL64(currentPacket.Substring(subStringSkip));
        //            subStringSkip += Encoding.encodeVL64(countAnswers).Length;
        //            int[] Answer = new int[countAnswers];
        //            for (int i = 0; i < countAnswers; i++)
        //            {
        //                Answer[i] = Encoding.decodeVL64(currentPacket.Substring(subStringSkip));
        //                subStringSkip += Encoding.encodeVL64(Answer[i]).Length;
        //            }
        //            foreach (int a in Answer)
        //            {
        //                DB.runQuery("INSERT INTO poll_results (pid,qid,aid,answers,uid) VALUES ('" + pollID + "','" + questionID + "','" + a + "',' ','" + userID + "')");
        //            }
        //        }
        //        break;
        //    }
        #endregion

        #endregion

        #region Chat
        /// <summary>
        /// Chat - Talk to eachother (shout/say)
        /// </summary>
        public void talkInsideRoom()
        {
            validatePacket(currentPacket);
            if (!checkAllowedToTalk())
            {
                return; 
            }
            try
            {
                if (_isMuted == false && (Room != null && roomUser != null))
                {
                    string Message = currentPacket.Substring(4);
                    //userManager.addChatMessage(_Username, _roomID, Message);
                    Message = stringManager.filterSwearWords(Message);
                    if (Message.Length > 165)
                    {
                        reportExploiter("long chat message crash message was: " + Message );
                        return;
                    }
                    if (Message.Substring(0, 1) == ":" && isSpeechCommand(Message.Substring(1))) // Speechcommand invoked!
                    {
                        if (roomUser.isTyping)
                        {
                            hideChatBubble();
                            roomUser.isTyping = false;
                        }

                    }
                    else
                    {
                        if (stringManager.isGoodText(Message))
                        {
                            if (currentPacket.Substring(1, 1) == "w") // Shout
                            {
                                Room.sendShout(roomUser, Message);
                            }
                            else
                            {
                                Room.sendSaying(roomUser, Message);

                            }
                        }
                        else
                        {
                            sendWarning();
                        }
                    }
                }
            }
            catch { }
        }
        private Queue<DateTime> chatQueue = new Queue<DateTime>();
        private TimeSpan totalTimeCheckDifferenceChat = new TimeSpan(0, 0, 5);
        private int amountOfChatsPer5Seconds = 5;

        private TimeSpan blockTime = new TimeSpan(0, 5, 0);
        private DateTime chatBlockedUntill = DateTime.MinValue;

        private bool checkAllowedToTalk()
        {
            if (chatBlockedUntill > DateTime.Now)
            {
                sendNotify("Wacht aub nog " + ((int)(chatBlockedUntill - DateTime.Now).TotalSeconds) + " seconden tot je weer kan praten");
                return false;
            }
            if (chatQueue.Count > 0)
            {
                
                bool checking = true;
                DateTime currentCheck;
                while (checking && chatQueue.Count != 0)
                {
                    currentCheck = chatQueue.Peek();
                    //Double now = (DateTime.Now - currentCheck).TotalSeconds;
                    //double next = totalTimeCheckDifferenceChat.TotalSeconds;
                    //Out.WriteLine("now = " + now + " next =" + next, Out.logFlags.ImportantAction);
                    if ((DateTime.Now - currentCheck).TotalSeconds > totalTimeCheckDifferenceChat.TotalSeconds)
                    {
                        chatQueue.Dequeue();
                        //Out.WriteLine("Dqueue -> Queue size: " + chatQueue.Count, Out.logFlags.ImportantAction);
                    }
                    else
                    {
                        checking = false;
                        //Out.WriteLine("Dqueueing done -> Queue size: " + chatQueue.Count, Out.logFlags.ImportantAction);
                    }
                }
                if (chatQueue.Count > amountOfChatsPer5Seconds)
                {
                    chatBlockedUntill = DateTime.Now.AddSeconds(blockTime.TotalSeconds);
                }
                chatQueue.Enqueue(DateTime.Now);
                return chatQueue.Count < amountOfChatsPer5Seconds;
            }
            else
            {
                chatQueue.Enqueue(DateTime.Now);
                return true;
            }
        }

        /// <summary>
        /// Chat - whisper
        /// </summary>
        public void whisperToPlayer()
        {
            validatePacket(currentPacket);
            if (!checkAllowedToTalk())
            {
                return;
            }
            
            if (_isMuted == false && Room != null && roomUser != null)
            {
                string Receiver = currentPacket.Substring(4).Split(' ')[0];
                string Message = currentPacket.Substring(Receiver.Length + 5);
                userManager.addChatMessage(_Username, _roomID, Message);

                Message = stringManager.filterSwearWords(Message);
                if (Message.Length > 165)
                {
                    reportExploiter("long chat message crash message was: " + Message);
                    return;
                }
                Room.sendWhisper(roomUser, Receiver, Message);
                //Out.WriteChat("Whisper", _Username + "-" + Receiver, Message); 
            }
        }

        /// <summary>
        /// Chat - show speech bubble
        /// </summary>
        public void sendSpeechBubble()
        {
            if (_isMuted == false && Room != null && roomUser != null)
            {
                Room.sendUserIsTyping(roomUser.roomUID, true);
                roomUser.isTyping = true;
            }
        }

        /// <summary>
        /// Chat - hide speech bubble
        /// </summary>
        public void hideChatBubble()
        {
            if (Room != null && roomUser != null && roomUser.isTyping)
            {
                Room.sendUserIsTyping(roomUser.roomUID, false);
                roomUser.isTyping = false;
            }
        }
        #endregion

        #region Guestroom - rights, kicking, roombans and room voting
        /// <summary>
        /// Give rights
        /// </summary>
        public void giveRightsToPlayer()
        {
            validatePacket(currentPacket);
            if (Room == null || roomUser == null || _inPublicroom || _isOwner == false)
                return;

            string Target = currentPacket.Substring(2);
            if (userManager.containsUser(Target) == false)
                return;

            virtualUser _Target = userManager.getUser(Target);
            if (_Target._roomID != _roomID || _Target._hasRights || _Target._isOwner)
                return;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.runQuery("INSERT INTO room_rights(roomid,userid) VALUES ('" + _roomID + "','" + _Target.userID + "')");
            }
            _Target._hasRights = true;
            _Target.statusManager.addStatus("flatctrl", "onlyfurniture", 0, null, 0, 0);
            _Target.sendData("@j");
        }

        /// <summary>
        /// Take rights
        /// </summary>
        public void removeRights()
        {
            validatePacket(currentPacket);
            if (Room == null || roomUser == null || _inPublicroom || _isOwner == false)
                return;

            string Target = currentPacket.Substring(2);
            if (userManager.containsUser(Target) == false)
                return;

            virtualUser _Target = userManager.getUser(Target);
            if (_Target._roomID != _roomID || _Target._hasRights == false || _Target._isOwner)
                return;

            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.runQuery("DELETE FROM room_rights WHERE roomid = '" + _roomID + "' AND userid = '" + _Target.userID + "' LIMIT 1");
            }
            _Target._hasRights = false;
            _Target.statusManager.removeStatus("flatctrl");
            _Target.sendData("@k");
        }

        /// <summary>
        /// Kick user from a room
        /// </summary>
        public void kickUserFromRoom()
        {
            validatePacket(currentPacket);
            if (Room == null || roomUser == null || _inPublicroom || _hasRights == false)
                return;

            string Target = currentPacket.Substring(2);
            if (userManager.containsUser(Target) == false)
                return;

            virtualUser _Target = userManager.getUser(Target);
            if (_Target._roomID != _roomID)
                return;
            if (_Target._Rank > 2)
                return;
            if (_Target._isOwner && (_Target._Rank > _Rank || rankManager.containsRight(_Target._Rank, "fuse_any_room_controller", _fuserights)))
                return;

            if (_Target.roomUser == null)
                return;
            _Target.roomUser.walkLock = true;
            _Target.roomUser.walkDoor = true;
            _Target.roomUser.goalX = Room.doorX;
            _Target.roomUser.goalY = Room.doorY;
        }

        /// <summary>
        /// Kick and apply roomban
        /// </summary>
        public void kickAndRoomBan()
        {
            validatePacket(currentPacket);
            if (_hasRights == false || _inPublicroom || Room == null || roomUser == null)
                return;

            string Target = currentPacket.Substring(2);
            if (userManager.containsUser(Target) == false)
                return;

            virtualUser _Target = userManager.getUser(Target);
            if (_Target._roomID != _roomID)
                return;
            if (_Target._Rank > 2)
                return;
            if (_Target._isOwner && (_Target._Rank > _Rank || rankManager.containsRight(_Target._Rank, "fuse_any_room_controller", _fuserights)))
                return;

            string banExpireMoment = DateTime.Now.AddMinutes(Config.Rooms_roomBan_banDuration).ToString();
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.runQuery("INSERT INTO room_bans (roomid,userid,ban_expire) VALUES ('" + _roomID + "','" + _Target.userID + "','" + banExpireMoment + "')");
            }
            if (_Target.roomUser != null)
            {
                _Target.roomUser.walkLock = true;
                _Target.roomUser.walkDoor = true;
                _Target.roomUser.goalX = Room.doorX;
                _Target.roomUser.goalY = Room.doorY;
            }
        }

        /// <summary>
        /// Vote -1 or +1 on a room
        /// </summary>
        public void voteForRoom()
        {
            validatePacket(currentPacket);
            if (_inPublicroom || Room == null || roomUser == null)
                return;

            int Vote = Encoding.decodeVL64(currentPacket.Substring(2));

            if ((Vote == 1 || Vote == -1) && !Room.hasUserVoted(this.userID))
            {
                Room.voteForRoom(userID, Vote);
                Room.sendNewVoteAmount(Room.getVoteCount());
            }

        }

        #endregion

        #region Catalogue and Recycler
        /// <summary>
        /// Catalogue - open, retrieve index of pages
        /// </summary>
        public void sendIndexPage()
        {
            sendData("A~" + catalogueManager.getPageIndex(_Rank));

        }
        /// <summary>
        /// Catalogue, open page, get page content
        /// </summary>
        public void openPageContent()
        {
            validatePacket(currentPacket);
            string pageIndexName = currentPacket.Split('/')[1];
            sendData("A" + catalogueManager.getPage(pageIndexName, _Rank));
        }

        /// <summary>
        /// Validates a name of a pet
        /// </summary>
        public void validatePetName()
        {
            validatePacket(currentPacket);

            string substringPart = currentPacket.Substring(currentPacket.Length - 1);
            bool isPet = (substringPart == "I");
            if (isPet)
            {
                string name = currentPacket.Substring(4, currentPacket.Length - 5);
                if (stringManager.usernameIsValid(name))
                    sendData("@dH");
                else
                    sendData("@dJJ");
            }
        }
        /// <summary>
        /// Catalogue - purchase
        /// </summary>
        public void doPurchase()
        {
            string[] packetContent = currentPacket.Split(Convert.ToChar(13));
            string pageName = packetContent[1];
            string itemCct = packetContent[3];
            string[] varItem = packetContent[4].Split('\x02');
            bool present = (packetContent[5] == "1");
            string presentMessage = stringManager.filterSwearWords(packetContent[7]);

            #region exploit checking
            if (!(itemCct == "pet0") && !(itemCct == "pet1") && !(itemCct == "pet2"))
            {
                validatePacket(currentPacket);
            }
            else
            {
                for (int i = 0; i < packetContent.Length; i++)
                {
                    if (i != 4)
                        validatePacket(packetContent[i]);
                    else
                    {
                        for (int x = 0; x < varItem.Length; x++)
                            validatePacket(varItem[x]);
                    }
                }
            }
            #endregion
            cataloguePage page = catalogueManager.getPageObject(pageName, this._Rank);
            itemTemplate temp = catalogueManager.getTemplate(itemCct);

            if (page == null || !page.hasItem(temp))
            { return; }


            int TID = catalogueManager.getTemplate(itemCct).templateID;

            int price = catalogueManager.getTemplate(itemCct).cost;
            int receiverid = this.userID;
            if (present)
            {
                receiverid = userManager.getUserID(packetContent[6]);
                if (receiverid == 0)
                    sendError(errorList.USER_NOT_EXIST, packetContent[6]);
            }

            purchaseItem pItem = new purchaseItem(TID, packetContent[4], pageName, itemCct, price, present, receiverid, presentMessage, false);


            int error = catalogueManager.handlePurchase(pItem, this);
            if (error != 0)
            {
                sendError(error, "");
            }

        }


        /// <summary>
        /// Buy game-tickets
        /// </summary>
        public void buyIngameTickets()
        {
            validatePacket(currentPacket);
            string args = currentPacket.Substring(2);
            int Amount = Encoding.decodeVL64(args.Substring(0, 3));
            string Receiver = args.Substring(3);
            int Ticketamount = 0;
            int Price = 0;

            if (Amount == 1) // Look how much tickets you want
            {
                Ticketamount = 2;
                Price = 1;
            }
            else if (Amount == 2) // And again
            {
                Ticketamount = 20;
                Price = 6;
            }
            else // Wrong parameter
                return;

            if (Price > _Credits) // Enough credits?
            {
                sendData("AD");
                return;
            }
            int ReceiverID;
            if (Receiver != this._Username)
            {
                ReceiverID = userManager.getUserID(Receiver);
            }
            else
            {
                ReceiverID = this.userID;
            }

            if (!(ReceiverID > 0)) // Does the user exist?
            {
                sendData("AL" + Receiver);
                return;
            }

            _Credits -= Price; // New credit amount
            sendData("@F" + _Credits); // Send the new credits
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                // Update receivers ticketamount
                dbClient.runQuery("UPDATE users SET tickets = tickets+" + Ticketamount + " WHERE id = '" + ReceiverID + "' LIMIT 1");
            }


            if (ReceiverID == userID)
            {
                refreshValueables(false, true, false, false);
            }
            else
            {
                if (userManager.containsUser(ReceiverID))
                {
                    virtualUser _Receiver = userManager.getUser(ReceiverID);
                    _Receiver._Tickets = _Receiver._Tickets + Ticketamount;
                    _Receiver.refreshValueables(false, true, false, false);

                }
            }

        }

        /// <summary>
        /// Recycler - proceed input items
        /// </summary>
        public void addRecyclerJob()
        {
            validatePacket(this.currentPacket);
            string currentPacket = this.currentPacket;
            if (Config.enableRecycler == false || Room == null || recyclerManager.sessionExists(userID))
                return;

            int itemCount = Encoding.decodeVL64(currentPacket.Substring(2));
            if (recyclerManager.rewardExists(itemCount))
            {
                recyclerManager.createSession(userID, itemCount);
                currentPacket = this.currentPacket.Substring(Encoding.encodeVL64(itemCount).ToString().Length + 2);
                for (int i = 0; i < itemCount; i++)
                {
                    int itemID = Encoding.decodeVL64(currentPacket);
                    //Database dbClient = new Database(true, false, 109);

                    if (handItem.hasItem(itemID))
                    {
                        handItem.removeItem(itemID);
                        currentPacket = currentPacket.Substring(Encoding.encodeVL64(itemID).ToString().Length);
                    }

                    else
                    {
                        recyclerManager.dropSession(userID, true);
                        sendData("DpH");

                        return;
                    }

                }

                sendData("Dp" + recyclerManager.sessionString(userID));
                refreshHand("update");
            }

        }

        /// <summary>
        /// Recycler - redeem/cancel session
        /// </summary>
        public void redeemRecyclerJob()
        {
            validatePacket(currentPacket);
            if (Config.enableRecycler == false || Room != null && recyclerManager.sessionExists(userID))
            {
                bool Redeem = (currentPacket.Substring(2) == "I");
                if (Redeem && recyclerManager.sessionReady(userID))
                    handItem.addItemList(recyclerManager.rewardSession(userID));
                recyclerManager.dropSession(userID, Redeem);

                sendData("Dp" + recyclerManager.sessionString(userID));
                if (Redeem)
                    refreshHand("last");
                else
                    refreshHand("new");
            }
        }
        #endregion

        #region Hand and item handling

        /// <summary>
        /// Returns the current hand
        /// </summary>
        public void getHand()
        {
            validatePacket(currentPacket);
            if (Room == null || roomUser == null)
                return;

            string Mode = currentPacket.Substring(2);
            refreshHand(Mode);
        }

        //case "LB": // Hand    ---- same as AA?
        //    {
        //        if (Room == null || roomUser == null)
        //            return;
        //        string Mode = currentPacket.Substring(2);
        //        refreshHand(Mode);
        //        break;
        //    }

        // Item handling - apply wallpaper/floor/landscape to room
        public void applyNewDecoration()
        {
            validatePacket(currentPacket);
            if (_hasRights == false || _inPublicroom || Room == null || roomUser == null)
                return;
            int itemID = 0;
            int.TryParse(currentPacket.Split('/')[1], out itemID);
            if (itemID == 0)
                return;
            string decorType = currentPacket.Substring(2).Split('/')[0];
            if (decorType != "wallpaper" && decorType != "floor" && decorType != "landscape") // Non-valid decoration type
                return;

            ;
            if (!handItem.hasItem(itemID))
                return;


            genericItem Item = handItem.getItem(itemID);


            if (Item.template.Sprite != decorType) // This item isn't the decoration item the client thinks it is. (obv scripter) If the item wasn't found (so the user didn't owned the item etc), then an empty item template isn't required, which also doesn't match this condition
            {
                return;
            }
            string decorVal = Item.Var;
            Room.sendData("@n" + decorType + "/" + decorVal); // "@n" (46) is a generic message for setting a room's decoration. Since the introduction of landscapes, it can be 'wallpaper', 'floor' and 'landscape'
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.AddParamWithValue("value", decorVal);
                dbClient.runQuery("UPDATE rooms SET " + decorType + " = @value WHERE id = '" + _roomID + "' LIMIT 1"); // Generates query like 'UPDATE rooms SET floor/wallpaper/landscape blabla' (the string decorType is containing either 'floor', 'wallpaper' or 'landscape')
                dbClient.runQuery("DELETE FROM furniture WHERE id = '" + itemID + "' LIMIT 1");
                //dbClient.runQuery("DELETE FROM user_ furniture WHERE furniture_id = " + itemID);
            }
            handItem.removeItem(itemID);
        }
        /// <summary>
        /// Item handling - place item down
        /// </summary>
        public void placeItemInRoom()
        {
            validatePacket(currentPacket);
            if (_hasRights == false || _inPublicroom || Room == null || roomUser == null)
                return;
            int itemID;
            if (!int.TryParse((currentPacket.Split(' ')[0].Substring(2)), out itemID))
                return;

            if (!handItem.hasItem(itemID))
                return;

            genericItem Item = handItem.getItem(itemID);
            bool needsRemove = true;
            if (Item.template.typeID == 0)
            {
                string _INPUTPOS = currentPacket.Substring(itemID.ToString().Length + 3);
                string _CHECKEDPOS = catalogueManager.wallPositionOK(_INPUTPOS);
                if (_CHECKEDPOS != _INPUTPOS)
                {
                    return;
                }
                string Var;
                int VarValue = 0;

                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {

                    Var = Item.Var;

                    if (stringManager.getStringPart(Item.Sprite, 0, 7) == "post.it")
                    {
                        if (!Int32.TryParse(Var, out VarValue))
                        {
                            handItem.removeItem(itemID);
                            dbClient.runQuery("DELETE FROM furniture WHERE id = '" + itemID + "' LIMIT 1");
                            return;
                        }
                        if (VarValue > 1)
                        {
                            dbClient.runQuery("UPDATE furniture SET var = var - 1 WHERE id = '" + itemID + "' LIMIT 1");

                            Item.Var = (VarValue - 1).ToString();
                            needsRemove = false;
                        }
                        else
                        {
                            dbClient.runQuery("DELETE FROM furniture WHERE id = '" + itemID + "' LIMIT 1");

                        }
                        int lastItemID = (int)dbClient.insertQuery("INSERT INTO furniture(tid, var) VALUES ('" + Item.template.templateID + "', 'FFFF33')");
                        Item = new genericItem(lastItemID, Item.template.templateID, "FFFF33", "");
                        dbClient.insertQuery("INSERT INTO furniture_stickies(id) VALUES ('" + lastItemID + "')");
                        Item.makeWallposInformation("FFFF33");
                    }
                    Item.wallpos = _CHECKEDPOS;

                    if (Room.wallItemManager.addItem(Item, true))
                    {
                        if (needsRemove)
                            handItem.removeItem(itemID);
                    }
                }
            }
            else
            {
                string[] locDetails = currentPacket.Split(' ');
                byte X = byte.Parse(locDetails[1]);
                byte Y = byte.Parse(locDetails[2]);
                byte Z = byte.Parse(locDetails[3]);
                byte typeID = Item.template.typeID;
                int errorID = Room.floorItemManager.placeItem(Item, X, Y, typeID, Z, Item.Var);
                if (errorID == 0)
                {
                    handItem.removeItem(itemID);
                }
                else
                {
                    if (errorID == -1)
                        return;
                    sendError(errorID, null);
                }
            }
        }

        /// <summary>
        /// Item handling - pickup item
        /// </summary>
        public void removeItemFromRoom()
        {
            validatePacket(currentPacket);
            if (_isOwner == false || _inPublicroom || Room == null || roomUser == null)
                return;

            int itemID = int.Parse(currentPacket.Split(' ')[2]);
            if (Room.floorItemManager.containsItem(itemID))
            {
                genericItem toReturn = Room.floorItemManager.removeItem(itemID, userID);
                if (toReturn != null)
                    handItem.addItem(toReturn);
            }
            else if (Room.wallItemManager.containsItem(itemID) && stringManager.getStringPart(Room.wallItemManager.getItem(itemID).Sprite, 0, 7) != "post.it") // Can't pickup stickies from room
            {
                genericItem toReturn = Room.wallItemManager.removeItem(itemID, userID);
                if (toReturn != null)
                    handItem.addItem(toReturn);
            }
            else
                return;

            refreshHand("update");
        }

        /// <summary>
        /// Item handling - move/rotate item
        /// </summary>
        public void rotateItemInRoom()
        {
            validatePacket(currentPacket);
            if (_hasRights == false || _inPublicroom || Room == null || roomUser == null)
                return;

            int itemID = int.Parse(currentPacket.Split(' ')[0].Substring(2));
            try
            {
                if (Room.floorItemManager.containsItem(itemID))
                {
                    string[] locDetails = currentPacket.Split(' ');
                    int X = int.Parse(locDetails[1]);
                    int Y = int.Parse(locDetails[2]);
                    byte Z = byte.Parse(locDetails[3]);

                    Room.floorItemManager.relocateItem(itemID, X, Y, Z);
                }
            }
            catch { }
        }

        /// <summary>
        /// Item handling - toggle wallitem status
        /// </summary>
        public void toggleWallItemStatus()
        {
            validatePacket(currentPacket);
            try
            {
                if (_inPublicroom || Room == null || roomUser == null)
                    return;

                int itemID = int.Parse(currentPacket.Substring(4, Encoding.decodeB64(currentPacket.Substring(2, 2))));
                int toStatus = Encoding.decodeVL64(currentPacket.Substring(itemID.ToString().Length + 4));

                Room.wallItemManager.toggleItemStatus(itemID, toStatus);
                return;
            }
            catch { }
        }

        /// <summary>
        /// Item handling - toggle flooritem status
        /// </summary>
        public void toggleFloorItemStatus()
        {
            validatePacket(currentPacket);
            if (Room == null || roomUser == null)
                return;
            try
            {
                int itemID = int.Parse(currentPacket.Substring(4, Encoding.decodeB64(currentPacket.Substring(2, 2))));
                string toStatus = currentPacket.Substring(itemID.ToString().Length + 6);
                int tester;
                if (toStatus.ToLower() == "false" || toStatus.ToLower() == "on" || toStatus.ToLower() == "off" || toStatus.ToLower() == "true" || int.TryParse(toStatus, out tester) || toStatus.ToLower() == "c" || toStatus.ToLower() == "o")
                    Room.floorItemManager.toggleItemStatus(itemID, toStatus, _hasRights);
                else
                    Disconnect();

            }
            catch { }
        }

        /// <summary>
        /// Item handling - open presentbox
        /// </summary>
        public void openPresentBox()
        {
            validatePacket(currentPacket);
            if (_isOwner == false || _inPublicroom || Room == null || roomUser == null)
                return;

            int itemID = int.Parse(currentPacket.Substring(2));
            if (Room.floorItemManager.containsItem(itemID) == false)
                return;

            DataColumn dCol;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dCol = dbClient.getColumn("SELECT itemid FROM furniture_presents WHERE id = '" + itemID + "'");

                if (dCol.Table.Rows.Count > 0)
                {
                    int item;

                    DataRow dRow2;
                    itemTemplate Template = new itemTemplate();
                    foreach (DataRow dRow in dCol.Table.Rows)
                    {
                        item = Convert.ToInt32(dRow["itemid"]);

                        dRow2 = dbClient.getRow("SELECT tid,var FROM furniture WHERE id = '" + item + "'");
                        genericItem itemz = new genericItem(item, Convert.ToInt32(dRow2[0]), dRow2[1].ToString(), "");
                        handItem.addItem(itemz);
                        Template = itemz.template;
                    }
                    Room.floorItemManager.removeItem(itemID, 0);



                    if (Template.typeID > 0)
                        sendData("BA" + Template.Sprite + "\r" + Template.Sprite + "\r" + Template.Length + Convert.ToChar(30) + Template.Width + Convert.ToChar(30) + Template.Colour);
                    else
                        sendData("BA" + Template.Sprite + "\r" + Template.Sprite + " " + Template.Colour + "\r");
                }
                dbClient.runQuery("DELETE FROM furniture_presents WHERE id = '" + itemID + "' LIMIT " + dCol.Table.Rows.Count);
                dbClient.runQuery("DELETE FROM furniture WHERE id = '" + itemID + "' LIMIT 1");
                handItem.removeItem(itemID);
                //dbClient.runQuery("DELETE FROM user_ furniture WHERE furniture_id = '" + itemID + "'");
                //dbClient.runQuery("DELETE FROM room_furniture WHERE furniture_id = '" + itemID + "'");

            }
            refreshHand("last");
        }

        /// <summary>
        /// Item handling - redeem credit item
        /// </summary>
        public void redeemCreditItem()
        {
            validatePacket(currentPacket);
            if (_isOwner == false || _inPublicroom || Room == null || roomUser == null)
                return;

            int itemID = Encoding.decodeVL64(currentPacket.Substring(2));
            if (Room.floorItemManager.containsItem(itemID))
            {
                string Sprite = Room.floorItemManager.getItem(itemID).Sprite;
                if (Sprite.Substring(0, 3).ToLower() != "cf_" && Sprite.Substring(0, 3).ToLower() != "cfc_")
                    return;
                int redeemValue = 0;
                try { redeemValue = int.Parse(Sprite.Split('_')[1]); }
                catch { return; }

                Room.floorItemManager.removeItem(itemID, 0);

                _Credits += redeemValue;
                sendData("@F" + _Credits);
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.runQuery("UPDATE users SET credits = '" + _Credits + "' WHERE id = '" + userID + "' LIMIT 1");
                }
            }
        }

        /// <summary>
        /// Item handling - teleporters - enter teleporter
        /// </summary>
        public void goEnterTeleporter()
        {
            validatePacket(currentPacket);
            if (_inPublicroom || Room == null || roomUser == null)
                return;

            int itemID = int.Parse(currentPacket.Substring(2));
            if (Room.floorItemManager.containsItem(itemID))
            {
                genericItem Teleporter = Room.floorItemManager.getItem(itemID);
                // Prevent clientside 'jumps' to teleporter, check if user is removed one coord from teleporter entrance
                if (Teleporter.Z == 2 && roomUser.X != Teleporter.X + 1 && roomUser.Y != Teleporter.Y)
                    return;
                else if (Teleporter.Z == 4 && roomUser.X != Teleporter.X && roomUser.Y != Teleporter.Y + 1)
                    return;
                roomUser.goalX = -1;
                Room.moveUser(this.roomUser, Teleporter.X, Teleporter.Y, true);
            }
        }

        /// <summary>
        /// Item handling - teleporters - flash teleporter
        /// </summary>
        public void doTeleporterFlash()
        {
            validatePacket(currentPacket);
            if (_inPublicroom || Room == null || roomUser == null)
                return;

            int itemID = int.Parse(currentPacket.Substring(2));
            if (Room.floorItemManager.containsItem(itemID))
            {
                genericItem Teleporter1 = Room.floorItemManager.getItem(itemID);
                if (roomUser.X != Teleporter1.X && roomUser.Y != Teleporter1.Y)
                    return;
                int roomIDTeleporter2;
                int idTeleporter2;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    idTeleporter2 = dbClient.getInt("SELECT teleportid FROM furniture WHERE id = '" + itemID + "'");
                    roomIDTeleporter2 = dbClient.getInt("SELECT room_id FROM room_furniture WHERE furniture_id = '" + idTeleporter2 + "'");
                }
                if (roomIDTeleporter2 > 0)
                    new TeleporterUsageSleep(useTeleporter).BeginInvoke(Teleporter1, idTeleporter2, roomIDTeleporter2, null, null);
            }
        }

        /// <summary>
        /// Item handling - dices - close dice
        /// </summary>
        public void closeDice()
        {
            validatePacket(currentPacket);
            if (Room == null || roomUser == null || _inPublicroom)
                return;

            int itemID = int.Parse(currentPacket.Substring(2));
            Room.closeDice(itemID, this.roomUser);
        }

        /// <summary>
        /// Item handling - dices - spin dice
        /// </summary>
        public void spinDice()
        {
            validatePacket(currentPacket);
            if (Room == null || roomUser == null || _inPublicroom)
                return;

            int itemID = int.Parse(currentPacket.Substring(2));
            Room.throwDice(itemID, this.roomUser);
        }

        /// <summary>
        /// Item handling - spin Wheel of fortune
        /// </summary>
        public void spinWheelOfFortune()
        {
            validatePacket(currentPacket);
            if (_hasRights == false || Room == null || roomUser == null || _inPublicroom)
                return;
            int itemID = Encoding.decodeVL64(currentPacket.Substring(2));
            Room.spinWheel(itemID, roomUser);
        }

        /// <summary>
        /// Item handling - activate Love shuffler sofa
        /// </summary>
        public void activateLoveSofa()
        {
            validatePacket(currentPacket);
            if (Room == null || roomUser == null || _inPublicroom)
                return;

            int itemID = Encoding.decodeVL64(currentPacket.Substring(2));
            if (Room.floorItemManager.containsItem(itemID) && Room.floorItemManager.getItem(itemID).Sprite == "val_randomizer")
            {
                int rndNum = new Random(DateTime.Now.Millisecond).Next(1, 5);
                Room.sendData("AX" + itemID + Convert.ToChar(2) + "123456789" + Convert.ToChar(2));
                Room.sendData("AX" + itemID + Convert.ToChar(2) + rndNum + Convert.ToChar(2), 5000);
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.runQuery("UPDATE furniture SET var = '" + rndNum + "' WHERE id = '" + itemID + "' LIMIT 1");
                }
            }
        }

        /// <summary>
        /// Item handling - stickies/photo's - open stickie/photo
        /// </summary>
        public void openStickyOrPhoto()
        {
            validatePacket(currentPacket);
            if (Room == null || roomUser == null || _inPublicroom)
                return;

            int itemID = int.Parse(currentPacket.Substring(2));
            if (Room.wallItemManager.containsItem(itemID))
            {

                string Message;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    Message = dbClient.getString("SELECT text FROM furniture_stickies WHERE id = '" + itemID + "'");
                }
                sendData("@p" + itemID + Convert.ToChar(9) + Room.wallItemManager.getItem(itemID).Var + " " + Message);
            }
        }

        /// <summary>
        /// Item handling - stickies - edit stickie colour/message
        /// </summary>
        public void editSticky()
        {
            validatePacket(currentPacket);
            if (_hasRights == false || Room == null || roomUser == null || _inPublicroom)
                return;

            int itemID = int.Parse(currentPacket.Substring(2, currentPacket.IndexOf("/") - 2));
            if (Room.wallItemManager.containsItem(itemID))
            {
                genericItem Item = Room.wallItemManager.getItem(itemID);
                string Sprite = Item.Sprite;
                if (Sprite != "post.it" && Sprite != "post.it.vd")
                    return;
                string Colour = "FFFFFF"; // Valentine stickie default colour
                if (Sprite == "post.it") // Normal stickie
                {
                    Colour = currentPacket.Substring(2 + itemID.ToString().Length + 1, 6);
                    if (Colour != "FFFF33" && Colour != "FF9CFF" && Colour != "9CFF9C" && Colour != "9CCEFF")
                        return;
                }

                string Message = currentPacket.Substring(2 + itemID.ToString().Length + 7);
                if (Message.Length > 684)
                    return;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.AddParamWithValue("text", stringManager.filterSwearWords(Message).Replace("/r", Convert.ToChar(13).ToString()));
                    if (Colour != Item.Var)
                    {
                        dbClient.AddParamWithValue("colour", Colour);
                        dbClient.runQuery("UPDATE furniture SET var = @colour WHERE id = '" + itemID + "' LIMIT 1");
                        Item.Var = Colour;
                    }

                    dbClient.runQuery("UPDATE furniture_stickies SET text = @text WHERE id = '" + itemID + "' LIMIT 1");
                }
                Room.sendData("AU" + itemID + Convert.ToChar(9) + Sprite + Convert.ToChar(9) + " " + Item.wallpos + Convert.ToChar(9) + Colour);
            }
        }

        /// <summary>
        /// Item handling - stickies/photo - delete stickie/photo
        /// </summary>
        public void deleteSticky()
        {
            validatePacket(currentPacket);
            if (_isOwner == false || Room == null || roomUser == null || _inPublicroom)
                return;

            int itemID = int.Parse(currentPacket.Substring(2));
            if (Room.wallItemManager.containsItem(itemID) && stringManager.getStringPart(Room.wallItemManager.getItem(itemID).Sprite, 0, 7) == "post.it")
            {
                Room.wallItemManager.removeItem(itemID, 0);
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.runQuery("DELETE FROM furniture_stickies WHERE id = '" + itemID + "' LIMIT 1");
                }

            }
        }
        #endregion

        #region Soundmachines
        /// <summary>
        /// Soundmachine - initialize songs in soundmachine
        /// </summary>
        public void initializeSoundMachine()
        {
            if (_isOwner && Room != null && Room.floorItemManager.soundMachineID > 0)
                sendData("EB" + soundMachineManager.getMachineSongList(Room.floorItemManager.soundMachineID));
        }

        /// <summary>
        /// Soundmachine - enter room initialize playlist
        /// </summary>
        public void initializePlaylists()
        {
            if (Room != null && Room.floorItemManager.soundMachineID > 0)
                sendData("EC" + soundMachineManager.getMachinePlaylist(Room.floorItemManager.soundMachineID));
        }

        /// <summary>
        /// Soundmachine - get song title and data of certain song
        /// </summary>
        public void getSongDetails()
        {
            validatePacket(currentPacket);
            if (Room != null && Room.floorItemManager.soundMachineID > 0)
            {
                int songID = Encoding.decodeVL64(currentPacket.Substring(2));
                sendData("Dl" + soundMachineManager.getSong(songID));
            }
        }

        /// <summary>
        /// Soundmachine - save playlist
        /// </summary>
        public void savePlaylistSoundMachine()
        {
            validatePacket(this.currentPacket);
            string currentPacket = this.currentPacket;
            if (_isOwner && Room != null && Room.floorItemManager.soundMachineID > 0)
            {
                int Amount = Encoding.decodeVL64(currentPacket.Substring(2));
                if (Amount < 6) // Max playlist size
                {
                    currentPacket = currentPacket.Substring(Encoding.encodeVL64(Amount).Length + 2);
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        dbClient.runQuery("DELETE FROM soundmachine_playlists WHERE machineid = '" + Room.floorItemManager.soundMachineID + "'");
                    }
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < Amount; i++)
                    {
                        int songID = Encoding.decodeVL64(currentPacket);
                        sb.Append(" ,('" + Room.floorItemManager.soundMachineID + "','" + songID + "','" + i + "')");
                        currentPacket = currentPacket.Substring(Encoding.encodeVL64(songID).Length);
                    }
                    if (sb.Length != 0)
                    {
                        using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                        {
                            dbClient.runQuery("INSERT INTO soundmachine_playlists(machineid,songid,pos) VALUES " + sb.ToString().Substring(2));
                        }
                    }

                    Room.sendData("EC" + soundMachineManager.getMachinePlaylist(Room.floorItemManager.soundMachineID)); // Refresh playlist
                }
            }
        }

        /// <summary>
        /// Sound machine - burn song to disk
        /// </summary>
        public void burnSongToList()
        {
            validatePacket(currentPacket);
            if (_isOwner && Room != null && Room.floorItemManager.soundMachineID > 0)
            {
                int songID = Encoding.decodeVL64(currentPacket.Substring(2));
                bool result = false;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    result = dbClient.findsResult("SELECT id FROM soundmachine_songs WHERE id = '" + songID + "' AND userid = '" + userID + "' AND machineid = '" + Room.floorItemManager.soundMachineID + "'");
                }
                if (_Credits > 0 && result)
                {
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        DataRow dRow = dbClient.getRow("SELECT title, length FROM soundmachine_songs WHERE id = '" + songID + "'");
                        string Status = Encoding.encodeVL64(songID) + _Username + "\n" + DateTime.Today.Day + "\n" + DateTime.Today.Month + "\n" + DateTime.Today.Year + "\n" + Convert.ToString(dRow["length"]) + "\n" + Convert.ToString(dRow["title"]);

                        dbClient.AddParamWithValue("tid", Config.Soundmachine_burnToDisk_diskTemplateID);
                        dbClient.AddParamWithValue("ownerid", userID);
                        dbClient.AddParamWithValue("var", Status);
                        int itemID = (int)dbClient.insertQuery("INSERT INTO furniture(tid,var) VALUES (@tid,@var)");
                        dbClient.runQuery("UPDATE soundmachine_songs SET burnt = '1' WHERE id = '" + songID + "' LIMIT 1");

                        handItem.addItem(new genericItem(itemID, Config.Soundmachine_burnToDisk_diskTemplateID, Status, ""));
                    }
                    _Credits--;
                    sendData("@F" + _Credits);
                    sendData("EB" + soundMachineManager.getMachineSongList(Room.floorItemManager.soundMachineID));
                    refreshHand("last");

                }
                else // Virtual user doesn't has enough credits to burn this song to disk, or this song doesn't exist in his/her soundmachine
                    sendData("AD");

            }
        }

        /// <summary>
        /// Sound machine - delete song
        /// </summary>
        public void deleteSongFromMachine()
        {
            validatePacket(currentPacket);
            if (_isOwner && Room != null && Room.floorItemManager.soundMachineID > 0)
            {
                int songID = Encoding.decodeVL64(currentPacket.Substring(2));
                bool result = false;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    result = dbClient.findsResult("SELECT id FROM soundmachine_songs WHERE id = '" + songID + "' AND machineid = '" + Room.floorItemManager.soundMachineID + "'");
                }
                if (result)
                {
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        dbClient.runQuery("UPDATE soundmachine_songs SET machineid = '0' WHERE id = '" + songID + "' AND burnt = '1'"); // If the song is burnt atleast once, then the song is removed from this machine
                        dbClient.runQuery("DELETE FROM soundmachine_songs WHERE id = '" + songID + "' AND burnt = '0' LIMIT 1"); // If the song isn't burnt; delete song from database
                        dbClient.runQuery("DELETE FROM soundmachine_playlists WHERE machineid = '" + Room.floorItemManager.soundMachineID + "' AND songid = '" + songID + "'"); // Remove song from playlist
                    }
                    Room.sendData("EC" + soundMachineManager.getMachinePlaylist(Room.floorItemManager.soundMachineID));
                }

            }
        }

        #region Song editor
        /// <summary>
        /// Soundmachine - song editor - initialize soundsets and samples
        /// </summary>
        public void initializeSoundSetPreview()
        {
            if (songEditor != null)
                unloadSongMachine();
            if (_isOwner && Room != null && Room.floorItemManager.soundMachineID > 0)
            {
                songEditor = new virtualSongEditor(Room.floorItemManager.soundMachineID);
                songEditor.loadSoundsets();
                sendData("Dm" + songEditor.getSoundsets());
                sendData("Dn" + songEditor.getHandSoundsets(handItem.getAllItems()));
            }
        }



        /// <summary>
        /// Soundmachine - song editor - add soundset
        /// </summary>
        public void addSoundsetToSoundmachine()
        {
            validatePacket(currentPacket);
            if (songEditor != null && _isOwner && Room != null && Room.floorItemManager.soundMachineID > 0)
            {
                int soundSetID = Encoding.decodeVL64(currentPacket.Substring(2));
                byte slotID = (byte)Encoding.decodeVL64(currentPacket.Substring(Encoding.encodeVL64(soundSetID).Length + 2));
                if (slotID > 0 && slotID < 5 && songEditor.slotFree(slotID))
                {
                    int itemid = songEditor.addSoundset(soundSetID, slotID);
                    if (itemid > 0)
                    {
                        handItem.removeItem(itemid);
                        songEditor.saveDisks();
                        refreshHand("update");
                    }
                    sendData("Dn" + songEditor.getHandSoundsets(handItem.getAllItems()));
                    sendData("Dm" + songEditor.getSoundsets());
                }
            }
        }

        /// <summary>
        /// Soundmachine - song editor - remove soundset
        /// </summary>
        public void removeSoundSetFromMachine()
        {
            validatePacket(currentPacket);
            if (songEditor != null && _isOwner && Room != null && Room.floorItemManager.soundMachineID > 0)
            {
                int slotID = Encoding.decodeVL64(currentPacket.Substring(2));
                if (songEditor.slotFree(slotID) == false)
                {
                    genericItem toAdd = songEditor.removeSoundset(slotID);
                    if (toAdd != null)
                    {
                        handItem.addItem(toAdd);
                        songEditor.saveDisks();
                        refreshHand("update");
                    }
                    sendData("Dm" + songEditor.getSoundsets());
                    sendData("Dn" + songEditor.getHandSoundsets(handItem.getAllItems()));
                }
            }
        }

        /// <summary>
        /// Soundmachine - song editor - save new song
        /// </summary>
        public void saveSoundmachineSong()
        {
            validatePacket(currentPacket);
            if (songEditor != null && _isOwner && Room != null && Room.floorItemManager.soundMachineID > 0)
            {
                int nameLength = Encoding.decodeB64(currentPacket.Substring(2, 2));
                string Title = currentPacket.Substring(4, nameLength);
                string Data = currentPacket.Substring(nameLength + 6);
                int Length = soundMachineManager.calculateSongLength(Data);

                if (Length != -1)
                {
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        dbClient.AddParamWithValue("userid", userID);
                        dbClient.AddParamWithValue("machineid", Room.floorItemManager.soundMachineID);
                        dbClient.AddParamWithValue("title", stringManager.filterSwearWords(Title));
                        dbClient.AddParamWithValue("length", Length);
                        dbClient.AddParamWithValue("data", Data);
                        dbClient.runQuery("INSERT INTO soundmachine_songs (userid,machineid,title,length,data) VALUES (@userid,@machineid,@title,@length,@data)");
                    }
                    sendData("EB" + soundMachineManager.getMachineSongList(Room.floorItemManager.soundMachineID));
                    sendData("EK" + Encoding.encodeVL64(Room.floorItemManager.soundMachineID) + Title + Convert.ToChar(2));
                }
            }
        }

        /// <summary>
        /// Soundmachine - song editor - request edit of existing song
        /// </summary>
        public void requestExistingSongSoundMachine()
        {
            validatePacket(currentPacket);
            if (_isOwner && Room != null && Room.floorItemManager.soundMachineID > 0)
            {
                int songID = Encoding.decodeVL64(currentPacket.Substring(2));
                sendData("Dl" + soundMachineManager.getSong(songID));

                songEditor = new virtualSongEditor(Room.floorItemManager.soundMachineID);
                songEditor.loadSoundsets();

                sendData("Dm" + songEditor.getSoundsets());
                sendData("Dn" + songEditor.getHandSoundsets(handItem.getAllItems()));
            }
        }

        /// <summary>
        /// Soundmachine - song editor - save edited existing song
        /// </summary>
        public void saveEdittedSongSoundMachine()
        {
            validatePacket(currentPacket);
            if (songEditor != null && _isOwner && Room != null && Room.floorItemManager.soundMachineID > 0)
            {
                int songID = Encoding.decodeVL64(currentPacket.Substring(2));

                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    if (dbClient.findsResult("SELECT id FROM soundmachine_songs WHERE id = '" + songID + "' AND userid = '" + userID + "' AND machineid = '" + Room.floorItemManager.soundMachineID + "'"))
                    {
                        int idLength = Encoding.encodeVL64(songID).Length;
                        int nameLength = Encoding.decodeB64(currentPacket.Substring(idLength + 2, 2));
                        string Title = currentPacket.Substring(idLength + 4, nameLength);
                        string Data = currentPacket.Substring(idLength + nameLength + 6);
                        int Length = soundMachineManager.calculateSongLength(Data);
                        if (Length != -1)
                        {

                            dbClient.AddParamWithValue("id", songID);
                            dbClient.AddParamWithValue("title", stringManager.filterSwearWords(Title));
                            dbClient.AddParamWithValue("length", Length);
                            dbClient.AddParamWithValue("data", Data);

                            dbClient.runQuery("UPDATE soundmachine_songs SET title = @title,data = @data,length = @length WHERE id = @id LIMIT 1");
                            sendData("ES");
                            sendData("EB" + soundMachineManager.getMachineSongList(Room.floorItemManager.soundMachineID));
                            Room.sendData("EC" + soundMachineManager.getMachinePlaylist(Room.floorItemManager.soundMachineID));
                        }
                    }
                }
            }
        }
        #endregion Song editor

        #endregion

        #region Trading
        /// <summary>
        /// Trading - start
        /// </summary>
        public void startTrading()
        {
            validatePacket(currentPacket);
            if (Room != null && roomUser != null && _tradePartner == null)
            {
                if (Config.enableTrading == false) { sendNotify(stringManager.getString("trading_disabled")); return; }

                int partnerUID = int.Parse(currentPacket.Substring(2));
                if (Room.containsUser(partnerUID))
                {
                    virtualUser Partner = Room.getUser(partnerUID);
                    if (Partner.statusManager.containsStatus("trd"))
                        return;

                    this._tradePartner = Partner;
                    this.statusManager.addStatus("trd", null, 0, null, 0, 0);

                    Partner._tradePartner = this;
                    Partner.statusManager.addStatus("trd", null, 0, null, 0, 0);

                    this.refreshTradeBoxes();
                    Partner.refreshTradeBoxes();
                }
            }
        }

        /// <summary>
        /// Trading - offer item
        /// </summary>
        public void offerTradeItem()
        {
            validatePacket(currentPacket);
            if (Room != null && roomUser != null && _tradePartner != null && Room.containsUser(_tradePartner.roomUser.roomUID))
            {
                int itemID = int.Parse(currentPacket.Substring(2));
                //Database dbClient = new Database(true, true, 129);
                genericItem item = handItem.getItem(itemID);
                if (item == null)
                { return; }
                if (!item.template.isTradeable)
                {
                    sendNotify("Dit item is niet ruilbaar!");
                    return;
                }
                if (_tradeItems.Contains(item))
                    return;


                _tradeItems.Add(item);

                this._tradeAccept = false;
                _tradePartner._tradeAccept = false;

                this.refreshTradeBoxes();
                _tradePartner.refreshTradeBoxes();
            }
        }

        /// <summary>
        /// Trading - decline trade
        /// </summary>
        public void declineTrade()
        {
            if (Room != null && roomUser != null && _tradePartner != null && Room.containsUser(_tradePartner.roomUser.roomUID))
            {
                this._tradeAccept = false;
                _tradePartner._tradeAccept = false;
                this.refreshTradeBoxes();
                _tradePartner.refreshTradeBoxes();
            }
        }
        /// <summary>
        /// Trading - accept trade (and, if both partners accept, swap items]
        /// </summary>
        public void acceptTrade()
        {
            if (Room != null && roomUser != null && _tradePartner != null && Room.containsUser(_tradePartner.roomUser.roomUID))
            {
                this._tradeAccept = true;
                this.refreshTradeBoxes();
                _tradePartner.refreshTradeBoxes();

                if (_tradePartner._tradeAccept)
                {
                    foreach (genericItem item in _tradeItems)
                    {
                        this.handItem.removeItem(item.ID);
                        _tradePartner.handItem.addItem(item);
                    }
                    foreach (genericItem item in _tradePartner._tradeItems)
                    {
                        _tradePartner.handItem.removeItem(item.ID);
                        this.handItem.addItem(item);
                    }
                    abortTrade();
                }
            }
        }
        /// <summary>
        /// Trading - abort trade
        /// </summary>
        public void stopTrading()
        {
            if (Room != null && roomUser != null && _tradePartner != null && Room.containsUser(_tradePartner.roomUser.roomUID))
            {
                abortTrade();
                refreshHand("update");
            }
        }
        #endregion

        #region Games
        /// <summary>
        /// Gamelobby - refresh gamelist
        /// </summary>
        public void refreshGameList()
        {
            if (Room != null && Room.Lobby != null)
                sendData("Ch" + Room.Lobby.gameList());
        }

        /// <summary>
        /// Gamelobby - checkout single game sub
        /// </summary>
        public void getSingeleGameSub()
        {
            validatePacket(currentPacket);
            if (Room != null && roomUser != null && Room.Lobby != null && gamePlayer == null)
            {
                int gameID = Encoding.decodeVL64(currentPacket.Substring(2));
                if (Room.Lobby.Games.ContainsKey(gameID))
                {
                    this.gamePlayer = new gamePlayer(this, roomUser.roomUID, (Game)Room.Lobby.Games[gameID]);
                    gamePlayer.Game.Subviewers.Add(gamePlayer);
                    sendData("Ci" + gamePlayer.Game.Sub);
                }
            }
        }

        /// <summary>
        /// Gamelobby - request new game create
        /// </summary>
        public void createNewGame()
        {
            validatePacket(currentPacket);
            if (Room != null && roomUser != null && Room.Lobby != null && gamePlayer == null)
            {
                if (_Tickets > 1) // Atleast two tickets in inventory
                {
                    if (Room.Lobby.validGamerank(roomUser.gamePoints))
                    {
                        if (Room.Lobby.isBattleBall)
                            sendData("Ck" + Room.Lobby.getCreateGameSettings());
                        else
                            sendData("Ck" + "RA" + "secondsUntilRestart" + Convert.ToChar(2) + "HIRGIHHfieldType" + Convert.ToChar(2) + "HKIIIISAnumTeams" + Convert.ToChar(2) + "HJJIII" + "PA" + "gameLengthChoice" + Convert.ToChar(2) + "HJIIIIK" + "name" + Convert.ToChar(2) + "IJ" + Convert.ToChar(2) + "H" + "secondsUntilStart" + Convert.ToChar(2) + "HIRBIHH");
                    }
                    else
                        sendData("Cl" + "K"); // Error [3] = Skillevel not valid in this lobby
                }
                else
                    sendData("Cl" + "J"); // Error [2] = Not enough tickets
            }
        }

        /// <summary>
        /// Gamelobby - process new created game
        /// </summary>
        public void procesNewGame()
        {
            validatePacket(this.currentPacket);
            string currentPacket = this.currentPacket;
            if (Room != null && roomUser != null && Room.Lobby != null && gamePlayer == null)
            {
                if (_Tickets > 1) // Atleast two tickets in inventory
                {
                    if (Room.Lobby.validGamerank(roomUser.gamePoints))
                    {
                        try
                        {
                            int mapID = -1;
                            int teamAmount = -1;
                            int[] Powerups = null;
                            string Name = "";

                            #region Game settings decoding
                            int keyAmount = Encoding.decodeVL64(currentPacket.Substring(2));
                            currentPacket = currentPacket.Substring(Encoding.encodeVL64(keyAmount).Length + 2);
                            for (int i = 0; i < keyAmount; i++)
                            {
                                int j = Encoding.decodeB64(currentPacket.Substring(0, 2));
                                string Key = currentPacket.Substring(2, j);
                                if (currentPacket.Substring(j + 2, 1) == "H") // VL64 value
                                {
                                    int Value = Encoding.decodeVL64(currentPacket.Substring(j + 3));
                                    switch (Key)
                                    {
                                        case "fieldType":
                                            //if (Value != 5)
                                            //{
                                            //    sendNotify("Soz but only the maps for Oldskool are added to db yet kthx.");
                                            //    return;
                                            //}
                                            mapID = Value;
                                            break;

                                        case "numTeams":
                                            teamAmount = Value;
                                            break;
                                    }
                                    int k = Encoding.encodeVL64(Value).Length;
                                    currentPacket = currentPacket.Substring(j + k + 3);
                                }
                                else // B64 value
                                {

                                    int valLen = Encoding.decodeB64(currentPacket.Substring(j + 3, 2));
                                    string Value = currentPacket.Substring(j + 5, valLen);

                                    switch (Key)
                                    {
                                        case "allowedPowerups":
                                            string[] ps = Value.Split(',');
                                            Powerups = new int[ps.Length];
                                            for (int p = 0; p < ps.Length; p++)
                                            {
                                                int P = int.Parse(ps[p]);
                                                if (Room.Lobby.allowsPowerup(P))
                                                    Powerups[p] = P;
                                                else // Powerup not allowed in this lobby
                                                    return;
                                            }
                                            break;

                                        case "name":
                                            Name = stringManager.filterSwearWords(Value);
                                            break;
                                    }
                                    currentPacket = currentPacket.Substring(j + valLen + 5);
                                }
                            }
                            #endregion

                            if (mapID == -1 || teamAmount == -1 || Name == "") // Incorrect keys supplied by client
                                return;
                            this.gamePlayer = new gamePlayer(this, roomUser.roomUID, null);
                            Room.Lobby.createGame(this.gamePlayer, Name, mapID, teamAmount, Powerups);
                        }
                        catch { }
                    }
                    else
                        sendData("Cl" + "K"); // Error [3] = Skillevel not valid in this lobby
                }
                else
                    sendData("Cl" + "J"); // Error [2] = Not enough tickets
            }
        }

        /// <summary>
        /// Gamelobby - switch team in game
        /// </summary>
        public void switchTeamsInGame()
        {
            validatePacket(currentPacket);
            if (Room != null && Room.Lobby != null && gamePlayer != null && gamePlayer.Game.State == Game.gameState.Waiting)
            {
                if (_Tickets > 1) // Atleast two tickets in inventory
                {
                    if (Room.Lobby.validGamerank(roomUser.gamePoints))
                    {
                        int j = Encoding.decodeVL64(currentPacket.Substring(2));
                        int teamID = Encoding.decodeVL64(currentPacket.Substring(Encoding.encodeVL64(j).Length + 2));

                        if (teamID != gamePlayer.teamID && gamePlayer.Game.teamHasSpace(teamID))
                        {
                            if (gamePlayer.teamID == -1) // User was a subviewer
                                gamePlayer.Game.Subviewers.Remove(gamePlayer);
                            gamePlayer.Game.movePlayer(gamePlayer, gamePlayer.teamID, teamID);
                        }
                        else
                            sendData("Cl" + "H"); // Error [0] = Team full
                    }
                    else
                        sendData("Cl" + "K"); // Error [3] = Skillevel not valid in this lobby
                }
                else
                    sendData("Cl" + "J"); // Error [2] = Not enough tickets
            }
        }

        /// <summary>
        /// Gamelobby - leave single game sub
        /// </summary>
        public void leaveCurrentGame()
        {
            leaveGame();
        }

        /// <summary>
        /// Gamelobby - kick player from game
        /// </summary>
        public void kickPlayerFromGame()
        {
            validatePacket(currentPacket);
            if (Room != null && Room.Lobby != null && gamePlayer != null && gamePlayer.Game != null && gamePlayer == gamePlayer.Game.Owner)
            {
                int roomUID = Encoding.decodeVL64(currentPacket.Substring(2));
                for (int i = 0; i < gamePlayer.Game.Teams.Length; i++)
                {
                    foreach (gamePlayer Member in gamePlayer.Game.Teams[i])
                    {
                        if (Member.roomUID == roomUID)
                        {
                            Member.sendData("Cl" + "RA"); // Error [6] = kicked from game
                            gamePlayer.Game.movePlayer(Member, i, -1);
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gamelobby - start game
        /// </summary>
        public void startGameFromLoby()
        {
            if (Room != null && Room.Lobby != null && gamePlayer != null && gamePlayer == gamePlayer.Game.Owner)
            {
                gamePlayer.Game.startGame();
                sendData("Cl" + "I");
            }
        }

        /// <summary>
        /// Game - ingame - move unit
        /// </summary>
        public void movePlayerInGame()
        {
            validatePacket(currentPacket);
            if (gamePlayer != null && gamePlayer.Game.State == Game.gameState.Started && gamePlayer.teamID != -1)
            {
                gamePlayer.goalX = Encoding.decodeVL64(currentPacket.Substring(3));
                gamePlayer.goalY = Encoding.decodeVL64(currentPacket.Substring(Encoding.encodeVL64(gamePlayer.goalX).Length + 3));

            }
        }

        /// <summary>
        /// Game - ingame - proceed with restart of game
        /// </summary>
        public void proceedStartGame()
        {
            if (gamePlayer != null && gamePlayer.Game.State == Game.gameState.Ended && gamePlayer.teamID != -1)
            {
                gamePlayer.Game.sendData("BK" + "" + _Username + " wil nog een keer spelen!");
            }
        }
        #endregion

        #region Games Joystick

        /// <summary>
        /// The joytick command
        /// </summary>
        public void playMiniGame()
        {
            //Not coded yet (NOT A BUG)!
        }

        #endregion

        #region Moodlight
        /// <summary>
        /// Turn moodlight on/off
        /// </summary>
        public void toggleMoodLight()
        {
            validatePacket(currentPacket);
            if (_isOwner == false && _hasRights == false && Room != null && roomUser != null)
                return;
            roomManager.moodlight.setSettings(Room, false, 0, 0, null, 0);
        }

        /// <summary>
        /// Load moodlight settings
        /// </summary>
        public void loadMoodlightSettings()
        {
            validatePacket(currentPacket);
            if (_isOwner == false && _hasRights == false && Room != null && roomUser != null)
                return;
            string settingData = roomManager.moodlight.getSettings(_roomID);
            if (settingData != null)
                sendData("Em" + settingData);
        }

        /// <summary>
        /// Apply modified moodlight settings
        /// </summary>
        public void applyMoodlightSettings()
        {
            validatePacket(currentPacket);
            if (_isOwner == false && _hasRights == false && Room != null && roomUser != null)
                return;
            int presetID = Encoding.decodeVL64(currentPacket.Substring(2, 1));
            int bgState = Encoding.decodeVL64(currentPacket.Substring(3, 1));
            string presetColour = currentPacket.Substring(6, Encoding.decodeB64(currentPacket.Substring(4, 2)));
            int presetDarkF = Encoding.decodeVL64(currentPacket.Substring(presetColour.Length + 6));
            roomManager.moodlight.setSettings(Room, true, presetID, bgState, presetColour, presetDarkF);
        }
        #endregion

        #region lido

        /// <summary>
        /// A user starts diving
        /// </summary>
        public void dive()
        {
            validatePacket(currentPacket);
            if (Room == null)
                return;
            if (roomUser.Diving == false)
                return;
            string DivePacket = currentPacket.Substring(2);
            Room.sendData("AJ" + roomUser.roomUID + Convert.ToChar(13) + DivePacket);

        }
        /// <summary>
        /// Dives!
        /// </summary>
        public void splashDive()
        {
            validatePacket(currentPacket);
            if (Room == null)
                return;
            if (roomUser.Diving == false)
                return;
            Room.DiveDoorOpen = true;
            roomUser.walkLock = false;
            string[] Cords = currentPacket.Substring(2).Split(",".ToCharArray(), 2, StringSplitOptions.None);
            Room.sendData("AGBIGSPLASH position " + Cords[0] + "," + Cords[1]);
            Room.stopDiving();
            Room.sendSpecialCast("cam1", "targetcamera " + roomUser.roomUID);
            Room.sqUNIT[roomUser.X, roomUser.Y] = false;
            roomUser.X = int.Parse(Cords[0]);
            roomUser.Y = int.Parse(Cords[1]);
            roomUser.Diving = false;
            roomUser.InQueue = false;
            roomUser.H = Room.sqFLOORHEIGHT[int.Parse(Cords[0]), int.Parse(Cords[1])];
            if (roomUser.H <= 7)
                statusManager.addStatus("swim", null, 0, null, 0, 0);
            roomUser.goalX = 20;
            roomUser.goalY = 19;
        }
        #endregion

        #region pets

        /// <summary>
        /// Pets - 128 - "B@" get pet details
        /// </summary>
        public void getPetInformation()
        {
            if (Room != null && roomUser != null && !_inPublicroom)
            {
                int petID = int.Parse(this.currentPacket.Split(Convert.ToChar(4))[0].Substring(4));

                roomPet pPet = this.Room.getRoomPet(petID);
                if (pPet != null)
                {
                    StringBuilder Response = new StringBuilder("CR");

                    Response.Append(Encoding.encodeVL64(pPet.roomUID));
                    Response.Append(Encoding.encodeVL64(pPet.Information.Age));
                    Response.Append(Encoding.encodeVL64((int)pPet.Information.Hunger));
                    Response.Append(Encoding.encodeVL64((int)pPet.Information.Thirst));
                    Response.Append(Encoding.encodeVL64((int)pPet.Information.Happiness));
                    Response.Append(Encoding.encodeVL64((int)pPet.Information.Energy));
                    Response.Append(Encoding.encodeVL64((int)pPet.Information.Friendship));

                    sendData(Response.ToString());
                }
            }
        }
        #endregion

        #endregion

        #endregion

        #endregion

        #region Update voids
        /// <summary>
        /// Refreshes
        /// </summary>
        /// <param name="Reload">Specifies if the details have to be reloaded from database, or to use current _</param>
        /// <param name="refreshSettings">Specifies if the @E packet (which contains username etc) has to be resent.</param>
        ///<param name="refreshRoom">Specifies if the user has to be refreshed in room by using the 'poof' animation.</param>
        internal void refreshAppearance(bool Reload, bool refreshSettings, bool refreshRoom)
        {
            if (isGeest)
                return;
            if (Reload)
            {
                DataRow dRow;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dRow = dbClient.getRow("SELECT figure,sex,mission FROM users WHERE id = '" + userID + "'");
                }
                _Figure = Convert.ToString(dRow["figure"]);
                _Sex = Convert.ToChar(dRow["sex"]);
                _Mission = Convert.ToString(dRow["mission"]);
            }

            if (refreshSettings)
                if (roomUser == null)
                    sendData("@E" + this._ConnectionManager.ConnectionID + Convert.ToChar(2) + _Username + Convert.ToChar(2) + _Figure + Convert.ToChar(2) + _Sex + Convert.ToChar(2) + _Mission + Convert.ToChar(2) + "H" + Convert.ToChar(2) + "PCch=s02/53,51,44" + Convert.ToChar(2) + "HI");
                else
                    sendData("@E" + this._ConnectionManager.ConnectionID + Convert.ToChar(2) + _Username + Convert.ToChar(2) + _Figure + Convert.ToChar(2) + _Sex + Convert.ToChar(2) + _Mission + Convert.ToChar(2) + Convert.ToChar(2) + "PC" + roomUser.swimOutfit + Convert.ToChar(2) + "HI");
            if (refreshRoom && Room != null && roomUser != null)
                Room.sendData("DJ" + Encoding.encodeVL64(roomUser.roomUID) + _Figure + Convert.ToChar(2) + _Sex + Convert.ToChar(2) + _Mission + Convert.ToChar(2));
        }
        /// <summary>
        /// Reloads the valueables (tickets and credits) from database and updates them for client.
        /// </summary>
        /// <param name="Credits">Specifies if to reload and update the Credit count.</param>
        /// <param name="Tickets">Specifies if to reload and update the Ticket count.</param>
        internal void refreshValueables(bool Credits, bool Tickets, bool belcredits, bool refreshFromDatabase)
        {
            StringBuilder sb = new StringBuilder();
            if (Credits)
            {
                if (refreshFromDatabase)
                {
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        this.credits = dbClient.getInt("SELECT credits FROM users WHERE id = '" + userID + "' LIMIT 1");
                    }
                }
                sendData("@F" + this._Credits);
            }
            if (Tickets)
            {
                if (refreshFromDatabase)
                {
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        this._Tickets = dbClient.getInt("SELECT tickets FROM users WHERE id = '" + userID + "' LIMIT 1");
                    }
                    sendData("A|" + this._Tickets);
                }
            }
            //if (belcredits)
            //{
            //if (refreshFromDatabase)
            //{
            //using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            //{
            //    this._BelCredits = dbClient.getInt("SELECT belcredits FROM users WHERE id = " + this.userID);
            //}
            //}
            //sb.Append("@H" + this._BelCredits + "\x01");
            //}
            //sendData(sb.ToString().Substring(0, sb.ToString().Length - 1));
        }
        /// <summary>
        /// Refreshes the users Club subscription status.
        /// </summary>
        internal void refreshClub()
        {
            int restingDays = 0;
            int passedMonths = 0;
            int restingMonths = 0;
            DataRow dRow;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dRow = dbClient.getRow("SELECT months_expired,months_left,date_monthstarted FROM users_club WHERE userid = '" + userID + "' LIMIT 1");
            }
            if (dRow.Table.Columns.Count > 0)
            {
                passedMonths = Convert.ToInt32(dRow["months_expired"]);
                restingMonths = Convert.ToInt32(dRow["months_left"]) - 1;
                restingDays = (int)(DateTime.Parse(Convert.ToString(dRow["date_monthstarted"]), new System.Globalization.CultureInfo("en-GB"))).Subtract(DateTime.Now).TotalDays + 32;
                _clubMember = true;
            }
            sendData("@Gclub_habbo" + Convert.ToChar(2) + Encoding.encodeVL64(restingDays) + Encoding.encodeVL64(passedMonths) + Encoding.encodeVL64(restingMonths) + "I");
        }
        /// <summary>
        /// Refreshes the user's badges.
        /// </summary>

        internal void refreshBadges()
        {
            _Badges.Clear(); // Clear old badges
            _badgeSlotIDs.Clear(); // Clear old badge IDs
            DataTable dTable;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dTable = dbClient.getTable("SELECT badgeid, slotid FROM users_badges WHERE userid = '" + userID + "' ");
            }
            string[] myBadges = new string[dTable.Rows.Count];
            string[] myBadgeSlotIDs = new string[dTable.Rows.Count];
            int z = 0;
            foreach (DataRow dRow in dTable.Rows)
            {
                myBadges[z] = dRow[0].ToString();
                myBadgeSlotIDs[z] = dRow[1].ToString();
                z++;
            }
            StringBuilder sbMessage = new StringBuilder();
            sbMessage.Append(Encoding.encodeVL64(myBadges.Length)); // Total amount of badges
            for (int i = 0; i < myBadges.Length; i++)
            {
                sbMessage.Append(myBadges[i]);
                sbMessage.Append(Convert.ToChar(2));

                _Badges.Add(myBadges[i]);
            }

            for (int i = 0; i < myBadges.Length; i++)
            {
                if (Convert.ToInt32(myBadgeSlotIDs[i]) > 0) // Badge enabled!
                {
                    sbMessage.Append(Encoding.encodeVL64((Convert.ToInt32(myBadgeSlotIDs[i]))));
                    sbMessage.Append(myBadges[i]);
                    sbMessage.Append(Convert.ToChar(2));

                    _badgeSlotIDs.Add(Convert.ToInt32(myBadgeSlotIDs[i]));
                }
                else
                    _badgeSlotIDs.Add(0); // :(
            }

            sendData("Ce" + sbMessage.ToString());
            //sendData("Ft" + "SHJIACH_Graduate1" + Convert.ToChar(2) + "PAIACH_Login1" + Convert.ToChar(2) + "PAJACH_Login2" + Convert.ToChar(2) + "PAKACH_Login3" + Convert.ToChar(2) + "PAPAACH_Login4" + Convert.ToChar(2) + "PAQAACH_Login5" + Convert.ToChar(2) + "PBIACH_RoomEntry1" + Convert.ToChar(2) + "PBJACH_RoomEntry2" + Convert.ToChar(2) + "PBKACH_RoomEntry3" + Convert.ToChar(2) + "SBRAACH_RegistrationDuration6" + Convert.ToChar(2) + "SBSAACH_RegistrationDuration7" + Convert.ToChar(2) + "SBPBACH_RegistrationDuration8" + Convert.ToChar(2) + "SBQBACH_RegistrationDuration9" + Convert.ToChar(2) + "SBRBACH_RegistrationDuration10" + Convert.ToChar(2) + "RAIACH_AvatarLooks1" + Convert.ToChar(2) + "IJGLB" + Convert.ToChar(2) + "IKGLC" + Convert.ToChar(2) + "IPAGLD" + Convert.ToChar(2) + "IQAGLE" + Convert.ToChar(2) + "IRAGLF" + Convert.ToChar(2) + "ISAGLG" + Convert.ToChar(2) + "IPBGLH" + Convert.ToChar(2) + "IQBGLI" + Convert.ToChar(2) + "IRBGLJ" + Convert.ToChar(2) + "SAIACH_Student1" + Convert.ToChar(2) + "PCIHC1" + Convert.ToChar(2) + "PCJHC2" + Convert.ToChar(2) + "PCKHC3" + Convert.ToChar(2) + "PCPAHC4" + Convert.ToChar(2) + "PCQAHC5" + Convert.ToChar(2) + "QAIACH_GamePlayed1" + Convert.ToChar(2) + "QAJACH_GamePlayed2" + Convert.ToChar(2) + "QAKACH_GamePlayed3" + Convert.ToChar(2) + "QAPAACH_GamePlayed4" + Convert.ToChar(2) + "QAQAACH_GamePlayed5" + Convert.ToChar(2));
            sendData("Dt" + "IH" + Convert.ToChar(1) + "FCH");
        }
        /// <summary>
        /// Refreshes the user's group status.
        /// </summary>
        internal void refreshGroupStatus()
        {
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                _groupID = dbClient.getInt("SELECT groupid FROM groups_memberships WHERE userid = '" + userID + "' AND is_current = '1'");
            }
            if (_groupID > 0) // User is member of a group
            {
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    _groupMemberRank = dbClient.getInt("SELECT member_rank FROM groups_memberships WHERE userid = '" + userID + "' AND groupID = '" + _groupID + "'");
                }
            }

        }
        /// <summary>
        /// Refreshes the Hand, which contains virtual items, with a specified mode.
        /// </summary>
        /// <param name="Mode">The refresh mode, available: 'next', 'prev', 'update', 'last' and 'new'.</param>
        internal void refreshHand(string Mode)
        {
            StringBuilder Hand = new StringBuilder("BL");
            int startID = 0;
            int stopID = handItem.handLength();

            switch (Mode)
            {
                case "next":
                    _handPage++;
                    break;
                case "prev":
                    _handPage--;
                    break;
                case "last":
                    _handPage = (stopID - 1) / 9;
                    break;
                case "update": // Nothing, keep handpage the same
                    break;
                default: // Probably, "new"
                    _handPage = 0;
                    break;
            }

            try
            {
                if (stopID > 0)
                {
                reCount:
                    startID = _handPage * 9;
                    if (stopID > (startID + 9)) { stopID = startID + 9; }
                    if (startID > stopID || startID == stopID) { _handPage--; goto reCount; }
                    string Colour = "";
                    for (int f = startID; f < stopID; f++)
                    {
                        genericItem currentItem = handItem.getHandItem(f);
                        itemTemplate Template = currentItem.template;
                        char Recycleable = '1';
                        if (Template.isRecycleable == false)
                            Recycleable = '0';

                        if (Template.typeID == 0) // Wallitem
                        {
                            Colour = Template.Colour;
                            if (Template.Sprite == "post.it" || Template.Sprite == "post.it.vd") // Stickies - pad size
                                Colour = currentItem.Var;
                            Hand.Append("SI" + "" + currentItem.ID + "" + f + "" + "I" + "" + currentItem.ID + "" + Template.Sprite + "" + Colour + "" + Recycleable + "/");
                        }
                        else // Flooritem
                            Hand.Append("SI" + "" + currentItem.ID + "" + f +
                                "" + "S" + "" +
                                currentItem.ID + "" + Template.Sprite +
                                "" + Template.Length + "" +
                                Template.Width + "" + "" + "" +
                                Template.Colour + "" + Recycleable + "" +
                                Template.Sprite + "" + "/");

                    }
                }
                Hand.Append("\r" + handItem.handLength());
                sendData(Hand.ToString());
            }
            catch
            {
                sendData("BL" + "\r0");
            }
        }

        /// <summary>
        /// Generates new hand items
        /// </summary>
        internal void generateHandItems()
        {
            DataTable dTable;
            if (handItem != null)
                handItem.destroy();
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dTable = dbClient.getTable("SELECT f.id AS id, f.tid AS tid, f.var AS var, f.wallpos AS wallpos " +
                                            "FROM user_furniture INNER JOIN furniture AS f ON furniture_id = f.id " +
                                            "WHERE user_id = " + userID);
            }
            handItem = new HandItemManager(dTable, userID);
        }

        /// <summary>
        /// Refreshes the trade window for the user.
        /// </summary>
        internal void refreshTradeBoxes()
        {
            if (Room != null && _tradePartner != null && roomUser != null && Room.containsUser(_tradePartner.roomUser.roomUID))
            {
                StringBuilder tradeBoxes = new StringBuilder("Al" + _Username + Convert.ToChar(9) + _tradeAccept.ToString().ToLower() + Convert.ToChar(9));
                if (_tradeItems.Count > 0) { tradeBoxes.Append(catalogueManager.tradeItemList(_tradeItems)); }
                tradeBoxes.Append(Convert.ToChar(13) + _tradePartner._Username + Convert.ToChar(9) + _tradePartner._tradeAccept.ToString().ToLower() + Convert.ToChar(9));
                if (_tradePartner._tradeItems.Count > 0) { tradeBoxes.Append(catalogueManager.tradeItemList(_tradePartner._tradeItems)); }
                sendData(tradeBoxes.ToString());
            }
        }
        /// <summary>
        /// Aborts the trade between this user and his/her partner.
        /// </summary>

        internal void abortTrade()
        {
            if (_tradePartner != null && _tradePartner.abortingTrade == false)
            {
                this.abortingTrade = true;
                if (this._ConnectionManager != null && handItem != null)
                {
                    this.sendData("An");
                    this.refreshHand("update");
                }
                if (_tradePartner._ConnectionManager != null && _tradePartner != null)
                {
                    _tradePartner.sendData("An");
                    _tradePartner.refreshHand("update");
                }


                _tradePartner._tradeAccept = false;
                _tradePartner._tradeItems = new List<genericItem>();
                if (_tradePartner.statusManager != null)
                    _tradePartner.statusManager.removeStatus("trd");

                this._tradeAccept = false;
                this._tradeItems = new List<genericItem>();
                if (this.statusManager != null)
                    this.statusManager.removeStatus("trd");

                _tradePartner._tradePartner = null;
                this._tradePartner = null;
                this.abortingTrade = false;
            }
        }

        /// <summary>
        /// Saves the current hand
        /// </summary>
        internal void saveHand()
        {
            this.handItem.update();
        }

        #endregion

        #region Misc functions
        /// <summary>
        /// Sends a message to the user
        /// </summary>
        /// <param name="message">The message to be send</param>
        internal void sendNotify(string message)
        {
            sendData("BK" + message);
        }
        /// <summary>
        /// Checks if a certain chat message was a 'speech command', if so, then the action for this command is processed and a 'true' boolean is returned. Otherwise, 'false' is returned.
        /// </summary>
        /// <param name="Text">The chat message that was used.</param>
        private bool isSpeechCommand(string Text)
        {
            string[] args = Text.Split(' ');
            try // Try/catch, on error (eg, target user offline, parameters incorrect etc) then failure message will be sent
            {
                switch (args[0].ToLower()) // arg[0] = command itself
                {
                    #region Public commands
                    #region :about
                    case "about": // Display information about the emulator
                        {
                            sendData("BK" +
                                "Holograph Emulator\r" +
                                "The free and open source C# Habbo Hotel server emulator\r" +
                                "Originally written by Nillus\r\r" +
                                "Geedit door sunnieday om het nog soepeler te laten lopen!\r" +
                                "Hey " + _Username + "! Het ziet er naar uit dat je momenteel met sunnie bent verbonden,\r" +
                                "Er zijn momenteel " + Holo.GameConnectionSystem.gameSocketServer.acceptedConnections + " connecties geaccepteerd.\r" +
                                "Er zijn " + userManager.userCount + " sunnie's online, het meest online ooit is " + userManager.peakUserCount + ".\r" +
                                "Er zijn " + roomManager.roomCount + " kamers in gebruik op het moment.\r\r" +
                                "Nog een fijne tijd in sunnie!!\r\r" +
                                "Groeten\r" +
                                "Het Sunnie team!\r\r");

                            break;
                        }
                    #endregion

                    #region :cleanhand
                    case "cleanhand": // Deletes everything from the senders hand
                        {
                            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                            {
                                dbClient.runQuery("DELETE user_furniture, furniture FROM user_furniture, furniture WHERE user_id = '" + userID + "' and furniture_id = id");
                            }
                            handItem.clearItems(true);
                            refreshHand("update");
                            break;
                        }
                    #endregion

                    #region :brb/:back
                    case "brb": // Shows the user has brb in the room
                        {
                            Room.sendShout(this.roomUser, "Ik ben zo terug..");
                            break;
                        }

                    case "back": // Stops the user from being shown as brb
                        {

                            Room.sendShout(this.roomUser, "Ik ben weer terug");
                            break;
                        }
                    #endregion

                    #region :commands
                    case "commands": // Displays a list of commands
                    case "cmds": // Displays a list of commands
                        sendData(userManager.generateCommands(_Rank, _fuserights));
                        break;
                    #endregion

                    #region :whosonline
                    case "whosonline": // Generates a list of users connected
                        sendData(userManager.generateWhosOnline(rankManager.containsRight(_Rank, "fuse_administrator_access", _fuserights)));
                        break;
                    #endregion

                    #region :version
                    case "hotel":
                    case "info":
                    case "version": // Displays the Server Version
                        {
                            sendNotify("You are currently connected to " + Eucalypt.serverVersion);
                            break;
                        }
                    #endregion

                    #region :staff
                    case "staff": // / How to Contact Hotel Staff
                        {
                            sendNotify("If you need to contact the Hotel Staff Please use the Call for help");
                            break;
                        }
                    #endregion

                    #region kamerStill
                    case "kamerstil":
                        {
                            if (_Rank < 4)
                                return false;
                            if (args.Length == 2)
                            {
                                Room.setQuiet(args[1] == "1");
                            }
                            else
                            {
                                sendNotify("Gebruik het command wel goed:\rkamerstil 1 - kamer is stil\rkamerstil 0 - kamer kan weer praten");
                            }

                            break;
                        }
                    case "laatpraten":
                        {
                            if (_Rank < 4)
                                return false;
                            if (args.Length == 2)
                            {
                                laatPraten(args[1]);
                            }
                            else
                            {
                                sendNotify("Gebruik het command wel goed:\rlaatpraten naam");
                            }

                            break;
                        }
                    case "stoppraten":
                        {
                            if (_Rank < 4)
                                return false;
                            if (args.Length == 2)
                            {
                                stopPraten(args[1]);
                            }
                            else
                            {
                                sendNotify("Gebruik het command wel goed:\rstoppraten naam");
                            }
                            break;
                        }

                    #endregion

                    #endregion

                    #region moderation

                    #region kickuithotel
                    case "kickuithotel":
                        {
                            if (_Rank < 4)
                                return false;
                            if (userManager.containsUser(args[1]))
                                userManager.getUser(args[1]).Disconnect();
                            break;
                        }
                    #endregion

                    #region :ban
                    case "ban": // Bans a virtual user from server (no IP ban)
                        {
                            if (rankManager.containsRight(_Rank, "fuse_ban", _fuserights) == false)
                                return false;
                            else
                            {
                                DataRow dRow;
                                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                                {
                                    dbClient.AddParamWithValue("name", args[1]);
                                    dRow = dbClient.getRow("SELECT id,rank FROM users WHERE name = @name");
                                }
                                if (dRow.Table.Rows.Count == 0)
                                    sendNotify(stringManager.getString("modtool_actionfailed") + "\r" + stringManager.getString("modtool_usernotfound"));
                                else if (Convert.ToByte(dRow["rank"]) > _Rank)
                                    sendNotify(stringManager.getString("modtool_actionfailed") + "\r" + stringManager.getString("modtool_rankerror"));
                                else
                                {
                                    int banHours = int.Parse(args[2]);
                                    string Reason = stringManager.wrapParameters(args, 3);
                                    if (banHours == 0 || Reason == "")
                                        this.sendNotify(stringManager.getString("scommand_failed"));
                                    else
                                    {
                                        staffManager.addStaffMessage("ban", userID, Convert.ToInt32(dRow["id"]), Reason, "");
                                        userManager.setBan(Convert.ToInt32(dRow["id"]), banHours, Reason);
                                        sendNotify(userManager.generateBanReport(Convert.ToInt32(dRow["id"])));
                                    }
                                }
                            }
                            break;
                        }
                    #endregion

                    #region :deleteroom
                    case "deleteroom":
                        {
                            if (this.Room != null)
                            {
                                if (_Rank >= 5)
                                {
                                    roomManager.deleteRoom(Room.roomID, Room.getOwner());
                                    return true;
                                }
                                else
                                    return false;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    #endregion

                    #region :deletemission
                    case "deletemission":
                        {
                            if (_Rank >= 4)
                            {
                                if (args.Length == 2)
                                {
                                    if (userManager.containsUser(args[1]))
                                    {
                                        virtualUser user = userManager.getUser(args[1]);
                                        using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                                        {
                                            dbClient.runQuery("UPDATE users SET mission = '" + "Lees de regels over missies!" + "' WHERE id = " + user.userID);
                                        }
                                        user._Mission = "Lees de regels over missies!";
                                        user.refreshAppearance(false, true, true);

                                    }
                                    return true;
                                }
                                else
                                {
                                    sendNotify("Je moet wel de naam van de persoon mee geven!");
                                    return true;
                                }
                            }
                            return false;
                        }

                    #endregion

                    #endregion

                    #region staff only

                    #region :hw
                    case "hw": // Broadcoasts a message to all virtual users (hotel alert)
                        {
                            if (rankManager.containsRight(_Rank, "fuse_administrator_access", _fuserights) || _fuserights.Contains("hotel_alert_acces"))
                            {
                                string Message = Text.Substring(3);
                                userManager.sendData("BK" + stringManager.getString("scommand_hotelalert") + "\r" + Message);
                                staffManager.addStaffMessage("halert", userID, 0, Message, "");
                            }

                            else
                                return false;
                        }
                        break;
                    #endregion

                    #region :loguit
                    case "loguit":
                        {
                            sendNotify("Je wordt nu uitgelogd\rBedankt voor het spelen!");
                            Thread.Sleep(2000);
                            Disconnect();
                            break;
                        }
                    #endregion

                    #region :teleport/:warp
                    case "teleport": // Toggles the user's teleport ability on/off
                        {
                            if (rankManager.containsRight(_Rank, "fuse_administrator_access", _fuserights) == false)
                                return false;
                            else
                            {
                                roomUser.SPECIAL_TELEPORTABLE = (roomUser.SPECIAL_TELEPORTABLE != true); // Reverse the bool
                                refreshAppearance(false, false, true); // Use the poof animation
                            }
                            break;
                        }

                    case "warp": // Warps the virtual user to a certain X,Y coordinate
                        {
                            if (rankManager.containsRight(_Rank, "fuse_administrator_access", _fuserights) == false)
                                return false;
                            else
                            {
                                int X = int.Parse(args[1]);
                                int Y = int.Parse(args[2]);
                                roomUser.X = X;
                                roomUser.Y = Y;
                                roomUser.goalX = -1;
                                refreshAppearance(false, false, true); // Use the poof animation
                            }
                            break;
                        }

                    #endregion

                    #region :userinfo
                    case "ui":
                    case "userinfo": // Generates a list of information about a certain virtual user
                        {
                            if (rankManager.containsRight(_Rank, "fuse_moderator_access", _fuserights) == false)
                                return false;
                            else
                                sendNotify(userManager.generateUserInfo(userManager.getUserID(args[1]), _Rank));
                            break;
                        }
                    #endregion

                    #region :cords
                    case "cords": // Returns the cords of the user
                        {
                            if (rankManager.containsRight(_Rank, "fuse_administrator_access", _fuserights) == false)
                                return false;
                            else
                                sendNotify("X: " + roomUser.X + "\rY: " + roomUser.Y + "\rH: " + roomUser.H);
                            break;
                        }
                    #endregion

                    #region :sendme

                    case "sendme": // Sends the user the packet they enter (Debug Reasons);
                        {
                            if (rankManager.containsRight(_Rank, "fuse_administrator_access", _fuserights) == false)
                                return false;
                            else
                                sendData(stringManager.wrapParameters(args, 1));
                            break;
                        }
                    #endregion

                    #region :Serversave/shutdown stuff
                    case "serversave":
                        {
                            if (rankManager.containsRight(_Rank, "fuse_administrator_access", _fuserights) == false)
                                return false;
                            roomManager.saveAllRooms();
                            break;
                        }
                    case "shutdown":
                        {
                            if (rankManager.containsRight(_Rank, "fuse_administrator_access", _fuserights) == false)
                                return false;
                            Eucalypt.Shutdown();
                            return true;
                        }
                    #endregion

                    #region :refresh
                    case "refresh": // Updates certain parts of the server.
                        {
                            if (rankManager.containsRight(_Rank, "fuse_administrator_access", _fuserights) == false)
                                return false;
                            else
                            {
                                try
                                {
                                    Thread Refresher;
                                    ThreadStart tStarter = null;
                                    switch (args[1])
                                    {
                                        case "catalogue": // Refresh the catalogue
                                            {
                                                tStarter = new ThreadStart(refresh_cat);
                                                break;
                                            }
                                        case "strings": // Refresh the catalogue
                                            {
                                                tStarter = new ThreadStart(refresh_strings);
                                                break;
                                            }
                                        case "config": // Refresh the catalogue
                                            {
                                                tStarter = new ThreadStart(refresh_config);
                                                break;
                                            }
                                        case "filter": // Refresh the catalogue
                                            {
                                                tStarter = new ThreadStart(refresh_filter);
                                                break;
                                            }
                                        case "fuse": // Refresh the catalogue
                                            {
                                                tStarter = new ThreadStart(refresh_fuse);
                                                break;
                                            }
                                        case "eco": // Refresh the catalogue
                                            {
                                                tStarter = new ThreadStart(refresh_eco);
                                                break;
                                            }
                                        default:
                                            sendNotify(stringManager.getString("scommand_failed"));
                                            return true;
                                    }
                                    Refresher = new Thread(tStarter);
                                    Refresher.Priority = ThreadPriority.BelowNormal;
                                    Refresher.Start();
                                    sendNotify(stringManager.getString("scommand_success"));
                                }
                                catch
                                {
                                    sendNotify(":refresh catalogue\r" +
                                        ":refresh eco\r" +
                                        ":refresh strings\r" +
                                        ":refresh config\r" +
                                        ":refresh filter\r" +
                                        ":refresh fuse\r");
                                }
                                break;
                            }
                        }
                    #endregion

                    #region geest
                    case "geest":
                        {
                            if (rankManager.containsRight(_Rank, "fuse_administrator_access", _fuserights) == false)
                                return false;

                            this.makeGenie();
                            break;
                        }
                    #endregion

                    #region massageest
                    case "massageest":
                        {
                            if (rankManager.containsRight(_Rank, "fuse_administrator_access", _fuserights) == false)
                                return false;
                            if (args.Length > 1)
                                userManager.state = (args[1] == "1");
                            else
                                userManager.state = !userManager.state;
                            Thread genies = new Thread(new ThreadStart(userManager.makeMassGeest));
                            genies.Start();
                            break;
                        }
                    #endregion

                    #region kamergeest
                    case "kamergeest":
                        {
                            if (rankManager.containsRight(_Rank, "fuse_administrator_access", _fuserights) == false)
                                return false;
                            if (args.Length > 1)
                            {
                                Room.makeGhosts((args[1] == "1"));
                            }
                            else
                            {
                                sendNotify("Vergeet er niet 1 of 0 achter te zetten!");
                            }
                            break;
                        }
                    #endregion

                    #endregion

                    default:
                        return false;
                }
            }
            catch { sendNotify(stringManager.getString("scommand_failed")); return true; }
            return true;
        }

        /// <summary>
        /// Checks if the user is involved with a 'BattleBall' or 'SnowStorm' game. If so, then the removal procedure is invoked. If the user is the owner of the game, then the game will be aborted.
        /// </summary>
        internal void leaveGame()
        {
            if (gamePlayer != null && gamePlayer.Game != null)
            {
                if (gamePlayer.Game.Owner == gamePlayer) // Owner leaves game
                {
                    try { gamePlayer.Game.Lobby.Games.Remove(gamePlayer.Game.ID); }
                    catch { }
                    gamePlayer.Game.Abort();
                }
                else if (gamePlayer.teamID != -1) // Team member leaves game
                    gamePlayer.Game.movePlayer(gamePlayer, gamePlayer.teamID, -1);
                else
                {
                    gamePlayer.Game.Subviewers.Remove(gamePlayer);
                    sendData("Cm" + "H");
                }
            }
            this.gamePlayer = null;
        }
        internal void stopWSgame()
        {
            if (_WSVariables != null)
            {
                if (_WSVariables.QueuePosition != -1 && Room != null)
                    Room._WSQueueManager.LeaveQueue(_WSVariables.QueuePosition, _WSVariables.Side);
                _WSVariables.WaitingToJoinQueue = false;
                _WSVariables = null;
            }
        }

        #region :refresh voids
        private void refresh_cat()
        {
            catalogueManager.Init();
        }
        private void refresh_strings()
        {
            stringManager.Init("en", true);
        }
        private void refresh_config()
        {
            Config.Init(true);
        }
        private void refresh_filter()
        {
            stringManager.initFilter(true);
        }
        private void refresh_fuse()
        {
            rankManager.Init(true);
        }
        private void refresh_eco()
        {
            recyclerManager.Init(true);
        }
        #endregion

        private delegate void TeleporterUsageSleep(genericItem Teleporter1, int idTeleporter2, int roomIDTeleporter2);
        private void useTeleporter(genericItem Teleporter1, int idTeleporter2, int roomIDTeleporter2)
        {
            try
            {
                roomUser.walkLock = true; //nullpointer
                string Sprite = Teleporter1.Sprite;
                if (roomIDTeleporter2 == _roomID) // Partner teleporter is in same room, don't leave room
                {
                    genericItem Teleporter2 = Room.floorItemManager.getItem(idTeleporter2);
                    Thread.Sleep(500); // <--
                    Room.sendData("AY" + Teleporter1.ID + "/" + _Username + "/" + Sprite);
                    Room.sendData(@"A\" + Teleporter2.ID + "/" + _Username + "/" + Sprite);
                    roomUser.X = Teleporter2.X;
                    roomUser.Y = Teleporter2.Y;
                    roomUser.H = Teleporter2.H;
                    roomUser.Z1 = Teleporter2.Z;
                    roomUser.Z2 = Teleporter2.Z;
                    roomUser.walkLock = false;
                }
                else
                {
                    _teleporterID = idTeleporter2;
                    _teleportRoomID = roomIDTeleporter2;
                    sendData("@~" + Encoding.encodeVL64(idTeleporter2) + Encoding.encodeVL64(roomIDTeleporter2));
                    Room.sendData("AY" + Teleporter1.ID + "/" + _Username + "/" + Sprite);
                }
            }
            catch { }
        }

        /// <summary>
        /// Warns a user about his actions
        /// </summary>
        internal void sendWarning()
        {
            if (!warning)
            {
                sendNotify("Probeer dat niet nog een keer!");
                sendData("@R");
                warning = true;
            }
            else
            {
                sendNotify("Je wordt gekickt uit het hotel!\rNiet meer misbruik maken van bugs!");
                Thread.Sleep(5000);
                Disconnect();
            }
        }

        /// <summary>
        /// Makes a genie for a specific state
        /// </summary>
        /// <param name="state">True for geny, false for normal</param>
        internal void makeGenie(bool state)
        {
            this.isGeest = !state;
            makeGenie();
        }

        /// <summary>
        /// Makes a genie of the this user.
        /// </summary>
        internal void makeGenie()
        {
            if (Room == null || roomUser == null || statusManager == null)
                return;
            if (Room.hasSwimmingPool)
                return; //dan kan het niet!
            if (isGeest == false)
            {
                roomUser.swimOutfit = "ch=s01/250,56,49";
                this._Figure = "hd-180-1.ch-878-79.lg-270-103.sh-906-89.wa-2006-.ha-1015-";
                statusManager.addStatus("dance", "2", 0, null, 0, 0);
                refreshAppearance(false, false, true);
                isGeest = true;
                Room.sendShout(this.roomUser, "I R GENY!");
            }
            else
            {
                roomUser.swimOutfit = null;
                statusManager.removeStatus("dance");
                Room.sendShout(this.roomUser, "Ik ben geen geestje meer");
                isGeest = false;
                refreshAppearance(true, true, true);
            }
            Room.sendData(@"@\" + roomUser.detailsString);
        }

        /// <summary>
        /// Lets a user talk
        /// </summary>
        /// <param name="username">The username of the user</param>
        internal void laatPraten(string username)
        {
            if (sameRoomCheck(username))
                userManager.getUser(username).roomUser.allowedToTalk = true;
            else
                sendNotify("De gebruiker " + username + " bevind zich niet in deze kamer");

        }

        /// <summary>
        /// Lets a user shut up for a moment!
        /// </summary>
        /// <param name="username">The username of the user</param>
        internal void stopPraten(string username)
        {
            if (sameRoomCheck(username))
                userManager.getUser(username).roomUser.allowedToTalk = false;
            else
                sendNotify("De gebruiker " + username + " bevind zich niet in deze kamer");

        }

        /// <summary>
        /// Checks if the user is in the same room
        /// </summary>
        /// <param name="username">The name of the user</param>
        internal bool sameRoomCheck(string username)
        {
            if (userManager.containsUser(username))
            {
                virtualUser user = userManager.getUser(username);
                if (user.roomUser != null)
                {
                    if (user.Room == this.Room)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        private void reportExploiter(string exploitReason)
        {
            using (DatabaseClient client = Eucalypt.dbManager.GetClient())
            {
                client.AddParamWithValue("reason", exploitReason);
                client.runQuery("INSERT INTO exploit_users SET id = " + this.userID + " , reason = @reason");
            }
        }
    }
}