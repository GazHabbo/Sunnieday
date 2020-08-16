using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Holo;



using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Holo.Managers;
using Ion.Storage;
using Holo.Virtual.Rooms;
using Holo.Source.Managers.RoomManager;
using Holo.Source.Virtual.Rooms;

namespace Holo.Source.Managers
{
    public static class navigatorManager
    {
        #region collections
        private static Dictionary<int, int> type;
        private static Dictionary<int, int> parent;
        private static Dictionary<int, string> name;
        private static Dictionary<byte, string> categoryIndex;
        private static Dictionary<string, string> navigatorCategories;        
        private static Dictionary<string, string> roomsInsideCategories;
        private static Dictionary<string, string> roomAccesName;
        private static Dictionary<string, List<int>> roomAccesParent;
        private static Dictionary<string, DataTable> guestRooms;
        private static Dictionary<int, bool> tradeFloor;
        private static string Rooms;
        private static bool renewRandomRooms;
        #endregion

        #region updater threads
        private static Thread randomizerThread;
        private static Thread reNewRooms;
        #endregion

        #region misc
        private static Random random = new Random();
        #endregion

        #region initializer
        /// <summary>
        /// Initializes the navigtor
        /// </summary>
        public static void Init()
        {
            try
            {
                navigatorCategories = new Dictionary<string, string>();
                categoryIndex = new Dictionary<byte, string>();
                type = new Dictionary<int, int>();
                parent = new Dictionary<int, int>();
                tradeFloor = new Dictionary<int, bool>();
                name = new Dictionary<int, string>();
                roomAccesName = new Dictionary<string, string>();
                roomAccesParent = new Dictionary<string, List<int>>();
                roomsInsideCategories = new Dictionary<string, string>();
                guestRooms = new Dictionary<string, DataTable>();
                renewRandomRooms = true;


                DataTable dTable;
                using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                {
                    dTable = dbClient.getTable("SELECT id , type FROM room_categories");
                }
                foreach (DataRow dRow in dTable.Rows)
                    type.Add(Convert.ToInt32(dRow[0]), Convert.ToInt32(dRow[1]));



                using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                {
                    dTable = dbClient.getTable("SELECT id, parent FROM room_categories");
                }
                foreach (DataRow dRow in dTable.Rows)
                    parent.Add(Convert.ToInt32(dRow[0]), Convert.ToInt32(dRow[1]));

                using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                {
                    dTable = dbClient.getTable("SELECT id, name FROM room_categories");
                }

                foreach (DataRow dRow in dTable.Rows)
                    name.Add(Convert.ToInt32(dRow[0]), Convert.ToString(dRow[1]));

                for (byte i = 1; i <= 7; i++)
                    addCategoryIndex(i);
                dTable = null;
                using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                {
                    dTable = dbClient.getTable("SELECT id, trading FROM `room_categories`");
                }
                int cataID;
                bool trade;
                foreach (DataRow dRow in dTable.Rows)
                {
                    cataID = int.Parse(dRow[0].ToString());
                    trade = (dRow[1].ToString() == "1");
                    addTradeFloor(cataID, trade);
                }
                
                for (int i = 0; i <= 7; i++)
                {
                    using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                    {
                        dTable = dbClient.getTable("SELECT id, name FROM room_categories WHERE (access_rank_min = " + i + " OR access_rank_hideforlower = '0')");
                    }
                    foreach (DataRow dRow in dTable.Rows)
                    {
                        try
                        {
                            roomAccesName.Add(i + "-" + (Convert.ToString(dRow[0])), Convert.ToString(dRow[1]));
                        }
                        catch (Exception e) { Out.WriteError("error: " + e.ToString()); }
                    }
                }

                ThreadStart guestRoomRefresh = new ThreadStart(reNewRoomsThread);
                reNewRooms = new Thread(guestRoomRefresh);
                reNewRooms.Priority = ThreadPriority.BelowNormal;
                reNewRooms.Name = "Updates guestrooms categories";
                reNewRooms.Start();

                ThreadStart randoms = new ThreadStart(randomRooms);
                randomizerThread = new Thread(randoms);
                randomizerThread.Priority = ThreadPriority.Lowest;
                randomizerThread.Name = "Update Random Rooms";
                randomizerThread.Start();


                Out.WriteLine("Navigator names cached: " + name.Count);
                Out.WriteLine("Navigator \"Parent id's\" cached: " + parent.Count);
                Out.WriteLine("Navigator type's cached: " + type.Count);
                Out.WriteLine("Navigator categorie indexes cached: " + categoryIndex.Count);
            }

            catch (Exception e) { Out.writeSeriousError(e.ToString()); Eucalypt.Shutdown(); }
        }
        #endregion

        #region getters
        /// <summary>
        /// Returns an indicator wheter the floor a room is on is tradeable or not
        /// </summary>
        /// <param name="cataID">The id of the category</param>
        public static bool isTradeFloor(int cataID)
        {
            if (!tradeFloor.ContainsKey(cataID))
                return false;
            else
                return tradeFloor[cataID];
        }

        /// <summary>
        /// returns the name of the catagory with the given ID
        /// </summary>
        public static string getName(int id)
        {
            return name[id];
        }

        /// <summary>
        /// hets the type of thr room with the given id
        /// </summary>
        public static int getType(int id)
        {
            return type[id];
        }


        /// <summary>
        /// returns the ID of the parent
        /// </summary>
        public static int getParent(int id)
        {
            return parent[id];
        }

        /// <summary>
        /// returns a category-index packet
        /// </summary>
        /// <param name="_Rank">The rank (byte format)</param>
        /// <returns>A string with the packet</returns>
        public static string getCategoryIndex(byte _Rank)
        {
            lock (categoryIndex)
            {
                if (categoryIndex.ContainsKey(_Rank))
                    return categoryIndex[_Rank];
                else
                    return addCategoryIndex(_Rank);
            }
        }

        /// <summary>
        /// Gets the name of the acces rank returns "" if the user hasn't got the rank to acces it
        /// </summary>
        /// <param name="rank">Rank of the user</param>
        /// <param name="id">ID of room</param>
        /// <returns>Name of the room</returns>
        public static string getNameAcces(int rank, int id)
        {
            lock (roomAccesParent)
            {
                if (roomAccesName.ContainsKey(rank.ToString() + "-" + id.ToString()))
                    return roomAccesName[rank.ToString() + "-" + id.ToString()];
                else
                    return "";
            }
        }

        /// <summary>
        /// Gets a Parent from the local collection, if it's not present it will create it
        /// </summary>
        /// <param name="rank">Rank of the user</param>
        /// <param name="cataid">Index of the catalogus</param>
        /// <returns></returns>
        public static List<int> getAccesParent(int rank, int cataID)
        {
            lock (roomAccesName)
            {
                if (roomAccesParent.ContainsKey(rank.ToString() + "-" + cataID.ToString()))
                    return roomAccesParent[rank.ToString() + "-" + cataID.ToString()];
                else
                    return addAccesParent(rank, cataID);
            }
        }

        /// <summary>
        /// gets a new guestroom which havn't been queried yet
        /// </summary>
        /// <param name="query">the query name</param>
        /// <param name="update">Does it need an update or not (only true in thread updateGuestrooms)</param>
        /// <returns></returns>
        public static DataTable getGuestroomQuery(string query, bool update)
        {
            lock (guestRooms)
            {
                if (!update)
                {

                    if (guestRooms.ContainsKey(query))
                    {
                        return guestRooms[query];
                    }
                    else
                    {
                        DataTable dTable;
                        using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                        {
                            dTable = dbClient.getTable(query);
                        }
                        guestRooms.Add(query, dTable);
                        return guestRooms[query];
                    }
                }
                else
                {
                    if (guestRooms.ContainsKey(query))
                    {
                        DataTable dTable;
                        using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                        {
                            dTable = dbClient.getTable(query);
                        }
                        guestRooms.Remove(query);
                        guestRooms.Add(query, dTable);
                        return guestRooms[query];
                    }
                    else
                    {
                        DataTable dTable;
                        using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                        {
                            dTable = dbClient.getTable(query);
                        }
                        guestRooms.Add(query, dTable);
                        return guestRooms[query];
                    }
                }
            }
        }

        /// <summary>
        /// Returns a packet with category details depending on the category id and rank
        /// </summary>
        /// <param name="hideFull">Hide full rooms 1 for yes, 0 for no</param>
        /// <param name="cataID">The catagory ID</param>
        /// <param name="_Rank">The rank in byte format</param>
        /// <returns>A complete packet with details</returns>
        public static string getCategoryDetails(int hideFull, int cataID, byte _Rank)
        {
            lock (navigatorCategories)
            {
                if (navigatorCategories.ContainsKey(hideFull.ToString() + cataID.ToString() + "-" + _Rank.ToString()))
                    return navigatorCategories[hideFull.ToString() + cataID.ToString() + "-" + _Rank.ToString()];
                else
                    return makeCategoryDetails(hideFull, cataID, _Rank);
            }
        }


        /// <summary>
        /// returns a list of random rooms
        /// </summary>
        /// <returns></returns>
        public static string getRandomRooms()
        {
            return Rooms;
        }

        public static string getGuestRoomDetails(int roomID, bool hideDetails)
        {
            RoomStructure roomStruct = NavigatorHelper.getRoom(roomID);

            if (roomStruct.getID() > 0) // Guestroom does exist
            {
                StringBuilder Details = new StringBuilder(Encoding.encodeVL64(roomStruct.getSuperusers()) + Encoding.encodeVL64(roomStruct.getState()) + Encoding.encodeVL64(roomID));

                if (roomStruct.getShowName() == 0 && !hideDetails) // The room owner has decided to hide his name at this room, and this user hasn't got the fuseright to see all room owners, hide the name
                    Details.Append("-");
                else
                    Details.Append(roomStruct.getOwner());

                Details.Append(Convert.ToChar(2) + "model_" + roomStruct.getModel() + Convert.ToChar(2) + roomStruct.getName() + Convert.ToChar(2) + roomStruct.getDescription() + Convert.ToChar(2) + Encoding.encodeVL64(Convert.ToInt32(roomStruct.getShowName())));

                if (navigatorManager.isTradeFloor(roomStruct.getCategory()))
                    Details.Append("I"); // Allow trading
                else
                    Details.Append("H"); // Disallow trading
                Details.Append(Encoding.encodeVL64(roomStruct.getVisitorsNow()) + Encoding.encodeVL64(roomStruct.getMaxInside()));

                return ("@v" + Details.ToString());
            }
            else
                return "";

        }
        #endregion

        #region adders / managers

        /// <summary>
        /// Adds a tradefloor indicator to the manager
        /// </summary>
        /// <param name="id">The id of the category</param>
        /// <param name="tradeAble">Indicates if the floor is tradable or not</param>
        public static void addTradeFloor(int id, bool tradeAble)
        {
            if (tradeFloor.ContainsKey(id))
                tradeFloor.Remove(id);
            tradeFloor.Add(id, tradeAble);
        }


        /// <summary>
        /// Creates a acces level of the Database
        /// </summary>
        /// <param name="rank">Rank of the user</param>
        /// <param name="cataid">Index of the catalogus</param>
        /// <returns></returns>
        public static List<int> addAccesParent(int rank, int cataid)
        {
            lock (roomAccesParent)
            {
                List<int> parentChilds = new List<int>();
                DataTable dTabel;
                using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                {
                    dTabel = dbClient.getTable("SELECT id FROM room_categories WHERE parent = '" + cataid + "' AND (access_rank_min <= " + rank + " OR access_rank_hideforlower = '0') ");
                }
                foreach (DataRow dRow in dTabel.Rows)
                    parentChilds.Add(Convert.ToInt32(dRow[0].ToString()));
                if (roomAccesParent.ContainsKey(rank.ToString() + "-" + cataid.ToString()))
                    roomAccesParent.Remove(rank.ToString() + "-" + cataid.ToString());
                roomAccesParent.Add(rank.ToString() + "-" + cataid.ToString(), parentChilds);
                return parentChilds;
            }
        }
      
        /// <summary>
        /// Creates category details depending on the category id and rank
        /// </summary>
        /// <param name="hideFull">Hide full rooms 1 for yes, 0 for no</param>
        /// <param name="cataID">The catagory ID</param>
        /// <param name="_Rank">The rank in byte format</param>
        /// <returns>A complete packet with details</returns>
        private static string makeCategoryDetails(int hideFull, int cataID, byte _Rank)
        {
            lock (navigatorCategories)
            {
                string Name = navigatorManager.getNameAcces(_Rank, cataID); //editted for caching
                if (Name == "") // User has no access to this category/it does not exist
                {
                    return "";
                }
                int Type = navigatorManager.getType(cataID);
                int parentID = navigatorManager.getParent(cataID);

                StringBuilder Navigator = new StringBuilder(@"C\" + 
                    Encoding.encodeVL64(hideFull) + 
                    Encoding.encodeVL64(cataID) + 
                    Encoding.encodeVL64(Type) + Name + 
                    Convert.ToChar(2) + 
                    Encoding.encodeVL64(0) + 
                    Encoding.encodeVL64(10000) + 
                    Encoding.encodeVL64(parentID));

                List<RoomStructure> roomList;
                if (Type == 0) // Publicrooms
                {
                    roomList = NavigatorHelper.getPublicRooms(cataID, hideFull == 1);
                }
                else // Guestrooms
                {
                    roomList = NavigatorHelper.getAmountOfRooms(Config.Navigator_openCategory_maxResults, cataID, hideFull == 1);
                }
                if (Type == 2) // Guestrooms
                    Navigator.Append(Encoding.encodeVL64(roomList.Count));

                bool canSeeHiddenNames = false;

                if (Type != 0) // Publicroom
                    canSeeHiddenNames = (_Rank >= 6);

                foreach (RoomStructure room in roomList)
                {
                    if (Type == 0)
                    {
                        // Navigator.Append(Encoding.encodeVL64(Convert.ToInt32(dRow["id"])) + "I" + 
                        // Convert.ToString(dRow["name"]) + Convert.ToChar(2) + 
                        // Encoding.encodeVL64(Convert.ToInt32(dRow["visitors_now"])) +
                        // Encoding.encodeVL64(Convert.ToInt32(dRow["visitors_max"])) + 
                        // Encoding.encodeVL64(cataID) + Convert.ToString(dRow["description"]) + Convert.ToChar(2) + 
                        // Encoding.encodeVL64(Convert.ToInt32(dRow["id"])) + "H" + Convert.ToString(dRow["ccts"]) + Convert.ToChar(2) + "HI");
                        Navigator.Append(
                            Encoding.encodeVL64(room.getID()) + "I" +
                            Convert.ToString(room.getName()) + Convert.ToChar(2) +
                            Encoding.encodeVL64(room.getVisitorsNow()) +
                            Encoding.encodeVL64(room.getMaxInside()) +
                            Encoding.encodeVL64(cataID) +
                            room.getDescription() + Convert.ToChar(2) +
                            Encoding.encodeVL64(room.getID()) + "H" +
                            Convert.ToString(room.getCCTs()) + Convert.ToChar(2) + "HI");
                    }
                    else
                    {
                        Navigator.Append(Encoding.encodeVL64(room.getID()));
                        Navigator.Append(room.getName() + Convert.ToChar(2));
                        Navigator.Append(Convert.ToString((room.getShowName() == 0 || canSeeHiddenNames == true) ? room.getOwner() : "-") + Convert.ToChar(2));
                        Navigator.Append(roomManager.getRoomState(room.getState()) + Convert.ToChar(2));
                        Navigator.Append(Encoding.encodeVL64(room.getVisitorsNow()));
                        Navigator.Append(Encoding.encodeVL64(room.getMaxInside()));
                        Navigator.Append(Convert.ToString(room.getDescription()) + Convert.ToChar(2));
                    }


                }
                List<int> parentMaps = navigatorManager.getAccesParent(_Rank, cataID);
                foreach (int i in parentMaps)
                {
                    if (NavigatorHelper.hasPeopleInCategory(i))
                        Navigator.Append(Encoding.encodeVL64(i) + "H" + navigatorManager.getName(i) + Convert.ToChar(2) + Encoding.encodeVL64(1) + Encoding.encodeVL64(5) + Encoding.encodeVL64(cataID));
                    else
                        Navigator.Append(Encoding.encodeVL64(i) + "H" + navigatorManager.getName(i) + Convert.ToChar(2) + "HH" +                                            Encoding.encodeVL64(cataID));
                }

                if (navigatorCategories.ContainsKey(hideFull.ToString() + cataID.ToString() + "-" + _Rank.ToString()))
                    navigatorCategories.Remove(hideFull.ToString() + cataID.ToString() + "-" + _Rank.ToString());
                navigatorCategories.Add(hideFull.ToString() + cataID.ToString() + "-" + _Rank.ToString(), Navigator.ToString());
                return Navigator.ToString();

            }
        }
        
        /// <summary>
        /// Adds or updates a category in the current dictionary
        /// </summary>
        /// <param name="_Rank">The rank (byte format)</param>
        /// <returns>A string with the packet</returns>
        private static string addCategoryIndex(byte _Rank)
        {
            lock (categoryIndex)
            {
                StringBuilder Categories = new StringBuilder();
                DataTable dTable;
                using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                {
                    dTable = dbClient.getTable("SELECT id,name FROM room_categories WHERE type = '2' AND parent > 0 AND access_rank_min <= " + _Rank);
                }
                foreach (DataRow dRow in dTable.Rows)
                    Categories.Append(Encoding.encodeVL64(Convert.ToInt32(dRow["id"])) + dRow["name"] + Convert.ToChar(2));
                if(categoryIndex.ContainsKey(_Rank))
                    categoryIndex.Remove(_Rank);
                categoryIndex.Add(_Rank, ("C]" + Encoding.encodeVL64(dTable.Rows.Count) + Categories.ToString()));
                return ("C]" + Encoding.encodeVL64(dTable.Rows.Count) + Categories.ToString());
            }

        }
        #endregion

        #region updaters
        /// <summary>
        /// returns if there are people in the category
        /// </summary>
        /// <param name="i">ID of the category</param>
        /// <returns></returns>
        public static bool peopleInCategory(int i)
        {
            return NavigatorHelper.hasPeopleInCategory(i);
        }
        
        /// <summary>
        /// re-news different dictionary's in this instance
        /// </summary>
        private static void reNewRoomsThread()
        {
            try
            {
                while (renewRandomRooms)
                {
                    lock (guestRooms)
                    {
                        guestRooms.Clear();
                    }
                    lock (navigatorCategories)
                    {
                        navigatorCategories.Clear();
                    }
                    Thread.Sleep(30000);
                }
            }
            catch (ThreadAbortException) { } 
            catch (Exception e) { Out.writeSeriousError(e.ToString()); Eucalypt.Shutdown(); }
        }

        
        /// <summary>
        /// refreshes random rooms every 30 seconds
        /// </summary>
        private static void randomRooms()
        {
            int i;
            try
            {
                while (renewRandomRooms)
                {
                    i = 0;
                    Rooms = "";
                    RoomStructure[] roomArray = NavigatorHelper.getRandomRooms();
                    while (i < 3)
                    {


                        Rooms += Encoding.encodeVL64(roomArray[i].getID()) + roomArray[i].getName() + Convert.ToChar(2) + roomArray[i].getOwner() + Convert.ToChar(2) + roomManager.getRoomState(roomArray[i].getState()) + Convert.ToChar(2) + Encoding.encodeVL64(roomArray[i].getVisitorsNow()) + Encoding.encodeVL64(roomArray[i].getMaxInside()) + roomArray[i].getDescription() + Convert.ToChar(2);
                        i++;

                    }
                    Thread.Sleep(30000);

                }
            }
            catch (ThreadAbortException) { }//nothing special

        }


        #endregion

        #region shutdown manager
        public static void killAllActivity()
        {
            if (randomizerThread != null)
            {
                if (randomizerThread.IsAlive) 
                    randomizerThread.Abort();
            }
            
            if (reNewRooms != null)
            {
                renewRandomRooms = true;
            }
        }
        #endregion
    }

}