using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Model;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;
using System;

namespace Simitone.Client.UI.Controls
{
    /// <summary>
    /// A plain rectangular text button, drawn without textures. Used where Simitone's large image buttons don't fit,
    /// such as rows of options in the settings dialog.
    /// </summary>
    public class UIFlatButton : UIContainer
    {
        public event Action<UIFlatButton> OnClick;
        public bool Selected;
        public bool Disabled;

        private Vector2 BSize;
        private UILabel Label;
        private Texture2D Px;
        private UIMouseEventRef MouseRef;
        private bool Hover;
        private bool Down;

        public string Caption
        {
            get { return Label.Caption; }
            set { Label.Caption = value; }
        }

        public Vector2 ButtonSize
        {
            get { return BSize; }
            set
            {
                BSize = value;
                Label.Size = value;
                if (MouseRef != null) MouseRef.Region = new Rectangle(0, 0, (int)value.X, (int)value.Y);
            }
        }

        public UIFlatButton(string caption, Vector2 size, int fontSize = 15)
        {
            Px = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            Label = new UILabel();
            Label.CaptionStyle = Label.CaptionStyle.Clone();
            Label.CaptionStyle.Size = fontSize;
            Label.CaptionStyle.Color = UIStyle.Current.Text;
            Label.Alignment = TextAlignment.Center | TextAlignment.Middle;
            Label.Caption = caption;
            Add(Label);
            MouseRef = ListenForMouse(new Rectangle(0, 0, (int)size.X, (int)size.Y), OnMouse);
            ButtonSize = size;
        }

        private void OnMouse(UIMouseEventType type, UpdateState state)
        {
            switch (type)
            {
                case UIMouseEventType.MouseOver:
                    Hover = true;
                    break;
                case UIMouseEventType.MouseOut:
                    Hover = false;
                    Down = false;
                    break;
                case UIMouseEventType.MouseDown:
                    Down = true;
                    break;
                case UIMouseEventType.MouseUp:
                    if (Down && Hover && !Disabled)
                    {
                        FSO.HIT.HITVM.Get()?.PlaySoundEvent(UISounds.Click);
                        OnClick?.Invoke(this);
                    }
                    Down = false;
                    break;
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            Color back;
            if (Selected) back = UIStyle.Current.SecondaryText * (Hover ? 0.8f : 0.6f);
            else if (Disabled) back = Color.White * 0.05f;
            else back = Color.White * (Down ? 0.35f : (Hover ? 0.25f : 0.12f));
            DrawLocalTexture(batch, Px, null, Vector2.Zero, BSize, back);
            Label.CaptionStyle.Color = Disabled ? UIStyle.Current.BtnDisable : UIStyle.Current.Text;
            base.Draw(batch);
        }
    }
}
