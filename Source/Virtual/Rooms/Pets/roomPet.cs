using System;
using Holo.Manager.Text;
using Holo.Virtual.Item;
using Holo.Virtual.Rooms;
using Holo.Virtual.Rooms.Pathfinding;
using Holo.Virtual.Users;

namespace Holo.Source.Virtual.Rooms.Pets
{
    public class roomPet
    {
        #region Fields
        #region talk items
        /// <summary>
        /// Talk items
        /// </summary>
        public static string[] dogTalk = { "Woef woef!", "Waf blaf!", "Wruf!" };
        /// <summary>
        /// Talk items
        /// </summary>
        public static string[] catTalk = { "Mauwwww", "Yawn!", "purr purr purr!" };
        /// <summary>
        /// Talk items
        /// </summary>
        public static string[] crockTalk = { "Tik tak!", "grr!!", "*bijt* gneghne!" };
        #endregion

        /// <summary>
        /// The random generator for this instance
        /// </summary>
        private Random RND;
        
        /// <summary>
        /// A virtualPetInformation instance with all kinds of information of this pet, such as it's appearance etc.
        /// </summary>
        public virtualPetInformation Information;

        /// <summary>
        /// The statusmanager of this pet
        /// </summary>
        internal virtualRoomStatusManager statusManager;

        private DateTime lastUserInteraction;

        /// <summary>
        /// an indication if this pet is sleeping or not
        /// </summary>
        private bool sleeping = false;

        /// <summary>
        /// Indicates if this pet is interacting with other people
        /// </summary>
        private bool interacting
        {
            get
            {
                if ((DateTime.Now - lastUserInteraction).TotalSeconds > 10)
                    return false;
                else
                    return true;
            }
            set
            {
                lastUserInteraction = DateTime.Now;
            }
        }

        /// <summary>
        /// The room this pet is currently in
        /// </summary>
        private virtualRoom room;
        
        /// <summary>
        /// Used for pathfinding. The X coordinate of the bot's target square in the virtual room.
        /// </summary>
        internal int goalX;
        
        /// <summary>
        /// Used for pathfinding. The Y coordinate of the bot's target square in the virtual room.
        /// </summary>
        internal int goalY;

        /// <summary>
        /// The last AI action timestamp
        /// </summary>
        DateTime lastAiAction = DateTime.Now.AddSeconds(new Random((int)DateTime.Now.Ticks).Next(0,4));

        /// <summary>
        /// The pets nest
        /// </summary>
        internal genericItem nest
        {
            get
            {
                return room.floorItemManager.getItem(Information.ID);
            }
        }

        /// <summary>
        /// The field used by the AI interface to play or interact with an item
        /// </summary>
        internal genericItem itemTimeItem;

        /// <summary>
        /// The x location in byte format
        /// </summary>
        internal byte X
        {
            get
            {
                return Information.lastX;
            }
            set
            {
                Information.lastX = value;
            }
        }

        /// <summary>
        /// The y location in byte format
        /// </summary>
        internal byte Y
        {
            get
            {
                return Information.lastY;
            }
            set
            {
                Information.lastY = value;
            }
        }

        /// <summary>
        /// the h location of this item
        /// </summary>
        internal double H;
        
        /// <summary>
        /// rotation
        /// </summary>
        internal byte Z1;

        /// <summary>
        /// Head rotation
        /// </summary>
        internal float Z2;

        /// <summary>
        /// The room unique ID
        /// </summary>
        internal int roomUID;

        /// <summary>
        /// The ID of the action this instance is currently doing
        /// -1 = nothing
        /// 0 = walking
        /// 1 = walking to bed to sleep
        /// 2 = sleeping
        /// 3 = going to drink
        /// 4 = going to food
        /// 5 = going to toy
        /// 6 = following user in room
        /// 7 = waiting for next command
        /// </summary>
        internal int actionID = -1;

        /// <summary>
        /// The user which this item is following
        /// </summary>
        internal virtualRoomUser followUser;

        /// <summary>
        /// An indicator if this instance is following a person
        /// </summary>
        internal bool following
        {
            get
            {
                return followUser != null;
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Constructs a roomPet object for a given virtualPetInformation object.
        /// </summary>
        /// <param name="pInfo">The virtualPetInformation that holds the values for this room pet.</param>
        /// <param name="roomUID">The room unique id of this item</param>
        /// <param name="room"> The room this item is currently in</param>
        /// <param name="X">The x location of this item</param>
        /// <param name="Y">The y location of this item</param>
        public roomPet(virtualPetInformation pInfo, int roomUID, virtualRoom room, byte X, byte Y)
        {
            this.statusManager = new virtualRoomStatusManager(roomUID, room);

            this.Information = pInfo;
            this.X = X;
            this.Y = Y;
            this.roomUID = roomUID;
            this.room = room;
            if (room.sqITEMHEIGHT[X, Y] == 0)
            {
                H = room.sqITEMHEIGHT[X, Y];
            }
            else
            {
                H = room.sqITEMHEIGHT[X, Y];
            }
            room.sqUNIT[X, Y] = true;
            this.RND = new Random(Information.nature);
        }
        /// <summary>
        /// Constructs a roomPet object for a given virtualPetInformation object.
        /// </summary>
        /// <param name="pInfo">The virtualPetInformation that holds the values for this room pet.</param>
        /// <param name="roomUID">The room unique id of this item</param>
        /// <param name="room"> The room this item is currently in</param>
        public roomPet(virtualPetInformation pInfo, int roomUID, virtualRoom room)
        {
            this.statusManager = new virtualRoomStatusManager(roomUID, room);
            
            this.Information = pInfo;
            
            this.roomUID = roomUID;
            this.room = room;
            if (room.sqITEMHEIGHT[X, Y] == 0)
            {
                H = room.sqITEMHEIGHT[X, Y];
            }
            else
            {
                H = room.sqITEMHEIGHT[X, Y];
            }
            room.sqUNIT[X, Y] = true;
            this.RND = new Random(Information.nature);
            rotate();
        }
        #endregion

        #region Methods
        
        #region getters
        /// <summary>
        /// Converts this virtual room pet to a string to make it appear for game clients.
        /// </summary>
        public override string ToString()
        {
            fuseStringBuilder FSB = new fuseStringBuilder();
            if (this.Information != null)
            {
                FSB.appendKeyValueParameter("i", this.roomUID);
                FSB.appendKeyValueParameter("n", this.Information.ID.ToString() + Convert.ToChar(4).ToString() + this.Information.Name);
                FSB.appendKeyValueParameter("f", this.Information.Figure);
                FSB.appendKeyValueParameter("l", this.X + " " + this.Y + " " + this.H);

                return FSB.ToString();
            }

            return FSB.ToString();
        }
       
        /// <summary>
        /// The status string of the bot, containing positions, movements, statuses (animations) etc.
        /// </summary>
        internal string statusString
        {
            get
            {
                return roomUID + " " + X + "," + Y + "," + H.ToString().Replace(",", ".") + "," + Z1 + "," + Z2 + "/" + statusManager.ToString();
            }
        }
        #endregion

        #region Status management
        /// <summary>
        /// Adds a status key and a value to the bot's statuses. If the status is already inside, then the previous one will be removed.
        /// </summary>
        /// <param name="Key">The key of the status.</param>
        /// <param name="Value">The value of the status.</param>
        internal void addStatus(string Key, string Value)
        {
            statusManager.addStatus(Key, Value, 0, null, 0, 0);
        }
        /// <summary>
        /// Removes a certain status from the bot's statuses.
        /// </summary>
        /// <param name="Key">The key of the status to remove.</param>
        internal void removeStatus(string Key)
        {
            statusManager.removeStatus(Key);
        }
        /// <summary>
        /// Returns a bool that indicates if the bot has a certain status at the moment.
        /// </summary>
        /// <param name="Key">The key of the status to check.</param>
        internal bool containsStatus(string Key)
        {
            return statusManager.containsStatus(Key);
        }
       
        /// <summary>
        /// Adds a status to the bot, keeps it for a specified amount of time [in ms] and removes the status. Refreshes at add and remove.
        /// </summary>
        /// <param name="Key">The key of the status, eg, 'sit'.</param>
        /// <param name="Value">The value of the status, eg, '1.0'.</param>
        /// <param name="Length">The amount of milliseconsd to keep the status before removing it again.</param>
        internal void handleStatus(string Key, string Value, int Length)
        {
            statusManager.addStatus(Key, Value, Length, null, 0, 0);
        }
        #endregion

        #region AI

        #region AI cycle functions
        /// <summary>
        /// Handles the bot's artifical intelligence, by interacting with users and using random values etc.
        /// This is called every 410 milli seconds
        /// </summary>
        internal void doAI()
        {
            if (busyWithAction())
                finishAction();
            else
                doNewAction();
        }

        /// <summary>
        /// Finishes an action
        /// </summary>
        private void finishAction()
        {
            if (!sleeping && actionID == 1 && goalX == -1)
                setSleeping(true);
            else if (actionID == 0 && goalX == -1)
                this.actionID = -1;
            else if (actionID == 3 && goalX == -1)
                doDrink();
            else if (actionID == 4 && goalX == -1)
                doEat();
            else if (actionID == 5 && goalX == -1)
                doPlay();
            else if (actionID == 6)
                doFollow();
        }

        
        /// <summary>
        /// Does a new action
        /// </summary>
        private void doNewAction()
        {
            
            int action = RND.Next(0, 7707070) % 7;

            switch (action)
            {
                case 0: // Move - done
                case 1:
                    walk();
                    break;
                case 2: // Rotate - done
                    rotate();
                    break;
                case 3: // Say - done
                    sendSaying();
                    break;
                case 4: // Status - done
                    doNewStatus();
                    break;
                case 5: // Item time - done
                    doItemTime();
                    break;
                case 6: // sleep/walk to nest - done
                    if (RND.Next(0, 10) == 5)
                        goToSleep();
                    else
                        walkToNest();
                    break;
            }
            this.lastAiAction = DateTime.Now;
        }

        /// <summary>
        /// Checks if this instance is busy with an action or not
        /// </summary>
        /// <returns></returns>
        private bool busyWithAction()
        {
            return !(actionID == -1 && (DateTime.Now - lastAiAction).TotalSeconds > 4 && goalX == -1 && !interacting);
        }
        #endregion

        #region AI functions

        #region eating and drinking
        /// <summary>
        /// Lets the pet drink something
        /// </summary>
        private void doDrink()
        {
            this.actionID = -1;
            if (itemTimeItem == null)
                return;
            if (!sameLocationAsItem())
                return;
            rotateToItem(itemTimeItem);
            if (this.Information.thirsty)
            {
                this.Information.Thirst++;
                room.sendSaying(this.roomUID, "*lik lik lik* Aahhh....");
            }
            else
            {
                if (RND.Next(0, 3) == 2)
                    room.sendSaying(this.roomUID, "*burp*");
            }
            lastAiAction = DateTime.Now;
        }

        /// <summary>
        /// Lets the pet eat something
        /// </summary>
        private void doEat()
        {
            this.actionID = -1;
            if (itemTimeItem == null)
                return;
            if (!sameLocationAsItem())
                return;
            lastAiAction = DateTime.Now;
            this.Information.Hunger++;
            rotateToItem(itemTimeItem);
            if (!correctFood(itemTimeItem))
            {
                room.sendSaying(this.roomUID, "Blech...!");
            }
            else
            {
                if (this.Information.hungry)
                {
                    room.petEatsItem(itemTimeItem);
                    this.Information.Hunger++;
                    if (!this.Information.hungry || !room.floorItemManager.containsItem(itemTimeItem.ID))
                        room.sendSaying(this.roomUID, "Hap hap hap.     YUM! |");
                    else
                        this.actionID = 4;
                }
                else
                {
                    if (RND.Next(0, 3) == 2)
                        room.sendSaying(this.roomUID, "*burp*");
                }
            }
        }

        /// <summary>
        /// Checks if an eatable item is ment for this ind of pet
        /// </summary>
        /// <param name="item">The item which will be consumed, or not</param>
        private bool correctFood(genericItem item)
        {
            if (item.template.petFoodID == 0)
                return true;
            switch (Information.Type)
            {
                case 0:
                    return item.template.petFoodID == 1;
                case 1:
                    return item.template.petFoodID == 2;
                case 2:
                    return item.template.petFoodID == 3;
                default:
                    return false;
            }
        }
        #endregion

        #region Item interaction
        /// <summary>
        /// Lets the pet play with an item
        /// </summary>
        private void doPlay()
        {
            this.actionID = -1;
            if (itemTimeItem == null)
                return;
            if (!sameLocationAsItem())
                return;
            this.room.doFurniPlay(this.itemTimeItem, 3);
            rotateToItem(itemTimeItem);
            room.sendSaying(this.roomUID, "*pok pok pats* wii!");
            this.goLay(4000);

            lastAiAction = DateTime.Now;
        }

        /// <summary>
        /// does an check if the location of the pet is the same as an item (to avoid ghost-actions)
        /// </summary>
        /// <returns>Indication if this pet is in the same location as the ItemTimeItem</returns>
        private bool sameLocationAsItem()
        {
            return (this.X == itemTimeItem.X && this.Y == itemTimeItem.Y);
        }

        /// <summary>
        /// Rotates the head to the given item.
        /// </summary>
        private void rotateToItem(genericItem item)
        {
            this.Z1 = item.Z;
            this.Z2 = Z1;
        }

        /// <summary>
        /// Gets an item and the pet walks to it and tries to interact with it.
        /// </summary>
        private void doItemTime()
        {
            this.itemTimeItem = room.floorItemManager.getPetRandomItem();
            doItemTimeItemCheck();

        }

        /// <summary>
        /// Does an check for the current itemTimeItem
        /// </summary>
        private void doItemTimeItemCheck()
        {
            if (itemTimeItem == null)
            {
                walk();
                return;
            }

            this.goalX = itemTimeItem.X;
            this.goalY = itemTimeItem.Y;

            if (itemTimeItem.template.isPetDrink)
                this.actionID = 3;
            else if (itemTimeItem.template.isPetFood)
                this.actionID = 4;
            else if (itemTimeItem.template.isPetToy)
                this.actionID = 5;
            else
                this.actionID = 0;
        }

        #endregion

        #region misc
        /// <summary>
        /// Lets this item sleep or wake up
        /// </summary>
        /// <param name="value">a boolean indicating if this pet is going to sleep or not</param>
        public void setSleeping(bool value)
        {
            if (value)
            {
                if (X == this.nest.X && Y == this.nest.Y)
                {
                    this.Z1 = (byte)RND.Next(1, 8);
                    this.Z2 = this.Z1;
                    statusManager.addStatus("slp", H.ToString(), 0, null, 0, 0);
                    room.sendSaying(roomUID, "ZZZzZZzzZZzz...");
                    Information.dtLastNap = DateTime.Now;
                    this.sleeping = true;
                    this.actionID = 2;
                }
                else
                {
                    this.actionID = -1;
                }
            }
            else
            {
                this.actionID = -1;
                int amountSlept = (int)Math.Round((DateTime.Now - Information.dtLastNap).TotalMinutes / 1);
                //Out.WriteLine(amountSlept.ToString());
                statusManager.Clear();
                room.sendSaying(roomUID, "|| *gaap* ||");
                this.sleeping = false;
            }
        }

        /// <summary>
        /// Goes to sleep
        /// </summary>
        private void goToSleep()
        {
            walkToNest();
            actionID = 1;
        }

        /// <summary>
        /// Walks this pet to its nest
        /// </summary>
        private void walkToNest()
        {
            goalX = nest.X;
            goalY = nest.Y;
        }

        #endregion

        #endregion

        #endregion

        #region new status
        
        /// <summary>
        /// Lets a pet wake up
        /// </summary>
        internal void wakeUp()
        {
            setSleeping(false);
        }

        /// <summary>
        /// Does a new status (sit/lay/sleep)
        /// </summary>
        internal void doNewStatus()
        {
            if (statusManager.containsStatus("sit")) 
                return;
            switch (RND.Next(1, 3))
            {
                case 1:
                    goSit(4000);
                    break;
                case 2:
                    goLay(4000);
                    break;
                case 3:
                    goJump(2000);
                    break;
            }
        }

        /// <summary>
        /// Lets this instance sit down
        /// </summary>
        /// <param name="duration">The total duration in milli seconds</param>
        internal void goSit(int duration)
        {
            removeAllStatusses();
            handleStatus("sit", H.ToString(), duration);
        }

        /// <summary>
        /// Clears all statusses of this instance
        /// </summary>
        private void removeAllStatusses()
        {
            statusManager.Clear();
        }

        /// <summary>
        /// Lets this instance lay on the floor
        /// </summary>
        /// <param name="duration">The total duration in milli seconds</param>
        internal void goLay(int duration)
        {
            removeAllStatusses();
            handleStatus("lay", H.ToString(), duration);
        }

        /// <summary>
        /// Lets this instance jump on the floot
        /// </summary>
        /// <param name="duration">The total duration in milli seconds</param>
        internal void goJump(int duration)
        {
            removeAllStatusses();
            handleStatus("jmp", Z1.ToString(), duration);
        }
        #endregion

        #region rotation / walking
        /// <summary>
        /// Rotates a pet
        /// </summary>
        private void rotate()
        {
            if (containsStatus("sit") || containsStatus("lay"))
                return;
            byte R = (byte)(RND.Next(0, 1000) % 11);
            while (R == Z2)
                R = (byte)(RND.Next(0, 1000) % 11);
            Rotate(R);
        }

        /// <summary>
        /// Sets a new rotation for the bot and refreshes it in the room. If the bot is sitting, then rotating will be ignored.
        /// </summary>
        /// <param name="R">The new rotation to use.</param>
        internal void Rotate(byte R)
        {
            if (R != Z1 && !containsStatus("sit") && !containsStatus("lay"))
            {
                Z1 = R;
                Z2 = R;
            }
        }

        /// <summary>
        /// Walks to a new random coordinate
        /// </summary>
        internal void walk()
        {
            Coord Next = new Coord();
            int[] Borders = room.getMapBorders();
            Next = new Coord(RND.Next(0, Borders[0]), RND.Next(0, Borders[1]));

            if (Next.X == room.doorX && Next.Y == room.doorY)
                return;
            if (Next.X == X && Next.Y == Y) // Coord didn't changed
            {
                Z1 = (byte)RND.Next(0, 10);
                Z2 = Z1;
            }
            else
            {
                goalX = Next.X;
                goalY = Next.Y;
                this.actionID = 0;
            }
        }
        #endregion
        
        #region chatting
        
        /// <summary>
        /// Sends a new saying to this room
        /// </summary>
        internal void sendSaying()
        {
            int x = RND.Next(0,30000) % 3;
            switch (Information.Type)
            {
                case 0:
                    room.sendSaying(roomUID, dogTalk[x]);
                    break;
                case 1:
                    room.sendSaying(roomUID, catTalk[x]);
                    break;
                case 2:
                    room.sendSaying(roomUID, crockTalk[x]);
                    break;
            }
        }
        #endregion

        #endregion

        #region user interaction
        /// <summary>
        /// Does an action based on user speech
        /// </summary>
        /// <param name="action">The action in string format</param>
        private bool listenAction(string action)
        {
            switch (action)
            {
                case "eet":
                    this.itemTimeItem = room.floorItemManager.getPetFoodItem(Information.Type + 1);
                    doItemTimeItemCheck();
                    return true;
                case "drink":
                    this.itemTimeItem = room.floorItemManager.getPetDrinkItem();
                    doItemTimeItemCheck();
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Does an action with user controll
        /// </summary>
        /// <param name="action">The action which may be excecuted</param>
        /// <param name="user">The user which send the command (owner only)</param>
        internal void doAction(string action, virtualRoomUser user)
        {
            interacting = true;
            if (this.sleeping)
            {
                wakeUp();
                return;
            }

            this.Z1 = Holo.Virtual.Rooms.Pathfinding.Rotation.Calculate(X, Y, user.X, user.Y);
            this.Z2 = Z1;

            if (listenAction(action))
                return;
            
            if (!wantsToListen())
                return;

            switch (action)
            {
                case "zit":
                    this.goSit(0);
                    break;
                case "lig":
                    this.goLay(0);
                    break;
                case "spring":
                    this.goJump(2000);
                    break;
                case "kom":
                    this.goalX = user.lastX;
                    this.goalY = user.lastY;
                    this.sendSaying();
                    this.actionID = 0;
                    return;
                case "speeltje":
                    this.itemTimeItem = room.floorItemManager.getPetPlayItem();
                    doItemTimeItemCheck();
                    break;
                case "volg":
                    follow(user);
                    break;
                case "blijf":
                    doStay();
                    break;
                case "mand":
                    walkToNest();
                    break;
                case "slaap":
                    goToSleep();
                    break;
            }
        }

        /// <summary>
        /// Lets the pet stay in his current place, while sitting
        /// </summary>
        private void doStay()
        {
            this.actionID = 7;
            this.goSit(0);
        }

        /// <summary>
        /// Follows a user
        /// </summary>
        /// <param name="user">the user which wants to be followed</param>
        private void follow(virtualRoomUser user)
        {
            if (following)
            {
                if (user == followUser)
                {
                    followUser = null;
                    actionID = -1;
                }
                else
                    followUser = user;
            }
            else
            {
                followUser = user;
                actionID = 6;
            }
        }

        /// <summary>
        /// Follows the user
        /// </summary>
        private void doFollow()
        {
            lastAiAction = DateTime.Now;
            actionID = -1;
            if(following)
            {
                if (room.containsUser(followUser.roomUID))
                {
                    if (room.getRoomUser(followUser.roomUID) == followUser)
                    {
                        this.goalX = followUser.lastX;
                        this.goalY = followUser.lastY;
                        actionID = 6;
                    }
                }
            }
        }
        /// <summary>
        /// Does a % calculation if this pet wants to obey his master
        /// based on the friendship
        /// </summary>
        /// <returns></returns>
        private bool wantsToListen()
        {
            if (RND.Next(0, 11) <= Information.Friendship)
                return true;
            room.sendSaying(roomUID, "grmpf");
            walk();
            return false;
                
        }
        #endregion
        
    }
}
