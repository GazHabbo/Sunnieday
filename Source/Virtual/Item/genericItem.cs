using System;
using Holo.Managers;
using Ion.Storage;
using Holo.Managers.catalogue;

namespace Holo.Virtual.Item
{
    public class genericItem
    {
        #region declares -item information
        internal int ID;
        internal itemTemplate template;
        internal int X = 0;
        internal int Y = 0;
        internal byte Z = 0;
        internal double H = 0.00;
        
        internal string Var;
        internal string wallpos = "";
        internal DateTime statusEndTime;
        internal string itemValue;
        internal int statusTime;
        internal string itemStatus
        {
            get
            {
                if ((DateTime.Now - statusEndTime).TotalSeconds < statusTime)
                    return itemValue;
                else
                    return "";

            }
        }
        internal bool updated = false;
        #endregion

        public genericItem(int id)
        {
            System.Data.DataRow dRow;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dRow = dbClient.getRow("SELECT * FROM furniture WHERE id = " + id);
            }
            this.ID = id;
            this.template = catalogueManager.getTemplate(int.Parse(dRow[1].ToString()));
            this.Var = dRow[6].ToString();
        }
        /// <summary>
        /// Initializes a new instance of a virtual wallitem in a virtual room.
        /// </summary>
        /// <param name="ID">The ID of this item.</param>
        /// <param name="tID">The template ID of this item.</param>
        /// <param name="wallPosition">The wallposition of this item. [!Rabbit format]</param>
        /// <param name="Var">The variable of this item. [optional, if not supplied then sprite color will be set]</param>
        /// <param name="wallitem">indicates if it is an wallitem</param>
        public genericItem(int ID, int tID, string wallPosition, string Var, bool wallitem)
        {
            this.ID = ID;
            setTemplateID(tID);
            this.wallpos = wallPosition;
            if (Var == "")
                this.Var = template.Colour;
            else
                this.Var = Var;
        }
        
        /// <summary>
        /// makes a new item for this instance in a handItem format
        /// </summary>
        /// <param name="ID">The id of this item</param>
        /// <param name="tID">The template Id of this item</param>
        /// <param name="Var">the Variable of this item</param>
        /// <param name="wallPos">the wallposition of this item</param>
        public genericItem(int ID, int tID, string Var, string wallPos)
        {
            this.ID = ID;
            setTemplateID(tID);
            this.Var = Var;
            this.wallpos = wallPos;
            makeWallposInformation(Var);
        }

        /// <summary>
        /// Sets the new TID of this item
        /// </summary>
        /// <param name="tID">the new template ID</param>
        public void setTemplateID(int tID)
        {
            this.template = catalogueManager.getTemplate(tID);
        }
        /// <summary>
        /// Initializes a new instance of a virtual wallitem in a virtual room.
        /// </summary>
        /// <param name="ID">The ID of this item.</param>
        /// <param name="tID">The template ID of this item.</param>
        /// <param name="H">height of the item</param>
        /// <param name="X">x of the item</param>
        /// <param name="Y">y of the item</param>
        /// <param name="Var">The variable of this item. [optional, if not supplied then sprite color will be set]</param>
        /// <param name="wallitem">indicates if it is an wallitem</param>
        public genericItem(int ID, int tID, int X, int Y, byte Z, double H, string Var)
        {
            this.ID = ID;
            setTemplateID(tID);
            this.X = X;
            this.Y = Y;
            this.Z = Z;
            this.H = H;
            this.Var = Var;
        }

        /// <summary>
        /// Makes new information for the wall position
        /// </summary>
        /// <param name="Var"></param>
        public void makeWallposInformation(string Var)
        {
            if (Var == "")
                this.Var = template.Colour;
            else
                this.Var = Var;
        }

        /// <summary>
        /// Returns the sprite name of this item by accessing itemTemplate with the template ID.
        /// </summary>
        internal string Sprite
        {
            get
            {
                return template.Sprite;
            }
        }

        /// <summary>
        /// returns a wall-item string
        /// </summary>
        /// <returns></returns>
        public string wallItemToString()
        {
            return ID + Convert.ToChar(9).ToString() + Sprite + Convert.ToChar(9) + " " + Convert.ToChar(9) + wallpos + Convert.ToChar(9) + Var;
        }

        /// <summary>
        /// Returns the item string of this item.
        /// </summary>
        public string floorItemToString()
        {
            if (Sprite == "song_disk")
                return ID.ToString() + Convert.ToChar(2) + template.Sprite + Convert.ToChar(2) + Encoding.encodeVL64(X) + Encoding.encodeVL64(Y) + Encoding.encodeVL64(template.Length) + Encoding.encodeVL64(template.Width) + Encoding.encodeVL64(Z) + H.ToString().Replace(",", ".") + Convert.ToChar(2) + template.Colour + Convert.ToChar(2) + itemStatus + Convert.ToChar(2) + Var + Convert.ToChar(2);
            else
            {
                return ID.ToString() + Convert.ToChar(2) + template.Sprite + Convert.ToChar(2) + Encoding.encodeVL64(X) + Encoding.encodeVL64(Y) + Encoding.encodeVL64(template.Length) + Encoding.encodeVL64(template.Width) + Encoding.encodeVL64(Z) + H.ToString().Replace(",", ".") + Convert.ToChar(2) + template.Colour + Convert.ToChar(2) + itemStatus + Convert.ToChar(2) + (template.isPetFood? "" : "H" )+ Var + Convert.ToChar(2);
            }
        }
        /// <summary>
        /// Saves the structure's
        /// </summary>
        /// <param name="dbClient">an open and connected database client</param>
        internal void storeChanges(DatabaseClient dbClient)
        {
            if (updated)
            {
                dbClient.resetParams();
                dbClient.AddParamWithValue("var", Var);
                dbClient.AddParamWithValue("wallpos", wallpos);
                dbClient.runQuery("UPDATE furniture SET wallpos = @wallpos,x = '" + X + "',y = '" + Y + "',z = '" + Z + "',h = '" + H.ToString().Replace(',', '.') + "', var = @var  WHERE id = '" + ID + "' LIMIT 1"); // sla de nieuwe item informatie op, pas nadat hij niet meer nodig is!
            }
        }

        /// <summary>
        /// Sets the item status for a given while
        /// </summary>
        /// <param name="value">The value of the item status</param>
        /// <param name="time">The time in seconds</param>
        internal void setItemStatus(string value, int time)
        {
            this.itemValue = value;
            this.statusTime = time;
            this.statusEndTime = DateTime.Now;
        }
    }
}