using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Holo.Virtual.Item;

namespace Holo.Source.Managers.RoomManager
{
    class RoomItemKeeper
    {
        private List<int> itemIDsAdded;
        private List<int> itemIDsRemoved;
        private List<int> itemIDsStart;
        private int roomID;

        public RoomItemKeeper(int roomID)
        {
            itemIDsAdded = new List<int>();
            itemIDsRemoved = new List<int>();
            itemIDsStart = new List<int>();
            this.roomID = roomID;
        }

        /// <summary>
        /// Adds an start item
        /// </summary>
        /// <param name="item"></param>
        internal void addStartItem(genericItem item)
        {
            itemIDsStart.Add(item.ID);
        }
        /// <summary>
        /// Adds an item to the list of items to delete from the database.
        /// </summary>
        /// <param name="item">The item which is beeing deleted</param>
        internal void deleteDatabaseEntry(genericItem item)
        {
            if (itemIDsAdded.Contains(item.ID))
                itemIDsAdded.Remove(item.ID);
            if (itemIDsRemoved.Contains(item.ID))
                return;

            itemIDsRemoved.Add(item.ID);
        }

        /// <summary>
        /// Adds an item to the list of "inserts" for the database
        /// </summary>
        /// <param name="item">The item which is beeing added</param>
        internal void addDatabaseEntry(genericItem item)
        {
            if (itemIDsRemoved.Contains(item.ID))
                itemIDsRemoved.Remove(item.ID);
            if (itemIDsStart.Contains(item.ID))
                return;
            if (itemIDsAdded.Contains(item.ID))
                return;
            itemIDsAdded.Add(item.ID);
        }

        /// <summary>
        /// Does an database update
        /// </summary>
        private void doDatabaseUpdate(bool update)
        {
            if (itemIDsAdded.Count > 0 || itemIDsRemoved.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                if (itemIDsAdded.Count > 0)
                {
                    sb.Append("INSERT INTO room_furniture (room_id,furniture_id) VALUES ");
                    foreach (int id in itemIDsAdded)
                    {
                        sb.Append("('" + roomID + "','" + id + "'), ");
                    }
                    sb.Remove(sb.Length - 2, 2);
                    sb.Append(";\r\n");
                }
                if (itemIDsRemoved.Count > 0)
                {
                    sb.Append("DELETE FROM room_furniture WHERE ");
                    foreach (int id in itemIDsRemoved)
                    {
                        sb.Append("(room_id = '" + roomID + "' AND furniture_id = '" + id + "') OR ");
                    }
                    sb.Remove(sb.Length - 3, 3);
                }
                using (Ion.Storage.DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.runQuery(sb.ToString());
                }
                if (update)
                {
                    foreach (int i in itemIDsAdded)
                    {
                        itemIDsStart.Add(i);
                    }
                    foreach (int i in itemIDsRemoved)
                    {
                        itemIDsStart.Remove(i);
                    }
                }
                //Out.WritePlain(sb.ToString());
                itemIDsAdded.Clear();
                itemIDsRemoved.Clear();
            }
        }

        internal void destroy()
        {
            doDatabaseUpdate(false);
        }

        internal void saveFurni()
        {
            doDatabaseUpdate(true);
        }
    }
}
