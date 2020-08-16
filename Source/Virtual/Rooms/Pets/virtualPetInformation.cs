using System;
using System.Data;
using Ion.Storage;

namespace Holo.Source.Virtual.Rooms.Pets
{
    public class virtualPetInformation
    {
        #region Fields
        #region Permanent values
        /// <summary>
        /// The database ID of this virtual pet. The ID is equal to the database ID of the 'nest item' of the pet.
        /// </summary>
        public int ID;
        /// <summary>
        /// The type of this virtual pet. (specie)
        /// </summary>
        public byte Type;
        /// <summary>
        /// The name of this virtual pet.
        /// </summary>
        public string Name;

        public int nature;

        /// <summary>
        /// The race of this virtual pet. Pets are shipped in many races.
        /// </summary>
        public byte Race;
        /// <summary>
        /// The color/and or pattern of this pet as a hex HTML color.
        /// </summary>
        public string Color;
        /// <summary>
        /// The figure string of this pet, consisting out of it's type, race and color.
        /// </summary>
        public string Figure
        {
            get
            {
                return
                    this.Type.ToString() + " " +
                    String.Format("{0:000}", this.Race) + " " +
                    this.Color;
            }
        }
        #endregion

        #region Dynamical values
        /// <summary>
        /// The last saved X position of this pet in the room. Default = 0.
        /// </summary>
        public byte lastX;
        /// <summary>
        /// The last saved Y position of this pet in the room. Default = 0.
        /// </summary>
        public byte lastY;
        public float fFriendship;
        public DateTime dtLastNap;
        public DateTime dtLastFed;
        public DateTime dtLastDrink;
        public DateTime dtLastPlayToy;
        public DateTime dtLastPlayUser;
        public DateTime dtBorn;

        /// <summary>
        /// The age of this pet as an integer. This is the amount of days since the day the pet was 'born'. (purchased)
        /// </summary>
        public int Age
        {
            get
            {
                return (int)((DateTime.Now - this.dtBorn)).TotalDays;
            }
        }
        public byte Hunger // 1 - 2 - 3 - 4 - 5 - 6 max
        {
            get 
            {
                int returnValue = 6 - (int)Math.Round((DateTime.Now - this.dtLastFed).TotalHours);
                if (returnValue < 0)
                    returnValue = 0;
                return (byte)returnValue; 
            }
            set
            {
                switch (Hunger + 1)
                {
                    case 1:
                        this.dtLastFed = DateTime.Now.AddHours(-5);
                        break;
                    case 2:
                        this.dtLastFed = DateTime.Now.AddHours(-4);
                        break;
                    case 3:
                        this.dtLastFed = DateTime.Now.AddHours(-3);
                        break;
                    case 4:
                        this.dtLastFed = DateTime.Now.AddHours(-2);
                        break;
                    case 5:
                        this.dtLastFed = DateTime.Now.AddHours(-1);
                        break;
                    case 6:
                    case 7:
                        this.dtLastFed = DateTime.Now;
                        break;
                    default:
                        throw new ArgumentException("Item can not be greater than 6 or smaller than 1!");
                }
            }
        }
        public bool hungry
        {
            get
            {
                return Hunger < 6;
            }
        }
        public byte Thirst // 1 - 2 - 3 max
        {
            get
            {
                int returnValue = 3 - (int)Math.Round((DateTime.Now - this.dtLastDrink).TotalMinutes / 30);
                if (returnValue < 0)
                    returnValue = 0;
                return (byte)returnValue;  
            }
            set
            {
                this.dtLastDrink = DateTime.Now;
            }
        }
        public bool thirsty
        {
            get
            {
                return Thirst < 3;
            }
        }
        public byte Happiness // 1,2,3,4,5,6 max
        {
            get
            {
                try
                {
                    return (byte)(Thirst + (Hunger / 2));
                }
                catch
                {
                    return Thirst;
                }
            }
        }
        public byte Energy // 11 max
        {
            get 
            {
                int returnValue = 11 - (int)Math.Round((DateTime.Now - dtLastNap).TotalMinutes / 15);
                if (returnValue < 0)
                    return 0;
                return (byte)returnValue; 
            }
        }
        public byte Friendship // 11 max
        {
            get 
            {
                return (byte)(Happiness + (Energy/2)); 
            }
        }
        #endregion
        #endregion

        #region Methods
        /// <summary>
        /// Updates the dynamical fields for this pet in the database.
        /// </summary>
        public void Update()
        {
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.AddParamWithValue("id", this.ID);
                dbClient.AddParamWithValue("friendship", this.fFriendship);
                dbClient.AddParamWithValue("x", this.lastX);
                dbClient.AddParamWithValue("y", this.lastY);
                dbClient.AddParamWithValue("last_kip", this.dtLastNap);
                dbClient.AddParamWithValue("last_eat", this.dtLastFed);
                dbClient.AddParamWithValue("last_drink", this.dtLastDrink);
                dbClient.AddParamWithValue("last_playtoy", this.dtLastPlayToy);
                dbClient.AddParamWithValue("last_playuser", this.dtLastPlayUser);

                dbClient.runQuery(
                    "UPDATE furniture_pets SET " +

                    "friendship = @friendship," +
                    "x = @x," +
                    "y = @y," +
                    "last_nap = @last_kip," +
                    "last_eat = @last_eat," +
                    "last_drink = @last_drink," +
                    "last_playtoy = @last_playtoy," +
                    "last_playuser = @last_playuser " +

                    "WHERE id = @id ");
            }
        }


       

        /// <summary>
        /// Parses a System.Data.DataRow with the required fields to a full virtualPetInformation object. Null is returned on errors.
        /// </summary>
        /// <param name="dRow">The System.Data.DataRow object with all the required fields for the parse.</param>
        public static virtualPetInformation Parse(DataRow dRow)
        {
            if (dRow == null)
                return null;

            virtualPetInformation Pet = new virtualPetInformation();
            // Constant values
            Pet.ID = (int)dRow["id"];
            Pet.Name = (string)dRow["name"];
            Pet.Type = byte.Parse(dRow["type"].ToString());
            Pet.Race = byte.Parse(dRow["race"].ToString());
            Pet.Color = "#" + dRow["color"].ToString();
            Pet.nature = (int)dRow["nature_positive"];

            // Event recordings
            Pet.dtBorn = DateTime.Parse(dRow["born"].ToString());
            Pet.dtLastNap = DateTime.Parse(dRow["last_nap"].ToString());
            Pet.dtLastFed = DateTime.Parse(dRow["last_eat"].ToString());
            Pet.dtLastDrink = DateTime.Parse(dRow["last_drink"].ToString());
            Pet.dtLastPlayToy = DateTime.Parse(dRow["last_playtoy"].ToString());
            Pet.dtLastPlayUser = DateTime.Parse(dRow["last_playuser"].ToString());

            // Special values
            Pet.fFriendship = (float)dRow["friendship"];
            Pet.lastX = byte.Parse(dRow["x"].ToString());
            Pet.lastY = byte.Parse(dRow["y"].ToString());

            return Pet;
        }
        #endregion
    }
}