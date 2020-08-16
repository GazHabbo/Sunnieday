using System.Collections.Generic;
using Holo.Virtual.Item;
using System.Data;
using System;
using System.Text;

namespace Holo.Source.Virtual.Users.ItemManagement
{
    class HandItemManager
    {

        /// <summary>
        /// The user this handitem manager belongs to
        /// </summary>
        private int userID;
        /// <summary>
        /// The list of items, with the ID as key field
        /// </summary>
        private Dictionary<int, genericItem> Items;
        private List<int> itemIDsAdded;
        private List<int> itemIDsRemoved;
        private List<int> itemIDsStart;

        /// <summary>
        /// The list of handitems, can be approached as an array
        /// </summary>
        private List<genericItem> handItems;

        /// <summary>
        /// Generates the users new list of items
        /// </summary>
        /// <param name="Items">The items in Datatable format</param>
        public HandItemManager(DataTable Items, int userID)
        {
            this.Items = new Dictionary<int, genericItem>();
            handItems = new List<genericItem>();
            itemIDsAdded = new List<int>();
            itemIDsRemoved = new List<int>();
            itemIDsStart = new List<int>();
            this.userID = userID;
            makeItemList(Items);
        }

        /// <summary>
        /// Generates the list of items
        /// </summary>
        /// <param name="itemTable">the item list in DataTable format</param>
        public void makeItemList(DataTable itemTable)
        {
            genericItem toAdd;
            string Var;
            int templateID;
            int itemID;
            string wallPos;
            foreach (DataRow dRow in itemTable.Rows)
            {
                Var = dRow["var"].ToString();
                templateID = Convert.ToInt32(dRow["tid"]);
                itemID = Convert.ToInt32(dRow["id"]);
                wallPos = dRow["wallpos"].ToString();
                toAdd = new genericItem(itemID, templateID, Var, wallPos);
                Items.Add(itemID, toAdd);
                handItems.Add(toAdd);
                itemIDsStart.Add(itemID);
            }
        }

        /// <summary>
        /// returns the item 
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns></returns>
        public genericItem getItem(int itemID)
        {
            if (Items.ContainsKey(itemID))
                return Items[itemID];
            else
                return null;
        }

        /// <summary>
        /// Looks if a user has an item or not
        /// </summary>
        /// <param name="itemID">the ID of the item</param>
        /// <returns>True if it has an item, False if it doesn't</returns>
        public bool hasItem(int itemID)
        {
            return Items.ContainsKey(itemID);
        }

        /// <summary>
        /// Adds a list of items
        /// </summary>
        /// <param name="toAdd">The list which should be added</param>
        public void addItemList(List<genericItem> toAdd)
        {
            foreach (genericItem toadd in toAdd)
            {
                addItem(toadd);
            }
        }

        /// <summary>
        /// Adds an item to the list of items to delete from the database.
        /// </summary>
        /// <param name="item">The item which is beeing deleted</param>
        private void deleteDatabaseEntry(genericItem item)
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
        private void addDatabaseEntry(genericItem item)
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
        /// Clears all items in this hand item manager
        /// </summary>
        public void clearItems(bool purge)
        {
            if (purge)
            {
                foreach (genericItem item in Items.Values)
                {
                    deleteDatabaseEntry(item);
                }

            }

            Items.Clear();
            handItems.Clear();
        }

        /// <summary>
        /// adds an item to the collection
        /// </summary>
        /// <param name="item">The item to add</param>
        public void addItem(genericItem item)
        {
            if (!Items.ContainsKey(item.ID))
            {
                Items.Add(item.ID, item);
                handItems.Add(item);
                addDatabaseEntry(item);
            }
        }

        /// <summary>
        /// Updates an given item
        /// </summary>
        /// <param name="item">The item that needed the update</param>
        public void updateItem(genericItem item)
        {
            if (Items.ContainsKey(item.ID))
            {
                removeItem(item.ID);
            }
            addItem(item);
        }

        /// <summary>
        /// removes an item in the item manager
        /// </summary>
        /// <param name="itemID">The id of the item</param>
        /// <returns>The item that was removed</returns>
        public genericItem removeItem(int itemID)
        {
            if (Items.ContainsKey(itemID))
            {
                genericItem backupItem = Items[itemID];
                Items.Remove(itemID);
                deleteDatabaseEntry(backupItem);
                if (handItems.Contains(backupItem))
                    handItems.Remove(backupItem);

                return backupItem;
            }
            else
            {
                return null;
            }

        }

        /// <summary>
        /// Gets a handitem at the desired location
        /// </summary>
        /// <param name="location">The location of the item</param>
        /// <returns>A Handitem</returns>
        public genericItem getHandItem(int location)
        {
            if (location < handItems.Count)
            {
                return handItems[location];
            }
            return null;
        }

        /// <summary>
        /// Returns the length of the list of items
        /// </summary>
        /// <returns>The amount of items</returns>
        public int handLength()
        {
            return handItems.Count;
        }

        /// <summary>
        /// Destroys this instance.
        /// </summary>
        internal void destroy()
        {
            clearItems(false);
            handItems = null;
            Items = null;
            doDatabaseUpdate(false);
        }

        internal void update()
        {
            doDatabaseUpdate(true);
        }

        /// <summary>
        /// Does an database update
        /// </summary>
        internal void doDatabaseUpdate(bool update)
        {
            lock (this)
            {
                if (itemIDsAdded.Count > 0 || itemIDsRemoved.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    if (itemIDsAdded.Count > 0)
                    {
                        sb.Append("INSERT INTO user_furniture (user_id,furniture_id) VALUES ");
                        foreach (int id in itemIDsAdded)
                        {
                            sb.Append("('" + userID + "','" + id + "'), ");
                        }
                        sb.Remove(sb.Length - 2, 2);
                        sb.Append(";\r\n");
                    }
                    if (itemIDsRemoved.Count > 0)
                    {
                        sb.Append("DELETE FROM user_furniture WHERE ");
                        foreach (int id in itemIDsRemoved)
                        {
                            sb.Append("(user_id = '" + userID + "' AND furniture_id = '" + id + "') OR ");
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
                    itemIDsAdded.Clear();
                    itemIDsRemoved.Clear();

                }
            }
        }

        /// <summary>
        /// Returns all items of this instance
        /// </summary>
        /// <returns></returns>
        internal List<genericItem> getAllItems()
        {
            return handItems;
        }
    }
}
