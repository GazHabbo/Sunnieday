using System;
using System.Data;
using System.Text;
using System.Collections;
using System.Threading;
using Ion.Storage;
using System.Collections.Generic;
using Holo.Virtual.Item;
using Holo.Virtual.Users;
using Holo.Source.Managers.Catalogue_manager;
using Holo.Source.Managers;
using Holo.Managers.catalogue;

namespace Holo.Managers
{
    /// <summary>
    /// Manager for catalogue page caching, catalogue item templates, catalogue purchase handling and few other catalogue related tasks.
    /// </summary>
    public static class catalogueManager
    {
        private static Hashtable cataDeals;
        private static Hashtable cataloguePages;
        private static Hashtable itemCache;
        private static Dictionary<byte, string> cataIndex;
        private static Hashtable itemCCTname;
        /// <summary>
        /// Initializes the catalogue manager, (re)caching all the pages and item templates.
        /// </summary>
        public static void Init()
        {
            //try
            {
                Out.WriteLine("Starting caching of catalogue + items...");
                DataColumn dCol;
                using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                {
                    dCol = dbClient.getColumn("SELECT indexid FROM catalogue_pages ORDER BY displayname ASC");
                }
                cataIndex = new Dictionary<byte, string>();
                cataloguePages = new Hashtable();
                itemCache = new Hashtable();
                itemCCTname = new Hashtable();
                cataDeals = new Hashtable();
                cacheItems();
                foreach (DataRow dRow in dCol.Table.Rows)
                {
                    cachePage(Convert.ToInt32(dRow["indexid"]));
                }

                cachePage(-1);
                for (byte i = 1; i <= 7; i++)
                    createPageIndex(i);
                Out.WriteLine("Successfully cached " + cataloguePages.Count + " catalogue pages and " + itemCache.Count + " item templates!");

            }
            //catch (Exception e) { Out.writeSeriousError(e.ToString()); Eucalypt.Shutdown(); }
        }

        /// <summary>
        /// Caches all item-information from the database
        /// </summary>
        private static void cacheItems()
        {
            DataTable dTable;
            using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
            {
                dTable = dbClient.getTable("SELECT tid, typeid, length, width, catalogue_cost, door, tradeable, recycleable, catalogue_name, catalogue_description, name_cct, colour, top, costs_belcredits FROM catalogue_items ");
            }
            int count = dTable.Rows.Count;
            int itemTemplateIDs;
            int itemTypeIDs;
            int itemLengths;
            int itemWidths;
            int itemCosts;
            int itemDoorFlags;
            int itemTradeableFlags;
            int itemRecycleableFlags;
            string itemNames;
            string itemDescs;
            string itemCCTs;
            string itemColours;
            string itemTopHs;
            bool costsBelCredits;
            itemTemplate template;
           
            foreach (DataRow dbRow in dTable.Rows)
            {
                itemTemplateIDs = Convert.ToInt32(dbRow["tid"]);
                itemTypeIDs = Convert.ToInt32(dbRow["typeid"]);
                itemLengths = Convert.ToInt32(dbRow["length"]);
                itemWidths = Convert.ToInt32(dbRow["width"]);
                itemCosts = Convert.ToInt32(dbRow["catalogue_cost"]);
                itemDoorFlags = Convert.ToInt32(dbRow["door"]);
                itemTradeableFlags = Convert.ToInt32(dbRow["tradeable"]);
                itemRecycleableFlags = Convert.ToInt32(dbRow["recycleable"]);
                itemNames = Convert.ToString(dbRow["catalogue_name"]);
                itemDescs = Convert.ToString(dbRow["catalogue_description"]);
                itemCCTs = Convert.ToString(dbRow["name_cct"]);
                itemColours = Convert.ToString(dbRow["colour"]);
                itemTopHs = Convert.ToString(dbRow["top"]);
                costsBelCredits = (Convert.ToString(dbRow["costs_belcredits"]) == "1");

                template = new itemTemplate(itemCCTs, Convert.ToByte(itemTypeIDs), itemColours, itemLengths, itemWidths, double.Parse(itemTopHs), (itemDoorFlags == 1), (itemTradeableFlags == 1), (itemRecycleableFlags == 1), itemTemplateIDs, itemCosts, (itemCCTs.Equals("nest")), costsBelCredits);
                if (!itemCCTname.Contains(itemCCTs))
                    itemCCTname.Add(itemCCTs, template);
                itemCache.Add(itemTemplateIDs, template);
            }
        }

        /// <summary>
        /// Caches a specified catalogue page, plus the items on this page.
        /// </summary>
        /// <param name="pageID">The ID of the page to cache. If -1 is specified, all the items that aren't on a page are cached.</param>
        private static void cachePage(int pageID)
        {
            DataRow dRow;
            using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
            {
                dRow = dbClient.getRow("SELECT indexname,minrank,displayname,style_layout,img_header,img_side,label_description,label_misc,label_moredetails FROM catalogue_pages WHERE indexid = '" + pageID + "' LIMIT 1");
            }

            if (pageID > 0 && dRow.Table.Rows.Count == 0)
            {
                return;
            }

            object[] pageObject = dRow.ItemArray;
            string[] pageData = new string[pageObject.Length];
            for (int a = 0; a < pageData.Length; a++)
                pageData[a] = pageObject[a].ToString();


            string pageIndexName = "";
            StringBuilder pageBuilder = new System.Text.StringBuilder();
            cataloguePage objPage = new cataloguePage();

            if (pageID > 0)
            {
                pageIndexName = pageData[0];
                objPage.displayName = pageData[2];
                if (pageData[1].ToString() == "True")
                {
                    objPage.minRank = 1;

                }
                else if (pageData[1].ToString() == "False")
                {
                    objPage.minRank = 0;

                }
                else
                {
                    objPage.minRank = Convert.ToByte(Byte.Parse(pageData[1]));
                }

                // Add the required fields for catalogue page (indexname, showname, page layout style (boxes etc)) 
                pageBuilder.Append("i:" + pageIndexName + Convert.ToChar(13) + "n:" + pageData[2] + Convert.ToChar(13) + "l:" + pageData[3] + Convert.ToChar(13));

                if (pageData[4] != "") // If there's a headline image set, add it 
                    pageBuilder.Append("g:" + pageData[4] + Convert.ToChar(13));
                if (pageData[5] != "")  // If there is/are side image(s) set, add it/them 
                    pageBuilder.Append("e:" + pageData[5] + Convert.ToChar(13));
                if (pageData[6] != "") // If there's a description set, add it 
                    pageBuilder.Append("h:" + pageData[6] + Convert.ToChar(13));
                if (pageData[8] != "") // If there's a 'Click here for more details' label set, add it 
                    pageBuilder.Append("w:" + pageData[8] + Convert.ToChar(13));
                if (pageData[7] != "") // If the misc additions field is not blank 
                {
                    string[] miscDetail = pageData[7].Split(Convert.ToChar(13));
                    for (int m = 0; m < miscDetail.Length; m++)
                        pageBuilder.Append(miscDetail[m] + Convert.ToChar(13));
                }
            }

            DataTable dTable;
            using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
            {
                dTable = dbClient.getTable("SELECT tid, typeid, length, width, catalogue_cost, door, tradeable, recycleable, catalogue_name, catalogue_description, name_cct, colour, top FROM catalogue_items WHERE catalogue_id_page = '" + pageID + "' ORDER BY catalogue_id_index ASC");
            }
            int count = dTable.Rows.Count;
            int[] itemTemplateIDs = new int[count];
            int[] itemTypeIDs = new int[count];
            int[] itemLengths = new int[count];
            int[] itemWidths = new int[count];
            int[] itemCosts = new int[count];
            int[] itemDoorFlags = new int[count];
            int[] itemTradeableFlags = new int[count];
            int[] itemRecycleableFlags = new int[count];
            string[] itemNames = new string[count];
            string[] itemDescs = new string[count];
            string[] itemCCTs = new string[count];
            string[] itemColours = new string[count];
            string[] itemTopHs = new string[count];
            int i = 0;
           
            foreach (DataRow dbRow in dTable.Rows)
            {
                itemTemplateIDs[i] = Convert.ToInt32(dbRow["tid"]);
                itemTypeIDs[i] = Convert.ToInt32(dbRow["typeid"]);
                itemLengths[i] = Convert.ToInt32(dbRow["length"]);
                itemWidths[i] = Convert.ToInt32(dbRow["width"]);
                itemCosts[i] = Convert.ToInt32(dbRow["catalogue_cost"]);
                itemDoorFlags[i] = Convert.ToInt32(dbRow["door"]);
                itemTradeableFlags[i] = Convert.ToInt32(dbRow["tradeable"]);
                itemRecycleableFlags[i] = Convert.ToInt32(dbRow["recycleable"]);
                itemNames[i] = Convert.ToString(dbRow["catalogue_name"]);
                itemDescs[i] = Convert.ToString(dbRow["catalogue_description"]);
                itemCCTs[i] = Convert.ToString(dbRow["name_cct"]);
                itemColours[i] = Convert.ToString(dbRow["colour"]);
                itemTopHs[i] = Convert.ToString(dbRow["top"]);
                i++;
            }
            for (i = 0; i < itemTemplateIDs.Length; i++)
            {
                itemTemplate template = getTemplate(itemTemplateIDs[i]);
                objPage.addItem(template);
                if (stringManager.getStringPart(itemCCTs[i], 0, 4) != "deal")
                {
                    if (pageID == -1)
                        continue;

                    pageBuilder.Append("p:" + itemNames[i] + Convert.ToChar(9) + itemDescs[i] + Convert.ToChar(9) + itemCosts[i] + Convert.ToChar(9) + Convert.ToChar(9));

                    if (itemTypeIDs[i] == 0)
                        pageBuilder.Append("i");
                    else
                        pageBuilder.Append("s");

                    pageBuilder.Append(Convert.ToChar(9) + itemCCTs[i] + Convert.ToChar(9));

                    if (itemTypeIDs[i] == 0)
                        pageBuilder.Append(Convert.ToChar(9));
                    else
                        pageBuilder.Append("0" + Convert.ToChar(9));

                    if (itemTypeIDs[i] == 0)
                        pageBuilder.Append(Convert.ToChar(9));
                    else
                        pageBuilder.Append(itemLengths[i] + "," + itemWidths[i] + Convert.ToChar(9));

                    pageBuilder.Append(itemCCTs[i] + Convert.ToChar(9));

                    if (itemTypeIDs[i] > 0)
                        pageBuilder.Append(itemColours[i]);

                    pageBuilder.Append(Convert.ToChar(13));
                }
                else
                {
                    int dealID = int.Parse(itemCCTs[i].Substring(4));
                    deals dealStruct = new deals();

                    using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                    {
                        dTable = dbClient.getTable("SELECT tid, amount FROM catalogue_deals WHERE id = '" + dealID + "'");
                    }
                    count = dTable.Rows.Count;
                    itemTemplate[] dealItemTIDs = new itemTemplate[count];
                    int[] dealItemAmounts = new int[count];
                    int n = 0;
                    foreach (DataRow dbRow in dTable.Rows)
                    {

                        dealItemTIDs[n] = getTemplate(Convert.ToInt32(dbRow["tid"]));
                        dealItemAmounts[n] = Convert.ToInt32(dbRow["amount"]);
                        n++;
                    }
                    dealStruct.cost = itemCosts[i];
                    dealStruct.dealItemAmounts = dealItemAmounts;
                    dealStruct.dealItemTemplates = dealItemTIDs;
                    pageBuilder.Append("p:" + itemNames[i] + Convert.ToChar(9) + itemDescs[i] + Convert.ToChar(9) + itemCosts[i] + Convert.ToChar(9) + Convert.ToChar(9) + "d");
                    pageBuilder.Append(Convert.ToChar(9), 4);
                    pageBuilder.Append("deal" + dealID + Convert.ToChar(9) + Convert.ToChar(9) + dealItemTIDs.Length + Convert.ToChar(9));

                    for (int l = 0; l < dealItemTIDs.Length; l++)
                    {
                        pageBuilder.Append(dealItemTIDs[l].Sprite + Convert.ToChar(9) + dealItemAmounts[l] + Convert.ToChar(9) + dealItemTIDs[l].Colour + Convert.ToChar(9));
                    }
                    cataDeals.Add(dealID, dealStruct);
                }
            }
            if (pageID == -1)
                return;

            objPage.pageData = pageBuilder.ToString();
            cataloguePages.Add(pageIndexName, objPage);
        }
        /// <summary>
        /// Returns a bool that specifies if the catalogue manager contains a certain page, specified by name.
        /// </summary>
        /// <param name="pageName">The name of the catalogue page to check.</param>
        public static bool getPageExists(string pageName)
        {
            return cataloguePages.ContainsKey(cataloguePages.ContainsKey(pageName));
        }
        /// <summary>
        /// Returns the index of catalogue pages for a certain user rank.
        /// </summary>
        /// <param name="userRank">The rank of the user to handout the index to.</param> 

        public static string getPageIndex(byte userRank)
        {
            lock (cataIndex)
            {
                if (cataIndex.ContainsKey(userRank))
                    return cataIndex[userRank];
                else
                    return createPageIndex(userRank);
            }
        }
        /// <summary>
        /// Returns the content of a certain catalogue page as string.
        /// </summary>
        /// <param name="pageName">The name of the catalogue page to retrieve the content of.</param>
        /// <param name="userRank">The rank of the user to handout the page content to. If this rank is lower than the required minimum rank to access this page, the 'access denied' cast is returned.</param>
        /// <returns></returns>
        public static cataloguePage getPageObject(string pageName, byte userRank)
        {
            try
            {
                cataloguePage objPage = ((cataloguePage)cataloguePages[pageName]);
                if (userRank < objPage.minRank)
                    return null;
                return objPage;
            }

            catch
            {
                return null;
            }
        }
        /// <summary>
        /// Creates an index page which contains all information about a page
        /// </summary>
        /// <param name="userRank">The rank of the user</param>
        /// <returns>A string containing the page, char 13 if there was an error</returns>
        private static string createPageIndex(byte userRank)

        {
            try
            {
                StringBuilder listBuilder = new StringBuilder();
                DataColumn dCol;
                using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                {
                    dCol = dbClient.getColumn("SELECT indexname FROM catalogue_pages WHERE minrank <= '" + userRank + "' ORDER BY indexid ASC");
                }
                string[] pageNames = dataHandling.dColToArray(dCol);

                for (int i = 0; i < pageNames.Length; i++)
                {
                    if (cataloguePages.ContainsKey(pageNames[i]))
                        listBuilder.Append(pageNames[i] + Convert.ToChar(9) + ((cataloguePage)cataloguePages[pageNames[i]]).displayName + Convert.ToChar(13));
                }
                if (cataIndex.ContainsKey(userRank))
                    cataIndex.Remove(userRank);
                cataIndex.Add(userRank, listBuilder.ToString());
                return listBuilder.ToString();
            }

            catch
            {
                if (cataIndex.ContainsKey(userRank))
                    cataIndex.Remove(userRank);
                cataIndex.Add(userRank, Convert.ToChar(13).ToString());
                return Convert.ToChar(13).ToString();

            }
        }

        /// <summary>
        /// Returns the content of a certain catalogue page as string.
        /// </summary>
        /// <param name="pageName">The name of the catalogue page to retrieve the content of.</param>
        /// <param name="userRank">The rank of the user to handout the page content to. If this rank is lower than the required minimum rank to access this page, the 'access denied' cast is returned.</param>
        /// <returns></returns>
        public static string getPage(string pageName, byte userRank)
        {
            try
            {
                cataloguePage objPage = ((cataloguePage)cataloguePages[pageName]);
                if (userRank < objPage.minRank)
                    return "holo.cast.catalogue.access_denied";

                return objPage.pageData;
            }

            catch
            {
                return "cast_catalogue.access_denied";
            }
        }
        
        /// <summary>
        /// gets a string with the item information
        /// </summary>
        /// <param name="itemIDs"></param>
        /// <returns></returns>
        public static string tradeItemList(List<genericItem> tradeItems)
        {
            StringBuilder List = new StringBuilder();
            int i = 0;
            foreach (genericItem item in tradeItems)
            {
                

                itemTemplate Template = item.template;
                List.Append("SI" + Convert.ToChar(30).ToString() + item.ID + Convert.ToChar(30).ToString() + i + Convert.ToChar(30));
                if (Template.typeID > 0)
                    List.Append("S");
                else
                    List.Append("I");
                List.Append(Convert.ToChar(30).ToString() + item.ID + Convert.ToChar(30).ToString() + Template.Sprite + Convert.ToChar(30));
                if (Template.typeID > 0) { List.Append(Template.Length + Convert.ToChar(30).ToString() + Template.Width + Convert.ToChar(30).ToString() + item.Var + Convert.ToChar(30).ToString()); }
                List.Append(Template.Colour + Convert.ToChar(30).ToString() + i + Convert.ToChar(30).ToString() + "/");
                i++;
            }
            return List.ToString();
        }
        /// <summary>
        /// \ if the wallposition for a wallitem is correct, if it is, then the output should be exactly the same as the input. If not, then the wallposition is invalid.
        /// </summary>
        /// <param name="wallPosition">The original wallposition. [input]</param>
        public static string wallPositionOK(string wallPosition)
        {
            try
            {
                string[] posD = wallPosition.Split(' ');
                if (posD[2] != "l" && posD[2] != "r")
                    return "";

                string[] widD = posD[0].Substring(3).Split(',');
                int widthX = int.Parse(widD[0]);
                int widthY = int.Parse(widD[1]);
                if (widthX < 0 || widthY < 0 || widthX > 200 || widthY > 200)
                    return "";

                string[] lenD = posD[1].Substring(2).Split(',');
                int lengthX = int.Parse(lenD[0]);
                int lengthY = int.Parse(lenD[1]);
                if (lengthX < 0 || lengthY < 0 || lengthX > 200 || lengthY > 200)
                    return "";

                return ":w=" + widthX + "," + widthY + " " + "l=" + lengthX + "," + lengthY + " " + posD[2];
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Returns the itemTemplate object matching a certain template ID. If the specified item template is not loaded, then an empty item template is returned.
        /// </summary>
        /// <param name="templateID">The template ID to return the item template of.</param>
        public static itemTemplate getTemplate(int templateID)
        {
            try { return (itemTemplate)itemCache[templateID]; }
            catch { return new itemTemplate(); }
        }

        /// <summary>
        /// Returns the deals object matching a certain deal ID. If the specified item template is not loaded, then an empty item template is returned.
        /// </summary>
        /// <param name="cctName">The CCT name to return the item template of.</param>
        public static deals getDeal(int dealID)
        {
            try { return (deals)cataDeals[dealID]; }
            catch { return new deals(); }
        }

        /// <summary>
        /// Returns the itemTemplate object matching a certain template ID. If the specified item template is not loaded, then an empty item template is returned.
        /// </summary>
        /// <param name="cctName">The CCT name to return the item template of.</param>
        public static itemTemplate getTemplate(string cctName)
        {
            try { return (itemTemplate)itemCCTname[cctName]; }
            catch { return new itemTemplate(); }
        }

        /// <summary>
        /// Inserts a new item into the database
        /// </summary>
        /// <param name="item">the item which is purchased</param>
        /// <param name="presentBoxID">The present box id</param>
        /// <returns>boolean indicating if its a present or not</returns>
        public static int insertItemInDatabase(purchaseItem item, int presentBoxID, int teleportID)
        {
            int newItemID;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.AddParamWithValue("var", item.var);
                newItemID = (int)dbClient.insertQuery("INSERT INTO furniture (tid, var, teleportid) VALUES (" + item.templateID + ", @var, " + teleportID + ")");
                if (item.present)
                {
                    dbClient.runQuery("INSERT INTO furniture_presents(id,itemid) VALUES ('" + presentBoxID + "','" + newItemID + "')");
                    newItemID = 0;
                }
            }

            return newItemID;
        }
        /// <summary>
        /// Handles a purchase of the catalogue
        /// </summary>
        /// <param name="user">The user which is buying an item</param>
        /// <param name="item">The item which he wants to purchase</param>
        public static int handlePurchase(purchaseItem item, virtualUser buyer)
        {
            if (!item.freeItem)
            {
                if (item.template.costsBelCredits)
                {
                    if (item.templateID == buyer.belcreditsWarningItemTemplateId)
                    {
                        if (item.Cost == 0 || item.Cost > buyer._BelCredits)
                            return errorList.COST_ERROR;
                    }
                    else
                    {
                        if (item.Cost == 0 || item.Cost > buyer._BelCredits)
                        {
                            buyer.sendNotify("Dit item kost " + item.Cost + " belcredits!\rJe hebt momenteel " + buyer._BelCredits + " belcredits!\r" +
                            "Je hebt dus niet genoeg belcredits om dit item te kopen!");
                        }
                        else
                        {
                            buyer.sendNotify("Dit item kost " + item.Cost + " belcredits!\rJe hebt momenteel " + buyer._BelCredits + " belcredits!\rAls je " +
                            "zeker weet dat je dit item wilt kopen klik dan nog een keer op kopen!");
                            buyer.belcreditsWarningItemTemplateId = item.templateID;
                        }
                        return 0;
                    }
                }

                else if (item.Cost == 0 || item.Cost > buyer._Credits)
                {
                    return errorList.COST_ERROR;
                }
            }
            int presentBoxID;
            List<genericItem> toAdd = new List<genericItem>();
            if (item.present) // Purchased as present
            {
                string boxSprite = "present_gen" + new Random().Next(1, 7);
                int boxTemplateID;
                boxTemplateID = getTemplate(boxSprite).templateID;
                using (DatabaseClient dbClient1 = Eucalypt.dbManager.GetClient())
                {
                    dbClient1.AddParamWithValue("tid", boxTemplateID);
                    dbClient1.AddParamWithValue("ownerid", item.receiver);
                    dbClient1.AddParamWithValue("var", "!" + item.boxMessage);
                    presentBoxID = (int)dbClient1.insertQuery("INSERT INTO furniture(tid,var) VALUES (@tid,@var)");
                    //dbClient1.runQuery("INSERT INTO user_ furniture (user_id, furniture_id) VALUES (@ownerid,'" + presentBoxID + "')");

                    toAdd.Add(new genericItem(presentBoxID, boxTemplateID, ("!" + item.boxMessage), ""));
                }
            }
            else { presentBoxID = 0; }
            int newItemID;
            switch (item.sprite)
            {
                case "pet0":
                case "pet1":
                case "pet2":
                    {
                        if (item.present)
                        {
                            buyer.sendNotify("Je kan geen huisdieren in een cadeau kopen!");
                            return 0;
                        }
                        string[] vars = item.var.Split('\x02');
                        int petType = int.Parse(item.sprite.Substring(3,1));
                        
                        string petName = vars[0];
                        int petRace = int.Parse(vars[1]);
                        string color = vars[2];
                        int randomNature = (new Random().Next(-10,10));
                        item.var = "";
                        item.templateID = 1600;
                        int id = insertItemInDatabase(item, presentBoxID, 0);
                        if (id == 0)
                            return 0;
                        using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                        {
                            dbClient.AddParamWithValue("name", petName);
                            dbClient.AddParamWithValue("type", petType);
                            dbClient.AddParamWithValue("color", color);
                            dbClient.runQuery("INSERT INTO `furniture_pets` (`id`, `name`, `type`, `race`, `color`, `nature_positive`, `born`, `last_nap`, `last_eat`, `last_drink`, `last_playtoy`, `last_playuser`, `friendship`, `x`, `y`) VALUES " +
                                "('" + id + "', @name , @type , '" + petRace + "', @color , '" + randomNature + "', '" + DateTime.Now.ToString() + "', '" + DateTime.Now.ToString() + "', '" + DateTime.Now.ToString() + "', '" + DateTime.Now.ToString() + "', '" + DateTime.Now.ToString() + "', '" + DateTime.Now.ToString() + "', '0.0', '0', '0')");
                        }
                        toAdd.Add(new genericItem(id, item.templateID, "", ""));
                        
                        break;
                    }
                case "landscape":
                case "wallpaper":
                case "floor":
                    {
                        if (int.TryParse(item.var, out newItemID))
                        {
                            if ((newItemID = insertItemInDatabase(item, presentBoxID, 0)) > 0)
                                toAdd.Add(new genericItem(newItemID, item.templateID, item.var, ""));
                        }
                        else
                            return 0;
                        break;
                    }

                case "roomdimmer":
                    {

                        item.var = "1,1,1,#000000,155";
                        if ((newItemID = insertItemInDatabase(item, presentBoxID, 0)) > 0)
                            toAdd.Add(new genericItem(newItemID, item.templateID, item.var, ""));
                        break;
                    }

                case "door":
                case "doorB":
                case "doorC":
                case "doorD":
                case "doorZ":
                case "ads_calip_tele":
                case "teleport_door":
                case "lostc_teleport":
                case "xmas08_telep":
                case "env_telep":
                case "JAN1x1T":
                case "JAN1x1TT":
                case "JAN1x1TTT":
                    {
                        int itemID1;
                        int itemID2;
                        using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                        {
                            itemID1 = (int)dbClient.insertQuery("INSERT INTO furniture(tid) VALUES ('" + item.templateID + "')");
                            itemID2 = (int)dbClient.insertQuery("INSERT INTO furniture(tid,teleportid) VALUES ('" + item.templateID + "','" + itemID1 + "')");
                            dbClient.runQuery("UPDATE furniture SET teleportid = '" + itemID2 + "' WHERE id = '" + itemID1 + "'");
                            if (item.present)
                            {
                                dbClient.runQuery("INSERT INTO furniture_presents(id,itemid) VALUES ('" + presentBoxID + "','" + itemID1 + "'), ('" + presentBoxID + "','" + itemID2 + "')");
                            }
                            else
                            {
                                //dbClient.runQuery("INSERT INTO user_ furniture (user_id, furniture_id) VALUES ('" + item.receiver + "','" + itemID2 + "'), ('" + item.receiver + "','" + itemID1 + "')");
                                toAdd.Add(new genericItem(itemID1, item.templateID, item.var, ""));
                                toAdd.Add(new genericItem(itemID2, item.templateID, item.var, ""));
                            }
                        }
                        break;
                    }


                case "post.it":
                case "post.it.vd":
                    {

                        item.var = "20";
                        if ((newItemID = insertItemInDatabase(item, presentBoxID, 0)) > 0)
                            toAdd.Add(new genericItem(newItemID, item.templateID, item.var, ""));
                        break;
                    }

                default:
                    {
                        if ((stringManager.getStringPart(item.Item, 0, 11) == "greektrophy") || (stringManager.getStringPart(item.Item, 0, 11) == "prizetrophy"))
                        {
                            string vari = buyer._Username + "\t" + DateTime.Today.ToShortDateString() + "\t" + stringManager.filterSwearWords(item.var);
                            vari = vari.Replace(@"\", "\\").Replace("'", @"\'").Replace("\n", "");
                            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                            {
                                newItemID = (int)dbClient.insertQuery("INSERT INTO furniture (tid, var) VALUES (" + item.templateID + ", '" + vari + "')");
                                if (item.present)
                                {
                                    dbClient.runQuery("INSERT INTO furniture_presents(id,itemid) VALUES ('" + presentBoxID + "','" + newItemID + "')");
                                }
                                else
                                {
                                    //dbClient.runQuery("INSERT INTO user_ furniture (user_id, furniture_id) VALUES (" + item.receiver + ",'" + newItemID + "')");
                                    toAdd.Add(new genericItem(newItemID, item.templateID, vari, ""));
                                }
                            }
                        }
                        else if (stringManager.getStringPart(item.Item, 0, 4) == "deal")
                        {
                            deals deal = getDeal(int.Parse(item.Item.Substring(4)));
                            StringBuilder sb = new StringBuilder();
                            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                            {
                                for (int i = 0; i < deal.dealItemTemplates.Length; i++)
                                {
                                    for (int x = 0; x < deal.dealItemAmounts[i]; x++)
                                    {
                                        string var = "";
                                        if (deal.dealItemTemplates[i].isPetFood)
                                            var = "H";
                                        newItemID = (int)dbClient.insertQuery("INSERT INTO furniture (tid, var) VALUES (" + deal.dealItemTemplates[i].templateID + ", '" + var + "')");
                                        if (item.present)
                                        {
                                            dbClient.runQuery("INSERT INTO furniture_presents(id,itemid) VALUES ('" + presentBoxID + "','" + newItemID + "')");
                                        }
                                        else
                                        {
                                            //dbClient.runQuery("INSERT INTO user_ furniture (user_id, furniture_id) VALUES (" + item.receiver + ",'" + newItemID + "')");
                                            toAdd.Add(new genericItem(newItemID, deal.dealItemTemplates[i].templateID, var, ""));
                                        }

                                    }
                                }
                            }
                        }

                        else if (item.template.isMusicDisk)
                        {
                            int soundSet = int.Parse(item.sprite.Substring(10));
                            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                            {
                                newItemID = (int)dbClient.insertQuery("INSERT INTO furniture (tid) VALUES ('" + item.templateID + "')");
                            }
                            toAdd.Add(new genericItem(newItemID, item.templateID, "", ""));
                        }
                        else if (item.template.isSoundMachine)
                        {

                            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                            {
                                
                                newItemID = (int)dbClient.insertQuery("INSERT INTO furniture (tid) VALUES ('" + item.templateID + "');  ");
                                dbClient.runQuery("INSERT INTO soundmachine (id, soundset_1, soundset_2, soundset_3, soundset_4) VALUES ( " + newItemID + ", '0 0', '0 0', '0 0', '0 0' )");
                                if (item.present)
                                {
                                    dbClient.runQuery("INSERT INTO furniture_presents(id,itemid) VALUES ('" + presentBoxID + "','" + newItemID + "')");
                                    newItemID = 0;
                                }
                                else
                                {
                                    toAdd.Add(new genericItem(newItemID, item.templateID, "", ""));
                                }
                            }

                        }
                        else if (item.template.isPetFood)
                        {
                            item.var = "H";
                            if ((newItemID = insertItemInDatabase(item, presentBoxID, 0)) > 0)
                                toAdd.Add(new genericItem(newItemID, item.templateID, "H", ""));
                        }
                        else
                        {
                            item.var = "";
                            if ((newItemID = insertItemInDatabase(item, presentBoxID, 0)) > 0)
                                toAdd.Add(new genericItem(newItemID, item.templateID, "", ""));
                            
                        }
                        break;
                    }
            }
            if (item.receiver == buyer.userID)
            {
                buyer.handItem.addItemList(toAdd);
                buyer.refreshHand("last");
            }
            else if (userManager.containsUser(item.receiver))
            {
                userManager.getUser(item.receiver).handItem.addItemList(toAdd);
                buyer.refreshHand("last");
            }
            if (item.template.costsBelCredits)
            {
                buyer.belcreditsWarningItemTemplateId = 0;
                buyer._BelCredits -= item.Cost;
            }
            else
            {
                buyer._Credits -= item.Cost;
            }
            buyer.sendData("@F" + buyer._Credits);
            
            return 0;


        }
    }
}
