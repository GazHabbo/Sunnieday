
using Holo.Managers;
using Holo.Managers.catalogue;
namespace Holo.Source.Managers.Catalogue_manager
{
    public class purchaseItem
    {
        internal int templateID;
        internal string var;
        internal string Page;
        internal string Item;
        internal int Cost;
        internal bool present;
        internal int receiver;
        internal string boxMessage;
        internal bool freeItem;
        internal string sprite
        {
            get
            {
                return catalogueManager.getTemplate(templateID).Sprite;
            }
        }
        internal itemTemplate template
        {
            get
            {
                return catalogueManager.getTemplate(templateID);
            }
        }

        public purchaseItem(int TID, string var, string page, string item, int cost, bool present, int receiver, string boxMessage, bool freeItem)
        {
            this.templateID = TID;
            this.var = var;
            this.Page = page;
            this.Cost = cost;
            this.Item = item;
            this.present = present;
            this.receiver = receiver;
            this.boxMessage = boxMessage;
            this.freeItem = freeItem;
        }
    }
}
