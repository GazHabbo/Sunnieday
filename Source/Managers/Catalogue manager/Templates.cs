using System.Collections;
using System.Collections.Generic;
namespace Holo.Managers.catalogue
{
    public struct itemTemplate
    {
        /// <summary>
        /// The type ID of the item, eg, 0 = walliten, 1 = flooritem, 2 = seat etc.
        /// </summary>
        internal byte typeID;
        /// <summary>
        /// The sprite of the item.
        /// </summary>
        internal string Sprite;
        /// <summary>
        /// The colour of the item.
        /// </summary>
        internal string Colour;
        /// <summary>
        /// The length of the item.
        /// </summary>
        internal int Length;
        /// <summary>
        /// The width of the item.
        /// </summary>
        internal int Width;
        /// <summary>
        /// The topheight of the item, if seat, then this indicates the sitheight. If a solid stackable item, then this is the stackheight. If 0.0, then the item is classified as non-stackable.
        /// </summary>
        internal double topH;
        /// <summary>
        /// Specifies if the item can be used as door.
        /// </summary>
        internal bool isDoor;
        /// <summary>
        /// Specifies if the item can be traded between virtual users.
        /// </summary>
        internal bool isTradeable;
        /// <summary>
        /// Specifies if the item can be recycled in the item recycler. 
        /// </summary>
        internal bool isRecycleable;
        /// <summary>
        /// The TID of this item
        /// </summary>
        internal int templateID;
        /// <summary>
        /// The cost of this item
        /// </summary>
        internal int cost;
        /// <summary>
        /// Indicator if this item costs belcredits or not
        /// </summary>
        internal bool costsBelCredits;
        /// <summary>
        /// Inidicates if it is a pet or not
        /// </summary>
        internal bool isPet;

        internal bool isPetFood;
        internal bool isPetDrink;
        internal bool isPetToy;
        internal bool isMoodLight;
        internal byte numberOfUses;
        internal bool isMusicDisk;
        internal int musicDiskNumber;
        internal bool isSoundMachine;
        /// <summary>
        /// 0 = all pets
        /// 1 = dog
        /// 2 = cat
        /// 3 = croko
        /// </summary>
        internal byte petFoodID;
        /// <summary>
        /// Initializes the item template.
        /// </summary>
        /// <param name="Sprite">The sprite of the item.</param>
        /// <param name="typeID">The type ID of the item, eg, 0 = walliten, 1 = flooritem, 2 = seat etc.</param>
        /// <param name="Colour">The colour of the item.</param>
        /// <param name="Length">The length of the item.</param>
        /// <param name="Width">The width of the item.</param>
        /// <param name="topH">The topheight of the item, if seat, then this indicates the sitheight. If a solid stackable item, then this is the stackheight. If 0.0, then the item is classified as non-stackable.</param>
        /// <param name="isDoor">Specifies if the item can be used as door.</param>
        /// <param name="isTradeable">Specifies if the item can be traded between virtual users.</param>
        /// <param name="isRecycleable">Specifies if the item can be recycled in the item recycler.</param>
        public itemTemplate(string Sprite, byte typeID, string Colour, int Length, int Width, double topH, bool isDoor, bool isTradeable, bool isRecycleable, int templateID, int cost, bool pet, bool costsBelCredits)
        {
            if (Sprite.Contains(" "))
            {
                this.Sprite = Sprite.Split(' ')[0];
                this.Colour = Sprite.Split(' ')[1];
            }
            else
            {
                this.Sprite = Sprite;
                this.Colour = Colour;
            }
            this.typeID = typeID;
            this.Length = Length;
            this.Width = Width;
            this.topH = topH;
            this.isDoor = isDoor;
            this.isTradeable = isTradeable;
            this.isRecycleable = isRecycleable;
            this.templateID = templateID;
            this.cost = cost;
            this.isPet = pet;
            this.numberOfUses = 0;
            this.petFoodID = 255;
            this.costsBelCredits = costsBelCredits;
            #region petFlags checking
            if (this.Sprite.Contains("goodie") || this.Sprite.Contains("petfood"))
            {
                if (this.Sprite.Contains("goodie"))
                {
                    this.numberOfUses = 1;
                    this.petFoodID = 0;
                }
                else
                {
                    this.numberOfUses = 5;
                    this.petFoodID = byte.Parse(this.Sprite.Substring(7));
                    if (petFoodID == 2 || petFoodID == 3)
                        this.petFoodID = 2;
                    else if (petFoodID == 4)
                        this.petFoodID = 3;
                }
                isPetFood = true;
                isPetDrink = false;
                isPetToy = false;

            }
            else if (this.Sprite.Contains("waterbowl"))
            {
                isPetDrink = true;
                isPetToy = false;
                isPetFood = false;
            }
            else if (this.Sprite.Contains("toy"))
            {
                isPetToy = true;
                isPetDrink = false;
                isPetFood = false;
            }
            else
            {
                isPetToy = false;
                isPetFood = false;
                isPetDrink = false;
            }
            #endregion

            this.isMoodLight = this.Sprite == "roomdimmer";
            this.isSoundMachine = (stringManager.getStringPart(Sprite, 0, 13) == "sound_machine") || (stringManager.getStringPart(Sprite, 0, 13) == "ads_idol_trax") || (stringManager.getStringPart(Sprite, 0, 13) == "nouvelle_trax");
            this.isMusicDisk = stringManager.getStringPart(Sprite, 0, 10) == "sound_set_";
            if (isMusicDisk)
            {
                this.musicDiskNumber = int.Parse(Sprite.Substring(10));
            }
            else
                this.musicDiskNumber = 0;
            
        }
    }
    public class cataloguePage
    {
        /// <summary>
        /// The display name of the page in the client.
        /// </summary>
        internal string displayName;
        /// <summary>
        /// The page string of the page, containing the layout, items etc.
        /// </summary>
        internal string pageData;
        /// <summary>
        /// The minimum rank that a virtual user requires to access this rank.
        /// </summary>
        internal byte minRank;
        internal List<int> items;


        public cataloguePage()
        {
            this.items = new List<int>();
        }


        internal void addItem(itemTemplate temp)
        {
            this.items.Add(temp.templateID);
        }

        internal bool hasItem(itemTemplate id)
        {
            return items.Contains(id.templateID);
        }
    }

    public struct deals
    {
        /// <summary>
        /// Information about the deal
        /// </summary>
        public itemTemplate[] dealItemTemplates;
        public int[] dealItemAmounts;
                    
        /// <summary>
        /// Cost of this item
        /// </summary>
        public int cost;
    }
}