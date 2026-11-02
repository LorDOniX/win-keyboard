using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using CzechKeyboard;

internal static class ThemeTests
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme-settings-" + Guid.NewGuid().ToString("N"));
        using (var keyboard = new KeyboardForm(directory)) {
            KeyControl toggle = Toggle(keyboard);
            Require(toggle.Text == "Tmavý", "Default dark theme");
            Require(!BoldToggle(keyboard).Selected, "Bold is off by default");
            CheckFonts(keyboard, false);
            CheckFooter(keyboard);
            Snapshot(keyboard, "theme-dark.png");
            Click(toggle);
            Require(toggle.Text == "Světlý" && toggle.Selected, "Toggle to light");
            Require(File.ReadAllText(Path.Combine(directory, "theme.txt")) == "light", "Immediate save");
            Require(keyboard.BackColor == KeyboardColors.Light.Background, "Light background");
            foreach (Control c in keyboard.Controls) {
                var key = c as KeyControl;
                if (key != null) Require(key.Colors == KeyboardColors.Light, "Light key palette");
            }
            Snapshot(keyboard, "theme-light.png");
            Click(BoldToggle(keyboard));
            Require(BoldToggle(keyboard).Selected, "Toggle bold on");
            CheckFonts(keyboard, true);
            Require(File.ReadAllText(Path.Combine(directory, "typography.txt")) == "bold", "Immediate typography save");
            Snapshot(keyboard, "theme-light-bold.png");
            using (var reopened = new KeyboardForm(directory)) {
                Require(Toggle(reopened).Text == "Světlý", "Restore light on restart");
                Require(BoldToggle(reopened).Selected, "Restore bold on restart");
                CheckFonts(reopened, true);
                Click(Toggle(reopened));
                CheckFonts(reopened, true);
                Click(BoldToggle(reopened));
                CheckFonts(reopened, false);
            }
        }
        using (var reopened = new KeyboardForm(directory)) {
            Require(Toggle(reopened).Text == "Tmavý", "Restore dark on restart");
            Require(!BoldToggle(reopened).Selected, "Restore regular on restart");
            CheckFonts(reopened, false);
            reopened.Size = reopened.MinimumSize;
            CheckFooter(reopened);
            Snapshot(reopened, "theme-minimum.png");
        }
        File.WriteAllText(Path.Combine(directory, "theme.txt"), "invalid");
        File.WriteAllText(Path.Combine(directory, "typography.txt"), "invalid");
        using (var reopened = new KeyboardForm(directory)) {
            Require(Toggle(reopened).Text == "Tmavý", "Invalid settings fallback");
            CheckFonts(reopened, false);
        }
        CheckPresets();
        Console.WriteLine("PASS: themes and bold text, defaults, immediate save, restart restoration, invalid settings, footer layout, preset parsing, exact selection, popup layout and no-activation styles.");
    }
    private static KeyControl Toggle(KeyboardForm keyboard)
    {
        foreach (Control c in keyboard.Controls) if (c is KeyControl && c.AccessibleName == "Tmavý / světlý režim") return (KeyControl)c;
        throw new Exception("Toggle not found");
    }
    private static KeyControl BoldToggle(KeyboardForm keyboard)
    {
        foreach (Control c in keyboard.Controls) if (c is KeyControl && c.AccessibleName == "Tučné písmo") return (KeyControl)c;
        throw new Exception("Bold toggle not found");
    }
    private static void CheckFonts(KeyboardForm keyboard, bool bold)
    {
        foreach (Control c in keyboard.Controls) Require(c.Font.Bold == bold, "Font weight on " + c.Text);
    }
    private static void Click(KeyControl control)
    {
        foreach (string method in new string[] { "OnMouseDown", "OnMouseUp" })
            typeof(KeyControl).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(control,
                new object[] { new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0) });
    }
    private static void CheckFooter(KeyboardForm keyboard)
    {
        KeyControl toggle = Toggle(keyboard);
        KeyControl bold = BoldToggle(keyboard);
        KeyControl plus = null;
        foreach (Control c in keyboard.Controls) if (c.AccessibleName == "Předvyplněné texty") plus = (KeyControl)c;
        Require(plus != null && plus.Right < bold.Left && plus.Top == bold.Top, "Plus beside bold toggle");
        Require(bold.Right < toggle.Left && bold.Top == toggle.Top, "Adjacent toggles do not overlap");
        foreach (Control c in keyboard.Controls) {
            if (c is Label) {
                Require(c.Text.StartsWith("CZ / QWERTZ"), "Info starts footer");
                Require(c.Right < plus.Left && c.Top == toggle.Top, "Footer and controls do not overlap");
            } else if (c != toggle && c != bold && c != plus) Require(c.Bottom < toggle.Top, "No header or keys overlapping footer");
        }
    }
    private static void CheckPresets()
    {
        var texts = PresetTexts.Read(new StringReader("\r\n \t\r\n  Žluťoučký kůň & € 😀  \r\nDruhý řádek\nDruhý řádek\n"));
        Require(texts.Count == 3 && texts[0] == "  Žluťoučký kůň & € 😀  " && texts[1] == texts[2], "Blank lines, exact content, duplicate lines, Czech and Unicode");
        string path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "predvolene-texty.txt"));
        using (var reader = new StreamReader(path, System.Text.Encoding.UTF8, true)) {
            var expected = PresetTexts.Read(reader);
            Require(String.Join("\n", expected.ToArray()) == String.Join("\n", PresetTexts.Load().ToArray()), "Source file embedded in executable");
        }
        using (var font = new Font("Segoe UI", 9F, FontStyle.Bold)) {
            string inserted = null;
            using (var popup = new PresetPopup(texts, font, KeyboardColors.Light, delegate(string value) { inserted = value; }, 1F)) {
                var parameters = (CreateParams)typeof(PresetPopup).GetProperty("CreateParams", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(popup, null);
                Require((parameters.ExStyle & 0x08000000) != 0, "Popup WS_EX_NOACTIVATE");
                Require((bool)typeof(PresetPopup).GetProperty("ShowWithoutActivation", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(popup, null), "Popup show without activation");
                Panel list = null;
                foreach (Control c in popup.Controls) if (c is Panel) list = (Panel)c;
                Require(list != null && list.Controls.Count == 3 && list.AutoScroll, "One scrollable button per nonempty line");
                Click((KeyControl)list.Controls[0]);
                Require(inserted == texts[0], "Selection sends exact whole line without Enter");
                Snapshot(popup, "presets-light.png");
            }
            var many = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 50; i++) many.Add("Dlouhý předvyplněný text, který se v nabídce zalomí na více řádků. " + i);
            using (var popup = new PresetPopup(many, font, KeyboardColors.Dark, delegate { }, 1F)) {
                Panel list = null;
                foreach (Control c in popup.Controls) if (c is Panel) list = (Panel)c;
                Require(list != null && list.Controls[49].Bottom > list.Height && list.AutoScroll, "Long list scrolls");
                Require(list.Controls[0].Height > 36, "Long text wraps");
                Snapshot(popup, "presets-long.png");
            }
            using (var popup = new PresetPopup(new string[0], font, KeyboardColors.Dark, delegate { }, 1F))
                Require(popup.Height > 0, "Empty list supported");
        }
    }
    private static void Snapshot(Form form, string name)
    {
        // Paint the client area directly: DrawToBitmap omits children of an unshown form.
        using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
        using (Graphics graphics = Graphics.FromImage(bitmap)) {
            graphics.Clear(form.BackColor);
            PaintChildren(form, graphics);
            bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name), ImageFormat.Png);
        }
    }
    private static void PaintChildren(Control parent, Graphics graphics)
    {
            foreach (Control control in parent.Controls) {
                if (control.Width <= 0 || control.Height <= 0) continue;
                using (var child = new Bitmap(control.Width, control.Height))
                using (Graphics childGraphics = Graphics.FromImage(child)) {
                    childGraphics.Clear(parent.BackColor);
                    if (control is KeyControl) {
                        typeof(KeyControl).GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(control,
                            new object[] { new PaintEventArgs(childGraphics, control.ClientRectangle) });
                    } else if (control is Panel) {
                        PaintChildren(control, childGraphics);
                    } else {
                        TextRenderer.DrawText(childGraphics, control.Text, control.Font, control.ClientRectangle, control.ForeColor,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    }
                    graphics.DrawImageUnscaled(child, control.Location);
                }
            }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
