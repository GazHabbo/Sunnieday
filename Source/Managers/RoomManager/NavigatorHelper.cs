using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using Holo.Source.Virtual.Rooms;
using Ion.Storage;
using System.Data;

namespace Holo.Source.Managers.RoomManager
{
    class NavigatorHelper
    {
        private static Hashtable categories;
        private static Hashtable roomInside;
        private static List<int> roomList;

        public static void init()
        {
            categories = new Hashtable();
            roomInside = new Hashtable();
            roomList = new List<int>();
            DataColumn dCol;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dCol = dbClient.getColumn("SELECT id FROM `room_categories`");
            }
            foreach (DataRow dRow in dCol.Table.Rows)
            {
                createCategory(Convert.ToInt32(dRow[0]));
                loadRoomsFromCategory(Convert.ToInt32(dRow[0]));
            }
        }

        private static void loadRoomsFromCategory(int category)
        {

            if (category == 4)
            {
            }
            DataTable dTable;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dTable = dbClient.getTable("SELECT id,name,owner,description,state,showname,visitors_now,visitors_max,category,model,showname, superusers, password, ccts FROM rooms WHERE category = '" + category + "' ORDER BY id DESC LIMIT " + Config.Navigator_openCategory_maxResults);
            }
            foreach (DataRow dRow in dTable.Rows)
            {
                new RoomStructure(Convert.ToInt32(dRow["id"]), Convert.ToString(dRow["name"]), Convert.ToString(dRow["owner"]),
                                        dRow["model"].ToString(), Convert.ToInt32(dRow["state"]), Convert.ToInt32(dRow["visitors_max"]),
                                     Convert.ToString(dRow["description"]), Convert.ToInt32(dRow["category"]), Convert.ToString(dRow["ccts"]), Convert.ToInt32(dRow["showname"]), 
                                     Convert.ToInt32(dRow["superusers"]), dRow["password"].ToString());
            }
        }

        public static List<RoomStructure> getPublicRooms(int categoryID, bool allowFull)
        {
            Hashtable information = getCategoryHashTable(categoryID);

            List<RoomStructure> returnList = new List<RoomStructure>(information.Count);
            {
                foreach (RoomStructure room in information.Values)
                {
                    if (allowFull && room.isFull())
                        continue;
                    returnList.Add(room);
                }
                
            }
            return returnList;

        }

        /// <summary>
        /// Returns a new list
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="category"></param>
        /// <param name="hideFull"></param>
        /// <returns></returns>
        public static List<RoomStructure> getAmountOfRooms(int amount, int category, bool hideFull)
        {
            lock(roomInside)
            {
                List<RoomStructure>[] rooms = getRoomInsideCategoryArray(category);
                List<RoomStructure> currentList;
                List<RoomStructure> returnList = new List<RoomStructure>(amount);
                for (int i = rooms.Length -1; i >= 0; i--)
                {
                    currentList = rooms[i];
                    foreach (RoomStructure room in currentList)
                    {
                        if (room == null || hideFull && room.isFull())
                            continue;
                        returnList.Add(room);
                        if (returnList.Count == amount)
                            return returnList;
                    }

                }
                return returnList;
            }

        }

        //public static List<RoomStructure>

        /// <summary>
        /// Creates a new room
        /// </summary>
        /// <param name="id">The id of this item</param>
        /// <returns></returns>
        private static RoomStructure CreateRoom(int id)
        {
            DataRow dRow;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dRow = dbClient.getRow("SELECT id,name,owner,description,state,showname,visitors_now,visitors_max,category,model,showname, superusers, password, ccts FROM rooms WHERE id = '" + id + "' ");
            }
            try
            {
                return new RoomStructure(Convert.ToInt32(dRow["id"]), Convert.ToString(dRow["name"]), Convert.ToString(dRow["owner"]),
                                    dRow["model"].ToString(), Convert.ToInt32(dRow["state"]), Convert.ToInt32(dRow["visitors_max"]),
                                 Convert.ToString(dRow["description"]), Convert.ToInt32(dRow["category"]), Convert.ToString(dRow["ccts"]), Convert.ToInt32(dRow["showname"]), 
                                 Convert.ToInt32(dRow["superusers"]), dRow["password"].ToString());
            }
            catch { return new RoomStructure(); }
        }

        /// <summary>
        /// Adds a roomstructure to this instance
        /// </summary>
        /// <param name="room">The room which needs to be added</param>
        internal static void addRoom(RoomStructure room)
        {
            if (hasRoom(room.getID()))
                return;
            getCategoryHashTable(room.getCategory()).Add(room.getID(), room);
            roomList.Add(room.getID());
            addRoomToRoomInsideCategory(room);
        }

        /// <summary>
        /// gets a room from a given category
        /// </summary>
        /// <param name="category">The category as an int</param>
        private static Hashtable getCategoryHashTable(int category)
        {
            return (Hashtable)categories[category];
        }

        /// <summary>
        /// Returns an indication if a room has been loaded or not
        /// </summary>
        /// <param name="id">The id of the room</param>
        public static bool hasRoom(int id)
        {
            return roomList.Contains(id);
        }

        /// <summary>
        /// Creates a new category
        /// </summary>
        /// <param name="category"></param>
        private static void createCategory(int category)
        {
            categories.Add(category, new Hashtable());
            roomInside.Add(category, new List<RoomStructure>[51]);
            for (int i = 0; i < 51; i++)
            {
                ((List<RoomStructure>[])roomInside[category])[i] = new List<RoomStructure>();
            }
        }

        /// <summary>
        /// Creates a bigger array for this instance
        /// </summary>
        /// <param name="current"></param>
        /// <param name="newCount"></param>
        /// <returns></returns>
        private static List<RoomStructure>[] expandArray(List<RoomStructure>[] current, int newCount)
        {
            List<RoomStructure>[] newArray = new List<RoomStructure>[newCount + 1];
                 
            for (int i = 0; i < newArray.Length; i++)
            {
                
                if (i < current.Length)
                    newArray[i] = current[i];
                else
                    newArray[i] = new List<RoomStructure>();
            }

            return newArray;
        }

        /// <summary>
        /// Gets a list for the checked insisde count
        /// </summary>
        /// <param name="category"></param>
        /// <param name="insideCount"></param>
        //private static List<RoomStructure> getRoomInsideCategory(int category, int insideCount)
        //{
        //    List<RoomStructure>[] array = getRoomInsideCategoryArray(category);
        //    lock (roomInside)
        //    {
        //        if (insideCount > array.Length)
        //        {
        //            array = expandArray(array, insideCount + 1);
        //            roomInside.Remove(category);
        //            roomInside.Add(category, array);
        //        }
        //    }
        //    return array[insideCount];
        //}

        /// <summary>
        /// indicates if there is a active room in the current category
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        internal static bool hasPeopleInCategory(int category)
        {
            List<RoomStructure>[] categoryInsideCount = getRoomInsideCategoryArray(category);

            for (int i = 1; i < categoryInsideCount.Length; i++)
            {
                if (categoryInsideCount[i].Count > 0)
                    return true;
            }
            return false;

        }

        /// <summary>
        /// Gets an array with all active rooms for a given category
        /// </summary>
        /// <param name="category">The category as an int</param>
        /// <returns></returns>
        private static List<RoomStructure>[] getRoomInsideCategoryArray(int category)
        {
            return ((List<RoomStructure>[])roomInside[category]);
        }

        /// <summary>
        /// Gets a room of the current 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="category">The category as an int</param>
        /// <returns></returns>
        internal static RoomStructure getRoom(int id, int category)
        {
            return (RoomStructure)getCategoryHashTable(category)[id];
        }

        /// <summary>
        /// Gets a room from this instance
        /// </summary>
        /// <param name="roomID"></param>
        /// <returns></returns>
        internal static RoomStructure getRoom(int roomID)
        {
            lock (categories)
            {
                foreach (Hashtable t in categories.Values)
                {
                    if (t.Contains(roomID))
                        return (RoomStructure)t[roomID];
                }
                return CreateRoom(roomID);
            }
        }

        /// <summary>
        /// Alters a room catagaory
        /// </summary>
        /// <param name="room">The referal of a roomstructure</param>
        /// <param name="newCatagoy">The category as an int</param>
        private static void alterRoomCategory(RoomStructure room, int newCatagoy)
        {
            removeRoom(room.getID(), room.getCategory());
            room.setCategory(newCatagoy);
            addRoom(room);
        }


        internal static void alterRoomInsideCount(RoomStructure room, int oldVisitors)
        {
            removeRoom(room, oldVisitors);
            addRoom(room);
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.runQuery("UPDATE rooms SET visitors_now = '" + room.getVisitorsNow() + "' WHERE id = '" + room.getID() + "' LIMIT 1");
            }
        }

        /// <summary>
        /// Removes a roomstructure from the manager
        /// </summary>
        /// <param name="roomID"></param>
        /// <param name="categoryID"></param>
        private static void removeRoom(RoomStructure room, int oldVisitors)
        {
            removeRoomFromRoomInside(room);
            getCategoryHashTable(room.getCategory()).Remove(room.getID());
            roomList.Remove(room.getID());
        }
        
        
        /// <summary>
        /// Gets the random rooms for the navigator
        /// </summary>
        /// <returns></returns>
        public static RoomStructure[] getRandomRooms()
        {
            int amount = 0;
            RoomStructure[] rooms = new RoomStructure[3];
            while (amount < 3)
            {
                rooms[amount] = getRoom(roomList[new Random(amount).Next(roomList.Count)]);
                amount++;
            }
            return rooms;
            
        }

        /// <summary>
        /// Modifies a room structure
        /// </summary>
        /// <param name="roomID">The id of the room</param>
        /// <param name="name">The Name of the room</param>
        /// <param name="visitorsMax">Max visitors in the room</param>
        /// <param name="state">The State of the room</param>
        /// <param name="description">The description of the room</param>
        /// <param name="updateDescription">boolean indicating if the description should be updated or not</param>
        /// <param name="category">Modifies a category id</param>
        /// <param name="password">Alters a password of the room</param>
        internal static void modifyRoom(int roomID, string name, int visitorsMax, int state, string description, bool updateDescription, int category, string password, string superUsers)
        {
            RoomStructure modified = getRoom(roomID);
            if (name != "")
                modified.setName(name);
            if (state != -1)
                modified.setState(state);
            if (visitorsMax != -1)
                modified.setMaxVisitors(visitorsMax);
            if (updateDescription)
                modified.setDescription(description);
            if (password != null)
                modified.setPassword(password);
            if (category != -1)
                alterRoomCategory(modified, category);
            if (superUsers == "0" || superUsers == "1")
                modified.setSuperUsers(superUsers);
           
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.AddParamWithValue("name", modified.getName());
                dbClient.AddParamWithValue("state", modified.getState().ToString());
                dbClient.AddParamWithValue("show", modified.getShowName().ToString());
                dbClient.AddParamWithValue("desc", modified.getDescription());
                dbClient.AddParamWithValue("super", modified.getSuperusers().ToString());
                dbClient.AddParamWithValue("max", modified.getMaxInside().ToString());
                dbClient.AddParamWithValue("pass", modified.getPassword());
                dbClient.AddParamWithValue("id", roomID.ToString());
                dbClient.runQuery("UPDATE rooms SET category = '" + modified.getCategory() + "', description = @desc,superusers = @super,visitors_max = @max, password = @pass, name = @name,state = @state, showname = @show WHERE id = @id  LIMIT 1");
            }
            
        }

        /// <summary>
        /// removes a room from this instance
        /// </summary>
        /// <param name="roomID">The roomid which has to be removed</param>
        internal static void removeRoom(int roomID, int currentInside)
        {
            lock (categories)
            {
                RoomStructure room = null;
                foreach (Hashtable t in categories.Values)
                {
                    if (t.Contains(roomID))
                    {
                        room = (RoomStructure)t[roomID];
                        removeRoomFromRoomInside(room);
                        t.Remove(roomID);
                        roomList.Remove(roomID);
                    }
                }
            }
        }

        internal static void removeRoomFromRoomInside(RoomStructure room)
        {
            List<RoomStructure>[] roomList = getRoomInsideCategoryArray(room.getCategory());
            for( int i = 0; i < roomList.Length; i++)
            {
                if (roomList[i].Contains(room))
                {
                    getRoomInsideCategoryArray(room.getCategory())[i].Remove(room);
                    //Out.WriteLine("removed room: " + room.getID() + " with found insideCount: " + i);
                }
                    
            }
        }

        internal static void addRoomToRoomInsideCategory(RoomStructure room)
        {
            
            getRoomInsideCategoryArray(room.getCategory())[room.getVisitorsNow()].Add(room);
            //Out.WriteLine("Added room: " + room.getID() + " with insideCount: " + room.getVisitorsNow());
        }
        
    }
}
