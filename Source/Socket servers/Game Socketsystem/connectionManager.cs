using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Holo.Managers;
using System.Collections;
using Ion.Storage;

namespace Holo.Source.GameConnectionSystem
{
    class connectionHelper
    {
        private static int BannedIPcount;
        private static Hashtable BannedIPs = new Hashtable();

        public static bool IpIsBanned(string IP)
        {
            if (BannedIPs.ContainsKey(IP) == true)
                return true;
            else
            {
                string banReason = userManager.getBanReason(IP);
                if (banReason != "")
                {
                    BannedIPs.Add(IP, BannedIPcount);
                    BannedIPcount++;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        //internal static void BanIP(string IP)
        //{
        //    if (IpIsBanned(IP) == false)
        //    {
        //        using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
        //        {
        //            if (!dbClient.findsResult("SELECT date_expire FROM users_bans WHERE ipaddress = '" + IP + "'"))
        //            {
        //                dbClient.runQuery("INSERT INTO users_bans (ipaddress,date_expire,descr) VALUES ('" + IP + "','" + DateTime.Now + "','Automaticly banned by system')");
        //                BannedIPs.Add(IP, BannedIPcount);
        //                BannedIPcount++;
        //            }
        //        }
        //    }
        //}
    }
}
