﻿using Terraria.UI;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;
using ReLogic.Graphics;

namespace CustomNPCNames.UI
{
    /// <summary>
    /// This class behaves like a UITextPanel, but you can also edit its caption by typing.
    /// </summary>
    public abstract class UIEntryPanel : UIElement
    {
        protected readonly UITextPanel focusVariant;    // this panel will have additional text input functionality
        protected readonly UITextPanel idleVariant;     // this panel will be used when EntryBox goes out of text focus
        public string Text
        {
            get { return HasFocus ? focusVariant.Text : idleVariant.Text; }
        }
        public bool HasFocus { get; set; }
        public int CaptionMaxLength { get; set; }  // trims exceeding characters, default -1 for infinity
        protected MouseState curMouse;
        protected MouseState oldMouse;
        protected int cursorPosition;
        protected int cursorClock;
        private static char lastKey;     //
        private static int  lastKeyTime; // these three variables are used for printing a character multiple times if its key is being held down long enough
        private static bool lastShift;   //
        protected static string clipboard = "";
        public float Scale { get; protected set; }
        protected bool containCaption;
        public bool ContainCaption
        {
            get { return containCaption; }
            set
            {
                containCaption = value;
                idleVariant.ContainCaption = value;
                focusVariant.ContainCaption = value;
            }
        }
        public int busyFlicker;
        protected string busyPrevText;
        protected byte _busy = 255;
        public byte Busy
        {
            get { return _busy; }
            set
            {
                if (_busy == 255 && value != 255) {
                    busyPrevText = idleVariant.Text;
                    CaptionMaxLength = -1;
                    idleVariant.Caption.TextColor = new Color(255, 200, 150);
                } else if (_busy != 255 && value == 255) {
                    if (busyPrevText != null) { SetText(busyPrevText); }
                    busyPrevText = null;
                    CaptionMaxLength = 25;
                    idleVariant.Caption.TextColor = Color.White;
                }
                _busy = value;
            }
        }

        public UIEntryPanel(string caption = "")
        {
            Scale = 1f;
            Height.Set(40, 0);
            CaptionMaxLength = -1;
            
            focusVariant = new UITextPanel();
            focusVariant.HAlign = 0.5f;
            focusVariant.Top.Set(0, 0);
            focusVariant.Left.Set(0, 0);
            focusVariant.SetColor(new Color(170, 90, 80), new Color(30, 15, 10));

            idleVariant = new UITextPanel();
            idleVariant.HAlign = 0.5f;
            idleVariant.Top.Set(0, 0);
            idleVariant.Left.Set(0, 0);

            ContainCaption = true;
            SetText(caption);
            Append(idleVariant);

            busyFlicker = 45;
        }

        public virtual void SetText(string text)
        {
            text = (CaptionMaxLength == -1) ? text : text.Substring(0, System.Math.Min(text.Length, CaptionMaxLength));
            if (!HasFocus) { idleVariant.SetText(text); }
            focusVariant.SetText(text);
            cursorPosition = text.Length;
            AdjustWidth();
        }

        public virtual void SetTextColor(Color focus, Color idle)
        {
            focusVariant.Caption.TextColor = focus;
            idleVariant.Caption.TextColor = idle;
        }

        public virtual void SetScale(float scale)
        {
            Width.Set(Width.Pixels / Scale * scale, 0);
            Height.Set(Height.Pixels / Scale * scale, 0);
            Scale = scale;
            focusVariant.SetScale(scale);
            idleVariant.SetScale(scale);
        }

        /// <summary>
        /// Use this method instead of Width.Set()
        /// </summary>
        public virtual void SetWidth(float pixels, float percent)
        {
            Width.Set(pixels, percent);
            focusVariant.Width.Set(pixels, percent);
            idleVariant.Width.Set(pixels, percent);
        }

        protected virtual void AdjustWidth()
        {
            SetWidth((HasFocus ? focusVariant.Width.Pixels : idleVariant.Width.Pixels), 0);
        }

        public virtual void SetFocusHoverText(string hoverText)
        {
            focusVariant.HoverText = hoverText;
        }

        public virtual void SetIdleHoverText(string hoverText)
        {
            idleVariant.HoverText = hoverText;
        }

        public virtual void SetFocusColor(Color background, Color border)
        {
            focusVariant.SetColor(background, border);
        }

        public virtual void SetIdleColor(Color background, Color border)
        {
            idleVariant.SetColor(background, border);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (RenameUI.IsNPCSelected) {
                byte getBusy = GetBusy();
                if (getBusy != 255 && Busy == 255) {
                    Busy = getBusy;
                    string typingMsg = "                             [c/FFFF00:" + Main.player[Busy].name + "] [c/FFFFAA:is typing...]";
                    SetText(typingMsg);
                    idleVariant.SetText(typingMsg);
                } else if (getBusy == 255 && Busy != 255) {
                    Busy = 255;
                    busyFlicker = 45;
                }
            }
            
            if (CustomNPCNames.WaitForServerResponse || Busy != 255) {
                if (HasFocus) { Deselect(false); }
                if (Busy != 255) {
                    if (--busyFlicker == 0) {
                        string typingMsg = "                             [c/FFFF00:" + Main.player[Busy].name + "] [c/FFFFAA:is typing...]";
                        SetText(idleVariant.Text == typingMsg ? busyPrevText : typingMsg);
                        busyFlicker = 45;
                    }
                }
                return;
            }

            oldMouse = curMouse;
            curMouse = Mouse.GetState();
            
            Rectangle dim = InterfaceHelper.GetFullRectangle(idleVariant);
            bool hover = curMouse.X > dim.X && curMouse.X < dim.X + dim.Width && curMouse.Y > dim.Y && curMouse.Y < dim.Y + dim.Height;

            if (MouseButtonPressed(this) && hover && !HasFocus)
            {
                Select();
            } else if (MouseButtonPressed(this) && !hover && HasFocus)
            {
                Deselect();
            }

            Main.blockInput = false;
            if (HasFocus)
            {
                PlayerInput.WritingText = true;
                Main.chatRelease = false;
                string str = focusVariant.Text;
                ProcessTypedKey(this, ref str, ref cursorPosition, CaptionMaxLength);

                if (CaptionMaxLength != -1)
                {
                    str = str.Substring(0, System.Math.Min(str.Length, CaptionMaxLength));
                }

                if (!str.Equals(focusVariant.Text))
                {
                    SetText(str);
                    cursorPosition = str.Length;
                }

                if (KeyPressed(Keys.Enter) || KeyPressed(Keys.Escape))
                {
                    SetText(KeyPressed(Keys.Escape) ? idleVariant.Text : focusVariant.Text);
                    Deselect();
                }
                AdjustWidth();
            }
        }

        public virtual void Select()
        {
            HasFocus = true;
            RemoveChild(idleVariant);
            Append(focusVariant);
            cursorClock = 0;
        }

        public virtual void Deselect(bool save = true)
        {
            HasFocus = false;
            if (save) { idleVariant.SetText(focusVariant.Text); }
            RemoveChild(focusVariant);
            Append(idleVariant);
        }

        protected abstract byte GetBusy();

        protected override void DrawChildren(SpriteBatch spriteBatch)
        {
            base.DrawChildren(spriteBatch);

            CalculatedStyle dim = focusVariant.GetDimensions();
            cursorClock = (++cursorClock) % 60;
            if (HasFocus && cursorClock < 30) {
                DynamicSpriteFont font = Main.fontMouseText;
                float drawCursor = font.MeasureString(focusVariant.Text.Substring(0, cursorPosition)).X;
                if (idleVariant.HAlign == focusVariant.HAlign) {
                    if (idleVariant.HAlign == 0) {
                        spriteBatch.DrawString(font, "|", new Vector2(dim.X + (Scale * (10 + drawCursor)), dim.Y + (focusVariant.Height.Pixels / 3.7f)), focusVariant.Caption.TextColor, 0f, Vector2.Zero, focusVariant.Scale, SpriteEffects.None, 0f);
                    } else if(idleVariant.HAlign == 0.5) {
                        spriteBatch.DrawString(font, "|", new Vector2(dim.X + (0.5f * dim.Width) + (0.5f * Scale * drawCursor), dim.Y + (focusVariant.Height.Pixels / 3.7f)), focusVariant.Caption.TextColor, 0f, Vector2.Zero, focusVariant.Scale, SpriteEffects.None, 0f);
                    } else if (idleVariant.HAlign == 1) {
                        spriteBatch.DrawString(font, "|", new Vector2(dim.X + dim.Width - (Scale * 10), dim.Y + (focusVariant.Height.Pixels / 3.7f)), focusVariant.Caption.TextColor, 0f, Vector2.Zero, focusVariant.Scale, SpriteEffects.None, 0f);
                    }
                }
            }
        }

        protected static void ProcessTypedKey(UIEntryPanel self, ref string str, ref int cursorPos, int limit)
        {
            char key = GetTypedKey(self, ref str);
            if (key != '\0' && key != '\t')
            {
                if (key == '\b')
                {
                    if (str.Length > 0) {
                        str = str.Remove(str.Length - 1);
                        cursorPos--;
                    }
                    lastKey = '\b';
                    lastKeyTime = 0;
                    lastShift = (KeyHeld(Keys.LeftShift) || KeyHeld(Keys.RightShift));
                    return;
                } else if (limit == -1 || str.Length < limit)
                {
                    str += key;
                    cursorPos++;
                }

                lastKey = key;
                lastKeyTime = 0;
                lastShift = (KeyHeld(Keys.LeftShift) || KeyHeld(Keys.RightShift));
            } else if (key == '\t')
            {
                lastKey = '\0';
                lastKeyTime = 0;
            } else if (lastKey != '\0' && KeyHeld(CharToKeys(lastKey)))
            {
                if (lastShift && !KeyHeld(Keys.LeftShift) && !KeyHeld(Keys.RightShift))
                {
                    lastKeyTime = 0;
                    lastShift = false;
                    if (lastKey >= 65 && lastKey <= 90) { lastKey = (char)(lastKey - 'A' + 'a'); } 
                    else {
                        switch (lastKey)
                        {
                            case '!': lastKey = '1'; break;
                            case '@': lastKey = '2'; break;
                            case '#': lastKey = '3'; break;
                            case '$': lastKey = '4'; break;
                            case '%': lastKey = '5'; break;
                            case '^': lastKey = '6'; break;
                            case '&': lastKey = '7'; break;
                            case '*': lastKey = '8'; break;
                            case '(': lastKey = '9'; break;
                            case ')': lastKey = '0'; break;
                            case '~': lastKey = '`'; break;
                            case '_': lastKey = '-'; break;
                            case '+': lastKey = '='; break;
                            case '{': lastKey = '['; break;
                            case '}': lastKey = ']'; break;
                            case ':': lastKey = ';'; break;
                            case '"': lastKey = '\'';break;
                            case '|': lastKey = '\\';break;
                            case '<': lastKey = ','; break;
                            case '>': lastKey = '.'; break;
                            case '?': lastKey = '/'; break;
                        }
                    }
                }
                lastKeyTime = System.Math.Min(lastKeyTime + 1, 30);
                if (lastKeyTime == 30)
                {
                    if (lastKey != '\b')
                    {
                        if (limit == -1 || str.Length < limit)
                        {
                            lastKeyTime = 28;
                            str += lastKey;
                            cursorPos++;
                        }
                    } else
                    {
                        if (str.Length > 0)
                        {
                            lastKeyTime = 28;
                            str  = str.Remove(str.Length - 1);
                            cursorPos--;
                        }
                    }
                }
            }
        }
        
        protected static char GetTypedKey(UIEntryPanel self, ref string str)
        {
            if (KeyHeld(Keys.LeftControl) || KeyHeld(Keys.RightControl))
            {
                if (KeyPressed(Keys.Back)) {
                    str = "";
                    self.cursorPosition = 0;
                    self.cursorClock = 0;
                    return '\0';
                } else if (KeyPressed(Keys.C)) {
                    clipboard = str;
                    return '\0';
                } else if (KeyPressed(Keys.X)) {
                    clipboard = str;
                    str = "";
                    self.cursorPosition = 0;
                    self.cursorClock = 0;
                    return '\0';
                } else if (KeyPressed(Keys.V) && clipboard != "") {
                    str += clipboard;
                    str = str.Substring(0, System.Math.Min(str.Length, 25));
                    self.cursorPosition = str.Length;
                    self.cursorClock = 0;
                    return '\0';
                }
            }
            else
            {
                if (KeyPressed(Keys.Back)) { return '\b'; }
                for (Keys i = Keys.A; i <= Keys.Z; i++)
                {
                    bool capslock = System.Windows.Forms.Control.IsKeyLocked(System.Windows.Forms.Keys.CapsLock);
                    bool uppercase = capslock ^ Main.keyState.PressingShift();
                    if (KeyPressed(i)) { return (char)((int)i - (!uppercase ? ('A' - 'a') : 0)); }
                }

                for (Keys i = Keys.D0; i <= Keys.D9; i++)
                {
                    if (KeyPressed(i))
                    {
                        if (!KeyHeld(Keys.LeftShift) && !KeyHeld(Keys.RightShift)) { return (char)i; } else
                        {
                            switch (i)
                            {
                                case Keys.D1: return '!';
                                case Keys.D2: return '@';
                                case Keys.D3: return '#';
                                case Keys.D4: return '$';
                                case Keys.D5: return '%';
                                case Keys.D6: return '^';
                                case Keys.D7: return '&';
                                case Keys.D8: return '*';
                                case Keys.D9: return '(';
                                case Keys.D0: return ')';
                            }
                        }
                    }
                }

                if (KeyPressed(Keys.Space)) { return ' '; }

                if (!KeyHeld(Keys.LeftShift) && !KeyHeld(Keys.RightShift))
                {
                    if (KeyPressed(Keys.OemTilde)) { return '`'; }
                    if (KeyPressed(Keys.OemMinus)) { return '-'; }
                    if (KeyPressed(Keys.OemPlus)) { return '='; }
                    if (KeyPressed(Keys.Divide)) { return '/'; }
                    if (KeyPressed(Keys.Multiply)) { return '*'; }
                    if (KeyPressed(Keys.Subtract)) { return '-'; }
                    if (KeyPressed(Keys.Add)) { return '+'; }
                    if (KeyPressed(Keys.OemOpenBrackets)) { return '['; }
                    if (KeyPressed(Keys.OemCloseBrackets)) { return ']'; }
                    if (KeyPressed(Keys.OemSemicolon)) { return ';'; }
                    if (KeyPressed(Keys.OemQuotes)) { return '\''; }
                    if (KeyPressed(Keys.OemPipe)) { return '\\'; }
                    if (KeyPressed(Keys.OemComma)) { return ','; }
                    if (KeyPressed(Keys.OemPeriod)) { return '.'; }
                    if (KeyPressed(Keys.OemQuestion)) { return '/'; }
                } else
                {
                    if (KeyPressed(Keys.OemTilde)) { return '~'; }
                    if (KeyPressed(Keys.OemMinus)) { return '_'; }
                    if (KeyPressed(Keys.OemPlus)) { return '+'; }
                    if (KeyPressed(Keys.Divide)) { return '/'; }
                    if (KeyPressed(Keys.Multiply)) { return '*'; }
                    if (KeyPressed(Keys.Subtract)) { return '-'; }
                    if (KeyPressed(Keys.Add)) { return '+'; }
                    if (KeyPressed(Keys.OemOpenBrackets)) { return '{'; }
                    if (KeyPressed(Keys.OemCloseBrackets)) { return '}'; }
                    if (KeyPressed(Keys.OemSemicolon)) { return ':'; }
                    if (KeyPressed(Keys.OemQuotes)) { return '"'; }
                    if (KeyPressed(Keys.OemPipe)) { return '|'; }
                    if (KeyPressed(Keys.OemComma)) { return '<'; }
                    if (KeyPressed(Keys.OemPeriod)) { return '>'; }
                    if (KeyPressed(Keys.OemQuestion)) { return '?'; }
                }
            }

            for (Keys i = (Keys)0; i < (Keys)255; i++)
            {
                if (KeyPressed(i)) { return '\t'; } // tab is a unique character that represents any character being pressed
            }

            return '\0';
        }

        public static bool KeyPressed(Keys key)
        {
            return Main.keyState.IsKeyDown(key) && !Main.oldKeyState.IsKeyDown(key);
        }

        public static bool KeyHeld(Keys key)
        {
            return Main.keyState.IsKeyDown(key);
        }

        protected static bool MouseButtonPressed(UIEntryPanel self)
        {
            return self.curMouse.LeftButton == ButtonState.Pressed && self.oldMouse.LeftButton == ButtonState.Released;
        }

        public static Keys CharToKeys(char chr)
        {
            if (chr >= 'A' && chr <= 'Z') { return (Keys)chr;               }
            if (chr >= '0' && chr <= '9') { return (Keys)chr;               }
            if (chr >= 'a' && chr <= 'z') { return (Keys)chr - ('a' - 'A'); }
            if (chr >= 'a' && chr <= 'z') { return (Keys)chr - ('a' - 'A'); }
            switch (chr)
            {
                case '!' : return Keys.D1;
                case '@' : return Keys.D2;
                case '#' : return Keys.D3;
                case '$' : return Keys.D4;
                case '%' : return Keys.D5;
                case '^' : return Keys.D6;
                case '&' : return Keys.D7;
                case '*' : return Keys.D8;
                case '(' : return Keys.D9;
                case ')' : return Keys.D0;
                case '`' : case '~': return Keys.OemTilde;
                case '-' : case '_': return Keys.OemMinus;
                case '=' : case '+': return Keys.OemPlus;
                case '[' : case '{': return Keys.OemOpenBrackets;
                case ']' : case '}': return Keys.OemCloseBrackets;
                case ';' : case ':': return Keys.OemSemicolon;
                case '\'': case '"': return Keys.OemQuotes;
                case '\\': case '|': return Keys.OemPipe;
                case ',' : case '<': return Keys.OemComma;
                case '.' : case '>': return Keys.OemPeriod;
                case '/' : case '?': return Keys.OemQuestion;
                case ' ' : return Keys.Space;
                case '\b': return Keys.Back;
            }
            return Keys.None;
        }
    }
}