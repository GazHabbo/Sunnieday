using System;
using System.Collections.Generic;
using System.Text;

namespace Holo.Virtual.Rooms.Games.Wobble_Squabble
{
    class UserVariables
    {
        internal int QueuePosition = -1;
        internal int Side = 0;
        internal bool WaitingToJoinQueue = false;
        internal int Balance = 0;
        internal string PlayMove = "-";
        internal int PlayPosition = 0;
        internal bool UsedBalance = false;
        internal bool BeenUpdated = false;
        internal bool HitThem = false;
        internal GameManager MyGame = null;
        internal DateTime LastHit;
    }
}
