using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Holo.Managers;
using System.Data;
using Ion.Storage;
using Holo.Source.Virtual.Rooms;
using Holo.Source.Managers.RoomManager;

namespace Holo.Source.Virtual.Users
{
    class ownRoomManager
    {
        #region declares
        /// <summary>
        /// The username where the rooms belong to
        /// </summary>
        internal string _Username;
        /// <summary>
        /// Indicates if the the own rooms where generated
        /// </summary>
        internal bool generatedOwnRooms = false;
        /// <summary>
        /// Indicates i
        /// </summary>
        internal bool ownRoomsUpdatedHelper = false;
        /// <summary>
        /// Indicates if the own room packet needs an update
        /// </summary>
        internal bool ownRoomsUpdated
        {
            get
            {
                if (ownRoomsUpdatedHelper)
                    return true;
                foreach (KeyValuePair<int, RoomStructure> own in ownRooms)
                {
                    if (own.Value.isActive())
                        return true;
                }
                return false;
            }
            set
            {
                ownRoomsUpdatedHelper = value;
            }
        }
        /// <summary>
        /// Contains all room data
        /// </summary>
        private Dictionary<int, RoomStructure> ownRooms;
        /// <summary>
        /// The packet
        /// </summary>
        internal string ownRoomPacket;
        #endregion

        #region private classes
        /// <summary>
        /// Private structure for the rooms
        /// </summary>
        
        #endregion

        public ownRoomManager(string userName)
        {
            this.ownRooms = new Dictionary<int, RoomStructure>();
            this._Username = userName;
            generateOwnRooms();
        }
        /// <summary>
        /// Generates the users own rooms
        /// </summary>
        internal void generateOwnRooms()
        {
            if (!this.generatedOwnRooms)
            {
                this.ownRooms = new Dictionary<int, RoomStructure>();
                DataTable dTable;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dTable = dbClient.getTable("SELECT id,name,description,state,showname,visitors_now,visitors_max,category,model,showname, superusers, password FROM rooms WHERE owner = '" + _Username + "' ");
                }
                if (dTable.Rows.Count > 0)
                {
                    foreach (DataRow dRow in dTable.Rows)
                    {
                        try
                        {
                            addOwnRoom(Convert.ToInt32(dRow["id"]), Convert.ToString(dRow["name"]),
                                 Convert.ToInt32(dRow["state"]), Convert.ToInt32(dRow["visitors_max"]),
                                 Convert.ToString(dRow["description"]), Convert.ToInt32(dRow["category"]), dRow["model"].ToString(),
                                 Convert.ToInt32(dRow["showname"]), Convert.ToInt32(dRow["superusers"]), dRow["password"].ToString());
                        }
                        catch { }
                    }
                }
                ownRoomsUpdated = true;
            }

        }
        /// <summary>
        /// Generates a own room string (the packet)
        /// </summary>
        internal void generatedOwnRoomString()
        {
            if (this.ownRooms.Count > 0)
            {
                ownRoomPacket = "@P";
                foreach (KeyValuePair<int, RoomStructure> kvPair in ownRooms)
                {
                    ownRoomPacket += kvPair.Value.toOwnRoomString();
                }
            }
            else
            {
                ownRoomPacket = "@y" + _Username;
            }
        }
        /// <summary>
        /// returns a own room packet
        /// </summary>
        /// <returns>your own rooms in packet format</returns>
        internal string getOwnRoomPacket()
        {
            generatedOwnRoomString();
            return ownRoomPacket;
        }
        /// <summary>
        /// Adds a room to your own room list
        /// </summary>
        /// <param name="id">The id of the room</param>
        /// <param name="name">The name of the room</param>
        /// <param name="state">The state of the room</param>
        /// <param name="visitorsMax">The maximum of visitors inside it</param>
        /// <param name="description">The description of the room</param>
        internal void addOwnRoom(int id, string name, int state, int visitorsMax, string description, int category, string model, int showname, int superusers, string password)
        {
            if (!ownRooms.ContainsKey(id))
            {
                if (NavigatorHelper.hasRoom(id))
                {
                    ownRooms.Add(id, NavigatorHelper.getRoom(id, category));
                }
                else
                {
                    ownRooms.Add(id, new RoomStructure(id, name, _Username, model, state, visitorsMax, description, category, "", showname, superusers, password));
                }
            }

        }

        /// <summary>
        /// Removes a room from your own room list
        /// </summary>
        /// <param name="id"></param>
        internal void removeOwnRoom(int id)
        {
            if (ownRooms.ContainsKey(id))
            {
                RoomStructure room = ownRooms[id];
                ownRooms.Remove(id);
                NavigatorHelper.removeRoom(id, room.getVisitorsNow());
                ownRoomsUpdated = true;
            }
        }
        internal int getCount()
        {
            return ownRooms.Count;
        }

        internal int getRoomCategory(int roomID)
        {
            return ownRooms[roomID].getCategory();
        }

        internal bool hasRoom(int roomID)
        {
            return ownRooms.ContainsKey(roomID);
        }

        /// <summary>
        /// Modifies a room in the collection
        /// </summary>
        /// <param name="roomID">The id of the room</param>
        /// <param name="name">The new name of the room, if empty it's not modified</param>
        /// <param name="visitorsMax">The new max visitors, -1 if not modified</param>
        /// <param name="state">The state of the room, -2 if it's not modified</param>
        /// <param name="description">The new description name</param>
        /// <param name="updateDescription">Indicates if the description should be modified or not</param>
        /// <param name="category">The category of the item, -1 if not different</param>
        internal void modifyRoom(int roomID, string name, int visitorsMax, int state, string description, bool updateDescription, int category, string password, string superUsers)
        {
            if (!ownRooms.ContainsKey(roomID))
                return;
            NavigatorHelper.modifyRoom(roomID, name, visitorsMax, state, description, updateDescription, category, password, superUsers);
            ownRoomsUpdated = true;
            
           
        }

        internal void destroy()
        {
            this._Username = null;
            this.ownRooms.Clear();
            this.ownRooms = null;
        }
    }
}
