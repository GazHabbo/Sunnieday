using System;
namespace Holo.Virtual.Rooms.Pathfinding
{
    public static class Rotation
    {
        public static byte Calculate(int X1, int Y1, int X2, int Y2)
        {
            byte Rotation = 0;
            if (X1 > X2 && Y1 > Y2)
                Rotation = 7;
            else if(X1 < X2 && Y1 < Y2)
                Rotation = 3;
            else if(X1 > X2 && Y1 < Y2)
                Rotation = 5;
            else if(X1 < X2 && Y1 > Y2)
                Rotation = 1;
            else if(X1 > X2)
                Rotation = 6;
            else if(X1 < X2)
                Rotation = 2;
            else if(Y1 < Y2)
                Rotation = 4;
            else if(Y1 > Y2)
                Rotation = 0;

            return Rotation;
        }
        public static byte headRotation(byte headRot, int X, int Y, int toX, int toY)
        {
            if(headRot == 2)
            {
                if(X <= toX && Y < toY)
                    return 3;
                else if(X <= toX && Y > toY)
                    return 5;
                else if(X < toX && Y == toY)
                    return 2;
            }
            else if(headRot == 4)
            {
                if(X > toX && Y <= toY)
                    return 5;
                else if(X < toX && Y <= toY)
                    return 3;
                else if(X == toX && Y < toY)
                    return 4;
            }
            else if(headRot == 6)
            {
                if(X >= toX && Y > toY)
                    return 7;
                else if(X >= toX && Y < toY)
                    return 5;
                else if(X > toX && Y == toY)
                    return 6;
            }
            else if(headRot == 0)
            {
                if(X > toX && Y >= toY)
                    return 9;
                if(X < toX && Y >= toY)
                    return 1;
                if(X == toX && Y > toY)
                    return 0;
            }
            return headRot;
        }
        //public static byte headRotation(byte oldRotation, int oldx, int oldy, int newx, int newy)
        //{
        //    byte headRotation = 0;
        //    if (oldx == newx)
        //    {
        //        if (oldy < newy)
        //            headRotation = 4;
        //        else
        //            headRotation = 0;

        //    } //Moved Left  
        //    else if (oldx > newx)
        //    {
        //        if (oldy == newy)
        //            headRotation = 6;
        //        else if (oldy < newy)
        //            headRotation = 5;
        //        else
        //            headRotation = 7;

        //    } //Moved Right  
        //    else if (oldx < newx)
        //    {
        //        if (oldy == newy)
        //            headRotation = 2;
        //        else if (oldy < newy)
        //            headRotation = 3;
        //        else
        //            headRotation = 1;
        //    }
        //    switch (headRotation)
        //    {
        //        case 0:
        //            if (headRotation > 1 || headRotation < 7)
        //                return oldRotation;
        //            break;
        //        case 1:
        //            if (headRotation > 2)
        //                return oldRotation;
        //            break;
        //        case 2:
        //            if (headRotation > 3 || headRotation < 1)
        //                return oldRotation;
        //            break;
        //        case 3:
        //            if (headRotation > 4 || headRotation < 2)
        //                return oldRotation;
        //            break;
        //        case 4:
        //            if (headRotation > 5 || headRotation < 3)
        //                return oldRotation;
        //            break;
        //        case 5:
        //            if (headRotation > 6 || headRotation < 4)
        //                return oldRotation;
        //            break;
        //        case 6:
        //            if (headRotation < 5)
        //                return oldRotation;
        //            break;
        //        case 7:
        //            if (headRotation > 0 || headRotation < 6)
        //                return oldRotation;
        //            break;
        //    }
            
        //    return headRotation;
        //}
    }
}
