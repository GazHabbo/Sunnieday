using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Holo.Managers;
using Ion.Storage;

namespace Holo.Virtual.Users.Messenger
{
    /// <summary>
    /// Represents the messenger for a virtual user, which provides keeping buddy lists, instant messaging, inviting friends to a user's virtual room and various other features. The virtual messenger object provides voids for updating status of friends, instant messaging and more.
    /// </summary>
    class virtualMessenger
    {
        #region Declares
        /// <summary>
        /// The database ID of the parent virtual user.
        /// </summary>
        private int userID;
        private Dictionary<int, virtualBuddy> Buddies = new Dictionary<int, virtualBuddy>();
        #endregion

        #region Constructors/destructors
        /// <summary>
        /// Initializes the virtual messenger for the parent virtual user, generating friendlist, friendrequests etc.
        /// </summary>
        /// <param name="userID">The database ID of the parent virtual user.</param>
        internal virtualMessenger(int userID)
        {
            this.userID = userID;
            this.Buddies = new Dictionary<int, virtualBuddy>();
        }

        internal string friendList()
        {
            Dictionary<int, virtualBuddy> userIDs = userManager.getUserFriendIDs(userID);
            StringBuilder Buddylist = new StringBuilder(Encoding.encodeVL64(200) + Encoding.encodeVL64(200) + Encoding.encodeVL64(600) + "H" + Encoding.encodeVL64(userIDs.Count));
            virtualBuddy Me = new virtualBuddy(userID);
            foreach (KeyValuePair<int, virtualBuddy> i in userIDs)
            {
                virtualBuddy Buddy = i.Value;
                try
                {
                    if (Buddy.Online)
                    {
                        userManager.getUser(i.Key).Messenger.addBuddy(Me, true);
                    }
                }
                catch { }

                if (Buddies.ContainsKey(i.Key) == false)
                    Buddies.Add(i.Key, Buddy);
                Buddylist.Append(Buddy.newUser(true));
                if (Buddy.mission == null || (Buddy.mission == ""))
                {
                    Buddylist.Append("Sunnie is cool!" + Convert.ToChar(2));
                }
                else { Buddylist.Append(Buddy.mission + Convert.ToChar(2)); }

                Buddylist.Append(Buddy.lastVisit);
                Buddylist.Append(Convert.ToChar(2));

            }
            Buddylist.Append("PYH");
            return Buddylist.ToString();
        }

        internal string friendRequests()
        {
            DataColumn dCol;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dCol = dbClient.getColumn("SELECT userid_from,requestid FROM messenger_friendrequests WHERE userid_to = '" + this.userID + "' ORDER by requestid ASC");
            }
            StringBuilder Requests = new StringBuilder(Encoding.encodeVL64(dCol.Table.Rows.Count) + Encoding.encodeVL64(dCol.Table.Rows.Count));
            if (dCol.Table.Rows.Count > 0)
            {
                int i = 0;
                foreach (DataRow dRow in dCol.Table.Rows)
                {
                    using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                    {
                        Requests.Append(Encoding.encodeVL64(Convert.ToInt32(dRow["requestid"])) + dbClient.getString("SELECT name FROM users WHERE id = '" + Convert.ToString(dRow["userid_from"]) + "'") + Convert.ToChar(2) + Convert.ToString(dRow["userid_from"]) + Convert.ToChar(2));
                    }
                    i++;
                }
            }


            return Requests.ToString();
        }
        internal void Clear()
        {

        }

        internal void addBuddy(virtualBuddy Buddy, bool Update)
        {
            if (Buddies.ContainsKey(Buddy.userID) == false)
                Buddies.Add(Buddy.userID, Buddy);
            if (Update)
                User.sendData("@MHII" + Buddy.ToString(true));
        }
        /// <summary>
        /// Deletes a buddy from the friendlist and virtual messenger of this user, but leaves the database row untouched.
        /// </summary>
        /// <param name="ID">The database ID of the buddy to delete from the friendlist.</param>
        internal void removeBuddy(int ID)
        {
            if (Buddies.ContainsKey(ID))
            {
                Buddies.Remove(ID);
                User.sendData("@MHI" + "M" + Encoding.encodeVL64(ID));
            }
        }
        internal string getUpdates()
        {
            //return "HH";
            //StringBuilder Updates = new StringBuilder();
            //string Updates = "";
            string PacketAdd = "";

            try
            {
                lock (Buddies)
                {
                    foreach (virtualBuddy Buddy in Buddies.Values)
                    {
                        if (Buddy.Updated)
                        {
                            User.sendData("@MHIH" + Buddy.ToString(false));
                        }
                    }
                }
                if (PacketAdd == "")
                    return "HH";
                return PacketAdd.Substring(0, (PacketAdd.Length - 3));
            }
            catch { return "HH"; }
        }
        #endregion
        /// <summary>
        /// Returns a boolean that indicates if the messenger contains a certain buddy, and this buddy is online.
        /// </summary>
        /// <param name="userID">The database ID of the buddy to check.</param>
        internal bool containsOnlineBuddy(int userID)
        {
            if (Buddies.ContainsKey(userID) == false)
                return false;
            else
                return userManager.containsUser(userID);
        }
        /// <summary>
        /// Returns a bool that indicates if there is a friendship between the parent virtual user and a certain user.
        /// </summary>
        /// <param name="userID">The database ID of the user to check.</param>
        internal bool hasFriendship(int userID)
        {
            return Buddies.ContainsKey(userID);
        }
        /// <summary>
        /// Returns a bool that indicates if there are friend requests hinth and forth between the the parent virtual user and a certain user.
        /// </summary>
        /// <param name="userID">The database ID of the user to check.</param>
        internal bool hasFriendRequests(int userID)
        {
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                return dbClient.findsResult("SELECT requestid FROM messenger_friendrequests WHERE (userid_to = '" + this.userID + "' AND userid_from = '" + userID + "') OR (userid_to = '" + userID + "' AND userid_from = '" + this.userID + "')");
            }
        }

        #region Object management
        /// <summary>
        /// Returns the parent virtual user instance of this virtual messenger.
        /// </summary>
        internal virtualUser User
        {
            get
            {
                return userManager.getUser(this.userID);
            }
        }
        #endregion
    }
}
