using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CzechKeyboard
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new KeyboardForm());
        }
    }

    internal sealed class KeySpec
    {
        public string Normal, Shifted, AltGr, Action;
        public ushort VirtualKey;
        public float Width;
        public KeyControl Control;
        public KeySpec(string normal, string shifted, string altGr, ushort vk, string action, float width)
        {
            Normal = normal; Shifted = shifted; AltGr = altGr;
            VirtualKey = vk; Action = action; Width = width;
        }
    }

    internal sealed class KeyboardColors
    {
        public Color Background, Text, Muted, Key, Number, Hover, Pressed, Border, Selected, SelectedBorder, SelectedText;
        public static readonly KeyboardColors Dark = new KeyboardColors {
            Background = Color.FromArgb(22, 28, 38), Text = Color.FromArgb(239, 245, 249),
            Muted = Color.FromArgb(162, 178, 196), Key = Color.FromArgb(38, 46, 59),
            Number = Color.FromArgb(38, 64, 74), Hover = Color.FromArgb(58, 70, 86),
            Pressed = Color.FromArgb(65, 86, 102), Border = Color.FromArgb(70, 83, 100),
            Selected = Color.FromArgb(29, 126, 117), SelectedBorder = Color.FromArgb(80, 218, 190),
            SelectedText = Color.FromArgb(239, 245, 249)
        };
        public static readonly KeyboardColors Light = new KeyboardColors {
            Background = Color.FromArgb(234, 240, 245), Text = Color.FromArgb(27, 42, 57),
            Muted = Color.FromArgb(65, 83, 101), Key = Color.White,
            Number = Color.FromArgb(217, 240, 237), Hover = Color.FromArgb(224, 234, 244),
            Pressed = Color.FromArgb(197, 217, 232), Border = Color.FromArgb(159, 177, 193),
            Selected = Color.FromArgb(18, 112, 102), SelectedBorder = Color.FromArgb(12, 91, 83),
            SelectedText = Color.White
        };
    }

    internal sealed class KeyControl : Control
    {
        public bool Selected, Accent, ToggleSwitch, Multiline;
        public KeyboardColors Colors = KeyboardColors.Dark;
        private bool hovering, pressed;
        public event EventHandler KeyPressed;
        public event EventHandler PressEnded;
        public KeyControl()
        {
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; }
            base.WndProc(ref m);
        }
        protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            pressed = true; Capture = true; Invalidate();
            if (KeyPressed != null) KeyPressed(this, EventArgs.Empty);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            pressed = false; Capture = false; Invalidate();
            if (PressEnded != null) PressEnded(this, EventArgs.Empty);
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) { pressed = false; Invalidate(); if (PressEnded != null) PressEnded(this, EventArgs.Empty); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Color fill = Selected && !ToggleSwitch ? Colors.Selected :
                pressed ? Colors.Pressed : hovering ? Colors.Hover :
                Accent ? Colors.Number : Colors.Key;
            using (var brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, ClientRectangle);
            using (var pen = new Pen(Selected && !ToggleSwitch ? Colors.SelectedBorder : Colors.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            Rectangle textBounds = ClientRectangle;
            if (ToggleSwitch) {
                int diameter = Math.Max(8, Font.Height - 3), trackWidth = diameter * 2;
                int x = Width - trackWidth - 8, y = (Height - diameter) / 2;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Selected ? Colors.Selected : Colors.Border)) {
                    e.Graphics.FillEllipse(brush, x, y, diameter, diameter);
                    e.Graphics.FillRectangle(brush, x + diameter / 2, y, trackWidth - diameter, diameter);
                    e.Graphics.FillEllipse(brush, x + trackWidth - diameter, y, diameter, diameter);
                }
                using (var brush = new SolidBrush(Color.White))
                    e.Graphics.FillEllipse(brush, x + (Selected ? trackWidth - diameter : 0) + 2, y + 2, diameter - 4, diameter - 4);
                textBounds.Width = x - 2;
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, textBounds,
                Selected && !ToggleSwitch ? Colors.SelectedText : Colors.Text, TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                (Multiline ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine));
        }
    }

    internal static class PresetTexts
    {
        internal static List<string> Read(TextReader reader)
        {
            var texts = new List<string>();
            string line;
            while ((line = reader.ReadLine()) != null)
                if (!String.IsNullOrWhiteSpace(line)) texts.Add(line);
            return texts;
        }
        internal static List<string> Load()
        {
            using (Stream stream = typeof(PresetTexts).Assembly.GetManifestResourceStream("PresetTexts.txt"))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true)) return Read(reader);
        }
    }

    // This window, like the keyboard, never takes focus from the insertion point.
    internal sealed class PresetPopup : Form
    {
        private readonly Timer dismiss = new Timer();
        private IntPtr targetWindow;
        internal Rectangle TriggerBounds;
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

        internal PresetPopup(IList<string> texts, Font font, KeyboardColors colors, Action<string> insert, float scale)
        {
            Text = "Předvyplněné texty";
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false; TopMost = true;
            BackColor = colors.Background;
            int margin = (int)(8 * scale), gap = (int)(5 * scale), header = (int)(36 * scale);
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            int width = Math.Min((int)(420 * scale), area.Width - margin * 2);
            var title = new Label { Text = Text, Font = font, ForeColor = colors.Text,
                TextAlign = ContentAlignment.MiddleLeft };
            title.SetBounds(margin, margin, width - header - margin * 3, header - margin);
            Controls.Add(title);
            var close = new KeyControl { Text = "×", AccessibleName = "Zavřít předvyplněné texty", Font = font, Colors = colors };
            close.SetBounds(width - header, margin, header - margin, header - margin);
            close.KeyPressed += delegate { Hide(); };
            Controls.Add(close);
            var list = new Panel { AutoScroll = true, BackColor = colors.Background };
            Controls.Add(list);
            int y = 0, itemWidth = width - margin * 2 - SystemInformation.VerticalScrollBarWidth;
            foreach (string text in texts) {
                string value = text;
                int height = TextRenderer.MeasureText(text, font, new Size(itemWidth - margin * 2, Int32.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + margin * 2;
                var item = new KeyControl { Text = value, AccessibleName = value, Font = font, Colors = colors, Multiline = true };
                item.SetBounds(0, y, itemWidth, Math.Max(header, height));
                item.KeyPressed += delegate { insert(value); Hide(); };
                list.Controls.Add(item);
                y += item.Height + gap;
            }
            if (texts.Count == 0) {
                var empty = new Label { Text = "Žádné předvyplněné texty.", Font = font, ForeColor = colors.Muted };
                empty.SetBounds(0, 0, itemWidth, header); list.Controls.Add(empty); y = header;
            }
            int listHeight = Math.Min(Math.Max(header, y), Math.Max(header, Math.Min((int)(360 * scale), area.Height - header - margin * 4)));
            list.SetBounds(margin, header + margin, width - margin * 2, listHeight);
            ClientSize = new Size(width, list.Bottom + margin);
            dismiss.Interval = 30;
            dismiss.Tick += delegate {
                bool outsideClick = (GetAsyncKeyState(1) & 0x8000) != 0 &&
                    !Bounds.Contains(Cursor.Position) && !TriggerBounds.Contains(Cursor.Position);
                if (outsideClick || GetForegroundWindow() != targetWindow) Hide();
            };
            VisibleChanged += delegate {
                if (Visible) { targetWindow = GetForegroundWindow(); dismiss.Start(); }
                else dismiss.Stop();
            };
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; p.ExStyle |= 0x08000000; return p; }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; }
            base.WndProc(ref m);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(ForeColor)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) dismiss.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class KeyboardForm : Form
    {
        private readonly List<List<KeySpec>> rows = new List<List<KeySpec>>();
        private readonly Timer repeat = new Timer();
        private readonly Label status = new Label();
        private readonly KeyControl themeToggle = new KeyControl();
        private readonly KeyControl boldToggle = new KeyControl();
        private readonly KeyControl presetsButton = new KeyControl();
        private PresetPopup presetPopup;
        private readonly Font keyFont = new Font("Segoe UI", 11F);
        private readonly Font smallFont = new Font("Segoe UI", 9F);
        private readonly Font boldKeyFont = new Font("Segoe UI", 11F, FontStyle.Bold);
        private readonly Font boldSmallFont = new Font("Segoe UI", 9F, FontStyle.Bold);
        private KeySpec repeating;
        private bool shift, caps, ctrl, alt, altGr, win;
        private string dead = "";
        private string error = "";
        private readonly string settingsPath, themePath, typographyPath;
        private bool lightTheme, boldText;

        public KeyboardForm() : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CeskaKlavesnice")) { }

        internal KeyboardForm(string settingsDirectory)
        {
            settingsPath = Path.Combine(settingsDirectory, "window.txt");
            themePath = Path.Combine(settingsDirectory, "theme.txt");
            typographyPath = Path.Combine(settingsDirectory, "typography.txt");
            Text = "Česká klávesnice";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(886, 350);
            MinimumSize = new Size(690, 316);
            MaximizeBox = false;
            SizeGripStyle = SizeGripStyle.Show;
            using (Stream iconStream = typeof(KeyboardForm).Assembly.GetManifestResourceStream("Keyboard.ico"))
            using (var icon = new Icon(iconStream))
                Icon = (Icon)icon.Clone();
            status.Font = smallFont;
            status.AutoEllipsis = true;
            status.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(status);
            themeToggle.Font = smallFont;
            themeToggle.ToggleSwitch = true;
            themeToggle.AccessibleName = "Tmavý / světlý režim";
            themeToggle.KeyPressed += delegate {
                lightTheme = !lightTheme;
                ApplyTheme();
                try {
                    Directory.CreateDirectory(Path.GetDirectoryName(themePath));
                    File.WriteAllText(themePath, lightTheme ? "light" : "dark");
                } catch (IOException) { ThemeSaveFailed(); } catch (UnauthorizedAccessException) { ThemeSaveFailed(); }
            };
            Controls.Add(themeToggle);
            boldToggle.ToggleSwitch = true;
            boldToggle.Text = "Tučné";
            boldToggle.AccessibleName = "Tučné písmo";
            boldToggle.KeyPressed += delegate {
                boldText = !boldText;
                ApplyTypography();
                try {
                    Directory.CreateDirectory(Path.GetDirectoryName(typographyPath));
                    File.WriteAllText(typographyPath, boldText ? "bold" : "regular");
                } catch (IOException) { TypographySaveFailed(); } catch (UnauthorizedAccessException) { TypographySaveFailed(); }
            };
            Controls.Add(boldToggle);
            presetsButton.Text = "+";
            presetsButton.AccessibleName = "Předvyplněné texty";
            presetsButton.KeyPressed += delegate { TogglePresets(); };
            Controls.Add(presetsButton);
            BuildLayout();
            try { lightTheme = File.ReadAllText(themePath).Trim() == "light"; }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            try { boldText = File.ReadAllText(typographyPath).Trim() == "bold"; }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            ApplyTheme();
            ApplyTypography();
            repeat.Interval = 450;
            repeat.Tick += delegate {
                repeat.Interval = 65;
                if (repeating != null) SendKey(repeating);
            };
            LoadPlacement();
            Resize += delegate { HidePresets(); Arrange(); };
            Move += delegate { HidePresets(); };
            FormClosing += delegate { repeat.Stop(); SavePlacement(); };
            Arrange(); RefreshLabels();
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; p.ExStyle |= 0x08000000; return p; } // WS_EX_NOACTIVATE
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; } // MA_NOACTIVATE
            base.WndProc(ref m);
        }

        private KeySpec Ch(string normal, string upper, string ag, ushort vk, float width)
        { return new KeySpec(normal, upper, ag, vk, "", width); }
        private KeySpec Vk(string name, ushort vk, float width)
        { return new KeySpec(name, name, null, vk, "vk", width); }
        private KeySpec Mod(string name, string action, float width)
        { return new KeySpec(name, name, null, 0, action, width); }
        private void Row(params KeySpec[] keys)
        {
            rows.Add(new List<KeySpec>(keys));
            foreach (KeySpec key in keys)
            {
                var k = key;
                var control = new KeyControl { Font = keyFont, Accent = rows.Count == 2 };
                k.Control = control;
                control.KeyPressed += delegate { Press(k); };
                control.PressEnded += delegate { repeat.Stop(); repeating = null; };
                Controls.Add(control);
            }
        }
        private void BuildLayout()
        {
            var functions = new List<KeySpec>();
            functions.Add(Vk("Esc", 0x1B, 1.1F));
            for (int i = 1; i <= 12; i++) functions.Add(Vk("F" + i, (ushort)(0x6F + i), 1));
            functions.Add(Vk("Del", 0x2E, 1.1F));
            Row(functions.ToArray());
            var numbers = new List<KeySpec>();
            foreach (char c in "1234567890") numbers.Add(Ch(c.ToString(), c.ToString(), null, (ushort)c, 1));
            numbers.Add(Vk("Home", 0x24, 1)); numbers.Add(Vk("End", 0x23, 1));
            numbers.Add(Vk("PgUp", 0x21, 1)); numbers.Add(Vk("PgDn", 0x22, 1));
            Row(numbers.ToArray());
            Row(Ch(";", "°", "`", 0xC0, 1), Ch("+", "1", "~", 0xBB, 1),
                Ch("ě", "2", "ˇ", 0x32, 1), Ch("š", "3", "^", 0x33, 1), Ch("č", "4", "˘", 0x34, 1),
                Ch("ř", "5", "°", 0x35, 1), Ch("ž", "6", "˛", 0x36, 1), Ch("ý", "7", "`", 0x37, 1),
                Ch("á", "8", "˙", 0x38, 1), Ch("í", "9", "´", 0x39, 1), Ch("é", "0", "˝", 0x30, 1),
                Ch("=", "%", "¨", 0xBD, 1), new KeySpec("´", "ˇ", "¸", 0, "dead", 1), Vk("⌫", 8, 1.5F));
            Row(Vk("Tab", 9, 1.5F), Ch("q", "Q", "\\", 0x51, 1), Ch("w", "W", "|", 0x57, 1),
                Ch("e", "E", "€", 0x45, 1), Ch("r", "R", null, 0x52, 1), Ch("t", "T", null, 0x54, 1),
                Ch("z", "Z", null, 0x5A, 1), Ch("u", "U", null, 0x55, 1), Ch("i", "I", null, 0x49, 1),
                Ch("o", "O", null, 0x4F, 1), Ch("p", "P", null, 0x50, 1),
                Ch("ú", "/", "÷", 0xDB, 1), Ch(")", "(", "×", 0xDD, 1), Ch("¨", "'", null, 0xDC, 1));
            Row(Mod("Caps", "caps", 1.75F), Ch("a", "A", null, 0x41, 1), Ch("s", "S", "đ", 0x53, 1),
                Ch("d", "D", "Đ", 0x44, 1), Ch("f", "F", "[", 0x46, 1), Ch("g", "G", "]", 0x47, 1),
                Ch("h", "H", null, 0x48, 1), Ch("j", "J", null, 0x4A, 1), Ch("k", "K", "ł", 0x4B, 1),
                Ch("l", "L", "Ł", 0x4C, 1), Ch("ů", "\"", "$", 0xBA, 1), Ch("§", "!", "ß", 0xDE, 1), Vk("Enter", 0x0D, 1.75F));
            Row(Mod("Shift", "shift", 2), Ch("\\", "|", null, 0xE2, 1), Ch("y", "Y", ">", 0x59, 1),
                Ch("x", "X", "#", 0x58, 1), Ch("c", "C", "&", 0x43, 1), Ch("v", "V", "@", 0x56, 1),
                Ch("b", "B", "{", 0x42, 1), Ch("n", "N", "}", 0x4E, 1), Ch("m", "M", null, 0x4D, 1),
                Ch(",", "?", "<", 0xBC, 1), Ch(".", ":", ">", 0xBE, 1), Ch("-", "_", "*", 0xBF, 1), Vk("↑", 0x26, 1), Mod("Shift", "shift", 1.5F));
            Row(Mod("Ctrl", "ctrl", 1.3F), Mod("Win", "win", 1.1F), Mod("Alt", "alt", 1.1F),
                Ch("Mezerník", "Mezerník", null, 0x20, 6.5F), Mod("AltGr", "altgr", 1.3F),
                Vk("←", 0x25, 1), Vk("↓", 0x28, 1), Vk("→", 0x27, 1));
        }

        private void Arrange()
        {
            float scale;
            using (Graphics graphics = CreateGraphics()) scale = graphics.DpiX / 96F;
            int margin = (int)(10 * scale), gap = (int)(4 * scale);
            int top = margin, bottom = (int)(36 * scale), toggleWidth = (int)(126 * scale);
            int footerTop = ClientSize.Height - bottom + (int)(5 * scale), footerHeight = (int)(26 * scale);
            themeToggle.SetBounds(ClientSize.Width - margin - toggleWidth, footerTop, toggleWidth, footerHeight);
            boldToggle.SetBounds(themeToggle.Left - gap - toggleWidth, footerTop, toggleWidth, footerHeight);
            presetsButton.SetBounds(boldToggle.Left - gap - footerHeight, footerTop, footerHeight, footerHeight);
            status.SetBounds(margin, footerTop, Math.Max(0, presetsButton.Left - margin * 2), footerHeight);
            float rowHeight = (ClientSize.Height - top - bottom - (rows.Count - 1) * gap) / (float)rows.Count;
            for (int r = 0; r < rows.Count; r++)
            {
                float total = 0; foreach (var k in rows[r]) total += k.Width;
                float unit = (ClientSize.Width - margin * 2 - (rows[r].Count - 1) * gap) / total;
                float x = margin;
                foreach (var k in rows[r])
                {
                    int right = (int)Math.Round(x + unit * k.Width);
                    k.Control.SetBounds((int)Math.Round(x), (int)Math.Round(top + r * (rowHeight + gap)),
                        right - (int)Math.Round(x), (int)Math.Round(rowHeight));
                    x += unit * k.Width + gap;
                }
            }
        }

        private void HidePresets() { if (presetPopup != null) presetPopup.Hide(); }
        private void TogglePresets()
        {
            if (presetPopup != null && presetPopup.Visible) { presetPopup.Hide(); return; }
            if (presetPopup != null) presetPopup.Dispose();
            repeat.Stop(); repeating = null;
            float scale;
            using (Graphics graphics = CreateGraphics()) scale = graphics.DpiX / 96F;
            presetPopup = new PresetPopup(PresetTexts.Load(), boldText ? boldSmallFont : smallFont,
                lightTheme ? KeyboardColors.Light : KeyboardColors.Dark, delegate(string text) {
                    error = NativeInput.Text(text) ? "" : "Text se nepodařilo vložit. Zkontrolujte oprávnění cílové aplikace.";
                    dead = ""; ResetOneShot(); RefreshLabels();
                }, scale);
            presetPopup.ForeColor = lightTheme ? KeyboardColors.Light.Border : KeyboardColors.Dark.Border;
            Rectangle trigger = presetsButton.RectangleToScreen(presetsButton.ClientRectangle);
            Rectangle area = Screen.FromRectangle(trigger).WorkingArea;
            int x = Math.Max(area.Left, Math.Min(trigger.Left, area.Right - presetPopup.Width));
            int y = trigger.Top - presetPopup.Height - (int)(4 * scale);
            if (y < area.Top) y = Math.Min(area.Bottom - presetPopup.Height, trigger.Bottom + (int)(4 * scale));
            presetPopup.Location = new Point(x, Math.Max(area.Top, y));
            presetPopup.TriggerBounds = trigger;
            presetPopup.VisibleChanged += delegate { presetsButton.Selected = presetPopup.Visible; presetsButton.Invalidate(); };
            presetPopup.Show(this);
        }

        private void ApplyTheme()
        {
            HidePresets();
            KeyboardColors colors = lightTheme ? KeyboardColors.Light : KeyboardColors.Dark;
            BackColor = colors.Background; ForeColor = colors.Text;
            status.ForeColor = colors.Muted;
            foreach (var row in rows) foreach (var key in row) {
                key.Control.Colors = colors; key.Control.Invalidate();
            }
            themeToggle.Colors = colors;
            boldToggle.Colors = colors;
            presetsButton.Colors = colors;
            presetsButton.Invalidate();
            boldToggle.Invalidate();
            themeToggle.Text = lightTheme ? "Světlý" : "Tmavý";
            themeToggle.Selected = lightTheme;
            themeToggle.AccessibleDescription = "Aktuální režim: " + (lightTheme ? "světlý" : "tmavý") + ". Kliknutím přepnete režim.";
            themeToggle.Invalidate(); Invalidate();
        }
        private void ThemeSaveFailed()
        {
            error = "Režim se nepodařilo uložit pro příští spuštění.";
            RefreshLabels();
        }
        private void ApplyTypography()
        {
            HidePresets();
            foreach (var row in rows) foreach (var key in row) {
                key.Control.Font = boldText ? boldKeyFont : keyFont;
                key.Control.Invalidate();
            }
            status.Font = themeToggle.Font = boldToggle.Font = presetsButton.Font = boldText ? boldSmallFont : smallFont;
            boldToggle.Selected = boldText;
            boldToggle.AccessibleDescription = "Tučné písmo: " + (boldText ? "zapnuto" : "vypnuto") + ". Kliknutím přepnete.";
            boldToggle.Invalidate();
        }
        private void TypographySaveFailed()
        {
            error = "Nastavení písma se nepodařilo uložit pro příští spuštění.";
            RefreshLabels();
        }

        private string Character(KeySpec k)
        {
            if (k.VirtualKey == 0x20) return " ";
            if (altGr && k.AltGr != null) return k.AltGr;
            // Caps affects letters only; the Czech number/diacritic row retains its Shift symbols.
            if (shift) {
                if (caps && k.Normal.Length == 1 && k.Shifted == k.Normal.ToUpperInvariant()) return k.Normal;
                return k.Shifted;
            }
            return caps ? k.Normal.ToUpperInvariant() : k.Normal;
        }
        private bool IsSelected(KeySpec k)
        {
            switch (k.Action) {
                case "shift": return shift; case "caps": return caps; case "ctrl": return ctrl;
                case "alt": return alt; case "altgr": return altGr; case "win": return win;
                case "dead": return dead.Length > 0; default: return false;
            }
        }
        private void RefreshLabels()
        {
            foreach (var row in rows) foreach (var k in row)
            {
                k.Control.Text = k.Action == "" && k.VirtualKey != 0x20 ? Character(k) :
                    k.Action == "dead" ? (shift ? k.Shifted : k.Normal) : k.Normal;
                k.Control.AccessibleName = k.Control.Text == "⌫" ? "Backspace" : k.Control.Text;
                k.Control.Selected = IsSelected(k); k.Control.Invalidate();
            }
            var modes = new List<string>();
            if (ctrl) modes.Add("Ctrl"); if (alt) modes.Add("Alt"); if (win) modes.Add("Win");
            if (shift) modes.Add("Shift"); if (altGr) modes.Add("AltGr"); if (caps) modes.Add("Caps");
            if (dead.Length > 0) modes.Add("znaménko " + dead);
            status.Text = "CZ / QWERTZ   ·   " + (error.Length > 0 ? error : modes.Count > 0 ?
                String.Join(" + ", modes.ToArray()) + "  ·  vyberte další klávesu" :
                "Vždy navrchu   ·   Shift / Ctrl / Alt pro další klávesu");
        }
        private void Press(KeySpec k)
        {
            HidePresets();
            repeat.Stop(); repeating = null; error = "";
            switch (k.Action) {
                case "shift": shift = !shift; break;
                case "caps": caps = !caps; break;
                case "ctrl": ctrl = !ctrl; altGr = false; break;
                case "alt": alt = !alt; altGr = false; break;
                case "altgr": altGr = !altGr; ctrl = alt = false; break;
                case "win": win = !win; break;
                case "dead":
                    if (altGr) { NativeInput.Text(k.AltGr); ResetOneShot(); break; }
                    string mark = shift ? "ˇ" : "´";
                    if (dead == mark) { NativeInput.Text(mark); dead = ""; }
                    else dead = mark;
                    shift = false; break;
                default:
                    bool canRepeat = !ctrl && !alt && !win && !shift && !altGr && dead.Length == 0 &&
                        (k.Action == "" || k.VirtualKey == 8 || (k.VirtualKey >= 0x21 && k.VirtualKey <= 0x28) || k.VirtualKey == 0x2E);
                    SendKey(k);
                    if (canRepeat && error.Length == 0) { repeating = k; repeat.Interval = 450; repeat.Start(); }
                    break;
            }
            RefreshLabels();
        }
        private void SendKey(KeySpec k)
        {
            bool success;
            if (dead.Length > 0 && (k.VirtualKey == 8 || k.VirtualKey == 0x1B))
            { dead = ""; ResetOneShot(); RefreshLabels(); return; }
            if (k.Action == "vk" || ctrl || alt || win)
            {
                dead = "";
                success = NativeInput.Key(k.VirtualKey, shift, ctrl, alt, win);
            }
            else
            {
                string text = Character(k);
                if (dead.Length > 0) {
                    if (text == " ") text = dead;
                    else {
                        string composed = (text + (dead == "´" ? "\u0301" : "\u030C")).Normalize(NormalizationForm.FormC);
                        text = composed.Length == 1 ? composed : dead + text;
                    }
                    dead = "";
                }
                success = NativeInput.Text(text);
            }
            if (!success) { error = "Vstup se nepodařilo odeslat. Pro aplikaci správce spusťte klávesnici také jako správce."; repeat.Stop(); }
            ResetOneShot(); RefreshLabels();
        }
        private void ResetOneShot() { shift = ctrl = alt = altGr = win = false; }
        private void LoadPlacement()
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - 24);
            try {
                var values = File.ReadAllText(settingsPath).Split(',');
                var bounds = new Rectangle(Int32.Parse(values[0]), Int32.Parse(values[1]), Int32.Parse(values[2]), Int32.Parse(values[3]));
                if (bounds.Width < MinimumSize.Width || bounds.Height < MinimumSize.Height) return;
                foreach (var screen in Screen.AllScreens) {
                    if (screen.WorkingArea.Contains(bounds)) { Bounds = bounds; break; }
                }
            } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (FormatException) { } catch (IndexOutOfRangeException) { } catch (OverflowException) { }
        }
        private void SavePlacement()
        {
            try {
                Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(settingsPath, String.Join(",", new string[] { b.X.ToString(), b.Y.ToString(), b.Width.ToString(), b.Height.ToString() }));
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                if (presetPopup != null) presetPopup.Dispose();
                repeat.Dispose(); keyFont.Dispose(); smallFont.Dispose(); boldKeyFont.Dispose(); boldSmallFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal static class NativeInput
    {
        [StructLayout(LayoutKind.Sequential)] internal struct INPUT { public uint type; public UNION data; }
        [StructLayout(LayoutKind.Explicit)] internal struct UNION {
            [FieldOffset(0)] public KEYBDINPUT keyboard;
            [FieldOffset(0)] public MOUSEINPUT mouse;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct KEYBDINPUT {
            public ushort vk, scan; public uint flags, time; public UIntPtr extra;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct MOUSEINPUT {
            public int dx, dy; public uint mouseData, flags, time; public UIntPtr extra;
        }
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        private static INPUT Event(ushort vk, ushort scan, uint flags)
        { return new INPUT { type = 1, data = new UNION { keyboard = new KEYBDINPUT { vk = vk, scan = scan, flags = flags } } }; }
        internal static bool Text(string value)
        {
            var inputs = new List<INPUT>();
            foreach (char c in value) { inputs.Add(Event(0, c, 4)); inputs.Add(Event(0, c, 6)); }
            return Send(inputs);
        }
        internal static bool Key(ushort vk, bool shift, bool ctrl, bool alt, bool win)
        {
            var inputs = new List<INPUT>();
            var modifiers = new List<ushort>();
            if (ctrl) modifiers.Add(0x11); if (alt) modifiers.Add(0x12);
            if (shift) modifiers.Add(0x10); if (win) modifiers.Add(0x5B);
            var owned = new List<ushort>();
            foreach (ushort modifier in modifiers) {
                // Do not release a modifier the user is holding on the physical keyboard.
                if ((GetAsyncKeyState(modifier) & 0x8000) == 0) { inputs.Add(Event(modifier, 0, modifier == 0x5B ? 1U : 0U)); owned.Add(modifier); }
            }
            uint extended = (vk >= 0x21 && vk <= 0x2E) ? 1U : 0U;
            inputs.Add(Event(vk, 0, extended)); inputs.Add(Event(vk, 0, extended | 2));
            owned.Reverse();
            foreach (ushort modifier in owned) inputs.Add(Event(modifier, 0, (modifier == 0x5B ? 1U : 0U) | 2));
            return Send(inputs);
        }
        private static bool Send(List<INPUT> inputs)
        {
            return SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT))) == inputs.Count;
        }
    }
}
