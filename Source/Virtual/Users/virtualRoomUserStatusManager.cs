using System;
using System.Threading;
using System.Collections;

using Holo.Managers;
using Holo.Virtual.Rooms;
using System.Collections.Generic;
using System.Text;

namespace Holo.Virtual.Users
{
    /// <summary>
    /// Provides management for the statuses of a virtual user.
    /// </summary>
    public class virtualRoomStatusManager
    {
        #region Declares
        /// <summary>
        /// The ID of the user that uses this status manager.
        /// </summary>
        private int roomUID;
        /// <summary>
        /// The ID of the room the user that uses this status manager is in.
        /// </summary>
        private virtualRoom room;
        /// <summary>
        /// Contains the status strings.
        /// </summary>
        private UserStatusManager[] statuses;
        #endregion

        #region Constructors/destructors
        public virtualRoomStatusManager(int roomUID, virtualRoom room)
        {
            this.roomUID = roomUID;
            this.room = room;
            statuses = new UserStatusManager[5];
        }

       
        #endregion

       
        #region Status management
        /// <summary>
        /// Adds a status key and a value to the status manager. If the status is already inside, then the previous one will be removed.
        /// </summary>
        /// <param name="name">Name of the value</param>
        /// <param name="data">height etc</param>
        /// <param name="lifeTimeSeconds">linftime of the item 0 if infinite</param>
        /// <param name="action">The action of the status, will be flipped with name etc.</param>
        /// <param name="actionSwitchSeconds">The total amount of seconds this action flips with the name.</param>
        /// <param name="actionLengthSeconds">The total amount of seconds that the action lasts before it flips</param>
        /// <returns></returns>
        
        public bool addStatus(string name, string data, int lifeTimeSeconds, string action, int actionSwitchSeconds, int actionLengthSeconds)
        {
            // Remove old status
            removeStatus(name);

            // Allocate status
            for (int i = 0; i < statuses.Length; i++)
            {
                if (statuses[i] == null)
                {
                    statuses[i] = new UserStatusManager(name, data, lifeTimeSeconds, action, actionSwitchSeconds, actionLengthSeconds);
                    
                    return true;
                }
            }

            // Could not allocate
            return false;
        }
        
        
        
        
        /// <summary>
        /// Removes a certain status from the status manager.
        /// </summary>
        /// <param name="Key">The key of the status to remove.</param>
        public bool removeStatus(String name)
        {
            for (int i = 0; i < statuses.Length; i++)
            {
                if (statuses[i] != null && statuses[i].name == name)
                {
                    statuses[i] = null;
                    return true;
                }
            }

            return false;
        }
        /// <summary>
        /// Returns a bool that indicates if a certain status is in the status manager.
        /// </summary>
        /// <param name="Key">The key of the status to check.</param>
        internal bool containsStatus(string Key)
        {
            for (int i = 0; i < statuses.Length; i++)
            {
                if (statuses[i] != null && statuses[i].name == Key)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the status string of all the statuses currently in the status manager.
        /// </summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < statuses.Length; i++)
            {
                UserStatusManager status = statuses[i];
                if (status == null)
                    continue;

                if (status.checkStatus())
                {
                    sb.Append(status.name);
                    if (status.data != null)
                    {
                        sb.Append(' ');
                        sb.Append(status.data);
                    }
                    sb.Append('/');
                }
                else
                {
                    statuses[i] = null;
                }
            }
            return sb.ToString();
        }
        #endregion

       

        internal void Clear()
        {
            for (int i = 0; i < statuses.Length; i++)
            {
                statuses[i] = null;
            }
        }
    }
}
