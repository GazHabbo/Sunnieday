using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ion.Storage;
using System.Data;


namespace Holo.Source.Managers.RoomManager
{
    public class modelData
    {
        internal string model;
        internal int roomomatic_subscr_only;
        internal int doorx;
        internal int doory;
        internal int doorh;
        internal byte doorz;

        internal Holo.Virtual.Rooms.virtualRoom.squareState[,] sqSTATE;
        internal byte[,] sqFLOORHEIGHT;
        internal byte[,] sqITEMROT;
        internal bool publicroom;
        internal double[,] sqITEMHEIGHT;
        internal bool[,] sqUNIT;
        internal squareTrigger[,] sqTRIGGER;

        internal string heightMap;

        internal string publicroom_items;
        internal bool hasSwimmingPool;
        internal string specialcast_emitter;
        internal int specialCast_interval;
        internal int specialcast_rnd_min;
        internal int specialcast_rnd_max;

        /// <summary>
        /// Creates a new model data
        /// </summary>
        /// <param name="model">The model data</param>
        /// <param name="roomomatic_subscr_only">declares wheter it's only available for subscribers</param>
        /// <param name="doorx">the x-location of the door</param>
        /// <param name="doory">the y-location of the door</param>
        /// <param name="doorh">the h-location of the door</param>
        /// <param name="doorz">the z-location of the door</param>
        /// <param name="heightMap">the heightmap in string format</param>
        /// <param name="publicroom_item">the public room items</param>
        /// <param name="hasSwimmingPool">indicator for the swimmingpool</param>
        /// <param name="specialcast_emitter">the name of the emitter in this room</param>
        /// <param name="specialCast_interval">the emitter change interval</param>
        /// <param name="specialcast_rnd_min">the specialcast minimum</param>
        /// <param name="specialcast_rnd_max">the specialcast maximum</param>
        public modelData(string model, int roomomatic_subscr_only,
                                int doorx, int doory, int doorh, byte doorz,
                                string heightMap, string publicroom_items, bool hasSwimmingPool,
                                string specialcast_emitter, int specialCast_interval, int specialcast_rnd_min, int specialcast_rnd_max)
        {
            
            this.model = model;
            this.roomomatic_subscr_only = roomomatic_subscr_only;
            this.doorx = doorx;
            this.doory = doory;
            this.doorh = doorh;
            this.doorz = doorz;
            this.heightMap = heightMap;
            this.publicroom = (publicroom_items != "");
            this.publicroom_items = publicroom_items;
            this.hasSwimmingPool = hasSwimmingPool;
            this.specialcast_emitter = specialcast_emitter;
            this.specialCast_interval = specialCast_interval;
            this.specialcast_rnd_max = specialcast_rnd_max;
            this.specialcast_rnd_min = specialcast_rnd_min;
            makeHeightMapDetails();
        }

        /// <summary>
        /// Generates information about the heightmap
        /// </summary>
        /// <param name="Heightmap">heightmap in string format</param>
        private void makeHeightMapDetails()
        {
            heightMap = heightMap.Replace("\n", null);
            string[] tmpHeightmap = heightMap.Split('\r');
            int colX = tmpHeightmap[0].Length;
            int colY = tmpHeightmap.Length - 1;

            this.sqSTATE = new Holo.Virtual.Rooms.virtualRoom.squareState[colX, colY];
            this.sqFLOORHEIGHT = new byte[colX, colY];
            this.sqITEMROT = new byte[colX, colY];
            this.sqITEMHEIGHT = new double[colX, colY];
            this.sqUNIT = new bool[colX,colY];
            this.sqTRIGGER = new squareTrigger[colX, colY]; 

            for (int y = 0; y < colY; y++)
            {
                for (int x = 0; x < colX; x++)
                {
                    string _SQ = tmpHeightmap[y].Substring(x, 1).Trim().ToLower();
                    if (_SQ == "x")
                        sqSTATE[x, y] = Holo.Virtual.Rooms.virtualRoom.squareState.Blocked;
                    else
                    {
                        sqSTATE[x, y] = Holo.Virtual.Rooms.virtualRoom.squareState.Open;
                        sqFLOORHEIGHT[x, y] = byte.Parse(_SQ);
                        
                    }
                }
            }


            if (publicroom)
            {
                string[] Items = this.publicroom_items.Split('\n');
                for (int i = 0; i < Items.Length; i++)
                {
                    string[] itemData = Items[i].Split(' ');
                    int X = int.Parse(itemData[2]);
                    int Y = int.Parse(itemData[3]);
                    Holo.Virtual.Rooms.virtualRoom.squareState sType;
                        sType = (Holo.Virtual.Rooms.virtualRoom.squareState)int.Parse(itemData[6]);
                    //catch { sType = (Holo.Virtual.Rooms.virtualRoom.squareState)2; }
                    sqSTATE[X, Y] = sType;
                    if (sType == Holo.Virtual.Rooms.virtualRoom.squareState.Seat)
                    {
                        sqITEMROT[X, Y] = byte.Parse(itemData[5]);
                        sqITEMHEIGHT[X, Y] = 1.0;
                    }

                    publicroom_items += itemData[0] + " " + itemData[1] + " " + itemData[2] + " " + itemData[3] + " " + itemData[4] + " " + itemData[5] + Convert.ToChar(13);

                }
                DataTable dTable;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dTable = dbClient.getTable("SELECT object,x,y,goalx,goaly,stepx,stepy,roomid,state FROM room_modeldata_triggers WHERE model = '" + model + "'");
                }
                foreach (DataRow dbRow in dTable.Rows)
                {
                    sqTRIGGER[Convert.ToInt32(dbRow["x"]), Convert.ToInt32(dbRow["y"])] = new squareTrigger(Convert.ToString(dbRow["object"]), Convert.ToInt32(dbRow["goalx"]), Convert.ToInt32(dbRow["goaly"]), Convert.ToInt32(dbRow["stepx"]), Convert.ToInt32(dbRow["stepy"]), (Convert.ToInt32(dbRow["state"]) == 1), Convert.ToInt32(dbRow["roomid"]));
                }
                sqSTATE[doorx, doory] = 0;
            }




        }
        /// <summary>
        /// Returns a copy of the squarestate
        /// </summary>
        /// <returns>The squarestate in a 2d array</returns>
        public Holo.Virtual.Rooms.virtualRoom.squareState[,] getSquareState()
        {
            return (Holo.Virtual.Rooms.virtualRoom.squareState[,])this.sqSTATE.Clone();
        }
        /// <summary>
        /// returns a copy of the floorheight
        /// </summary>
        /// <returns>The floorheight in a 2d array</returns>
        public byte[,] getSqFLOORHEIGHT()
        {
            return (byte[,])this.sqFLOORHEIGHT.Clone();
        }


        /// <summary>
        /// returns a copy of the item rotation
        /// </summary>
        /// <returns>The item rotation in a 2d array</returns>
        public byte[,] getSqITEMROT()
        {
            return (byte[,])sqITEMROT.Clone();
        }

        /// <summary>
        /// returns a copy of the item height
        /// </summary>
        /// <returns>The item height in a 2d array</returns>
        public double[,] getSqITEMHEIGHT()
        {
            return (double[,])sqITEMHEIGHT.Clone();
        }

        /// <summary>
        /// returns a copy of the square units
        /// </summary>
        /// <returns>The square units in a 2d array</returns>
        public bool[,] getSqUNIT()
        {
            return (bool[,])sqUNIT.Clone();
        }

        /// <summary>
        /// returns a copy of the triggerdata
        /// </summary>
        /// <returns>The triggerdata in a 2d array</returns>
        public squareTrigger[,] getSqTRIGGER()
        {
            return (squareTrigger[,])sqTRIGGER.Clone();
        }




    }
}
