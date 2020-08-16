using System;
using System.Data;
using System.Text;

using Holo.Managers;
using Ion.Storage;
using Holo.Virtual.Item;
using System.Collections.Generic;
namespace Holo.Virtual.Users.Items
{
    /// <summary>
    /// Represents the song editor of a virtual soundmachine, containing the active soundsets etc.
    /// </summary>
    class virtualSongEditor
    {
        private int machineID;
        private int[] Slot;
        private int[] SlotItemIDs;
        private bool[] SlotUpdated;
        private bool updated;
        Dictionary<int, int> musicDiskIds;

        internal virtualSongEditor(int machineID)
        {
            this.machineID = machineID;
            this.Slot = new int[4];
            this.SlotItemIDs = new int[4];
            this.SlotUpdated = new bool[4];
            updated = false;
            loadSoundsets();
        }
        #region Soundset slot management


        /// <summary>
        /// Returns the string with all the soundsets in the Hand of a certain user.
        /// </summary>
        /// <param name="userID">The database ID of the user to get the soundsets of.</param>
        public string getHandSoundsets(List<genericItem> handItem)
        {
            musicDiskIds = new Dictionary<int, int>();
            foreach (genericItem item in handItem)
            {
                if (item.template.isMusicDisk)
                {
                    if(!hasSoundSet(item.template.musicDiskNumber) && !musicDiskIds.ContainsKey(item.template.musicDiskNumber))
                    {
                        musicDiskIds.Add(item.template.musicDiskNumber, item.ID);
                    }
                }
            }

            StringBuilder Soundsets = new StringBuilder(Encoding.encodeVL64(musicDiskIds.Count));
            foreach (int i in musicDiskIds.Keys)
                Soundsets.Append(Encoding.encodeVL64(i));
            return Soundsets.ToString();
        }

        private bool hasSoundSet(int soundSetNumber)
        {
            for (int i = 0; i < Slot.Length; i++)
            {
                if (Slot[i] == soundSetNumber)
                    return true;
            }
            return false;
        }

        internal void loadSoundsets()
        {
            DataRow dRow;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dRow = dbClient.getRow("SELECT soundset_1, soundset_2, soundset_3, soundset_4 FROM soundmachine WHERE id = " + machineID);
            }
            string[] information;
            for(int i = 0; i < 4; i++)
            {
                information = dRow[i].ToString().Split(" ".ToCharArray());
                Slot[i] = int.Parse(information[0]);
                SlotItemIDs[i] = int.Parse(information[1]);
                SlotUpdated[i] = false;
            }

        }
        internal int addSoundset(int soundsetNumber, byte slotID)
        {
            if (musicDiskIds.ContainsKey(soundsetNumber))
            {
                Slot[slotID - 1] = soundsetNumber;
                SlotItemIDs[slotID - 1] = this.musicDiskIds[soundsetNumber];
                SlotUpdated[slotID - 1] = true;
                updated = true;
                return musicDiskIds[soundsetNumber];
            }
            return 0;

        }
        internal genericItem removeSoundset(int slotID)
        {
            Slot[slotID - 1] = 0;

            if (SlotItemIDs[slotID - 1] > 0)
            {
                genericItem item = new genericItem(this.SlotItemIDs[slotID - 1]);
                SlotUpdated[slotID - 1] = true;
                SlotItemIDs[slotID - 1] = 0;
                updated = true;
                return item;
            }
            else return null;
           
        }
        internal bool slotFree(int slotID)
        {
            return (Slot[slotID - 1] == 0);
        }
        internal string getSoundsets()
        {
            int Amount = 0;
            StringBuilder Soundsets = new StringBuilder();
            for (int slotID = 0; slotID < 4; slotID++)
            {
                int Soundset = Slot[slotID];
                if (Soundset > 0)
                {
                    Soundsets.Append(Encoding.encodeVL64(slotID + 1) + Encoding.encodeVL64(Soundset) + "QB"); // QB = 9, samples per set
                    int v = (Soundset * 9) - 8;
                    for (int j = v; j <= v + 8; j++)
                        Soundsets.Append(Encoding.encodeVL64(j));
                    Amount++;
                }
            }
            return "PA" + Encoding.encodeVL64(Amount) + Soundsets.ToString();
        }
        public void destroy()
        {
            saveDisks();
        }
        #endregion

        internal void saveDisks()
        {
            if (updated)
            {
                StringBuilder sb = new StringBuilder("UPDATE soundmachine set ");

                for (int i = 0; i < 4; i++)
                {
                    if (SlotUpdated[i])
                    {
                        sb.Append(" soundset_" + (i + 1) + " = '" + Slot[i] + " " + SlotItemIDs[i] + "' ,");
                    }
                }

                sb.Remove(sb.Length - 1, 1);
                sb.Append(" WHERE id = " + machineID);
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.runQuery(sb.ToString());
                }
            }
        }
    }
}
