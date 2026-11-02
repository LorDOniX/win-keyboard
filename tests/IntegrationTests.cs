using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
using CzechKeyboard;

// Runs the real keyboard controls and SendInput against a dedicated Windows text box.
// No clipboard, hooks, user documents or external applications are used.
internal static class IntegrationTests
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    private static Form target;
    private static TextBox editor;
    private static KeyboardForm keyboard;
    private static Label result;
    private static Timer timer;
    private static Queue<Action> steps = new Queue<Action>();
    private static List<string> report = new List<string>();
    private static int functionKey;

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        target = new Form { Text = "Test české klávesnice", StartPosition = FormStartPosition.Manual,
            Location = new Point(70, 70), ClientSize = new Size(720, 190) };
        editor = new TextBox { Multiline = true, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 17F) };
        result = new Label { Dock = DockStyle.Bottom, Height = 45, Text = "Probíhá integrační test…", Font = new Font("Segoe UI", 11F) };
        target.Controls.Add(editor); target.Controls.Add(result);
        editor.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.F5) functionKey++; };
        target.Shown += delegate {
            keyboard = new KeyboardForm(); keyboard.Show();
            target.Activate(); editor.Focus();
            Plan(); timer.Start();
        };
        target.FormClosed += delegate { if (keyboard != null) keyboard.Close(); };
        timer = new Timer { Interval = 100 };
        timer.Tick += delegate {
            try {
                if (steps.Count > 0) steps.Dequeue()();
                else Finish(true);
            } catch (Exception ex) { report.Add("FAIL: " + ex.ToString()); Finish(false); }
        };
        Application.Run(target);
    }
    private static void Finish(bool success)
    {
        timer.Stop();
        result.Text = success ? "PASS: všechny integrační testy prošly. Pole je připraveno pro ruční ověření." : "FAIL: viz tests/bin/results.txt";
        result.ForeColor = success ? Color.DarkGreen : Color.DarkRed;
        report.Add(success ? "PASS: all tests" : "FAIL");
        File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "results.txt"), report.ToArray());
    }
    private static KeyControl Find(string label)
    {
        foreach (Control c in keyboard.Controls) if (c is KeyControl && c.Text == label) return (KeyControl)c;
        throw new Exception("Key not found: " + label);
    }
    private static void Click(string label)
    {
        KeyControl key = Find(label);
        typeof(KeyControl).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(key, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0) });
        typeof(KeyControl).GetMethod("OnMouseUp", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(key, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0) });
    }
    private static void Press(params string[] labels) { foreach (string label in labels) { string k = label; steps.Enqueue(delegate { Click(k); }); } }
    private static void Check(string name, string expected)
    {
        steps.Enqueue(delegate {
            if (editor.Text != expected) throw new Exception(name + ": expected [" + expected + "] got [" + editor.Text + "]");
            if (GetForegroundWindow() != target.Handle || !editor.Focused) throw new Exception(name + ": focus lost");
            report.Add("PASS: " + name);
        });
    }
    private static void Clear() { steps.Enqueue(delegate { editor.Clear(); }); }
    private static void Plan()
    {
        steps.Enqueue(delegate {
            if (Marshal.SizeOf(typeof(NativeInput.INPUT)) != (IntPtr.Size == 8 ? 40 : 28)) throw new Exception("INPUT ABI size");
            if (!keyboard.TopMost) throw new Exception("Not topmost");
            if (SendMessage(keyboard.Handle, 0x21, IntPtr.Zero, IntPtr.Zero) != (IntPtr)3) throw new Exception("Mouse activates form");
            report.Add("PASS: INPUT ABI, always on top, MA_NOACTIVATE");
        });
        Press("1", "2", "3", "4", "5", "6", "7", "8", "9", "0"); Check("dedicated numbers", "1234567890"); Clear();
        Press("ě", "š", "č", "ř", "ž", "ý", "á", "í", "é", "ú", "ů"); Check("Czech diacritics", "ěščřžýáíéúů"); Clear();
        Press("q", "w", "e", "r", "t", "z", "y", "x"); Check("QWERTZ", "qwertzyx"); Clear();
        Press("Shift", "A", "a"); Check("one-shot Shift", "Aa"); Clear();
        Press("Caps", "Č", "A", "Shift", "a", "Caps"); Check("Caps and Shift", "ČAa"); Clear();
        Press("´", "o", "Shift", "ˇ", "d", "Shift", "ˇ", "t", "Shift", "ˇ", "n"); Check("dead accents", "óďťň");
        Press("⌫"); Check("Backspace", "óďť");
        Press("Ctrl", "a", "z"); Check("Ctrl+A and replacement", "z"); Clear();
        Press("AltGr", "@", "AltGr", "€", "AltGr", "{"); Check("AltGr", "@€{"); Clear();
        Press("´", "Mezerník", "´", "Esc", "a"); Check("literal and cancelled accent", "´a"); Clear();
        Press("a", "b", "←", "x"); Check("arrow navigation", "axb");
        Press("F5"); steps.Enqueue(delegate { if (functionKey != 1) throw new Exception("F5 not received"); report.Add("PASS: F5 native key event"); });
        Clear();
    }
}
