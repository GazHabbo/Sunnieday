using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Holo.Source.Managers.RoomManager
{
    /// <summary>
    /// Represents a trigger square that invokes a special event.
    /// </summary>
    public class squareTrigger
    {
        /// <summary>
        /// The object of this trigger.
        /// </summary>
        internal readonly string Object;
        /// <summary>
        /// Optional. The new destination X of the virtual unit that invokes the trigger, walking.
        /// </summary>
        internal readonly int goalX;
        /// <summary>
        /// Optional. The new destination Y of the virtual unit that invokes the trigger, walking.
        /// </summary>
        internal readonly int goalY;
        /// <summary>
        /// Optional. The next X step of the virtual unit that invokes the trigger, stepping.
        /// </summary>
        internal readonly int stepX;
        /// <summary>
        /// Optional. The next Y step of the virtual unit that invokes the trigger, stepping.
        /// </summary>
        internal readonly int stepY;
        /// <summary>
        /// Optional. Optional. In case of a warp tile, this is the database ID of the destination room.
        /// </summary>
        internal readonly int roomID;
        /// <summary>
        /// Optional. A boolean flag for the trigger.
        /// </summary>
        internal bool State;
        /// <summary>
        /// Initializes the new trigger.
        /// </summary>
        /// <param name="Object">The object of this rigger.</param>
        /// <param name="goalX">Optional. The destination X of the virtual unit that invokes the trigger, walking.</param>
        /// <param name="goalY">Optional. The destination Y of the virtual unit that invokes the trigger, walking.</param>
        /// <param name="stepX">Optional. The next X step of the virtual unit that invokes the trigger, stepping.</param>
        /// <param name="stepY">Optional. The next Y step of the virtual unit that invokes the trigger, stepping.</param>
        /// <param name="roomID">Optional. In case of a warp tile, this is the database ID of the destination room.</param>
        /// <param name="State">Optional. A boolean flag for the trigger.</param>
        internal squareTrigger(string Object, int goalX, int goalY, int stepX, int stepY, bool State, int roomID)
        {
            this.Object = Object;
            this.goalX = goalX;
            this.goalY = goalY;
            this.stepX = stepX;
            this.stepY = stepY;
            this.roomID = roomID;
            this.State = State;
        }
    }
}
