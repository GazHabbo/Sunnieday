using System;
using System.Collections.Generic;
using System.Text;
using Holo;
using Holo.Virtual.Users;
using Ion.Storage;

namespace Holo.Virtual.Rooms.Games.Wobble_Squabble
{
    class GameManager
    {
        internal virtualUser User0;
        internal virtualUser User1;
        internal bool StopPlay = false;
        System.Threading.Thread GameTimeOut = null;
        public GameManager(virtualUser User0, virtualUser User1)
        {
            this.User0 = User0;
            this.User1 = User1;
            System.Threading.Thread Game = new System.Threading.Thread(new System.Threading.ThreadStart(StartGame));
            Game.Start();
        }
        private void Timeout()
        {
            System.Threading.Thread.Sleep(Config.WS_GAMEMAX_TIME);
            StopPlay = true;
            Draw();
        }
        private void StartGame()
        {
            System.Threading.Thread.Sleep(3000); // Pause for 3 seconds b4 playing
            GameTimeOut = new System.Threading.Thread(new System.Threading.ThreadStart(Timeout));
            GameTimeOut.Start();
            try
            {
                User0._WSVariables.MyGame = this;
            }
            catch { Win(1,-1); return; }
            try
            {
                User1._WSVariables.MyGame = this;
            }
            catch { Win(0, -1); return; }
            User0._WSVariables.LastHit = DateTime.Now;
            User1._WSVariables.LastHit = DateTime.Now;
            User0.Room.sendData("Ar0:" + User0._roomID + "\r" + "1:" + User1._roomID);
            User0._WSVariables.PlayPosition = -3;
            User1._WSVariables.PlayPosition = 4;
            SendPlayMoves();
        }
        internal bool SpotIsFree(int Position)
        {
            if(User0._WSVariables.PlayPosition == Position)
                return false;
            if(User1._WSVariables.PlayPosition == Position)
                return false;
            return true;
        }
        internal void SendPlayMoves()
        {
            while(true)
            {
                if (User1._isLoggedIn == true || User0._isLoggedIn == true)
                {
                    break;
                }
                if(StopPlay == true)
                    break;
                if(User0._WSVariables.Balance <= -100)// User 1 wins
                {
                    Win(1,-1);
                    break;
                }
                else if(User0._WSVariables.Balance >= 100)
                {
                    Win(1, 1);
                    break;
                }
                else if(User1._WSVariables.Balance <= -100)// User 0 wins
                {
                    Win(0, -1);
                    break;
                }
                else if(User1._WSVariables.Balance >= 100)
                {
                    Win(0, 1);
                        break;
                }
                string User0BeenHit = "";
                string User1BeenHit = "";
                if(User0._WSVariables.HitThem == true)
                {
                    if(User0._WSVariables.PlayMove == "E")
                    {
                        User1BeenHit = "h";
                        User1._WSVariables.Balance -= (Config.WS_HIT_POINTS + new Random().Next(0, 10));
                    }
                    if(User0._WSVariables.PlayMove == "W")
                    {
                        User1BeenHit = "h";
                        User1._WSVariables.Balance += ( Config.WS_HIT_POINTS + new Random().Next(0, 10) );
                    }
                }
                if(User1._WSVariables.HitThem == true)
                {
                    if(User1._WSVariables.PlayMove == "E")
                    {
                        User0BeenHit = "h";
                        User0._WSVariables.Balance -= ( Config.WS_HIT_POINTS + new Random().Next(0, 10) );
                    }
                    if(User1._WSVariables.PlayMove == "W")
                    {
                        User0BeenHit = "h";
                        User0._WSVariables.Balance += ( Config.WS_HIT_POINTS + new Random().Next(0, 10) );
                    }
                }
                if(User0._WSVariables.BeenUpdated == true || User1._WSVariables.BeenUpdated == true)
                {
                    User0.Room.sendData("Av" + User0._WSVariables.PlayPosition + "\t" + User0._WSVariables.Balance + "\t" + User0._WSVariables.PlayMove + "\t" + User0BeenHit + "\r" +
                                                       User1._WSVariables.PlayPosition + "\t" + User1._WSVariables.Balance + "\t" + User1._WSVariables.PlayMove + "\t" + User1BeenHit);
                    User0._WSVariables.BeenUpdated = false;
                    User1._WSVariables.BeenUpdated = false;
                    User0._WSVariables.PlayMove = "-";
                    User1._WSVariables.PlayMove = "-";
                }
                System.Threading.Thread.Sleep(200);

            }
        }
        internal void Draw()
        {
            if(User0._WSVariables == null)
                return;
            if(User0._WSVariables.MyGame == null)
                return;
            // Null the game
            User0._WSVariables.MyGame = null;
            User1._WSVariables.MyGame = null;
            User0.Room._WSQueueManager.Game = null;
            // Move players into water
            // Player on left
            User0.roomUser.X += -1;
            User0.roomUser.H = User0.Room.sqFLOORHEIGHT[User0.roomUser.X, User0.roomUser.Y];
            // Player on right
            User1.roomUser.X += -1;
            User1.roomUser.H = User1.Room.sqFLOORHEIGHT[User1.roomUser.X, User1.roomUser.Y];
            
            // Deduct tickets from Both players
            User1._Tickets -= Config.WS_TICKET_COST;
            using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
            {
                dbClient.runQuery("UPDATE `users` SET `tickets` = '" + User1._Tickets + "' WHERE `ID` = '" + User1.userID + "'");
                User0._Tickets -= Config.WS_TICKET_COST;
                dbClient.runQuery("UPDATE `users` SET `tickets` = '" + User0._Tickets + "' WHERE `ID` = '" + User0.userID + "'");
            }
            // End game
            User0._WSVariables.PlayMove = "-";
            User1._WSVariables.PlayMove = "-";
            User0.Room.sendData("Av" + User0._WSVariables.PlayPosition + "\t" + User0._WSVariables.Balance + "\t" + User0._WSVariables.PlayMove + "\t" + "\r" +
                                                       User1._WSVariables.PlayPosition + "\t" + User1._WSVariables.Balance + "\t" + User1._WSVariables.PlayMove + "\t");
            User0.Room.sendData("At");
            // Remove both from queue
            User1.Room._WSQueueManager.LeaveQueue(User1._WSVariables.QueuePosition, User1._WSVariables.Side);
            User0.Room._WSQueueManager.LeaveQueue(User0._WSVariables.QueuePosition, User0._WSVariables.Side);
            User0._WSVariables = null;
            User1._WSVariables = null;
            User0.statusManager.addStatus("swim", null, 0, null, 0, 0);
            User1.statusManager.addStatus("swim", null, 0, null, 0, 0);
        }
        internal void Win(int Player, int Direction)
        {
            User0._WSVariables.MyGame = null;
            User1._WSVariables.MyGame = null;
            User0.Room._WSQueueManager.Game = null;
            try
            {
                GameTimeOut.Abort();
            }
            catch { }
            Direction = -1;
            User0.Room.sendData("Aw" + Player);
            if(Player == 1)
            {
                User0.roomUser.X += Direction;
                //User0.Room.GetInstanceUserValues(User0._roomID).Y += User0._WSVariables.PlayPosition - 4;
                User0.roomUser.H = User0.Room.sqFLOORHEIGHT[User0.roomUser.X, User0.roomUser.Y];
                User0.statusManager.addStatus("swim", null, 0, null, 0, 0);
                System.Threading.Thread.Sleep(1000);
                User0.Room.sendData("At");
                User0._Tickets-= Config.WS_TICKET_COST;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.runQuery("UPDATE `users` SET `tickets` = '" + User0._Tickets + "' WHERE `ID` = '" + User0.userID + "'");
                }
                User0.Room._WSQueueManager.LeaveQueue(User0._WSVariables.QueuePosition, User0._WSVariables.Side);
                User0._WSVariables = null;
                User1._WSVariables.Balance = 0;
                User1._WSVariables.HitThem = false;
                User1._WSVariables.UsedBalance = false;
            }
            else if(Player == 0)
            {
                User1.roomUser.X+= Direction;
                //User1.Room.GetInstanceUserValues(User0._roomID).Y += User1._WSVariables.PlayPosition - 4;
                User1.roomUser.H = User1.Room.sqFLOORHEIGHT[User0.roomUser.X, User1.roomUser.Y];
                User1.statusManager.addStatus("swim", null, 0, null, 0, 0);
                
                System.Threading.Thread.Sleep(1000);
                User1.Room.sendData("At");
                User1._Tickets-= Config.WS_TICKET_COST;
                using (DatabaseClient dbClient = Eucalypt.dbManager.GetClient())
                {
                    dbClient.runQuery("UPDATE `users` SET `tickets` = '" + User1._Tickets + "' WHERE `ID` = '" + User1.userID + "'");
                }
                User1.Room._WSQueueManager.LeaveQueue(User1._WSVariables.QueuePosition, User1._WSVariables.Side);
                User1._WSVariables = null;
                User0._WSVariables.Balance = 0;
                User0._WSVariables.HitThem = false;
                User0._WSVariables.UsedBalance = false;
            }
           
        }
    }
}
