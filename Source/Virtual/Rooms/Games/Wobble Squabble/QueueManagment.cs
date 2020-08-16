using System;
using System.Collections;
using Holo.Virtual;

namespace Holo.Virtual.Rooms.Games.Wobble_Squabble
{
    class QueueManagment
    {
        Holo.Virtual.Users.virtualUser[,] UsersInQueue = new Holo.Virtual.Users.virtualUser[4, 2]; // [Place in queue , Side]
        internal GameManager Game = null;
        internal bool JoinQueue(Holo.Virtual.Users.virtualUser usr, int Side)
        {
            while(UsersInQueue[0, Side] != null)
            {
                if(usr._WSVariables.WaitingToJoinQueue == false)
                    return false;
            }
            UsersInQueue[0, Side] = usr;
            return true;
        }
        internal string GetCurrentPlayingPacket()
        {
            if(Game == null)
                return null;
            return "AH39\x01Ar0:" + Game.User0._roomID + "\r" + "1:" + Game.User1._roomID;
        }
        internal void LeaveQueue(int Position, int Side)
        {
            UsersInQueue[Position, Side] = null;
            UpdateQueue();
        }
        internal void UpdateQueue()
        {
            bool AUser = false;
            for(int i = 0 ; i < 2 ; i++) // Side 0 & 1
            {
                for(int x = 2 ; x >= 0 ; x--) // Place in queue 0 - 2 (3 = 'Ready to play')
                {
                    Holo.Virtual.Users.virtualUser usr = UsersInQueue[x, i];
                    if(usr != null)
                    {
                        AUser = true;
                        if(UsersInQueue[x + 1, i] == null)
                        {
                            if(i == 0)
                            {
                                usr.roomUser.goalX = usr.roomUser.X;
                                usr.roomUser.goalY = usr.roomUser.Y - 1;
                                usr.Room.moveUser(usr.roomUser, usr.roomUser.X, (usr.roomUser.Y - 1), false);
                            }
                            else if(i == 1)
                            {
                                usr.roomUser.goalX = usr.roomUser.X;
                                usr.roomUser.goalY = usr.roomUser.Y + 1;
                                usr.Room.moveUser(usr.roomUser, usr.roomUser.X, (usr.roomUser.Y + 1), false);
                            }
                            UsersInQueue[x + 1, i] = usr;
                            UsersInQueue[x, i] = null;
                            usr._WSVariables.QueuePosition = x + 1;
                            if(x == 2) // ON Cord 3 aka ready to play
                            {
                                if(i == 0)
                                {
                                    System.Threading.Thread.Sleep(200);
                                    if(UsersInQueue[3, 1] != null)
                                    {
                                        // Both rdy to play! :)
                                        UsersInQueue[3, 0].Room.sendData("As0:" + UsersInQueue[3, 0]._roomID + "\r" + "1:" + UsersInQueue[3, 1]._roomID);
                                        Game = new GameManager(UsersInQueue[3,0], UsersInQueue[3,1]);
                                    }
                                }
                                if(i == 1)
                                {
                                    if(UsersInQueue[3, 0] != null)
                                    {
                                        // Both rdy to play! :)
                                        UsersInQueue[3, 1].Room.sendData("As0:" + UsersInQueue[3, 0]._roomID + "\r" + "1:" + UsersInQueue[3, 1]._roomID);
                                        Game = new GameManager(UsersInQueue[3, 0], UsersInQueue[3, 1]);
                                    }
                                }
                            }
                            System.Threading.Thread.Sleep(300 + new Random().Next(0, 150));
                        }
                    }
                }
            }
            if(AUser == false)
                System.Threading.Thread.Sleep(700);
        }
    }
}
