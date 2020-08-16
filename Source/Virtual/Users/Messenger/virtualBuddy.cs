using System;
using System.Data;
using Holo.Managers;
using Ion.Storage;

namespace Holo.Virtual.Users.Messenger
{
    /// <summary>
    /// Represents a virtual buddy used in the virtualMessenger object.
    /// </summary>
    public class virtualBuddy
    {
        /// <summary>
        /// The database ID of this user.
        /// </summary>
        internal int userID;
        internal string userName;
        internal string mission;
        internal string lastVisit;
        /// <summary>
        /// Indicates if this user was online at the moment of the latest ToString request.
        /// </summary>
        internal bool Online;
        /// <summary>
        /// Indicates if this user was in a room at the moment of the latest ToString request.
        /// </summary>
        private bool inRoom;

        /// <summary>
        /// Intializes a virtual buddy.
        /// </summary>
        /// <param name="userID">The database ID of this buddy.</param>
        internal virtualBuddy(int userID)
        {
            DataRow dRow;
            this.userID = userID;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dRow = dbClient.getRow("SELECT name, mission, lastvisit FROM users WHERE id = '" + userID + "'");
            }
            userName = dRow["name"].ToString();
            mission = dRow["mission"].ToString();
            lastVisit = dRow["lastvisit"].ToString();

            bool b = Updated; // Update
        }
        internal virtualBuddy(int userID, string mission, string visit, string name)
        {
            this.userID = userID;
            userName = name;
            this.mission = mission;
            this.lastVisit = visit;

            bool b = Updated; // Update
        }
        /// <summary>
        /// Updates the booleans for online and inroom, and returns if there has been changes (so: updates) since the last call to this bool.
        /// </summary>
        internal bool Updated
        {
            get
            {
                bool _Online = userManager.containsUser(userID);
                bool _inRoom = false;

                if (_Online)
                    _inRoom = (userManager.getUser(userID).roomUser != null);
                if (_inRoom != inRoom || _Online != Online)
                {
                    Online = _Online;
                    inRoom = _inRoom;
                    return true;
                }
                else
                    return false;
            }
        }
        /// <summary>
        /// Important to check the 'Updated' bool first. Returns the status string for a virtual buddy based on the statistics of the last call of 'Updated'.
        /// <param name="login">Login packet or not?</param>
        /// </summary>
        internal string ToString(bool login)
        {
            string OUT = Encoding.encodeVL64(userID);
            OUT += userName;
            OUT += "\x02";

            if (userManager.containsUser(userID))
            {
                OUT += "II";
                if (inRoom)
                    OUT += "I";
                else
                    OUT += "H";
                OUT += (userManager.getUser(userID)._Figure + "\x02" + "H");
                if (login)
                    OUT += (userManager.getUser(userID)._Mission);


            }
            else
            {
                OUT += "IHH";
                if (login == true)
                {
                    OUT += "\x02" + "H";

                    if (mission == "")
                    {
                        OUT += "|Sunnie rulez!|";
                    }
                    else
                    {
                        OUT += mission;
                    }
                    if (lastVisit != "")
                    {
                        OUT += "\x02" + lastVisit;
                    }
                    else
                    {
                        OUT += "\x02" + "nog niet online geweest";
                    }
                }

            }
            return OUT + "\x02";
        }

        internal string newUser(bool includeUsername)
        {
            string OUT = Encoding.encodeVL64(userID);
            if (includeUsername)
            {
                OUT += userName + Convert.ToChar(2);
            }

            if (Online)
            {
                OUT += "II";
                if (inRoom)
                    OUT += "I";
                else
                    OUT += "H";
                OUT += userManager.getUser(userID)._Figure;
            }
            else
                OUT += "IHH";

            return OUT + Convert.ToChar(2) + "H";
        }

        internal string newName(bool includeUsername)
        {
            string OUT = Encoding.encodeVL64(userID);
            if (includeUsername)
            {
                OUT += userName + Convert.ToChar(2);
            }

            if (Online)
            {
                OUT += "II";
                if (inRoom)
                    OUT += "I";
                else
                    OUT += "H";
                OUT += userManager.getUser(userID)._Figure;
            }
            else
                OUT += "IHH";

            return OUT + Convert.ToChar(2) + "H";
        }


    }
}