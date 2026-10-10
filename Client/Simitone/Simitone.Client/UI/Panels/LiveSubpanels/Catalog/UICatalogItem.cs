using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using FSO.SimAntics;
using FSO.SimAntics.Entities;
using FSO.LotView.Model;
using FSO.UI.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simitone.Client.UI.Panels.LiveSubpanels.Catalog
{
    public class UICatalogItem : UITSContainer
    {
        public static Dictionary<uint, Texture2D> IconCache = new Dictionary<uint, Texture2D>();
        public Texture2D BG;
        public Texture2D Icon;
        private uint ObjGUID;
        public Texture2D Outline;
        public bool Outlined;

        public UILabel PriceLabel;
        private UIBuyBrowsePanel BudgetProvider;

        public override void Draw(UISpriteBatch SBatch)
        {
            DrawLocalTexture(SBatch, BG, null, new Vector2(BG.Width-90, BG.Height-105) / -2, Vector2.One, new Color(104, 164, 184, 255));
            var iconSize = 55f;
            //a picture drawn from the object's own sprites arrives later (GenerateMissing).
            if (Icon == null && ObjGUID != 0 && IconCache.TryGetValue(ObjGUID, out var drawn)) Icon = drawn;
            if (Icon != null)
            {
                
                if (Icon.Width / (float)Icon.Height < 1.1f || Icon.Width == 127 || Icon.Width == 128)
                {
                    iconSize = 77.7f;
                    var scale = iconSize/(float)Math.Sqrt(Icon.Width * Icon.Width + Icon.Height * Icon.Height);
                    DrawLocalTexture(SBatch, Icon, new Rectangle(0, 0, Icon.Width, Icon.Height), new Vector2((Icon.Width*scale-90) / -2, (Icon.Height*scale-105) / -2), new Vector2(scale));
                }
                else DrawLocalTexture(SBatch, Icon, new Rectangle(0, 0, Icon.Width / 2, Icon.Height), new Vector2((iconSize-90) / -2, (iconSize- 105) / -2), new Vector2(iconSize / Icon.Height, iconSize / Icon.Height));
            }

            if (Outlined) DrawLocalTexture(SBatch, Outline, null, new Vector2(Outline.Width - 90, Outline.Height - 105) / -2, Vector2.One, UIStyle.Current.ActiveSelection);
            base.Draw(SBatch);
        }

        public UICatalogItem(UICatalogElement elem, UIBuyBrowsePanel budgetProvider)
        {
            BG = Content.Get().CustomUI.Get("pswitch_icon_bg.png").Get(GameFacade.GraphicsDevice);
            Icon = (elem.Special?.Res != null) ? elem.Special.Res.GetIcon(elem.Special.ResID) : GetObjIcon(elem.Item.GUID);
            if (elem.Special == null) ObjGUID = elem.Item.GUID;
            Outline = Content.Get().CustomUI.Get("pswitch_icon_sel.png").Get(GameFacade.GraphicsDevice);

            PriceLabel = new UILabel();
            PriceLabel.Alignment = TextAlignment.Center | TextAlignment.Middle;
            PriceLabel.Position = new Vector2(0, 110);
            PriceLabel.Size = new Vector2(90, 1);
            PriceLabel.CaptionStyle = PriceLabel.CaptionStyle.Clone();
            PriceLabel.CaptionStyle.Color = UIStyle.Current.Text;
            PriceLabel.CaptionStyle.Size = 14;
            PriceLabel.Caption = "§" + elem.Item.Price.ToString();
            Add(PriceLabel);

            BudgetProvider = budgetProvider;
        }

        public override void Selected()
        {
            Outlined = true;
            BudgetProvider.Selected(ItemID);
        }

        public override void Deselected()
        {
            Outlined = false;
        }

        public Texture2D GetObjIcon(uint GUID)
        {
            if (!IconCache.ContainsKey(GUID))
            {
                var obj = Content.Get().WorldObjects.Get(GUID);
                if (obj == null)
                {
                    IconCache[GUID] = null;
                    return null;
                }
                var bmp = obj.GetCatalogBmp();
                if (bmp != null) IconCache[GUID] = bmp.GetTexture(GameFacade.GraphicsDevice);
                else
                {
                    //No catalog picture in the object's files (EP6/EP7 sculptures, EP7 cookbook, ...): draw one from its sprites.
                    IconCache[GUID] = null;
                    if (Requested.Add(GUID)) Pending.Enqueue(GUID);
                }
            }
            return IconCache[GUID];
        }

        private static readonly HashSet<uint> Requested = new HashSet<uint>();
        private static readonly Queue<uint> Pending = new Queue<uint>();

        /// <summary>
        /// Draws one missing catalog picture per call, from a hidden ghost copy of the object (the same renderer as the
        /// object info panel, laid out like a catalog picture). Called from the catalog's Update, never from Draw.
        /// </summary>
        public static void GenerateMissing(VM vm)
        {
            if (Pending.Count == 0 || vm?.Context?.World == null || FSO.Common.FSOEnvironment.Enable3D) return;
            var guid = Pending.Dequeue();
            VMMultitileGroup group = null;
            try
            {
                group = vm.Context.CreateObjectInstance(guid, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                if (group != null) IconCache[guid] = CatThumbGenerator.GenerateThumb(group, vm);
            }
            catch (Exception) { /* leaves the item without a picture, as before */ }
            finally
            {
                try { group?.Delete(vm.Context); } catch (Exception) { }
            }
        }

        public static void ClearIconCache()
        {
            Requested.Clear();
            Pending.Clear();
            foreach (var item in IconCache)
            {
                item.Value?.Dispose();
            }
            IconCache.Clear();
        }
    }
}
