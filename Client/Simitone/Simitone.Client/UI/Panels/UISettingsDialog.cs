using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Client.Utils;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using FSO.Client.UI.Model;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// Options > Settings. Lists every SimitoneSettingsRegistry entry by section, with paging when a section has more
    /// rows than fit. Changes apply and save immediately; settings that need a restart say so.
    /// </summary>
    public class UISettingsDialog : UIMobileDialog
    {
        private const int ROW_HEIGHT = 58;
        private const int TOP = 100;

        private List<SimitoneSettingDef> Settings;
        private string Section = SimitoneSettingsRegistry.SIMULATION;
        private int Page;

        private UIContainer TabBar;
        private UIContainer Rows;
        private UIFlatButton PrevPage;
        private UIFlatButton NextPage;
        private UILabel PageLabel;
        private UILabel Notice;
        private UIBigButton CloseButton;
        private bool RestartPending;

        //descriptions are shown in a popup while hovering a row, so they are not cut off to fit beside the controls.
        private readonly List<KeyValuePair<Rectangle, Func<string>>> HelpAreas = new List<KeyValuePair<Rectangle, Func<string>>>();
        private readonly HelpPopup Popup = new HelpPopup();

        private int ContentWidth => Math.Min(Width - 80, 960);
        private int ContentX => (Width - ContentWidth) / 2;
        private int RowsPerPage => Math.Max(3, (Height - TOP - 190) / ROW_HEIGHT);

        public UISettingsDialog()
        {
            Caption = "Settings";
            Settings = SimitoneSettingsRegistry.All();

            TabBar = new UIContainer();
            Add(TabBar);
            Rows = new UIContainer();
            Add(Rows);

            PrevPage = new UIFlatButton("Previous", new Vector2(140, 36));
            PrevPage.OnClick += (b) => { Page--; Refresh(); };
            Add(PrevPage);
            NextPage = new UIFlatButton("Next", new Vector2(140, 36));
            NextPage.OnClick += (b) => { Page++; Refresh(); };
            Add(NextPage);
            PageLabel = MakeLabel(15, UIStyle.Current.Text);
            PageLabel.Alignment = TextAlignment.Center | TextAlignment.Middle;
            Add(PageLabel);

            Notice = MakeLabel(15, UIStyle.Current.SecondaryText);
            Notice.Alignment = TextAlignment.Left | TextAlignment.Middle;
            Add(Notice);

            CloseButton = new UIBigButton(true);
            CloseButton.Caption = "Close";
            CloseButton.Width = 275;
            CloseButton.OnButtonClick += (b) => Close();
            Add(CloseButton);
            Add(Popup); //last: drawn over everything else

            SetHeight(Math.Min(ScrHeight - 20, 720));
            Layout();
        }

        private static UILabel MakeLabel(int size, Color color)
        {
            var label = new UILabel();
            label.CaptionStyle = label.CaptionStyle.Clone();
            label.CaptionStyle.Size = size;
            label.CaptionStyle.Color = color;
            label.Alignment = TextAlignment.Left | TextAlignment.Top;
            return label;
        }

        public override void GameResized()
        {
            base.GameResized();
            SetHeight(Math.Min(ScrHeight - 20, 720));
            Layout();
        }

        private void Layout()
        {
            //section tabs
            foreach (var child in TabBar.GetChildren().ToList()) TabBar.Remove(child);
            var sections = SimitoneSettingsRegistry.Sections;
            var tabWidth = (ContentWidth - (sections.Length - 1) * 8) / sections.Length;
            for (int i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                var tab = new UIFlatButton(section, new Vector2(tabWidth, 40), 17);
                tab.Position = new Vector2(ContentX + i * (tabWidth + 8), TOP - 20);
                tab.Selected = section == Section;
                tab.OnClick += (b) => { Section = section; Page = 0; Layout(); };
                TabBar.Add(tab);
            }

            var footerY = Height - 120;
            PrevPage.Position = new Vector2(ContentX, footerY - 50);
            NextPage.Position = new Vector2(ContentX + ContentWidth - 140, footerY - 50);
            PageLabel.Position = new Vector2(ContentX + 150, footerY - 50);
            PageLabel.Size = new Vector2(ContentWidth - 300, 36);
            Notice.Position = new Vector2(ContentX, footerY + 10);
            Notice.Size = new Vector2(ContentWidth - 300, 80);
            CloseButton.Position = new Vector2(ContentX + ContentWidth - 275, footerY);
            Refresh();
        }

        private void Refresh()
        {
            foreach (var child in Rows.GetChildren().ToList()) Rows.Remove(child);
            HelpAreas.Clear();
            var items = Settings.Where(x => x.Section == Section).ToList();
            var pages = Math.Max(1, (items.Count + RowsPerPage - 1) / RowsPerPage);
            Page = Math.Max(0, Math.Min(pages - 1, Page));

            var y = TOP + 36;
            foreach (var item in items.Skip(Page * RowsPerPage).Take(RowsPerPage))
            {
                AddRow(item, y);
                y += ROW_HEIGHT;
            }

            PrevPage.Visible = pages > 1;
            NextPage.Visible = pages > 1;
            PrevPage.Disabled = Page == 0;
            NextPage.Disabled = Page == pages - 1;
            PageLabel.Caption = (pages > 1) ? $"Page {Page + 1} of {pages}" : "";
            Notice.Caption = RestartPending
                ? "Restart Simitone for the changes marked (restart) to take effect."
                : "Changes are saved immediately. Hover a setting to read what it does.";
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            var mouse = GetMousePosition(state.MouseState);
            var point = new Point((int)mouse.X, (int)mouse.Y);
            string text = null;
            foreach (var area in HelpAreas)
            {
                if (area.Key.Contains(point)) { text = area.Value(); break; }
            }
            Popup.Show(text, mouse, Width, Height);
        }

        /// <summary>A word-wrapped box that follows the mouse, shown while a setting row is hovered.</summary>
        private class HelpPopup : UIContainer
        {
            private const int WIDTH = 440;
            private const int PAD = 10;
            private const int LINE = 20;
            private readonly UILabel Text;
            private readonly Texture2D Px;
            private Vector2 BoxSize;

            public HelpPopup()
            {
                Px = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
                Text = MakeLabel(15, UIStyle.Current.Text);
                Text.Wrapped = true;
                Text.Position = new Vector2(PAD, PAD);
                Add(Text);
                Visible = false;
            }

            public void Show(string text, Vector2 mouse, int maxX, int maxY)
            {
                if (string.IsNullOrEmpty(text)) { Visible = false; return; }
                if (Text.Caption != text)
                {
                    Text.Caption = text;
                    var lines = UIUtils.WordWrap(text, WIDTH - PAD * 2, Text.CaptionStyle, int.MaxValue).Lines.Count;
                    BoxSize = new Vector2(WIDTH, lines * LINE + PAD * 2);
                    Text.Size = new Vector2(WIDTH - PAD * 2, lines * LINE);
                }
                var x = Math.Max(4, Math.Min(maxX - BoxSize.X - 4, mouse.X + 16));
                var y = mouse.Y + 22;
                if (y + BoxSize.Y > maxY - 4) y = mouse.Y - BoxSize.Y - 8;
                Position = new Vector2(x, Math.Max(4, y));
                Visible = true;
            }

            public override void Draw(UISpriteBatch batch)
            {
                if (!Visible) return;
                DrawLocalTexture(batch, Px, null, Vector2.Zero, BoxSize, Color.White * 0.5f);
                DrawLocalTexture(batch, Px, null, new Vector2(1, 1), BoxSize - new Vector2(2, 2), Color.Black * 0.95f);
                base.Draw(batch);
            }
        }

        private void AddRow(SimitoneSettingDef item, int y)
        {
            var name = MakeLabel(19, UIStyle.Current.Text);
            name.Caption = item.Label + (item.RestartRequired ? "  (restart)" : "");
            name.Position = new Vector2(ContentX, y);
            Rows.Add(name);

            name.Position = new Vector2(ContentX, y + 8);
            Func<string> helpText = () => (item.Departure ? "Simitone change: differs from the original game.\n" : "")
                + ((item.HelpState != null ? item.HelpState() : item.Help) ?? "");
            HelpAreas.Add(new KeyValuePair<Rectangle, Func<string>>(new Rectangle(ContentX, y - 4, ContentWidth, ROW_HEIGHT - 6), helpText));

            var right = ContentX + ContentWidth;
            if (item.Action != null)
            {
                var action = new UIFlatButton(item.ActionState?.Invoke() ?? item.ActionLabel ?? "Run", new Vector2(300, 40), 17);
                action.Position = new Vector2(right - 300, y + 4);
                action.OnClick += (b) =>
                {
                    item.Action();
                    if (item.ActionState != null) action.Caption = item.ActionState();
                    if (item.RestartRequired && !RestartPending)
                    {
                        RestartPending = true;
                        Refresh();
                    }
                };
                Rows.Add(action);
                return;
            }

            var value = new UIFlatButton(item.Choices[item.Get()], new Vector2(200, 40), 17);
            value.Position = new Vector2(right - 250, y + 4);
            var prev = new UIFlatButton("<", new Vector2(46, 40), 19);
            prev.Position = new Vector2(right - 300, y + 4);
            var next = new UIFlatButton(">", new Vector2(46, 40), 19);
            next.Position = new Vector2(right - 46, y + 4);

            Action<int> step = (dir) =>
            {
                var count = item.Choices.Length;
                var index = (item.Get() + dir + count) % count;
                item.Set(index);
                value.Caption = item.Choices[item.Get()];
                value.Selected = item.Choices == SimitoneSettingDef.OffOn && item.Get() == 1;
                if (item.RestartRequired && !RestartPending)
                {
                    RestartPending = true;
                    Refresh();
                }
            };
            value.Selected = item.Choices == SimitoneSettingDef.OffOn && item.Get() == 1;
            value.OnClick += (b) => step(1);
            prev.OnClick += (b) => step(-1);
            next.OnClick += (b) => step(1);
            Rows.Add(value);
            Rows.Add(prev);
            Rows.Add(next);
        }
    }
}
