using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading;
using Holo.Managers;
using Holo.Managers.catalogue;
using Holo.Source.Managers;
using Holo.Source.Managers.RoomManager;
using Holo.Source.Virtual.Rooms.Pets;
using Holo.Virtual.Item;
using Holo.Virtual.Rooms.Bots;
using Holo.Virtual.Rooms.Games;
using Holo.Virtual.Rooms.Games.Wobble_Squabble;
using Holo.Virtual.Users;
using Ion.Storage;

namespace Holo.Virtual.Rooms
{

    /// <summary>
    /// Represents a virtual publicroom or guestroom, with management for users, items and the map. Threaded.
    /// </summary>
    public class virtualRoom
    {
        #region Declares
        internal class magicTile
        {
            internal string toMode;
            internal string toStatus;
            internal int toID;
            internal int toX;
            internal int toY;
            internal byte toZ;
            internal int toH;

            /// <summary>
            /// Sets the values of this magictile.
            /// </summary>
            /// <param name="s5">toStatus</param>
            /// <param name="i6">toID</param>
            /// <param name="i7">toX</param>
            /// <param name="i8">toY</param>
            /// <param name="b9">toZ</param>
            /// <param name="i10">toH</param>
            internal magicTile(string s4, string s5, int i6, int i7, int i8, byte b9, int i10)
            {
                toMode = s4;
                toStatus = s5;
                toID = i6;
                toX = i7;
                toY = i8;
                toZ = b9;
                toH = i10;
            }
        }

        /// <summary>
        /// The list of pets which should be removed
        /// </summary>
        internal List<int> petsToUnload = new List<int>();

        internal RoomItemKeeper itemKeeper;
        /// <summary>
        /// Holds the data of this model
        /// </summary>
        internal modelData model;

        internal bool roomQuiet = false;
        /// <summary>
        /// The ID of this room.
        /// </summary>
        internal int roomID;
        /// <summary>
        /// Indicates if this room is a publicroom.
        /// </summary>
        internal QueueManagment _WSQueueManager = new QueueManagment();

        /// <summary>
        /// Contains all ifnormation about the room votes.
        /// </summary>
        internal List<int> _votes;
        internal int _votesTotal;
        /// <summary>
        /// magicTyles
        /// </summary>
        internal Dictionary<Pathfinding.Coord, magicTile> _magicTiles = new Dictionary<Pathfinding.Coord, magicTile>();
        /// <summary>
        /// Indicates if the status cycle should continue
        /// </summary>
        internal bool updateStatusses = true;
        /// <summary>
        /// Is the room
        /// </summary>
        internal bool isPublicroom;
        /// <summary>
        /// Items updated or not
        /// </summary>
        internal bool itemsUpdated = false;
        /// <summary>
        /// Manages the flooritems inside the room.
        /// </summary>
        internal FloorItemManager floorItemManager;
        /// <summary>
        /// Manages the wallitems inside the room.
        /// </summary>
        internal WallItemManager wallItemManager;
        /// <summary>
        /// Optional. The lobby manager incase of a game lobby.
        /// </summary>
        internal gameLobby Lobby;

        internal void sendDataToRights(string Data)
        {
            try
            {
                foreach (virtualRoomUser roomUser in _Users.Values)
                    if (roomUser.User._hasRights || roomUser.User._isOwner || rankManager.containsRight(roomUser.User._Rank, "fuse_pick_up_any_furni", roomUser.User._fuserights))
                        roomUser.User.sendData(Data);
            }
            catch { }
        }
        internal genericItem[] toyItems = new genericItem[5];
        private string _publicroomItems;
        /// <summary>
        /// Indicates if this room has a swimming pool.
        /// </summary>
        internal bool hasSwimmingPool;
        /// <summary>
        /// The state of a certain coord on the room map.
        /// </summary>
        private squareState[,] sqSTATE;
        /// <summary>
        /// The rotation of the item on a certain coord on the room map.
        /// </summary>
        private byte[,] sqITEMROT;
        /// <summary>
        /// The floorheight of a certain coord on the room's heightmap.
        /// </summary>
        public byte[,] sqFLOORHEIGHT;
        /// <summary>
        /// The height of the item on a certain coord on the room map.
        /// </summary>
        public double[,] sqITEMHEIGHT;
        /// <summary>
        /// Indicates if there is a user/bot/pet on a certain coord of the room map.
        /// </summary>
        public bool[,] sqUNIT;
        /// <summary>
        /// The item stack on a certain coord of the room map.
        /// </summary>
        /// 
        internal string emitter = "";
        private furnitureStack[,] sqSTACK;
        /// <summary>
        /// Enum
        /// 0 = open
        /// 1 = blocked
        /// 2 = seat
        /// 3 = bed
        /// 4 = rug
        /// </summary>
        public enum squareState { Open = 0, Blocked = 1, Seat = 2, Bed = 3, Rug = 4 };
        squareTrigger[,] sqTRIGGER;

        /// <summary>
        /// The collection that contains the virtualRoomUser objects for the virtual users in this room.
        /// </summary>
        private Hashtable _Users;
        /// <summary>
        /// The collection that contains the virtualBot objects for the bots in this room.
        /// </summary>
        private Hashtable _Bots;

        /// <summary>
        /// Holds pets
        /// </summary>
        private Hashtable _Pets;
        /// <summary>
        /// The collection that contains the IDs of the virtual user groups that are active in this room.
        /// </summary>
        private Dictionary<int, string> _activeGroups;

        private HashSet<int> _activeRoomIndentifiers;
        /// <summary>
        /// The thread that handles the @b status updating and walking of virtual unit.
        /// </summary>
        private Thread _statusHandler;
        internal bool DiveDoorOpen = true;

        /// <summary>
        /// The string that contains the status updates for the next cycle of the _statusHandler thread.
        /// </summary>
        private StringBuilder _statusUpdates;

        /// <summary>
        /// The X position of the room's door.
        /// </summary>
        internal int doorX;
        /// <summary>
        /// The Y position of the room's door.
        /// </summary>
        internal int doorY;
        /// <summary>
        /// Publicroom only. The rotation that the user should gain when staying in the room's door.
        /// </summary>
        private byte doorZ;
        /// <summary>
        /// The height that the user should gain when staying in the room's door.
        /// </summary>
        private int doorH;

        private Holo.Source.Virtual.Rooms.RoomStructure room;

        private string[] GestureList = { "sml", "agr", "sad", "srp" };
        private string[] Gestures_sml = { ":)", ":D" };
        private string[] Gestures_agr = { ">:(", ":@", ":/" };
        private string[] Gestures_sad = { ":(", ":'(" };
        private string[] Gestures_srp = { ":o", ":O", ":0" };
        private string[][] Gestures;


        /// <summary>
        /// Sends timed 'AG' casts to the room, such as disco lights and camera's.
        /// </summary>
        //private Thread specialCastHandler;
        //private System.Threading.Thread WobbleSquabbleQueueThread;
        #endregion

        #region Constructors/Destructors

        /// <summary>
        /// Initializes a new instance of a virtual room. The room is prepared for usage.
        /// </summary>
        /// <param name="roomID"></param>
        /// <param name="isPublicroom"></param>
        public virtualRoom(int roomID)
        {
            this.roomID = roomID;
            this.itemKeeper = new RoomItemKeeper(roomID);
            string[][] Gestures = { this.Gestures_sml, this.Gestures_agr, this.Gestures_sad, this.Gestures_srp };
            this.Gestures = Gestures;

            _Users = new Hashtable();
            _Bots = new Hashtable();
            _Pets = new Hashtable();
            _activeRoomIndentifiers = new HashSet<int>();
            _activeGroups = new Dictionary<int, string>();
            _statusUpdates = new StringBuilder();
            _statusHandler = new Thread(new ThreadStart(cycleStatuses));
            _votes = new List<int>();


            room = NavigatorHelper.getRoom(roomID);

            model = roomManager.getModelData(getModel());

            this.isPublicroom = model.publicroom;


            doorX = model.doorx;
            doorY = model.doory;
            doorH = model.doorh;
            doorZ = model.doorz;


            sqSTATE = model.getSquareState();
            sqFLOORHEIGHT = model.getSqFLOORHEIGHT();
            sqITEMROT = model.getSqITEMROT();
            sqITEMHEIGHT = model.getSqITEMHEIGHT();
            sqUNIT = model.getSqUNIT();

            sqSTACK = new furnitureStack[sqUNIT.GetUpperBound(0) + 1, sqUNIT.GetUpperBound(1) + 1];

            sqTRIGGER = model.getSqTRIGGER();
            hasSwimmingPool = model.hasSwimmingPool;
            DataRow dRow;
            if (isPublicroom)
            {
                this._publicroomItems = model.publicroom_items;
                bool result;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {

                    result = dbClient.findsResult("SELECT id FROM games_lobbies WHERE id = '" + roomID + "'");
                }

                if (result)
                {
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        dRow = dbClient.getRow("SELECT type,rank FROM games_lobbies WHERE id = '" + roomID + "'");
                    }
                    this.Lobby = new gameLobby(this, (Convert.ToString(dRow["type"]) == "bb"), Convert.ToString(dRow["rank"]));
                }
            }
            else
            {
                floorItemManager = new FloorItemManager(this);
                wallItemManager = new WallItemManager(this);

                DataTable dTable;
                DataTable dTable2;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dTable = dbClient.getTable("SELECT f.id as id , f.tid as tid, " +
                                                "f.x as x, f.y as y, f.z as z, f.h " +
                                                "as h, f.var as var, f.wallpos as wallpos " +
                                                "FROM room_furniture " +
                                                "INNER " +
                                                "JOIN furniture AS f " +
                                                "ON furniture_id = f.id " +
                                                "WHERE room_id = '" + roomID + "'");
                    dTable2 = dbClient.getTable("SELECT userid, vote FROM room_votes WHERE roomid = " + roomID);
                }
                foreach (DataRow dbRow in dTable.Rows)
                {
                    if (Convert.ToString(dbRow["wallpos"]) == "") // Flooritem
                    {
                        floorItemManager.addItem((new genericItem(Convert.ToInt32(dbRow["id"]), Convert.ToInt32(dbRow["tid"]), Convert.ToString(dbRow["var"]), "")), Convert.ToInt32(dbRow["x"]), Convert.ToInt32(dbRow["y"]), Convert.ToInt32(dbRow["z"]), Convert.ToDouble(dbRow["h"]));
                    }
                    else
                    {
                        wallItemManager.addItem(new genericItem(Convert.ToInt32(dbRow["id"]), Convert.ToInt32(dbRow["tid"]), Convert.ToString(dbRow["var"]), Convert.ToString(dbRow["wallpos"])), false);
                    }
                }
                foreach (DataRow dbRow in dTable2.Rows)
                {
                    _votes.Add(Convert.ToInt32(dbRow[0]));
                    _votesTotal += Convert.ToInt32(dbRow[1]);
                }
            }


            loadBots();


            _statusHandler.Start();

            sqSTATE[doorX, doorY] = 0; // Door always walkable
        }


        /// <summary>
        /// Invoked by CRL garbage collector. Destroys all the remaining objects if all references to this object have been removed.
        /// </summary>
        ~virtualRoom()
        {
            try
            {
                _statusHandler.Abort();
            }
            catch { }
        }
        #endregion

        #region User and bot adding/removing
        internal void addUser(virtualUser User)
        {
            if (User._teleporterID == 0 && (User._ROOMACCESS_PRIMARY_OK == false || User._ROOMACCESS_SECONDARY_OK == false))
                return;
            int roomid = getFreeRoomIdentifier();
            User.statusManager = new virtualRoomStatusManager(roomid, this);
            User.roomUser = new virtualRoomUser(User.userID, roomid, User, User.statusManager, !roomQuiet);

            if (User._teleporterID == 0)
            {

                if (User.spawnLoc[0].X == -1)
                {
                    User.roomUser.X = this.doorX;
                    User.roomUser.Y = this.doorY;
                    User.roomUser.Z1 = this.doorZ;
                    User.roomUser.Z2 = this.doorZ;
                    User.roomUser.H = this.doorH;
                }
                else
                {
                    User.roomUser.X = User.spawnLoc[0].X;
                    User.roomUser.Y = User.spawnLoc[0].Y;
                    User.roomUser.Z1 = Convert.ToByte(User.spawnLoc[1].X);
                    User.roomUser.Z2 = Convert.ToByte(User.spawnLoc[1].X);
                    User.roomUser.H = User.spawnLoc[1].Y;
                    if (User.spawnStatus != "")
                        User.statusManager.addStatus(User.spawnStatus, null, 0, null, 0, 0);
                    User.spawnLoc[0].X = -1;
                }
            }
            else
            {
                try
                {
                    genericItem Teleporter = floorItemManager.getItem(User._teleporterID);
                    User.roomUser.X = Teleporter.X;
                    User.roomUser.Y = Teleporter.Y;
                    User.roomUser.H = Teleporter.H;
                    User.roomUser.Z1 = Teleporter.Z;
                    User.roomUser.Z2 = Teleporter.Z;
                    User._teleporterID = 0;
                    User._teleportRoomID = 0;
                    sendData(@"A\" + Teleporter.ID + "/" + User._Username + "/" + Teleporter.Sprite);
                }
                catch { }
            }
            User.roomUser.goalX = -1;
            _Users.Add(User.roomUser.roomUID, User.roomUser);

            if (this.isPublicroom == false)
            {

                if (User._hasRights)
                    if (User._isOwner == false) { User.statusManager.addStatus("flatctrl", "onlyfurniture", 0, null, 0, 0); }
                if (User._isOwner)
                    User.statusManager.addStatus("flatctrl", "useradmin", 0, null, 0, 0);

            }
            else
            {
                if (this.hasSwimmingPool) // This room has a swimming pool
                {
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        User.roomUser.swimOutfit = dbClient.getString("SELECT figure_swim FROM users WHERE id = '" + User.userID + "'");
                    }
                }
                if (this.DiveDoorOpen == false)
                    sendSpecialCast("door", "close");

                if (this.Lobby != null) // Game lobby here
                {
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        User.roomUser.gamePoints = dbClient.getInt("SELECT " + Lobby.Type + "_totalpoints FROM users WHERE id = '" + User.userID + "'");
                    }
                    sendData("Cz" + "I" + Encoding.encodeVL64(User.roomUser.roomUID) + User.roomUser.gamePoints + Convert.ToChar(2) + rankManager.getGameRankTitle(Lobby.isBattleBall, User.roomUser.gamePoints) + Convert.ToChar(2));

                }
            }
            sendData(@"@\" + User.roomUser.detailsString);
            //sendData("DJ" + Encoding.encodeVL64(User.roomUser.roomUID) + User._Figure + Convert.ToChar(2) + User._Sex + Convert.ToChar(2) + User._Mission + Convert.ToChar(2));
            if (User._groupID > 0 && _activeGroups.ContainsKey(User._groupID) == false)
            {
                string groupBadge;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    groupBadge = dbClient.getString("SELECT badge FROM groups_details WHERE id = '" + User._groupID + "'");
                }
                sendData("Du" + "I" + Encoding.encodeVL64(User._groupID) + groupBadge + Convert.ToChar(2));
                _activeGroups.Add(User._groupID, groupBadge);
            }
            roomManager.updateRoomVisitorCount(this.roomID, this._Users.Count, this._Users.Count - 1);
        }
        /// <summary>
        /// Removes a room user from the virtual room.
        /// </summary>
        /// <param name="roomUID">The room identifier of the room user to remove.</param>
        /// <param name="sendKick">Specifies if the user must be kicked with the @R packet.</param>
        /// <param name="moderatorMessage">Specifies a moderator message [B!] packet to be used at kick.</param>
        internal void removeUser(int roomUID, bool sendKick, string moderatorMessage, bool trick) //deze heb jij ook, behalve bool trick, maar die moet k nog weg halen, was om alle dingen die het gebruikte op te sporen XD
        {
            if (_Users.Contains(roomUID) == false)
                return;

            virtualRoomUser roomUser = (virtualRoomUser)_Users[roomUID];
            if (sendKick)
            {
                roomUser.User.sendData("@R");
                if (moderatorMessage != "")
                    roomUser.User.sendData("@a" + moderatorMessage + Convert.ToChar(2) + "holo.cast.modkick");
            }

            sqUNIT[roomUser.X, roomUser.Y] = false;
            _Users.Remove(roomUID);
            _activeRoomIndentifiers.Remove(roomUID);
            if (_Users.Count > 0) // Still users in room
            {
                if (roomUser.User._groupID > 0)
                {
                    bool removeBadge = true;
                    try
                    {
                        lock (_Users)
                        {
                            foreach (virtualRoomUser rUser in _Users.Values)
                            {
                                if (rUser.roomUID != roomUser.roomUID && rUser.User._groupID == roomUser.User._groupID)
                                {
                                    removeBadge = false;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                    if (removeBadge)
                        _activeGroups.Remove(roomUser.User._groupID);
                }

                sendData("@]" + roomUID);
                roomManager.updateRoomVisitorCount(this.roomID, _Users.Count, this._Users.Count + 1);
            }
            else
            {
                foreach (roomPet pet in _Pets.Values)
                {
                    pet.Information.Update();
                }
                _Users.Clear();
                _Bots.Clear();
                _Pets.Clear();
                _activeRoomIndentifiers.Clear();

                _statusUpdates = null;
                if (isPublicroom == false)
                {
                    itemKeeper.destroy();
                    saveFurniStatus();
                    floorItemManager.Clear();
                    wallItemManager.Clear();
                }
                else
                {
                    //try { specialCastHandler.Abort(); } heb k eruit, vanwege een vage bug waar k te lui voor ben om op te lossen (de hele specialcast xD
                    //catch { }
                    if (Lobby != null)
                        Lobby.Clear();
                    Lobby = null;
                }

                roomManager.removeRoom(this.roomID, this._Users.Count + 1);
                this.updateStatusses = false;

            }
        }

        #endregion

        #region User data distribution
        /// <summary>
        /// Creates a speech bubble near the user indicating he/she is talking
        /// </summary>
        /// <param name="roomUID"></param>
        internal void sendUserIsTyping(int roomUID, bool showBubble)
        {
            sendData("Ei" + Encoding.encodeVL64(roomUID) + (showBubble ? "I" : "H"));
        }

        /// <summary>
        /// Sends a single packet to all users inside the user manager.
        /// </summary>
        /// <param name="Data">The packet to send.</param>
        internal void sendData(string Data)
        {
            try
            {
                foreach (virtualRoomUser roomUser in ((Hashtable)_Users.Clone()).Values)
                    roomUser.User.sendData(Data);
            }
            catch { }
        }
        /// <summary>
        /// Sends a single packet to all users inside the user manager, after sleeping (on different thread) for a specified amount of milliseconds.
        /// </summary>
        /// <param name="Data">The packet to send.</param>
        /// <param name="msSleep">The amount of milliseconds to sleep before sending.</param>
        internal void sendData(string Data, int msSleep)
        {
            new sendDataSleep(SENDDATASLEEP).BeginInvoke(Data, msSleep, null, null);
        }
        private delegate void sendDataSleep(string Data, int msSleep);
        private void SENDDATASLEEP(string Data, int msSleep)
        {
            Thread.Sleep(msSleep);
            Hashtable backupTable = (Hashtable)_Users.Clone();
            foreach (virtualRoomUser roomUser in (backupTable).Values)
                roomUser.User.sendData(Data);
        }
        /// <summary>
        /// Sends a single packet to a user in the usermanager.
        /// </summary>
        /// <param name="userID">The ID of the user.</param>
        /// <param name="Data">The packet to send.</param>
        internal void sendData(int userID, string Data)
        {
            lock (_Users)
            {
                foreach (virtualRoomUser roomUser in _Users.Values)
                {
                    if (roomUser.userID == userID)
                    {
                        roomUser.User.sendData(Data);
                        return;
                    }
                }
            }
        }
        /// <summary>
        /// Sends a single packet to a user in the usermanager.
        /// </summary>
        /// <param name="Username">The username of the user.</param>
        /// <param name="Data">The packet to send.</param>
        internal void sendData(string Username, string Data)
        {
            lock (_Users)
            {
                foreach (virtualRoomUser roomUser in _Users.Values)
                {
                    if (roomUser.User._Username == Username)
                    {
                        roomUser.User.sendData(Data);
                        return;
                    }
                }
            }
        }
        /// <summary>
        /// Sends a special cast to all users in the usermanager.
        /// </summary>
        /// <param name="Emitter">The objects that emits the cast.</param>
        /// <param name="Cast">The cast to emit.</param>
        internal void sendSpecialCast(string Emitter, string Cast)
        {
            sendData("AG" + Emitter + " " + Cast);
        }
        /// <summary>
        /// Updates the room votes amount for all users that have voted yet. User's that haven't voted yet are skipped so their vote buttons stay visible.
        /// </summary>
        /// <param name="voteAmount">The new amount of votes.</param>
        internal void sendNewVoteAmount(int voteAmount)
        {
            string Data = "EY" + Encoding.encodeVL64(voteAmount);
            lock (_Users)
            {
                try
                {
                    foreach (virtualRoomUser roomUser in _Users.Values)
                    {
                        if (hasUserVoted(roomUser.userID))
                            roomUser.User.sendData(Data);
                    }
                }
                catch { }
            }
        }
        #endregion

        #region Cycle statusses
        /// <summary>
        /// Ran on a thread and handles walking and pathfinding. All status updates are sent to all room users.
        /// </summary>
        private void cycleStatuses()
        {
            int loops = 0;
            TimeSpan cycleTime;
            while (updateStatusses)
            {
                try
                {
                    DateTime cycleStart = DateTime.Now;

                    #region cycle actions
                    handleUserAction();
                    checkPlayFurnis();
                    handlePetAction();
                    handleBotActions();
                    #endregion

                    sendStatusUpdates();

                    #region cycle stuff
                    if (!isPublicroom)
                    {
                        if (loops > 146)
                        {
                            saveFurniStatus();
                            itemKeeper.saveFurni();
                            loops = 0;
                        }
                        else
                            loops++;
                    }
                    #endregion

                    cycleTime = (DateTime.Now - cycleStart);
                    if ((410 - cycleTime.TotalMilliseconds) > 0)
                        Thread.Sleep(410 - cycleTime.Milliseconds);

                }
                catch (ThreadAbortException) { } // nothing special
                catch (Exception e) { Out.WriteCycleStatusError(e.ToString()); }
            }
        }


        #endregion

        #region status updates
        /// <summary>
        /// Sends status updates every 410 milliseconds
        /// </summary>
        private void sendStatusUpdates()
        {
            try
            {
                if (_statusUpdates.Length > 0)
                {
                    string sendUpdates = _statusUpdates.ToString();
                    sendUpdates.TrimEnd(Convert.ToChar(13));
                    sendData("@b" + _statusUpdates.ToString());
                    _statusUpdates = new StringBuilder();
                }
            }
            catch { }
        }
        #endregion

        #region misc item handeling

        /// <summary>
        /// Checks and removes items which has been marked as done for playing with pet furni
        /// </summary>
        private void checkPlayFurnis()
        {
            for (int i = 0; i < toyItems.Length; i++)
            {
                if (toyItems[i] == null)
                    continue;
                if (toyItems[i].itemStatus == "")
                {
                    sendData("A_" + toyItems[i].floorItemToString());
                    toyItems[i] = null;

                }
            }
        }
        #endregion

        #region user handeling
        /// <summary>
        /// Is called every 410 ms for user interaction
        /// </summary>
        private void handleUserAction()
        {
            #region virtualUsers handeling
            List<virtualUser> toRemove = new List<virtualUser>();
            lock (_Users)
            {
                #region Virtual user status handling
                foreach (virtualRoomUser roomUser in _Users.Values)
                {
                    if (roomUser.goalX == -1) // No destination set, user is not walking/doesn't start to walk, advance to next user
                    {
                        _statusUpdates.Append(roomUser.statusString + Convert.ToChar(13));
                        continue;
                    }

                    if (roomUser.walkDoor) // User has clicked the door to leave the room (and got stuck while walking or reached destination)
                    {
                        if (_Users.Count > 1)
                        {
                            toRemove.Add(roomUser.User);
                            _statusUpdates.Append(roomUser.statusString + Convert.ToChar(13));
                            continue;
                        }
                        else
                        {
                            toRemove.Add(roomUser.User);
                            break;
                        }
                    }

                    // If the goal is a seat, then allow to 'walk' on the seat, so seat the user
                    squareState[,] stateMap = (squareState[,])sqSTATE.Clone();
                    try
                    {
                        if (stateMap[roomUser.goalX, roomUser.goalY] == squareState.Seat || stateMap[roomUser.goalX, roomUser.goalY] == squareState.Bed)
                            stateMap[roomUser.goalX, roomUser.goalY] = squareState.Open;
                        if (sqUNIT[roomUser.goalX, roomUser.goalY])
                            stateMap[roomUser.goalX, roomUser.goalY] = squareState.Blocked;
                    }
                    catch { }
                    // Use AStar pathfinding to get the next step to the goal
                    int[] nextCoords = new Pathfinding.Pathfinder(stateMap, sqFLOORHEIGHT, sqUNIT).getNext(roomUser.X, roomUser.Y, roomUser.goalX, roomUser.goalY);

                    roomUser.statusManager.removeStatus("mv");
                    if (nextCoords == null) // No next steps found, destination reached/stuck
                    {
                        roomUser.goalX = -1; // Next time the thread cycles this user, it won't attempt to walk since destination has been reached
                        if (sqTRIGGER[roomUser.X, roomUser.Y] != null)
                        {
                            squareTrigger Trigger = sqTRIGGER[roomUser.X, roomUser.Y];
                            if (this.hasSwimmingPool) // This virtual room has a swimming pool
                            #region Swimming pool triggers
                            {
                                if (Trigger.Object == "curtains1" || Trigger.Object == "curtains2") // User has entered a swimming pool clothing booth
                                {
                                    roomUser.walkLock = true;
                                    roomUser.User.sendData("A`");
                                    sendSpecialCast(Trigger.Object, "close");
                                }
                                else if (roomUser.swimOutfit != "") // User wears a swim outfit and hasn't entered a swimming pool clothing booth
                                {
                                    if (Trigger.Object == "lidodive" && roomUser.User._Tickets != 33333) // User has entered the diving board elevator
                                    {
                                        roomUser.walkLock = true;
                                        roomUser.Diving = true;
                                        DiveDoorOpen = false;
                                        roomUser.goalX = 26;
                                        roomUser.goalY = 3;
                                        moveUser(roomUser, 26, 3, true);
                                        sendSpecialCast("door", "close");
                                        sqUNIT[26, 4] = true;
                                        roomUser.User.sendData("A}");
                                        roomUser.User._Tickets--;
                                        roomUser.User.sendData("A|" + roomUser.User._Tickets);

                                        using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                                        {
                                            dbClient.runQuery("UPDATE users SET tickets = '" + (roomUser.User._Tickets - 1) + "' WHERE id = '" + roomUser.userID + "' LIMIT 1");
                                        }
                                    }
                                    //if (Trigger.Object == "joinwsqueue0")
                                    //{
                                    //    if (WobbleSquabbleQueueThread.ThreadState == System.Threading.ThreadState.Unstarted)
                                    //        break; // Not WS Room!
                                    //    if (roomUser.User._Tickets < Config.WS_TICKET_COST)
                                    //    {
                                    //        //might error
                                    //        DataRow dRow;
                                    //        using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                                    //        {
                                    //            dRow = dbClient.getRow("SELECT `goalx` , `goaly` FROM `room_modeldata_triggers` WHERE `model` = 'rumble' AND `x` = '" + roomUser.X + "' AND `y` = '" + roomUser.Y + "'");
                                    //        }
                                    //        int goalx = Convert.ToInt32(dRow["goalx"]);
                                    //        int goaly = Convert.ToInt32(dRow["goaly"]);
                                    //        roomUser.User.sendData("BKNiet genoeg tickets!");
                                    //        roomUser.goalX = goalx;
                                    //        roomUser.goalY = goaly;
                                    //        break;
                                    //    }
                                    //    roomUser.User._WSVariables = new UserVariables();
                                    //    roomUser.User._WSVariables.QueuePosition = -1;
                                    //    roomUser.User._WSVariables.WaitingToJoinQueue = true;
                                    //    if (_WSQueueManager.JoinQueue(roomUser.User, 0))
                                    //    {
                                    //        roomUser.User._WSVariables.QueuePosition = 0;
                                    //        roomUser.User._WSVariables.Side = 0;
                                    //        roomUser.User.statusManager.removeStatus("swim");
                                    //        DataRow dRow2;
                                    //        using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                                    //        {
                                    //            dRow2 = dbClient.getRow("SELECT `goalx` , `goaly` FROM `room_modeldata_triggers` WHERE `model` = 'rumble' AND `x` = '" + roomUser.X + "' AND `y` = '" + roomUser.Y + "'");
                                    //        }
                                    //        int x = Convert.ToInt32(dRow2[0]);
                                    //        int y = Convert.ToInt32(dRow2[1]);
                                    //        moveUser(roomUser, x, y, true);
                                    //    }
                                    //}
                                    if (Trigger.Object.Contains("Splash")) // User has entered/left a swimming pool
                                    {
                                        sendData("AG" + Trigger.Object);
                                        if (Trigger.Object.Substring(8) == "enter")
                                        {
                                            roomUser.statusManager.removeStatus("carryd");
                                            roomUser.statusManager.addStatus("swim", null, 0, null, 0, 0);
                                        }
                                        else
                                            roomUser.statusManager.removeStatus("swim");
                                        moveUser(roomUser, Trigger.stepX, Trigger.stepY, false);

                                        roomUser.goalX = Trigger.goalX;
                                        roomUser.goalY = Trigger.goalY;
                                    }
                                }
                            }

                            #endregion
                            else
                            {
                                if (roomUser.X == roomUser.goalX && roomUser.Y == roomUser.goalY && _magicTiles.ContainsKey(new Pathfinding.Coord(roomUser.X, roomUser.Y)))
                                {
                                    magicTile mTile = _magicTiles[new Pathfinding.Coord(roomUser.X, roomUser.Y)];
                                    roomUser.User.spawnLoc[0].X = mTile.toX;
                                    roomUser.User.spawnLoc[0].Y = mTile.toY;
                                    roomUser.User.spawnLoc[1].X = mTile.toZ;
                                    roomUser.User.spawnLoc[1].Y = mTile.toH;
                                    roomUser.User.spawnStatus = mTile.toStatus;
                                    roomUser.User.sendData("D^" + mTile.toMode + Encoding.encodeVL64(mTile.toID));
                                }
                            }
                        }

                        _statusUpdates.Append(roomUser.statusString + Convert.ToChar(13));

                    }
                    else // Next steps found by pathfinder
                    {
                        int nextX = nextCoords[0];
                        int nextY = nextCoords[1];
                        squareState nextState = sqSTATE[nextX, nextY];

                        sqUNIT[roomUser.X, roomUser.Y] = false; // Free last position, allow other users to use that spot again
                        sqUNIT[nextX, nextY] = true; // Block the spot of the next steps
                        roomUser.Z1 = Pathfinding.Rotation.Calculate(roomUser.X, roomUser.Y, nextX, nextY); // Calculate the users new rotation
                        roomUser.Z2 = roomUser.Z1;
                        roomUser.statusManager.removeStatus("sit");
                        roomUser.statusManager.removeStatus("lay");

                        double nextHeight = 0;
                        if (nextState == squareState.Rug) // If next step is on a rug, then set user's height to that of the rug [floating stacked rugs in mid-air, petals etc]
                            nextHeight = sqITEMHEIGHT[nextX, nextY];
                        else
                            nextHeight = (double)sqFLOORHEIGHT[nextX, nextY];

                        // Add the walk status to users status manager + add users whole status string to stringbuilder
                        roomUser.statusManager.addStatus("mv", nextX + "," + nextY + "," + nextHeight.ToString().Replace(',', '.'), 0, null, 0, 0);
                        _statusUpdates.Append(roomUser.statusString + Convert.ToChar(13));

                        // Set new coords for virtual room user
                        roomUser.X = nextX;
                        roomUser.Y = nextY;
                        roomUser.H = nextHeight;
                        if (nextState == squareState.Open)
                            continue;
                        else if (nextState == squareState.Seat) // The next steps are on a seat, seat the user, prepare the sit status for next cycle of thread
                        {
                            roomUser.statusManager.removeStatus("dance"); // Remove dance status
                            roomUser.Z1 = sqITEMROT[nextX, nextY]; // 
                            roomUser.Z2 = roomUser.Z1;
                            roomUser.statusManager.addStatus("sit", sqITEMHEIGHT[nextX, nextY].ToString().Replace(',', '.'), 0, null, 0, 0);
                        }
                        else if (nextState == squareState.Bed)
                        {
                            roomUser.statusManager.removeStatus("dance"); // Remove dance status
                            roomUser.Z1 = sqITEMROT[nextX, nextY]; // 
                            roomUser.Z2 = roomUser.Z1;
                            roomUser.statusManager.addStatus("lay", sqITEMHEIGHT[nextX, nextY].ToString().Replace(',', '.'), 0, null, 0, 0);
                        }
                    }
                }

            }

            foreach (virtualUser toKick in toRemove)
            {
                toKick.leaveCurrentRoom("", true);
            }
            toRemove.Clear();
                #endregion
            #endregion

        }
        #endregion

        #region bot handeling
        /// <summary>
        /// Is called every 410 ms for bot interaction
        /// </summary>
        private void handleBotActions()
        {
            #region roombot walking
            foreach (virtualBot roomBot in _Bots.Values)
            {
                if (roomBot.goalX == -1)
                    continue;

                // If the goal is a seat, then allow to 'walk' on the seat, so seat the user
                squareState[,] stateMap = (squareState[,])sqSTATE.Clone();
                try
                {
                    if (stateMap[roomBot.goalX, roomBot.goalY] == squareState.Seat)
                        stateMap[roomBot.goalX, roomBot.goalY] = squareState.Open;
                    if (sqUNIT[roomBot.goalX, roomBot.goalY])
                        stateMap[roomBot.goalX, roomBot.goalY] = squareState.Blocked;
                }
                catch { }

                int[] nextCoords = new Pathfinding.Pathfinder(stateMap, sqFLOORHEIGHT, sqUNIT).getNext(roomBot.X, roomBot.Y, roomBot.goalX, roomBot.goalY);

                roomBot.removeStatus("mv");
                if (nextCoords == null) // No next steps found, destination reached/stuck
                {
                    if (roomBot.X == roomBot.goalX && roomBot.Y == roomBot.goalY)
                    {
                        roomBot.checkOrders();
                    }
                    roomBot.goalX = -1;
                    _statusUpdates.Append(roomBot.statusString + Convert.ToChar(13));
                }
                else
                {
                    int nextX = nextCoords[0];
                    int nextY = nextCoords[1];

                    sqUNIT[roomBot.X, roomBot.Y] = false; // Free last position, allow other users to use that spot again
                    sqUNIT[nextX, nextY] = true; // Block the spot of the next steps
                    roomBot.Z1 = Pathfinding.Rotation.Calculate(roomBot.X, roomBot.Y, nextX, nextY); // Calculate the bot's new rotation
                    roomBot.Z2 = roomBot.Z1;
                    roomBot.removeStatus("sit");

                    double nextHeight = (double)sqFLOORHEIGHT[nextX, nextY];
                    if (sqSTATE[nextX, nextY] == squareState.Rug) // If next step is on a rug, then set bot's height to that of the rug [floating stacked rugs in mid-air, petals etc]
                        nextHeight = sqITEMHEIGHT[nextX, nextY];
                    else
                        nextHeight = (double)sqFLOORHEIGHT[nextX, nextY];

                    roomBot.addStatus("mv", nextX + "," + nextY + "," + nextHeight.ToString().Replace(',', '.'));
                    _statusUpdates.Append(roomBot.statusString + Convert.ToChar(13));

                    // Set new coords for the bot
                    roomBot.X = nextX;
                    roomBot.Y = nextY;
                    roomBot.H = nextHeight;
                    if (sqSTATE[nextX, nextY] == squareState.Seat) // The next steps are on a seat, seat the bot, prepare the sit status for next cycle of thread
                    {
                        roomBot.removeStatus("dance"); // Remove dance status
                        roomBot.Z1 = sqITEMROT[nextX, nextY]; // 
                        roomBot.Z2 = roomBot.Z1;
                        roomBot.addStatus("sit", sqITEMHEIGHT[nextX, nextY].ToString().Replace(',', '.'));
                    }
                }
            }
            #endregion

        }
        #endregion

        #region pet actions
        /// <summary>
        /// Is called every 410 ms for pet interaction
        /// </summary>
        private void handlePetAction()
        {
            #region pet handeling
            lock (_Pets)
            {
                this.unloadPetsInList();
                foreach (roomPet pet in _Pets.Values)
                {

                    if (pet.goalX == -1)
                        pet.doAI();
                    if (pet.goalX == -1)
                    {
                        _statusUpdates.Append(pet.statusString + Convert.ToChar(13));
                        continue;
                    }

                    // If the goal is a seat, then allow to 'walk' on the seat, so seat the user
                    squareState[,] stateMap = (squareState[,])sqSTATE.Clone();
                    try
                    {
                        if (stateMap[pet.goalX, pet.goalY] == squareState.Seat)
                            stateMap[pet.goalX, pet.goalY] = squareState.Open;
                        if (sqUNIT[pet.goalX, pet.goalY])
                            stateMap[pet.goalX, pet.goalY] = squareState.Blocked;
                    }
                    catch { }

                    int[] nextCoords = new Pathfinding.Pathfinder(stateMap, sqFLOORHEIGHT, sqUNIT).getNext(pet.X, pet.Y, pet.goalX, pet.goalY);

                    pet.removeStatus("mv");
                    if (nextCoords == null) // No next steps found, destination reached/stuck
                    {
                        pet.goalX = -1;
                        pet.removeStatus("mv");
                        _statusUpdates.Append(pet.statusString + Convert.ToChar(13));
                    }
                    else
                    {
                        pet.removeStatus("sit");
                        pet.removeStatus("lay");
                        pet.removeStatus("sleep");

                        byte nextX = (byte)nextCoords[0];
                        byte nextY = (byte)nextCoords[1];

                        sqUNIT[pet.X, pet.Y] = false; // Free last position, allow other users to use that spot again
                        sqUNIT[nextX, nextY] = true; // Block the spot of the next steps
                        pet.Z1 = Pathfinding.Rotation.Calculate(pet.X, pet.Y, nextX, nextY); // Calculate the bot's new rotation
                        pet.Z2 = pet.Z1;


                        double nextHeight = (double)sqFLOORHEIGHT[nextX, nextY];
                        if (sqSTATE[nextX, nextY] == squareState.Rug) // If next step is on a rug, then set bot's height to that of the rug [floating stacked rugs in mid-air, petals etc]
                            nextHeight = sqITEMHEIGHT[nextX, nextY];
                        else
                            nextHeight = (double)sqFLOORHEIGHT[nextX, nextY];

                        pet.addStatus("mv", nextX + "," + nextY + "," + nextHeight.ToString().Replace(',', '.'));
                        _statusUpdates.Append(pet.statusString + Convert.ToChar(13));

                        // Set new coords for the bot
                        pet.X = nextX;
                        pet.Y = nextY;
                        pet.H = nextHeight;
                        if (sqSTATE[nextX, nextY] == squareState.Seat) // The next steps are on a seat, seat the bot, prepare the sit status for next cycle of thread
                        {
                            pet.Z1 = sqITEMROT[nextX, nextY]; // 
                            pet.Z2 = pet.Z1;
                            pet.addStatus("sit", (sqITEMHEIGHT[nextX, nextY] - 0.2).ToString().Replace(',', '.'));
                        }
                        else if (sqSTATE[nextX, nextY] == squareState.Bed) // Bed here
                        {
                            pet.Z1 = sqITEMROT[nextX, nextY];
                            pet.Z2 = pet.Z1;
                            pet.addStatus("lay", (sqITEMHEIGHT[nextX, nextY] - 0.2).ToString().Replace(',', '.'));
                        }
                    }
                }
            }
            #endregion
        }
        #endregion

        #region bots
        /// <summary>
        /// Updates the status of a virtualBot object in room. If the bot is walking, then the bot isn't refreshed immediately but processed at the next cycle of the status thread, to prevent double status strings in @b.
        /// </summary>
        /// <param name="roomBot">The virtualBot object to update.</param>
        internal void Refresh(virtualBot roomBot)
        {
            try
            {
                if (roomBot.goalX == -1)
                    _statusUpdates.Append(roomBot.statusString + Convert.ToChar(13));
            }
            catch { }
        }
        #endregion

        #region new bed / seat placing
        /// <summary>
        /// Updates users if they are standing on a location where an item is beeing placed
        /// </summary>
        /// <param name="X">the X location of the item</param>
        /// <param name="Y">the Y location of the item</param>
        internal void refreshCoord(int X, int Y)
        {
            if (!sqUNIT[X, Y])
                return;
            #region pet refreshing
            foreach (roomPet pet in _Pets.Values)
            {
                if (pet.X == X && pet.Y == Y)
                {
                    if (sqSTATE[X, Y] == squareState.Seat) // Seat here
                    {
                        pet.Z1 = sqITEMROT[X, Y];
                        pet.Z2 = pet.Z1;
                        pet.addStatus("sit", (sqITEMHEIGHT[X, Y] - 0.2).ToString().Replace(',', '.'));

                    }
                    else if (sqSTATE[X, Y] == squareState.Bed) // Bed here
                    {
                        pet.Z1 = sqITEMROT[X, Y];
                        pet.Z2 = pet.Z1;
                        pet.addStatus("lay", (sqITEMHEIGHT[X, Y] - 0.2).ToString().Replace(',', '.'));
                    }
                    else // No seat/bed here
                    {
                        pet.removeStatus("sit");
                        pet.removeStatus("lay");
                        pet.H = sqFLOORHEIGHT[X, Y];
                    }
                    return; // One user per coord
                }
            }
            #endregion

            #region user refreshing
            lock (_Users)
            {
                foreach (virtualRoomUser roomUser in _Users.Values)
                {
                    if (roomUser.X == X && roomUser.Y == Y)
                    {
                        if (sqSTATE[X, Y] == squareState.Seat) // Seat here
                        {
                            roomUser.Z1 = sqITEMROT[X, Y];
                            roomUser.Z2 = roomUser.Z1;
                            roomUser.statusManager.addStatus("sit", sqITEMHEIGHT[X, Y].ToString().Replace(',', '.'), 0, null, 0, 0);
                        }
                        else if (sqSTATE[X, Y] == squareState.Bed) // Bed here
                        {
                            roomUser.Z1 = sqITEMROT[X, Y];
                            roomUser.Z2 = roomUser.Z1;
                            roomUser.statusManager.addStatus("lay", sqITEMHEIGHT[X, Y].ToString().Replace(',', '.'), 0, null, 0, 0);
                        }
                        else // No seat/bed here
                        {
                            roomUser.statusManager.removeStatus("sit");
                            roomUser.statusManager.removeStatus("lay");
                            roomUser.H = sqFLOORHEIGHT[X, Y];
                        }
                        return; // One user per coord
                    }
            #endregion
                }
            }
        }
        #endregion

        #region Single step walking
        /// <summary>
        /// Moves a virtual room user one step to a certain coord [the coord has to be one step removed from the room user's current coords], with pauses and handling for seats and rugs.
        /// </summary>
        /// <param name="roomUser">The virtual room user to move.</param>
        /// <param name="toX">The X of the destination coord.</param>
        /// <param name="toY">The Y of the destination coord.</param>
        internal void moveUser(virtualRoomUser roomUser, int toX, int toY, bool secondRefresh)
        {
            new userMover(MOVEUSER).BeginInvoke(roomUser, toX, toY, secondRefresh, null, null);
        }
        private delegate void userMover(virtualRoomUser roomUser, int toX, int toY, bool secondRefresh);
        private void MOVEUSER(virtualRoomUser roomUser, int toX, int toY, bool secondRefresh)
        {
            try
            {
                sqUNIT[roomUser.X, roomUser.Y] = false;
                sqUNIT[toX, toY] = true;
                roomUser.Z1 = Pathfinding.Rotation.Calculate(roomUser.X, roomUser.Y, toX, toY);
                roomUser.Z2 = roomUser.Z1;
                roomUser.statusManager.removeStatus("sit");
                double nextHeight = 0;
                if (sqSTATE[toX, toY] == squareState.Rug)
                    nextHeight = sqITEMHEIGHT[toX, toY];
                else
                    nextHeight = (double)sqFLOORHEIGHT[toX, toY];
                roomUser.statusManager.addStatus("mv", toX + "," + toY + "," + nextHeight.ToString().Replace(',', '.'), 0, null, 0, 0);
                sendData("@b" + roomUser.statusString);

                Thread.Sleep(310);
                roomUser.X = toX;
                roomUser.Y = toY;
                roomUser.H = nextHeight;

                roomUser.statusManager.removeStatus("mv");
                if (secondRefresh)
                {
                    if (sqSTATE[toX, toY] == squareState.Seat) // The next steps are on a seat, seat the user, prepare the sit status for next cycle of thread
                    {
                        roomUser.statusManager.removeStatus("dance"); // Remove dance status
                        roomUser.Z1 = sqITEMROT[toX, toY];
                        roomUser.Z2 = roomUser.Z1;
                        roomUser.statusManager.addStatus("sit", sqITEMHEIGHT[toX, toY].ToString().Replace(',', '.'), 0, null, 0, 0);
                        roomUser.statusManager.removeStatus("mv");
                    }
                }
            }
            catch { }
        }
        #endregion

        #region Chat

        /// <summary>
        /// Filter the emotions in a chat message, and displays it at the face of the virtual user
        /// </summary>
        /// <param name="sourceUser">The virtualRoomUser object of the sender.</param>
        /// <param name="Message">The message being sent.</param>
        internal void checkEmotion(virtualRoomUser sourceUser, string Message)
        {
            for (int i = 0; i < Gestures.Length; i++)
            {
                string[] currentGestures = Gestures[i];
                string[] splitMessage = Message.Split(currentGestures, StringSplitOptions.None);

                if (splitMessage.Length > 1) // Result?
                {
                    sourceUser.statusManager.addStatus("gest", GestureList[i], 5000, "", 0, 0);
                    return;
                }
            }
        }

        /// <summary>
        /// Checks a message for pets commands, user talk allowence, etc
        /// </summary>
        /// <param name="sourceUser">The user which send the item</param>
        /// <param name="Message">The message he wants to send</param>
        /// <returns>An int indicating if he's allowed to talk</returns>
        /// <example>If a user is not allowed to talk, 1 will be returned. 
        /// In case a user is allowed to talk 0 will be returned</example>
        private int chatMessageCheck(virtualRoomUser sourceUser, string Message)
        {
            checkEmotion(sourceUser, Message);
            if (sourceUser.isTyping)
            {
                sendData("Ei" + Encoding.encodeVL64(sourceUser.roomUID) + "H");
                sourceUser.isTyping = false;
            }
            if (sourceUser.User._isOwner)
            {
                if (_Pets.Count > 0)
                {
                    int x;
                    if ((x = Message.IndexOf(' ')) != -1)
                    {
                        if (_Pets.Contains(Message.Substring(0, x)))
                        {
                            ((roomPet)_Pets[Message.Substring(0, x)]).doAction(Message.Substring(x + 1), sourceUser);
                        }
                    }
                }

            }
            if (sourceUser.allowedToTalk == false)
                return 1;
            sourceUser.statusManager.addStatus("talk", null, Message.Length * 190, null, 0, 0);

            return 0;
        }
        /// <summary>
        /// Sends a 'say' chat message from a virtualRoomUser to the room. Users and bots in a range of 5 squares will receive the message and bob their heads. Roombots will check the message and optionally interact to it.
        /// </summary>
        /// <param name="sourceUser">The virtualRoomUser object that sent the message.</param>
        /// <param name="Message">The message that was sent.</param>
        internal void sendSaying(virtualRoomUser sourceUser, string Message)
        {
            if (chatMessageCheck(sourceUser, Message) == 0)
            {
                string Data = "@X" + Encoding.encodeVL64(sourceUser.roomUID) + Message + Convert.ToChar(2);
                foreach (virtualRoomUser roomUser in ((Hashtable)_Users.Clone()).Values)
                {
                    if (Math.Abs(roomUser.X - sourceUser.X) < 6 && Math.Abs(roomUser.Y - sourceUser.Y) < 6)
                    {
                        #region  headrotation
                        //if (roomUser.roomUID != sourceUser.roomUID && roomUser.goalX == -1)
                        //{
                        //    roomUser.Z2 = Pathfinding.Rotation.headRotation(roomUser.Z2, roomUser.X, roomUser.Y, sourceUser.X, sourceUser.Y);
                        //}
                        #endregion
                        roomUser.User.sendData(Data);
                    }
                }
                foreach (virtualBot roomBot in _Bots.Values)
                {
                    if (Math.Abs(roomBot.X - sourceUser.X) < 6 && Math.Abs(roomBot.Y - sourceUser.Y) < 6)
                        roomBot.Interact(sourceUser, Message);
                }
            }

        }

        #region bot talking
        /// <summary>
        /// Sends a 'say' chat message from a virtualBot to the room. Users in a range of 5 squares will receive the message and bob their heads.
        /// </summary>
        /// <param name="sourceBot">The virtualBot object that sent the message.</param>
        /// <param name="Message">The message that was sent.</param>
        internal void sendSaying(virtualBot sourceBot, string Message)
        {
            string Data = "@X" + Encoding.encodeVL64(sourceBot.roomUID) + Message + Convert.ToChar(2);
            foreach (virtualRoomUser roomUser in ((Hashtable)_Users.Clone()).Values)
            {
                if (Math.Abs(roomUser.X - sourceBot.X) < 6 && Math.Abs(roomUser.Y - sourceBot.Y) < 6)
                {
                    roomUser.User.sendData(Data);
                }
            }
        }
        #endregion

        /// <summary>
        /// Sends a 'shout' chat message from a virtualRoomUser to the room. All users will receive the message and bob their heads. Roombots have a 1/10 chance to react with the 'please don't shout message' set for them.
        /// </summary>
        /// <param name="sourceUser">The virtualRoomUser object that sent the message.</param>
        /// <param name="Message">The message that was sent.</param>
        internal void sendShout(virtualRoomUser sourceUser, string Message)
        {
            if (chatMessageCheck(sourceUser, Message) == 0)
            {
                string Data = "@Z" + Encoding.encodeVL64(sourceUser.roomUID) + Message + Convert.ToChar(2);
                lock (_Users)
                {
                    foreach (virtualRoomUser roomUser in _Users.Values)
                    {
                        _statusUpdates.Append(roomUser.statusString + Convert.ToChar(13));
                        roomUser.User.sendData(Data);
                    }
                }
                foreach (virtualBot roomBot in _Bots.Values)
                {
                    if (Math.Abs(roomBot.X - sourceUser.X) < 6 && Math.Abs(roomBot.Y - sourceUser.Y) < 6)
                    {
                        if (new Random(DateTime.Now.Millisecond).Next(0, 11) == 0)
                            sendSaying(roomBot, roomBot.noShoutingMessage);
                    }
                }
            }
        }
        /// <summary>
        /// Sends a 'shout' chat message from a virtualBot to the room. All users will receive the message and bob their heads.
        /// </summary>
        /// <param name="sourceBot">The virtualRoomBot object that sent the message.</param>
        /// <param name="Message">The message that was sent.</param>
        internal void sendShout(int roomUID, string Message)
        {
            string Data = "@X" + Encoding.encodeVL64(roomUID) + Message + Convert.ToChar(2);
            lock (_Users)
            {
                foreach (virtualRoomUser roomUser in _Users.Values)
                {
                    roomUser.User.sendData(Data);
                }
            }
        }

        /// <summary>
        /// Sends a 'shout' chat message from a virtualBot to the room. All users will receive the message and bob their heads.
        /// </summary>
        /// <param name="sourceBot">The virtualRoomBot object that sent the message.</param>
        /// <param name="Message">The message that was sent.</param>
        internal void sendSaying(int roomUID, string Message)
        {
            string Data = "@Z" + Encoding.encodeVL64(roomUID) + Message + Convert.ToChar(2);
            lock (_Users)
            {
                foreach (virtualRoomUser roomUser in _Users.Values)
                {
                    roomUser.User.sendData(Data);
                }
            }
        }
        /// <summary>
        /// Sends a 'whisper' chat message, which is only visible for sender and receiver, from a certain user to a certain user in the virtual room.
        /// </summary>
        /// <param name="sourceUser">The virtualRoomUser object of the sender.</param>
        /// <param name="Receiver">The username of the receiver.</param>
        /// <param name="Message">The message being sent.</param>
        internal void sendWhisper(virtualRoomUser sourceUser, string Receiver, string Message)
        {
            checkEmotion(sourceUser, Message);
            if (sourceUser.isTyping)
            {
                sendData("Ei" + Encoding.encodeVL64(sourceUser.roomUID) + "H");
                sourceUser.isTyping = false;
            }
            if (sourceUser.allowedToTalk == false)
                return;
            string Data = "@Y" + Encoding.encodeVL64(sourceUser.roomUID) + Message + Convert.ToChar(2);
            lock (_Users)
            {
                foreach (virtualRoomUser roomUser in _Users.Values)
                {
                    if (roomUser.User._Username == Receiver)
                    {
                        sourceUser.User.sendData(Data);
                        roomUser.User.sendData(Data);
                        return;
                    }
                }
            }
        }
        #endregion

        #region Moderacy tasks
        /// <summary>
        /// Casts a 'roomkick' on the user manager, kicking all users from the room [with message] who have a lower rank than the caster of the roomkick.
        /// </summary>
        /// <param name="casterRank">The rank of the caster of the 'room kick'.</param>
        /// <param name="Message">The message that goes with the 'roomkick'.</param>
        internal void kickUsers(byte casterRank, string Message)
        {
            List<virtualUser> remove = new List<virtualUser>();
            lock (_Users)
            {
                foreach (virtualRoomUser roomUser in _Users.Values)
                {
                    if (roomUser.User._Rank < casterRank)
                        remove.Add(roomUser.User);
                }

                foreach (virtualUser roomUser in remove)
                {
                    roomUser.leaveCurrentRoom(Message, true);
                }
            }

        }
        /// <summary>
        /// Casts a 'room mute' on the user manager, muting all users in the room who aren't muted yet and have a lower rank than the caster of the room mute. The affected users receive a message with the reason of their muting, and they won't be able to chat anymore until another user unmutes them.
        /// </summary>
        /// <param name="casterRank">The rank of the caster of the 'room mute'.</param>
        /// <param name="Message">The message that goes with the 'room mute'.</param>
        internal void muteUsers(byte casterRank, string Message)
        {
            Message = "BK" + stringManager.getString("scommand_muted") + "\r" + Message;

            try
            {
                lock (_Users)
                {
                    foreach (virtualRoomUser roomUser in _Users.Values)
                    {
                        if (roomUser.User._isMuted == false && roomUser.User._Rank < casterRank)
                        {
                            roomUser.User._isMuted = true;
                            roomUser.User.sendData(Message);
                        }
                    }
                }
            }

            catch { }
        }
        /// <summary>
        /// Casts a 'room unmute' on the user manager, unmuting all users in the room who are muted and have a lower rank than the caster of the room mute. The affected users are notified that they can chat again.
        /// </summary>
        /// <param name="casterRank">The rank of the caster of the 'room unmute'.</param>
        internal void unmuteUsers(byte casterRank)
        {
            string Message = "BK" + stringManager.getString("scommand_unmuted");
            try
            {
                lock (_Users)
                {
                    foreach (virtualRoomUser roomUser in _Users.Values)
                    {
                        if (roomUser.User._isMuted && roomUser.User._Rank < casterRank)
                        {
                            roomUser.User._isMuted = false;
                            roomUser.User.sendData(Message);
                        }
                    }
                }
            }
            catch { }
        }
        #endregion

        #region Bot loading and deloading
        internal void loadBots()
        {
            DataColumn dCol;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dCol = dbClient.getColumn("SELECT id FROM roombots WHERE roomid = '" + this.roomID + "'");
            }
            int[] IDs = dataHandling.dColToArray(dCol, null);
            for (int i = 0; i < IDs.Length; i++)
            {
                virtualBot roomBot = new virtualBot(IDs[i], getFreeRoomIdentifier(), this);
                roomBot.H = sqFLOORHEIGHT[roomBot.X, roomBot.Y];
                sqUNIT[roomBot.X, roomBot.Y] = true;

                _Bots.Add(roomBot.roomUID, roomBot);
                sendData(@"@\" + roomBot.detailsString);
                sendData("@b" + roomBot.statusString);
            }
        }
        #endregion

        #region Map management
        /// <summary>
        /// Function to see whether a tile is blocked or not
        /// </summary>
        /// <param name="x">the X coordinate to check</param>
        /// <param name="y">the y coordinate to check</param>
        /// <returns></returns>
        internal bool checkLocationExsistence(int x, int y)
        {

            int maxX = sqSTATE.GetUpperBound(0);
            int maxY = sqSTATE.GetUpperBound(1);
            if (x <= maxX && y <= maxY && x > 0 && y > 0)
                return true;
            else
                return false;
        }


        internal bool squareBlocked(int X, int Y, int Length, int Width)
        {
            for (int jX = X; jX < X + Width; jX++)
            {
                for (int jY = Y; jY < X + Length; jY++)
                {
                    if (sqUNIT[jX, jY])
                        return true;
                }
            }
            return false;
        }
        internal void setSquareState(int X, int Y, int Length, int Width, squareState State)
        {
            for (int jX = X; jX < X + Width; jX++)
            {
                for (int jY = Y; jY < Y + Length; jY++)
                {
                    sqSTATE[jX, jY] = State;
                }
            }
        }
        internal squareTrigger getTrigger(int X, int Y)
        {
            try { return sqTRIGGER[X, Y]; }
            catch { return null; }
        }
        /// <summary>
        /// gets the borders of the map
        /// </summary>
        /// <returns>[0] as the maximum x amount [1] as the maximum y amount</returns>
        internal int[] getMapBorders()
        {
            int[] i = new int[2];
            i[0] = sqUNIT.GetUpperBound(0);
            i[1] = sqUNIT.GetUpperBound(1);
            return i;
        }

        #endregion

        #region Item managers

        #region flooritem manager
        /// <summary>
        /// Provides management for virtual flooritems in a virtual room.
        /// </summary>
        internal class FloorItemManager
        {
            #region declares
            private virtualRoom _Room;
            private Hashtable _Items = new Hashtable();
            /// <summary>
            /// The database ID of the soundmachine in this FloorItemManager.
            /// </summary>
            internal int soundMachineID;
            #endregion

            #region constructor/destructor
            /// <summary>
            /// Initializes the manager.
            /// </summary>
            /// <param name="Room">The parent room.</param>
            public FloorItemManager(virtualRoom Room)
            {
                this._Room = Room;
            }
            /// <summary>
            /// Removes all the items from the item manager and destructs all objects inside.
            /// </summary>
            internal void Clear()
            {
                try { _Items.Clear(); }
                catch { }
                _Room = null;
                _Items = null;
            }
            #endregion

            #region item placing/removing/relocating
            /// <summary>
            /// Adds a new virtual flooritem to the manager at initialization.
            /// </summary>
            /// <param name="itemID">The ID of the new item.</param>
            /// <param name="templateID">The template ID of the new item.</param>
            /// <param name="X">The X position of the new item.</param>
            /// <param name="Y">The Y position of the new item.</param>
            /// <param name="Z">The Z [rotation] of the new item.</param>
            /// <param name="H">The H position [height] of the new item.</param>
            /// <param name="Var">The variable of the new item.</param>
            internal bool addItem(genericItem newItem, int X, int Y, int Z, double H)
            {
                int itemID = newItem.ID;
                string Var = newItem.Var;
                try
                {
                    itemTemplate Template = newItem.template;
                    if (Template.isSoundMachine)
                        soundMachineID = itemID;

                    int Length = 0;
                    int Width = 0;
                    if (Z == 2 || Z == 6)
                    {
                        Length = Template.Length;
                        Width = Template.Width;
                    }
                    else
                    {
                        Length = Template.Width;
                        Width = Template.Length;
                    }

                    for (int jX = X; jX < X + Width; jX++)
                        for (int jY = Y; jY < Y + Length; jY++)
                        {
                            furnitureStack Stack = _Room.sqSTACK[jX, jY];
                            if (Stack == null)
                            {
                                if (Template.typeID != 2 && Template.typeID != 3)
                                {
                                    Stack = new furnitureStack();
                                    Stack.Add(itemID);
                                }
                            }
                            else
                                Stack.Add(itemID);

                            _Room.sqSTATE[jX, jY] = (squareState)Template.typeID;
                            if (Template.typeID == 2 || Template.typeID == 3)
                            {
                                _Room.sqITEMHEIGHT[jX, jY] = Template.topH + H - _Room.sqFLOORHEIGHT[jX, jY]; // H + (old)
                                _Room.sqITEMROT[jX, jY] = Convert.ToByte(Z);
                            }
                            else
                            {
                                if (Template.typeID == 4)
                                    _Room.sqITEMHEIGHT[jX, jY] = H;
                            }
                            _Room.sqSTACK[jX, jY] = Stack;
                        }
                    if (Template.isDoor)
                    {
                        if (_Room.squareBlocked(X, Y, Length, Width))
                            return false;
                        if (Var.ToLower() == "c")
                            _Room.setSquareState(X, Y, Length, Width, squareState.Blocked);
                        else if (Var.ToLower() == "o")
                            _Room.setSquareState(X, Y, Length, Width, squareState.Open);
                        else
                            _Room.setSquareState(X, Y, Length, Width, squareState.Blocked);
                    }

                    genericItem Item = new genericItem(itemID, newItem.template.templateID, X, Y, Convert.ToByte(Z), H, Var);
                    if (Template.isPet)
                    {
                        _Room.loadPet(itemID);
                    }
                    _Items.Add(itemID, Item);
                    _Room.itemKeeper.addStartItem(Item);
                    return true;
                }
                catch (Exception ex) { Out.WriteError("[" + ex.Source + "] Exception Error - " + ex.Message); return false; }
            }

            /// <summary>
            /// Removes a virtual flooritem from the item manager, handles the heightmap, makes it disappear in room and returns it back to the owners hand, or deletes it.
            /// </summary>
            /// <param name="itemID">The ID of the item to remove.</param>
            /// <param name="ownerID">The ID of the user who owns this item. If 0, then the item will be dropped from the database.</param>
            internal genericItem removeItem(int itemID, int ownerID)
            {
                if (_Items.ContainsKey(itemID))
                {
                    genericItem Item = (genericItem)_Items[itemID];
                    itemTemplate Template = Item.template;

                    int Length = 0;
                    int Width = 0;
                    if (Item.Z == 2 || Item.Z == 6)
                    {
                        Length = Template.Length;
                        Width = Template.Width;
                    }
                    else
                    {
                        Length = Template.Width;
                        Width = Template.Length;
                    }

                    for (int jX = Item.X; jX < Item.X + Width; jX++)
                    {
                        for (int jY = Item.Y; jY < Item.Y + Length; jY++)
                        {
                            furnitureStack Stack = _Room.sqSTACK[jX, jY];
                            if (Stack != null && Stack.Count > 1)
                            {
                                if (itemID == Stack.bottomItemID())
                                {
                                    int topID = Stack.topItemID();
                                    if (topID != 0)
                                    {
                                        genericItem topItem = (genericItem)_Items[topID];
                                        _Room.sqSTATE[jX, jY] = (squareState)topItem.template.typeID;
                                    }

                                }
                                else if (itemID == Stack.topItemID())
                                {
                                    int belowID = Stack.getBelowItemID(itemID);
                                    if (belowID != 0)
                                    {
                                        genericItem belowItem = (genericItem)_Items[belowID];

                                        byte typeID = belowItem.template.typeID;

                                        _Room.sqSTATE[jX, jY] = (squareState)typeID;
                                        if (typeID == 2 || typeID == 3)
                                        {
                                            _Room.sqITEMROT[jX, jY] = belowItem.Z;
                                            _Room.sqITEMHEIGHT[jX, jY] = belowItem.H + belowItem.template.topH;
                                        }
                                        else if (typeID == 4)
                                        {
                                            _Room.sqITEMHEIGHT[jX, jY] = belowItem.H;
                                        }
                                    }
                                }
                                Stack.Remove(itemID);
                                _Room.sqSTACK[jX, jY] = Stack;
                            }
                            else
                            {
                                _Room.sqSTATE[jX, jY] = 0;
                                _Room.sqITEMHEIGHT[jX, jY] = 0;
                                _Room.sqITEMROT[jX, jY] = 0;
                                _Room.sqSTACK[jX, jY] = null;
                            }
                            if (Template.typeID == 2 || Template.typeID == 3)
                                _Room.refreshCoord(jX, jY);
                        }
                    }

                    if (this.soundMachineID == 0 && ((stringManager.getStringPart(Template.Sprite, 0, 13) == "sound_machine") || (stringManager.getStringPart(Template.Sprite, 0, 13) == "ads_idol_trax") || (stringManager.getStringPart(Template.Sprite, 0, 13) == "nouvelle_trax")))
                        soundMachineID = 0;

                    _Room.sendData("A^" + itemID);
                    if (Template.isPet)
                    {
                        _Room.unloadPet(itemID);
                    }

                    _Items.Remove(itemID);
                    _Room.itemKeeper.deleteDatabaseEntry(Item);

                    if (ownerID == 0) // Return to current owner/new owner
                    {
                        using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                        {
                            dbClient.runQuery("DELETE FROM furniture WHERE id = '" + itemID + "'");
                        }
                    }

                    return Item;
                }
                return null;

            }

            /// <summary>
            /// Places an item in the room
            /// </summary>
            /// <param name="itemToPlace"></param>
            /// <param name="X"></param>
            /// <param name="Y"></param>
            /// <param name="typeID"></param>
            /// <param name="Z"></param>
            /// <param name="var"></param>
            /// <returns></returns>
            internal int placeItem(genericItem itemToPlace, byte X, byte Y, byte typeID, byte Z, string var)
            {
                int itemID = itemToPlace.ID;
                if (_Items.ContainsKey(itemID))
                    return errorList.FURNITURE_IS_IN_ROOM;

                try
                {
                    itemTemplate Template = itemToPlace.template;
                    bool isSoundMachine = ((stringManager.getStringPart(Template.Sprite, 0, 13) == "sound_machine") || (stringManager.getStringPart(Template.Sprite, 0, 13) == "ads_idol_trax") || (stringManager.getStringPart(Template.Sprite, 0, 13) == "nouvelle_trax"));

                    if (isSoundMachine && soundMachineID > 0)
                        return errorList.SOUNDMACHINE_IN_ROOM;

                    int Length = 0;
                    int Width = 0;
                    if (Z == 2 || Z == 6)
                    {
                        Length = Template.Length;
                        Width = Template.Width;
                    }
                    else
                    {
                        Length = Template.Width;
                        Width = Template.Length;
                    }

                    double testH = _Room.sqFLOORHEIGHT[X, Y];
                    double H = testH;
                    if (_Room.sqSTACK[X, Y] != null)
                    {
                        genericItem topItem = (genericItem)_Items[_Room.sqSTACK[X, Y].topItemID()];
                        H = topItem.H + topItem.template.topH;
                    }

                    for (int jX = X; jX < X + Width; jX++)
                    {
                        for (int jY = Y; jY < Y + Length; jY++)
                        {
                            if (!(Template.typeID == 2 || Template.typeID == 4) && _Room.sqUNIT[jX, jY])
                                return -1;

                            squareState jState = _Room.sqSTATE[jX, jY];
                            if (jState != squareState.Open)
                            {
                                if (jState == squareState.Blocked)
                                {
                                    if (_Room.sqSTACK[jX, jY] == null) // Square blocked and no stack here
                                        return -1;
                                    else
                                    {
                                        genericItem topItem = (genericItem)_Items[_Room.sqSTACK[jX, jY].topItemID()];
                                        itemTemplate topItemTemplate = (topItem.template);
                                        if (topItemTemplate.topH == 0 || topItemTemplate.typeID == 2 || topItemTemplate.typeID == 3) // No stacking on seat/bed
                                            return -1;
                                        else
                                        {
                                            if (topItem.H + topItemTemplate.topH > H) // Higher than previous topheight
                                                H = topItem.H + topItemTemplate.topH;
                                        }
                                    }
                                }
                                else if (jState == squareState.Rug && _Room.sqSTACK[jX, jY] != null)
                                {
                                    double jH = ((genericItem)_Items[_Room.sqSTACK[jX, jY].topItemID()]).H + 0.1;
                                    if (jH > H)
                                        H = jH;
                                }
                                else // Seat etc
                                    return -1;
                            }

                        }
                    }

                    if (H > Config.Items_Stacking_maxHeight)
                        H = Config.Items_Stacking_maxHeight;

                    for (int jX = X; jX < X + Width; jX++)
                    {
                        for (int jY = Y; jY < Y + Length; jY++)
                        {
                            furnitureStack Stack = null;
                            if (_Room.sqSTACK[jX, jY] == null)
                            {
                                if ((Template.typeID == 1 && Template.topH > 0) || Template.typeID == 4)
                                {
                                    Stack = new furnitureStack();
                                    Stack.Add(itemID);
                                }
                            }
                            else
                            {
                                Stack = _Room.sqSTACK[jX, jY];
                                Stack.Add(itemID);
                            }

                            _Room.sqSTATE[jX, jY] = (squareState)Template.typeID;
                            _Room.sqSTACK[jX, jY] = Stack;
                            if (Template.typeID == 2 || Template.typeID == 3)
                            {
                                _Room.sqITEMHEIGHT[jX, jY] = Template.topH + H - _Room.sqFLOORHEIGHT[jX, jY]; //H + Template.topH (old)
                                _Room.sqITEMROT[jX, jY] = Z;
                                _Room.sqITEMROT[jX, jY] = Z;
                                if (Template.typeID == 3 && (jX != X || jY != Y))
                                    if (Width == 1)
                                        _Room.sqSTATE[jX, jY] = squareState.Blocked;
                                    else if ((Z == 2 && jX != X) || (Z == 0 && jY != Y))
                                        _Room.sqSTATE[jX, jY] = squareState.Blocked;
                            }
                            else if (Template.typeID == 4)
                                _Room.sqITEMHEIGHT[jX, jY] = H;
                            if (_Room.sqSTATE[jX, jY] == squareState.Rug || _Room.sqSTATE[jX, jY] == squareState.Seat)
                                _Room.refreshCoord(jX, jY);
                        }
                    }
                    if (Template.isPet)
                    {
                        int error = _Room.loadPet(itemID, X, Y);
                        if (error != 0)
                        {
                            if (error == 1)
                            {
                                return errorList.TOO_MUCH_PETS;
                            }
                            else if (error == 2)
                            {
                                return errorList.PETNAME_IN_ROOM;
                            }
                        }
                    }

                    //dbClient.runQuery("UPDATE furniture SET x = '" + X + "',y = '" + Y + "',z = '" + Z + "',h = '" + H.ToString().Replace(',', '.') + "' WHERE id = '" + itemID + "' LIMIT 1");
                    //dbClient.runQuery("INSERT INTO room_furniture (room_id, furniture_id) VALUES ( '" + this._Room.roomID + "','" + itemID + "')");
                    //dbClient.runQuery("DELETE FROM user_ furniture WHERE furniture_id = '" + itemID + "'");

                    genericItem Item = new genericItem(itemID, itemToPlace.template.templateID, X, Y, Z, H, var);
                    Item.updated = true;
                    this._Room.itemsUpdated = true;
                    _Items.Add(itemID, Item);
                    _Room.itemKeeper.addDatabaseEntry(Item);
                    _Room.sendData("A]" + Item.floorItemToString());

                    if (isSoundMachine)
                        this.soundMachineID = itemID;



                    return 0;
                }
                catch { return -1; }
            }


            /// <summary>
            /// Relocates an item in the room
            /// </summary>
            /// <param name="itemID">the ID of the item</param>
            /// <param name="X">the new X location of the item</param>
            /// <param name="Y">the new Y location of the item</param>
            /// <param name="Z">the new Z location of the item</param>
            internal void relocateItem(int itemID, int X, int Y, byte Z)
            {
                try
                {
                    genericItem Item = (genericItem)_Items[itemID];
                    itemTemplate Template = Item.template;

                    int Length = 0;
                    int Width = 0;
                    if (Z == 2 || Z == 6)
                    {
                        Length = Template.Length;
                        Width = Template.Width;
                    }
                    else
                    {
                        Length = Template.Width;
                        Width = Template.Length;
                    }

                    double baseFloorH = _Room.sqFLOORHEIGHT[X, Y];
                    double H = baseFloorH;
                    if (_Room.sqSTACK[X, Y] != null)
                    {
                        genericItem topItem = (genericItem)_Items[_Room.sqSTACK[X, Y].topItemID()];
                        if (topItem != Item)
                        {
                            itemTemplate topTemplate = topItem.template;
                            if (topTemplate.typeID == 1)
                                H = topItem.H + topTemplate.topH;
                        }
                        else if (_Room.sqSTACK[X, Y].Count > 1)
                            H = topItem.H;
                    }

                    for (int jX = X; jX < X + Width; jX++)
                    {
                        for (int jY = Y; jY < Y + Length; jY++)
                        {
                            if (!(Template.typeID == 2 || Template.typeID == 4) && _Room.sqUNIT[jX, jY])
                                return;

                            squareState jState = _Room.sqSTATE[jX, jY];
                            furnitureStack Stack = _Room.sqSTACK[jX, jY];
                            if (jState != squareState.Open)
                            {
                                if (Stack == null)
                                {
                                    if (jX != Item.X || jY != Item.Y)
                                        return;
                                }
                                else
                                {
                                    genericItem topItem = (genericItem)_Items[Stack.topItemID()];
                                    if (topItem != Item)
                                    {
                                        itemTemplate topItemTemplate = topItem.template;
                                        if (topItemTemplate.typeID == 1 && topItemTemplate.topH > 0)
                                        {
                                            if (topItem.H + topItemTemplate.topH > H)
                                                H = topItem.H + topItemTemplate.topH;
                                        }
                                        else
                                        {
                                            if (topItemTemplate.typeID == 2)
                                                return;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    int oldLength = 1;
                    int oldWidth = 1;
                    if (Template.Length > 1 || Template.Width > 1)
                    {
                        if (Item.Z == 2 || Item.Z == 6)
                        {
                            oldLength = Template.Length;
                            oldWidth = Template.Width;
                        }
                        else
                        {
                            oldLength = Template.Width;
                            oldWidth = Template.Length;
                        }
                    }
                    if (H > Config.Items_Stacking_maxHeight)
                        H = Config.Items_Stacking_maxHeight;

                    for (int jX = Item.X; jX < Item.X + oldWidth; jX++)
                    {
                        for (int jY = Item.Y; jY < Item.Y + oldLength; jY++)
                        {
                            furnitureStack Stack = _Room.sqSTACK[jX, jY];
                            if (Stack != null && Stack.Count > 1)
                            {
                                if (itemID == Stack.bottomItemID())
                                {
                                    if ((((genericItem)_Items[Stack.topItemID()]).template).typeID == 2)
                                        _Room.sqSTATE[jX, jY] = squareState.Seat;
                                    else
                                        _Room.sqSTATE[jX, jY] = squareState.Open;
                                }
                                else if (itemID == Stack.topItemID())
                                {
                                    genericItem belowItem = (genericItem)_Items[Stack.getBelowItemID(itemID)];
                                    byte typeID = belowItem.template.typeID;

                                    _Room.sqSTATE[jX, jY] = (squareState)typeID;
                                    if (typeID == 2 || typeID == 3)
                                    {
                                        _Room.sqITEMROT[jX, jY] = belowItem.Z;
                                        _Room.sqITEMHEIGHT[jX, jY] = belowItem.H + belowItem.template.topH;

                                        if (Template.typeID == 3 && (jX != X || jY != Y))
                                            if (Width == 1)
                                                _Room.sqSTATE[jX, jY] = squareState.Blocked;
                                            else if ((Z == 2 && jX != X) || (Z == 0 && jY != Y))
                                                _Room.sqSTATE[jX, jY] = squareState.Blocked;
                                    }
                                    else if (typeID == 4)
                                        _Room.sqITEMHEIGHT[jX, jY] = belowItem.H;
                                }
                                Stack.Remove(itemID);
                                _Room.sqSTACK[jX, jY] = Stack;
                            }
                            else
                            {
                                _Room.sqSTATE[jX, jY] = 0;
                                _Room.sqITEMHEIGHT[jX, jY] = 0;
                                _Room.sqITEMROT[jX, jY] = 0;
                                _Room.sqSTACK[jX, jY] = null;
                            }
                            if (Template.typeID == 2 || Template.typeID == 3)
                                _Room.refreshCoord(jX, jY);
                        }
                    }

                    Item.X = X;
                    Item.Y = Y;
                    Item.Z = Z;
                    Item.H = H;
                    Item.updated = true;
                    this._Room.itemsUpdated = true;
                    _Room.sendData("A_" + Item.floorItemToString());

                    if (Item.template.isPet)
                    {
                        if (_Room.getRoomPet(itemID) != null)
                            _Room.getRoomPet(itemID).wakeUp();

                    }

                    for (int jX = X; jX < X + Width; jX++)
                    {
                        for (int jY = Y; jY < Y + Length; jY++)
                        {
                            furnitureStack Stack = null;
                            if (_Room.sqSTACK[jX, jY] == null)
                            {
                                if (Template.topH > 0 && Template.typeID != 2 && Template.typeID != 3 && Template.typeID != 4)
                                {
                                    Stack = new furnitureStack();
                                    Stack.Add(itemID);
                                }
                            }
                            else
                            {
                                Stack = _Room.sqSTACK[jX, jY];
                                Stack.Add(itemID);
                            }

                            _Room.sqSTATE[jX, jY] = (squareState)Template.typeID;
                            _Room.sqSTACK[jX, jY] = Stack;
                            if (Template.typeID == 2 || Template.typeID == 3)
                            {
                                _Room.sqITEMHEIGHT[jX, jY] = Template.topH + H - _Room.sqFLOORHEIGHT[jX, jY]; // H + Template.topH (old)
                                _Room.sqITEMROT[jX, jY] = Z;
                                _Room.refreshCoord(jX, jY);
                            }
                            else if (Template.typeID == 4)
                                _Room.sqITEMHEIGHT[jX, jY] = H;
                        }
                    }
                }
                catch { }
            }
            #endregion

            #region item handeling
            /// <summary>
            /// Updates the status of a virtual flooritem and updates it in the virtual room and in the database. Door items are also being handled if opened/closed.
            /// </summary>
            /// <param name="itemID">The ID of the item to update.</param>
            /// <param name="toStatus">The new status of the item.</param>
            /// <param name="hasRights">The bool that indicates if the user that signs this item has rights.</param>
            internal void toggleItemStatus(int itemID, string toStatus, bool hasRights)
            {
                if (_Items.ContainsKey(itemID) == false)
                    return;

                genericItem Item = (genericItem)_Items[itemID];
                string itemSprite = Item.Sprite;
                if (itemSprite == "edice" || itemSprite == "edicehc" || itemSprite == "gdice" || itemSprite == "tdice" || itemSprite == "pdice" || stringManager.getStringPart(itemSprite, 0, 11) == "prizetrophy" || stringManager.getStringPart(itemSprite, 0, 7) == "present") // Items that can't be signed [this regards dicerigging etc]
                    return;
                if (hasRights) // Rights check
                {
                    if (toStatus.ToLower() == "c" || toStatus.ToLower() == "o") // Door
                    {

                        #region Open/close doors
                        itemTemplate Template = Item.template;
                        if (Template.isDoor == false)
                            return;
                        int Length = 1;
                        int Width = 1;
                        if (Template.Length > 1 || Template.Width > 1)
                        {
                            if (Item.Z == 2 || Item.Z == 6)
                            {
                                Length = Template.Length;
                                Width = Template.Width;
                            }
                            else
                            {
                                Length = Template.Width;
                                Width = Template.Length;
                            }
                        }
                        if (toStatus.ToLower() == "c")
                        {
                            if (_Room.squareBlocked(Item.X, Item.Y, Length, Width))
                                return;
                            _Room.setSquareState(Item.X, Item.Y, Length, Width, squareState.Blocked);
                        }
                        else
                            _Room.setSquareState(Item.X, Item.Y, Length, Width, squareState.Open);
                        Item.Var = toStatus;
                        _Room.sendData("AX" + itemID + Convert.ToChar(2) + toStatus + Convert.ToChar(2));
                        Item.updated = true;
                        this._Room.itemsUpdated = true;
                        #endregion

                        return;
                    }

                    Item.Var = toStatus;
                    Item.updated = true;
                    this._Room.itemsUpdated = true;
                    _Room.sendData("AX" + itemID + Convert.ToChar(2) + toStatus + Convert.ToChar(2));
                }

            }
            #endregion

            #region getters

            #region normal getters
            /// <summary>
            /// Returns all floor items
            /// </summary>
            /// <returns></returns>
            public Hashtable getAllFloorItems()
            {
                return (Hashtable)_Items.Clone();
            }

            /// <summary>
            /// Returns a string with all the virtual wallitems in this item manager.
            /// </summary>
            internal string Items
            {
                get
                {
                    StringBuilder itemList = new StringBuilder(Encoding.encodeVL64(_Items.Count));
                    lock (_Items)
                    {
                        Hashtable items = (Hashtable)_Items.Clone();
                        foreach (genericItem Item in items.Values)
                            itemList.Append(Item.floorItemToString());
                    }
                    return itemList.ToString();
                }
            }
            #endregion

            #region pet-item getters
            /// <summary>
            /// Returns a bool that indicates if the item manager contains a certain virtual flooritem.
            /// </summary>
            /// <param name="itemID">The ID of the item to check.</param>
            internal bool containsItem(int itemID)
            {
                return _Items.ContainsKey(itemID);
            }
            /// <summary>
            /// Returns the floorItem object of a certain virtual flooritem in the item manager.
            /// </summary>
            /// <param name="itemID">The ID of the item to get the floorItem object of.</param>
            internal genericItem getItem(int itemID)
            {
                try
                {
                    return (genericItem)_Items[itemID];
                }
                catch { return null; }
            }

            /// <summary>
            /// Searches for food for a pet
            /// </summary>
            internal genericItem getPetFoodItem(int petTypeId)
            {
                lock (_Items)
                {
                    foreach (genericItem item in _Items.Values)
                    {
                        if (item.template.isPetFood && item.template.petFoodID == petTypeId)
                            return item;
                    }
                }
                return null;
            }

            /// <summary>
            /// Searches for food for a pet
            /// </summary>
            internal genericItem getPetDrinkItem()
            {
                lock (_Items)
                {
                    foreach (genericItem item in _Items.Values)
                    {
                        if (item.template.isPetDrink)
                            return item;
                    }
                }
                return null;
            }

            /// <summary>
            /// Searches for food for a pet
            /// </summary>
            internal genericItem getPetPlayItem()
            {
                lock (_Items)
                {
                    foreach (genericItem item in _Items.Values)
                    {
                        if (item.template.isPetToy)
                            return item;
                    }
                }
                return null;
            }

            /// <summary>
            /// Searches for food for a pet
            /// </summary>
            internal genericItem getPetRandomItem()
            {
                if (this._Items.Count > 0)
                {
                    int rnd = new Random().Next(0, _Items.Count);
                    int i = 0;
                    lock (_Items)
                    {
                        foreach (genericItem item in _Items.Values)
                        {
                            if (i == rnd)
                                return item;
                            i++;
                        }
                    }
                    return null;
                }
                else
                {
                    return null;
                }
            }
            #endregion

            #endregion
        }
        #endregion

        #region wallitem manager
        /// <summary>
        /// Provides management for virtual wallitems in a virtual room.
        /// </summary>
        internal class WallItemManager
        {
            #region declares
            private virtualRoom _Room;
            private Hashtable _Items = new Hashtable();
            #endregion

            #region constructor/destructor
            /// <summary>
            /// Initializes the manager.
            /// </summary>
            /// <param name="Room">The parent room.</param>
            public WallItemManager(virtualRoom Room)
            {
                this._Room = Room;
            }
            /// <summary>
            /// Removes all the items from the item manager and destructs all objects inside.
            /// </summary>
            internal void Clear()
            {
                try { _Items.Clear(); }
                catch { }
                _Room = null;
                _Items = null;
            }
            #endregion

            #region item adding/removing
            /// <summary>
            /// Adds a virtual wallitem to the item manager and optionally makes it appear in the room.
            /// </summary>
            /// <param name="itemID">The ID of the item to add.</param>
            /// <param name="Item">The item to add.</param>
            /// <param name="Place">Indicates if the item is put in the room now, so updating database and sending appear packet to room.</param>
            internal bool addItem(genericItem Item, bool Place)
            {
                int itemID = Item.ID;
                if (_Items.ContainsKey(itemID) == false)
                {
                    if (Place)
                    {
                        if (Item.template.isMoodLight)
                        {
                            foreach (genericItem item in this._Items.Values)
                            {
                                if (item.template.isMoodLight)
                                    return false;
                            }
                            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                            {
                                if (dbClient.findsResult("SELECT roomid FROM furniture_moodlight WHERE id='" + itemID + "'") == false)
                                {
                                    dbClient.runQuery("INSERT INTO furniture_moodlight VALUES ('" + itemID + "','" + _Room.roomID + "','1','1,#000000,225','1,#000000,200','1,#000000,175')");
                                }
                                else
                                {
                                    dbClient.runQuery("UPDATE furniture_moodlight SET roomid = '" + _Room.roomID + "' WHERE id= '" + itemID + "'");
                                }
                            }
                        }
                        //dbClient.runQuery("INSERT INTO room_furniture (room_id, furniture_id) VALUES  ('" + _Room.roomID + "', '" + itemID + "')");
                        //dbClient.runQuery("DELETE FROM user_ furniture WHERE furniture_id = '" + itemID + "'");
                        _Room.itemKeeper.addDatabaseEntry(Item);
                        _Room.sendData("AS" + Item.wallItemToString());
                        Item.updated = true;
                        this._Room.itemsUpdated = true;
                    }
                    else
                    {
                        _Room.itemKeeper.addStartItem(Item);
                    }


                    _Items.Add(itemID, Item);
                    return true;
                }
                return false;
            }


            /// <summary>
            /// Removes a virtual wallitem from the item manager, updates the database row/drops the item from database and makes it disappear in the room.
            /// </summary>
            /// <param name="itemID">The ID of the item to remove.</param>
            /// <param name="ownerID">The ID of the user that owns this item. If 0, then the item will be dropped from the database.</param>
            internal genericItem removeItem(int itemID, int ownerID)
            {
                if (_Items.ContainsKey(itemID))
                {
                    _Room.sendData("AT" + itemID);

                    genericItem backupItem = (genericItem)_Items[itemID];
                    _Items.Remove(itemID);

                    _Room.itemKeeper.deleteDatabaseEntry(backupItem);
                    if (ownerID > 0)
                    {
                        if (backupItem.template.isMoodLight)
                        {
                            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                            {
                                dbClient.runQuery("DELETE FROM furniture_moodlight where id = '" + itemID + "'");
                            }
                        }
                        return backupItem;
                    }
                    else
                    {

                        using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                        {
                            dbClient.runQuery("DELETE FROM furniture WHERE id = '" + itemID + "'");
                            if (backupItem.template.isMoodLight)
                            {
                                dbClient.runQuery("DELETE FROM furniture_moodlight where id = '" + itemID + "'");
                            }
                        }

                    }

                }
                return null;
            }
            #endregion

            #region wallitem handeling
            /// <summary>
            /// Updates the status of a virtual wallitem and updates it in the virtual room and in the database. Certain items can't switch status by this way, and they will be ignored to prevent exploiting.
            /// </summary>
            /// <param name="itemID">The ID of the item to update.</param>
            /// <param name="toStatus">The new status of the item.</param>
            internal void toggleItemStatus(int itemID, int toStatus)
            {
                if (_Items.ContainsKey(itemID) == false)
                    return;

                genericItem Item = (genericItem)_Items[itemID];
                string itemSprite = Item.Sprite;
                if (itemSprite == "roomdimmer" || itemSprite == "post.it" || itemSprite == "post.it.vd" || itemSprite == "poster" || itemSprite == "habbowheel")
                    return;

                Item.Var = toStatus.ToString();
                Item.updated = true;
                this._Room.itemsUpdated = true;
                _Room.sendData("AU" + itemID + Convert.ToChar(9) + itemSprite + Convert.ToChar(9) + " " + Item.wallpos + Convert.ToChar(9) + Item.Var);
            }
            /// <summary>
            /// refreshWallItem, taken from the Old VB.NET source. [Only used by Moodlight]
            /// </summary>
            internal void refreshWallitem(int itemID, string cctName, string wallPosition, string itemVariable)
            {
                _Room.sendData("AU" + itemID + Convert.ToChar(9) + cctName + Convert.ToChar(9) + " " + wallPosition + Convert.ToChar(9) + itemVariable);
            }
            #endregion

            #region getters and setters

            /// <summary>
            /// Gets all wallitems in a cloned hashtable
            /// </summary>
            /// <returns>cloned hashtable containing all items</returns>
            public Hashtable getAllWallItems()
            {
                return (Hashtable)_Items.Clone();
            }

            /// <summary>
            /// Returns a bool that indicates if the item manager contains a certain virtual wallitem.
            /// </summary>
            /// <param name="itemID">The ID of the item to check.</param>
            internal bool containsItem(int itemID)
            {
                return _Items.ContainsKey(itemID);
            }
            /// <summary>
            /// Returns the wallItem object of a certain virtual wallitem in the item manager.
            /// </summary>
            /// <param name="itemID">The ID of the item to get the wallItem object of.</param>
            internal genericItem getItem(int itemID)
            {
                return (genericItem)_Items[itemID];
            }

            /// <summary>
            /// Returns a string with all the virtual wallitems in this item manager.
            /// </summary>
            internal string Items
            {
                get
                {
                    StringBuilder itemList = new StringBuilder();
                    try
                    {
                        lock (_Items)
                        {
                            foreach (genericItem Item in _Items.Values)
                                itemList.Append(Item.wallItemToString() + Convert.ToChar(13));
                        }
                    }
                    catch { }
                    return itemList.ToString();
                }
            }
            #endregion

            #region moodlight

            /// <summary>
            /// Gets the setting of the moodlight in the current room
            /// </summary>
            internal string moodLight_GetSettings(int roomID)
            {
                try
                {
                    string[] itemSettings;
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        itemSettings = (string[])dbClient.getRow("SELECT preset_cur,preset_1,preset_2,preset_3 FROM furniture_moodlight WHERE roomid ='" + roomID + "'").ItemArray;
                    }
                    string settingPack = (Encoding.encodeVL64(3) + Encoding.encodeVL64(Convert.ToInt32(itemSettings[0])));
                    for (int i = 1; i <= 3; i++)
                    {
                        string[] curPresetData = itemSettings[i].Split(',');
                        settingPack = (settingPack + Encoding.encodeVL64(i) + Encoding.encodeVL64(Convert.ToInt32(curPresetData[0])) + curPresetData[1] + Convert.ToChar(2) + Encoding.encodeVL64(Convert.ToInt32(curPresetData[2])));
                    }
                    return settingPack;
                }
                catch
                {
                    return "";
                }
            }

            /// <summary>
            /// Sets the Settings of the Moodlight in the current room
            /// </summary>
            /// <param name="isEnabled"></param>
            /// <param name="presetID"></param>
            /// <param name="bgState"></param>
            /// <param name="presetColour"></param>
            /// <param name="alphaDarkF"></param>
            /// <param name="roomID"></param>
            internal void moodLight_SetSettings(bool isEnabled, int presetID, int bgState, string presetColour, int alphaDarkF, int roomID)
            {
                int itemID;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    itemID = dbClient.getInt("SELECT id FROM furniture_moodlight WHERE roomid ='" + roomID + "'");
                }
                string newPresetValue;
                if (isEnabled == false)
                {
                    string curPresetValue;
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        curPresetValue = dbClient.getString("SELECT var FROM furniture WHERE id ='" + itemID + "'");

                        if (curPresetValue.Substring(0, 1) == "2")
                        {
                            newPresetValue = ("1" + curPresetValue.Substring(1));
                        }
                        else
                        {
                            newPresetValue = ("2" + curPresetValue.Substring(1));
                        }
                        dbClient.AddParamWithValue("preset", newPresetValue);
                        dbClient.runQuery("UPDATE furniture SET var = @preset WHERE id ='" + itemID + "' LIMIT 1");
                    }
                }
                else
                {
                    newPresetValue = ("2," + presetID + "," + bgState + "," + presetColour + "," + alphaDarkF);
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        dbClient.AddParamWithValue("preset", newPresetValue);
                        dbClient.runQuery("UPDATE furniture SET var = @preset WHERE id ='" + itemID + "' LIMIT 1");
                        dbClient.runQuery("UPDATE furniture_moodlight SET preset_cur ='" + presetID + "',preset_" + presetID + " ='" + bgState + "," + presetColour + "," + alphaDarkF + "' WHERE id = '" + itemID + "' LIMIT 1");
                    }
                }
                DataRow dRow;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dRow = dbClient.getRow("SELECT wallpos,var FROM furniture WHERE id = '" + itemID + "'");
                }
                string wallPosition = Convert.ToString(dRow["wallpos"]);
                refreshWallitem(itemID, "roomdimmer", wallPosition, Convert.ToString(dRow["var"]));
            }

            #endregion
        }
        #endregion

        #endregion

        #region Special publicroom additions
        /// <summary>
        /// returns if there is an active specialcast with the emitter in it
        /// </summary>
        /// <param name="emiterValue">The value of the emitter in question</param>
        public bool hasSpecialCasts(string emiterValue)
        {
            if (emitter != null)
            {
                if (emitter == emiterValue)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        internal void stopDiving()
        {
            DiveDoorOpen = true;
            sendData("AGdoor open");
            sqUNIT[26, 3] = false;
            sqUNIT[26, 4] = false;
        }

        /// <summary>
        /// Threaded. Handles special casts such as disco lamps etc in the virtual room.
        /// </summary>
        /// <param name="o">The room model name as a System.Object.</param>
        private void handleSpecialCasts(object o)
        {
            try
            {
                DataRow dRow;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dRow = dbClient.getRow("SELECT specialcast_emitter,specialcast_interval,specialcast_rnd_min, specialcast_rnd_max FROM room_modeldata WHERE model = '" + (string)o + "'");
                }
                emitter = Convert.ToString(dRow["specialcast_emitter"]);
                int Interval = Convert.ToInt32(dRow["specialcast_interval"]);
                int rndMin = Convert.ToInt32(dRow["specialcast_rnd_min"]);
                int rndMax = Convert.ToInt32(dRow["specialcast_rnd_max"]);
                dRow = null;

                string prevCast = "";
                while (true)
                {
                    string Cast = "";
                    int RND = new Random().Next(rndMin, rndMax + 1);
                    //reCast:
                    if (emitter == "cam1") // User camera system
                    {
                        switch (RND)
                        {

                            case 1:
                                int roomUID = getRandomRoomIdentifier();
                                if (roomUID != -1)
                                    Cast = "targetcamera " + roomUID;
                                break;
                            case 2:
                                Cast = "setcamera 1";
                                break;
                            case 3:
                                Cast = "setcamera 2";
                                break;
                            default:
                                roomUID = getRandomRoomIdentifier();
                                if (roomUID != -1)
                                    Cast = "targetcamera " + roomUID;
                                break;
                        }
                    }
                    else if (emitter == "sf") // Flashing dancetiles system
                        Cast = RND.ToString();
                    else if (emitter == "lamp") // Discolights system
                        Cast = "setlamp " + RND;

                    if (Cast == "")
                    {
                        break;
                    }
                    if (Cast != prevCast) // Cast is not the same as previous cast
                    {
                        sendSpecialCast(emitter, Cast);
                        prevCast = Cast;
                    }
                    Thread.Sleep(Interval);
                }
            }
            catch { }
        }
        #endregion

        #region room trashing
        /// <summary>
        /// crashes the current room
        /// </summary>
        internal void Crash()
        {
            try
            {
                #region User kicking
                Hashtable users = (Hashtable)_Users.Clone();
                try
                {
                    foreach (virtualRoomUser roomUser in users.Values)
                    {
                        try
                        {
                            if (roomUser.User._tradePartner != null)
                                roomUser.User.abortTrade();
                            roomUser.User.sendData("@R");
                            roomUser.User.statusManager.Clear();
                            roomUser.User.statusManager = null;
                            roomUser.User._roomID = 0;
                            roomUser.User._inPublicroom = false;
                            roomUser.User._ROOMACCESS_PRIMARY_OK = false;
                            roomUser.User._ROOMACCESS_SECONDARY_OK = false;
                            roomUser.User._isOwner = false;
                            roomUser.User._hasRights = false;
                            roomUser.User.Room = null;
                            roomUser.Diving = false;
                            roomUser.User._WSVariables = null;
                            roomUser.User.roomUser = null;

                            if (roomUser.User.gamePlayer != null && roomUser.User.gamePlayer.enteringGame == false)
                                roomUser.User.leaveGame();
                            roomUser.User.sendNotify("Deze kamer is nu verwijderd..");
                        }
                        catch { }
                    }
                }
                catch { }


                foreach (virtualBot roomBot in _Bots.Values)
                    roomBot.Kill();

                _Users.Clear();
                _Bots.Clear();
                #endregion

                itemKeeper.destroy();

                _statusUpdates = null;
                if (isPublicroom == false)
                {
                    floorItemManager.Clear();
                    wallItemManager.Clear();
                }
                else
                {
                    if (Lobby != null)
                        Lobby.Clear();
                    Lobby = null;
                }

                roomManager.removeRoom(this.roomID, 0);
                _statusHandler.Abort();
            }
            catch { }
        }
        #endregion

        #region Private classes
        /// <summary>
        /// Represents a stack of virtual flooritems.
        /// </summary>
        private class furnitureStack
        {
            private int[] _itemIDs;
            /// <summary>
            /// Initializes a new stack.
            /// </summary>
            internal furnitureStack()
            {
                _itemIDs = new int[20];
            }
            /// <summary>
            /// Adds an item ID to the top position of the stack.
            /// </summary>
            /// <param name="itemID">The item ID to add.</param>
            internal void Add(int itemID)
            {
                for (int i = 0; i < 20; i++)
                {
                    if (_itemIDs[i] == 0)
                    {
                        _itemIDs[i] = itemID;
                        return;
                    }
                }
            }

            /// <summary>
            /// Removes an item ID from the stack and shrinks empty spots. [order is kept the same]
            /// </summary>
            /// <param name="itemID">The item ID to remove.</param>
            internal void Remove(int itemID)
            {
                for (int i = 0; i < 20; i++)
                    if (_itemIDs[i] == itemID)
                    {
                        _itemIDs[i] = 0;
                        break;
                    }

                int g = 0;
                int[] j = new int[20];
                for (int i = 0; i < 20; i++)
                    if (_itemIDs[i] > 0)
                    {
                        j[g] = _itemIDs[i];
                        g++;
                    }
                _itemIDs = j;
            }

            /// <summary>
            /// The most top item ID of the stack.
            /// </summary>
            internal int topItemID()
            {
                for (int i = 0; i < 20; i++)
                    if (_itemIDs[i] == 0)
                        return _itemIDs[i - 1];
                return 0;
            }

            /// <summary>
            /// The lowest located [so: first added] item ID of the stack.
            /// </summary>
            internal int bottomItemID()
            {
                return _itemIDs[0];
            }

            /// <summary>
            /// Returns the item ID located above a given item ID.
            /// </summary>
            /// <param name="aboveID">The item ID to get the item ID above of.</param>
            internal int getAboveItemID(int aboveID)
            {
                for (int i = 0; i < 20; i++)
                    if (_itemIDs[i] == aboveID)
                        return _itemIDs[i + 1];
                return 0;
            }

            /// <summary>
            /// Returns the item ID located below a given item ID.
            /// </summary>
            /// <param name="belowID">The item ID to get the item ID below of.</param>
            internal int getBelowItemID(int belowID)
            {
                for (int i = 0; i < 20; i++)
                    if (_itemIDs[i] == belowID)
                        return _itemIDs[i - 1];
                return 0;
            }

            /// <summary>
            /// Returns a bool that indicates if the stack contains a certain item ID.
            /// </summary>
            /// <param name="itemID">The item ID to check.</param>
            internal bool Contains(int itemID)
            {
                foreach (int i in _itemIDs)
                    if (i == itemID)
                        return true;

                return false;
            }

            /// <summary>
            /// The amount of item ID's in the stack.
            /// </summary>
            internal int Count
            {
                get
                {
                    int j = 0;
                    for (int i = 0; i < 20; i++)
                    {
                        if (_itemIDs[i] > 0)
                            j++;
                        else
                            return j;
                    }
                    return j;
                }
            }
        }
        #endregion

        #region furni items
        /// <summary>
        /// Closes a dice
        /// </summary>
        /// <param name="itemID">the itemID of the dice</param>
        /// <param name="roomUser">the romuser which triggered the event</param>
        internal void closeDice(int itemID, virtualRoomUser roomUser)
        {
            if (floorItemManager.containsItem(itemID))
            {
                genericItem Item = floorItemManager.getItem(itemID);
                string Sprite = Item.Sprite;
                if (Sprite != "edice" && Sprite != "edicehc" && Sprite != "gdice" && Sprite != "tdice" && Sprite != "pdice")  // Not a dice item
                    return;

                if (!(Math.Abs(roomUser.X - Item.X) > 1 || Math.Abs(roomUser.Y - Item.Y) > 1)) // User is not more than one square removed from dice
                {
                    Item.Var = "0";
                    this.sendData("AZ" + itemID + " " + (itemID * 38));
                    Item.updated = true;
                    itemsUpdated = true;
                }
            }
        }

        /// <summary>
        /// throws a dice if the user is next to the dice
        /// </summary>
        /// <param name="itemID">the itemID of the dice</param>
        /// <param name="roomUser">the romuser which triggered the event</param>
        internal void throwDice(int itemID, virtualRoomUser roomUser)
        {
            if (floorItemManager.containsItem(itemID))
            {
                genericItem Item = floorItemManager.getItem(itemID);

                if (Item.Sprite != "edice" && Item.Sprite != "edicehc" && Item.Sprite != "slot" && Item.Sprite != "rdice" && Item.Sprite != "gdice" && Item.Sprite != "tdice" && Item.Sprite != "pdice" && Item.Sprite != "JA1x1NNNNN") // Not a dice item
                    return;

                if (!(Math.Abs(roomUser.X - Item.X) > 1 || Math.Abs(roomUser.Y - Item.Y) > 1)) // User is not more than one square removed from dice
                {
                    this.sendData("AZ" + itemID);

                    int rndNum = new Random(DateTime.Now.Millisecond).Next(1, 7);
                    this.sendData("AZ" + itemID + " " + ((itemID * 38) + rndNum), 2000);
                    Item.Var = rndNum.ToString();
                    Item.updated = true;
                    itemsUpdated = true;
                }
            }
        }


        /// <summary>
        /// spins a habbowheel
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="roomUser"></param>
        internal void spinWheel(int itemID, virtualRoomUser roomUser)
        {
            if (this.wallItemManager.containsItem(itemID))
            {
                genericItem Item = this.wallItemManager.getItem(itemID);
                if (Item.Sprite == "habbowheel")
                {
                    int rndNum = new Random(DateTime.Now.Millisecond).Next(0, 10);
                    this.sendData("AU" + itemID + Convert.ToChar(9) + "habbowheel" + Convert.ToChar(9) + " " + Item.wallpos + Convert.ToChar(9) + "-1");
                    this.sendData("AU" + itemID + Convert.ToChar(9) + "habbowheel" + Convert.ToChar(9) + " " + Item.wallpos + Convert.ToChar(9) + rndNum, 4250);
                    Item.updated = true;
                    itemsUpdated = true;
                }
            }
        }
        #endregion

        #region furni saving
        /// <summary>
        /// saves furni's in this roomw hich have been modified
        /// </summary>
        internal void saveFurniStatus()
        {
            lock (this)
            {
                if (itemsUpdated)
                {
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        saveFurniStatus(dbClient);
                    }
                }
            }
        }

        /// <summary>
        /// Saves furni statusses with a pre-defined furni status
        /// </summary>
        /// <param name="dbClient"></param>
        internal void saveFurniStatus(DatabaseClient dbClient)
        {
            if (itemsUpdated)
            {
                Hashtable furnis = wallItemManager.getAllWallItems();

                lock (wallItemManager)
                {
                    foreach (genericItem item in furnis.Values)
                    {
                        item.storeChanges(dbClient);
                    }
                }

                furnis = floorItemManager.getAllFloorItems();
                lock (floorItemManager)
                {
                    foreach (genericItem item in furnis.Values)
                    {
                        item.storeChanges(dbClient);
                    }
                }
                itemsUpdated = false;
            }
        }
        #endregion

        #region getters and setters
        /// <summary>
        /// Returns the heightmap of this virtual room.
        /// </summary>
        internal string Heightmap
        {
            get
            {
                return model.heightMap;
            }
        }

        /// <summary>
        /// Returns a string with all the virtual flooritems in this room.
        /// </summary>
        internal string Flooritems
        {
            get
            {
                if (isPublicroom) // check for lido tiles
                    return "H";

                return floorItemManager.Items;
            }
        }

        /// <summary>
        /// Returns a string with all the virtual wallitems in this room.
        /// </summary>
        internal string Wallitems
        {
            get
            {
                if (isPublicroom)
                    return "";

                return wallItemManager.Items;
            }
        }
        /// <summary>
        /// Returns a string with all the virtual publicroom items in this virtual room.
        /// </summary>
        internal string PublicroomItems
        {
            get
            {
                return _publicroomItems;
            }
        }

        /// <summary>
        /// Returns a bool that indicates if the room contains a certain room user.
        /// </summary>
        /// <param name="roomUID">The ID that identifies the user in the virtual room.</param>
        internal bool containsUser(int roomUID)
        {
            return _Users.ContainsKey(roomUID);
        }
        /// <summary>
        /// The values of the _Users Hashtable as an ICollection object.
        /// </summary>
        internal ICollection Users
        {
            get
            {
                return _Users.Values;
            }
        }

        /// <summary>
        /// Returns a room identifier ID that isn't used by a virtual unit in this virtual room yet.
        /// </summary>
        /// <returns></returns>
        internal int getFreeRoomIdentifier()
        {
            int i = 1;
            while (true)
            {
                if (!_activeRoomIndentifiers.Contains(i))
                {
                    _activeRoomIndentifiers.Add(i);
                    return i;
                }
                else
                    i++;
            }
        }
        /// <summary>
        /// Returns a room identifier of a virtual unit in this room, by picking a unit at random. If there are no units in the room, then -1 is returned.
        /// </summary>
        /// <returns></returns>
        private int getRandomRoomIdentifier()
        {
            if (_Users.Count > 0)
            {
                while (true)
                {
                    int rndID = new Random(DateTime.Now.Millisecond).Next(0, _Users.Count);
                    if (_Bots.ContainsKey(rndID) || _Users.ContainsKey(rndID))
                        return rndID;
                }
            }
            else
                return -1;
        }
        /// <summary>
        /// Returns the virtualUser object of a room user.
        /// </summary>
        /// <param name="roomUID">The room identifier of the user.</param>
        internal virtualUser getUser(int roomUID)
        {
            return ((virtualRoomUser)_Users[roomUID]).User;
        }
        /// <summary>
        /// Returns the virtualRoomUser object of a room user.
        /// </summary>
        /// <param name="roomUID">The room identifier of the user.</param>
        internal virtualRoomUser getRoomUser(int roomUID)
        {
            return ((virtualRoomUser)_Users[roomUID]);
        }

        /// <summary>
        /// The details string for all the virtual units in this room.
        /// </summary>
        /// <summary>
        /// The details string for all the virtual units in this room.
        /// </summary>
        internal string dynamicUnits
        {
            get
            {
                lock (this)
                {
                    StringBuilder userList = new StringBuilder();
                    foreach (virtualBot roomBot in _Bots.Values)
                        userList.Append(roomBot.detailsString);
                    foreach (virtualRoomUser roomUser in _Users.Values)
                        userList.Append(roomUser.detailsString);
                    foreach (roomPet pet in _Pets.Values)
                        userList.Append(pet.ToString());
                    return userList.ToString();
                }

            }
        }
        /// <summary>
        /// The status string of all the virtual units in this room.
        /// </summary>
        internal string dynamicStatuses
        {
            get
            {
                lock (this)
                {
                    StringBuilder Statuses = new StringBuilder();
                    foreach (virtualBot roomBot in _Bots.Values)
                        Statuses.Append(roomBot.statusString + Convert.ToChar(13));

                    foreach (virtualRoomUser roomUser in _Users.Values)
                        Statuses.Append(roomUser.statusString + Convert.ToChar(13));

                    foreach (roomPet pet in _Pets.Values)
                        Statuses.Append(pet.statusString);

                    return Statuses.ToString();
                }
            }
        }
        /// <summary>
        /// The usernames of all the virtual users in this room.
        /// </summary>
        internal string Userlist
        {
            get
            {
                StringBuilder listBuilder = new StringBuilder(Encoding.encodeVL64(this.roomID) + Encoding.encodeVL64(_Users.Count));
                lock (_Users)
                {
                    foreach (virtualRoomUser roomUser in _Users.Values)
                        listBuilder.Append(roomUser.User._Username + Convert.ToChar(2));
                }
                return listBuilder.ToString();
            }
        }
        /// <summary>
        /// The IDs and badge strings of all the active user groups in this room.
        /// </summary>
        internal string Groups
        {
            get
            {
                StringBuilder listBuilder = new StringBuilder(Encoding.encodeVL64(_activeGroups.Count));
                foreach (KeyValuePair<int, string> s in _activeGroups)
                    listBuilder.Append(Encoding.encodeVL64(s.Key) + Convert.ToString(s.Value) + Convert.ToChar(2));
                return listBuilder.ToString();
            }
        }

        /// <summary>
        /// Returns the owner of the room
        /// </summary>
        /// <returns></returns>
        internal string getOwner()
        {
            return room.getOwner();
        }

        /// <summary>
        /// Gets the category this room is in
        /// </summary>
        /// <returns></returns>
        internal int getCategory()
        {
            return room.getCategory();
        }

        /// <summary>
        /// Gets an indicator of this room to show the name or not
        /// </summary>
        /// <returns></returns>
        internal int getShowName()
        {
            return room.getShowName();
        }

        /// <summary>
        /// Returns the state of this room
        /// </summary>
        internal int getState()
        {
            return room.getState();
        }

        /// <summary>
        /// Returns the room model
        /// </summary>
        internal string getModel()
        {
            return room.getModel();
        }

        /// <summary>
        /// Returns the name of the room
        /// </summary>
        /// <returns></returns>
        internal string getName()
        {
            return room.getName();
        }

        /// <summary>
        /// Returns the description of this room
        /// </summary>
        /// <returns></returns>
        internal string getDescription()
        {
            return room.getDescription();
        }

        /// <summary>
        /// Gets the max-inside people
        /// </summary>
        /// <returns></returns>
        internal int getMaxInside()
        {
            return room.getMaxInside();
        }

        /// <summary>
        /// Gets the current inside room count
        /// </summary>
        internal int getCurrentInside()
        {
            return this._Users.Count;
        }

        /// <summary>
        /// Gets the total vote count
        /// </summary>
        /// <returns></returns>
        internal int getVoteCount()
        {
            if (_votesTotal < 0)
                return 0;
            return this._votesTotal;
        }

        /// <summary>
        /// Checks if a user has voted or not.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        internal bool hasUserVoted(int userID)
        {
            return _votes.Contains(userID);
        }


        internal void voteForRoom(int userID, int vote)
        {
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.runQuery("INSERT INTO room_votes (userid,roomid,vote) VALUES ('" + userID + "','" + this.roomID + "','" + vote + "')");
            }
            _votes.Add(userID);
            _votesTotal += vote;

        }


        #endregion

        #region room quiet operations
        /// <summary>
        /// Creates ghosts in this room, all users are converted into a ghost.
        /// </summary>
        /// <param name="doGhost">True for ghosts, false for no ghosts</param>
        internal void makeGhosts(bool doGhost)
        {
            lock (_Users)
            {
                foreach (virtualRoomUser user in _Users.Values)
                {
                    user.User.makeGenie(doGhost);
                }
            }
        }

        /// <summary>
        /// Sets this room to a quiet state (for every one in this room lower than rank 4)
        /// </summary>
        internal void setQuiet(bool quiet)
        {
            this.roomQuiet = quiet;
            lock (this)
            {
                if (quiet)
                {
                    foreach (virtualRoomUser roomUser in _Users.Values)
                    {
                        if (roomUser.User._Rank < 5)
                            roomUser.allowedToTalk = false;
                    }
                }
                else
                {
                    foreach (virtualRoomUser roomUser in _Users.Values)
                    {
                        roomUser.allowedToTalk = true;
                    }
                }
            }
        }
        #endregion

        #region pets

        /// <summary>
        /// gets a room pet
        /// </summary>
        /// <param name="petID">the ID of the pet</param>
        /// <returns>The pet or NULL when there's nothing to be found</returns>
        internal roomPet getRoomPet(int petID)
        {
            foreach (roomPet pet in _Pets.Values)
            {
                if (pet.Information.ID == petID)
                    return pet;
            }
            return null;
        }

        /// <summary>
        /// Unloads a pet from the room
        /// </summary>
        /// <param name="id">The ID of the pet</param>
        private void unloadPet(int id)
        {
            if (!petsToUnload.Contains(id))
                petsToUnload.Add(id);
        }

        /// <summary>
        /// Unloads all pets
        /// The unloadPets should be locked before use
        /// This is used for leaving the room and in the cycleStatus
        /// </summary>
        private void unloadPetsInList()
        {
            foreach (int id in petsToUnload)
            {
                roomPet pet = getRoomPet(id);
                if (pet == null)
                    continue;

                sqUNIT[pet.X, pet.Y] = false;
                sendData("@]" + pet.roomUID);
                pet.Information.Update();
                _Pets.Remove(pet.Information.Name);
            }
            petsToUnload.Clear();
        }

        /// <summary>
        /// Adds a pet to the room
        /// </summary>
        /// <param name="petID">The int of the pet</param>
        /// <returns>int with an error indicator</returns>
        internal int loadPet(int petID)
        {
            if (_Pets.Count >= 3)
                return 1;
            else
            {
                DataRow dRow;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dRow = dbClient.getRow("SELECT * FROM furniture_pets WHERE id = " + petID);
                }
                roomPet pet = new roomPet(virtualPetInformation.Parse(dRow), getFreeRoomIdentifier(), this);
                return loadPet(pet);
            }
        }

        /// <summary>
        /// Loads a pet into the room
        /// </summary>
        /// <param name="petID">the ID of the pet</param>
        /// <param name="X">the X location of the pet</param>
        /// <param name="Y">the Y location of the pet</param>
        /// <returns>indicator if the pet could be loaded</returns>
        internal int loadPet(int petID, byte X, byte Y)
        {
            if (_Pets.Count >= 3)
                return 1;
            else
            {
                DataRow dRow;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dRow = dbClient.getRow("SELECT * FROM furniture_pets WHERE id = " + petID);
                }
                roomPet pet = new roomPet(virtualPetInformation.Parse(dRow), getFreeRoomIdentifier(), this, (byte)X, (byte)Y);
                return loadPet(pet);

            }
        }
        /// <summary>
        /// Loads a pet
        /// </summary>
        /// <param name="pet">The Room Pet pet</param>
        private int loadPet(roomPet pet)
        {
            if (_Pets.Contains(pet.Information.Name))
                return 2;
            _Pets.Add(pet.Information.Name, pet);

            sendData(@"@\" + pet.ToString());
            _statusUpdates.Append(pet.statusString);
            return 0;
        }

        /// <summary>
        /// Marks an item as "playing" for a X amount of seconds
        /// </summary>
        /// <param name="item">The item in the collection</param>
        /// <param name="time">The playtime in seconds</param>
        internal void doFurniPlay(genericItem item, int time)
        {
            if (!this.floorItemManager.containsItem(item.ID))
                return;
            if (item.template.isPetToy)
            {
                item.setItemStatus("1", time);
                sendData("A_" + item.floorItemToString());
                for (int i = 0; i < toyItems.Length; i++)
                {
                    if (toyItems[i] == null)
                    {
                        if (toyItems[i] == item)
                            continue;
                        toyItems[i] = item;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Deminishes the amount an item van be eaten.
        /// </summary>
        /// <param name="item">the pet-food item</param>
        internal void petEatsItem(genericItem item)
        {
            if (!floorItemManager.containsItem(item.ID))
                return;
            if (item.template.isPetFood)
            {
                int amount = Encoding.decodeVL64(item.Var);
                if (amount < item.template.numberOfUses - 1)
                {
                    amount++;
                    item.Var = Encoding.encodeVL64(amount);
                    sendData("A]" + item.floorItemToString());
                }
                else
                    floorItemManager.removeItem(item.ID, 0);
            }
            else
            {
                floorItemManager.removeItem(item.ID, 0);
            }
        }
        #endregion

    }
}
