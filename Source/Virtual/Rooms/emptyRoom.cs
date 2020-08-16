using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Holo.Managers;
using Holo.Source.Managers.RoomManager;

namespace Holo.Source.Virtual.Rooms
{
    public class RoomStructure
    {
        public RoomStructure()
        {
            this.id = 0;
        }
        public RoomStructure(int id, string name, string owner, string model,
                    int state, int visitorsMax, string description, int category,
                    string cct, int showname, int superusers, string password)
        {
            this.category = category;
            this.id = id;
            this.name = name;
            this.state = state;
            this.visitorsMax = visitorsMax;
            this.description = description;
            this.owner = owner;
            this.cct = cct;
            this.showname = showname;
            this.model = model;
            this.superusers = superusers;
            this.password = password;
            NavigatorHelper.addRoom(this);
        }
        private string password;
        private int superusers;
        private string model;
        private string cct;
        private int showname;
        private int category;
        private int id;
        private string name;
        private int state;
        internal bool isActive()
        {
            return roomManager.containsRoom(id);
        }
        internal int getVisitorsNow()
        {

            if (roomManager.containsRoom(id))
                return roomManager.getRoom(id).Users.Count;
            else
                return 0;

        }
        private int visitorsMax;
        private string description;
        private string owner;
        
        /// <summary>
        /// Returns the owner of the room
        /// </summary>
        /// <returns></returns>
        internal string getOwner()
        {
            return owner;
        }

        /// <summary>
        /// Returns the superusers
        /// </summary>
        /// <returns></returns>
        internal int getSuperusers()
        {
            return this.superusers;
        }
        /// <summary>
        /// Gets the category this room is in
        /// </summary>
        /// <returns></returns>
        internal int getCategory()
        {
            return category;
        }

        /// <summary>
        /// Gets an indicator of this room to show the name or not
        /// </summary>
        /// <returns></returns>
        internal int getShowName()
        {
            return this.showname;
        }

        /// <summary>
        /// Returns the state of this room
        /// </summary>
        internal int getState()
        {
            return this.state;
        }

        /// <summary>
        /// Returns the room model
        /// </summary>
        internal string getModel()
        {
            return this.model;
        }

        /// <summary>
        /// Returns the name of the room
        /// </summary>
        /// <returns></returns>
        internal string getName()
        {
            return this.name;
        }

        /// <summary>
        /// Returns the description of this room
        /// </summary>
        /// <returns></returns>
        internal string getDescription()
        {
            return this.description;
        }

        /// <summary>
        /// Gets the max-inside people
        /// </summary>
        /// <returns></returns>
        internal int getMaxInside()
        {
            return this.visitorsMax;
        }

        /// <summary>
        /// Gets the string for the lay-out of the room.
        /// </summary>
        /// <returns></returns>
        internal string toOwnRoomString()
        {
            return (id + "\t" + name + Convert.ToChar(9) + owner + Convert.ToChar(9) + roomManager.getRoomState(state) +
                    Convert.ToChar(9) + "x" + Convert.ToChar(9) + this.getVisitorsNow() + Convert.ToChar(9) +
                    visitorsMax + Convert.ToChar(9) + "null" + Convert.ToChar(9) + description +
                    Convert.ToChar(9) + description + Convert.ToChar(9) + Convert.ToChar(13));
        }


        internal int getID()
        {
            return this.id;
        }

        internal void setName(string name)
        {
            this.name = name;
        }

        internal void setState(int state)
        {
            this.state = state;
        }

        internal void setMaxVisitors(int visitorsMax)
        {
            this.visitorsMax = visitorsMax;
        }

        internal void setDescription(string description)
        {
            this.description = description;
        }

        internal void setCategory(int category)
        {
            this.category = category;
        }

        internal bool isFull()
        {
            return (this.getVisitorsNow() >= this.visitorsMax);
        }
        internal string getPassword()
        {
            return password;
        }

        internal void setPassword(string password)
        {
            this.password = password;
        }

        internal string getCCTs()
        {
            return this.cct;
        }

        internal void setSuperUsers(string superUsers)
        {
            this.superusers = int.Parse(superUsers);
        }
    }
}
