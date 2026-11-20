using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Theme Toggle")]
[assembly: AssemblyDescription("System tray toggle for Windows dark and light mode")]
[assembly: AssemblyCompany("awabkb")]
[assembly: AssemblyProduct("Theme Toggle")]
[assembly: AssemblyCopyright("Copyright 2026 awabkb")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

internal static class Native
{
    public const int WM_SETTINGCHANGE = 0x001A;
    public const int WM_HOTKEY = 0x0312;
    public const uint SMTO_ABORTIFHUNG = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;
    public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();
}

internal static class Theme
{
    const string RegPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsLight()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegPath))
        {
            if (key == null) return true;
            object value = key.GetValue("AppsUseLightTheme", 1);
            return Convert.ToInt32(value) == 1;
        }
    }

    public static void SetLight(bool light)
    {
        int value = light ? 1 : 0;
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegPath, true))
        {
            if (key == null) return;
            key.SetValue("AppsUseLightTheme", value, RegistryValueKind.DWord);
            key.SetValue("SystemUsesLightTheme", value, RegistryValueKind.DWord);
        }

        IntPtr result;
        Native.SendMessageTimeout(
            Native.HWND_BROADCAST,
            (uint)Native.WM_SETTINGCHANGE,
            IntPtr.Zero,
            "ImmersiveColorSet",
            Native.SMTO_ABORTIFHUNG,
            1000,
            out result);
    }
}

internal static class Icons
{
    // Draws at the tray's real pixel size (16 at 100% scaling, 24 at 150%, 32 at 200%)
    // so the icon stays sharp. Geometry is laid out on a 32-unit grid and scaled.
    public static Icon Create(bool darkMode)
    {
        int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
        float s = size / 32f;

        using (Bitmap bmp = new Bitmap(size, size))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                if (darkMode) DrawMoon(g, s);
                else DrawSun(g, s);
            }

            // GetHicon returns a handle that Icon.FromHandle does not own.
            // Clone it into an icon that owns its data, then free the handle.
            IntPtr handle = bmp.GetHicon();
            try
            {
                using (Icon temp = Icon.FromHandle(handle))
                {
                    return (Icon)temp.Clone();
                }
            }
            finally
            {
                Native.DestroyIcon(handle);
            }
        }
    }

    static void DrawMoon(Graphics g, float s)
    {
        // Crescent: a disc with an offset disc cut out, leaving real transparency
        // so it looks right on any taskbar colour.
        using (GraphicsPath disc = new GraphicsPath())
        using (GraphicsPath bite = new GraphicsPath())
        using (SolidBrush fill = new SolidBrush(Color.FromArgb(255, 225, 120)))
        {
            disc.AddEllipse(4 * s, 4 * s, 24 * s, 24 * s);
            bite.AddEllipse(12 * s, 0 * s, 22 * s, 22 * s);
            using (Region region = new Region(disc))
            {
                region.Exclude(bite);
                g.FillRegion(fill, region);
            }
        }
    }

    static void DrawSun(Graphics g, float s)
    {
        using (SolidBrush gold = new SolidBrush(Color.FromArgb(240, 160, 0)))
        using (Pen ray = new Pen(Color.FromArgb(230, 140, 0), Math.Max(1.5f, 2.6f * s)))
        {
            ray.StartCap = LineCap.Round;
            ray.EndCap = LineCap.Round;
            g.FillEllipse(gold, 9 * s, 9 * s, 14 * s, 14 * s);
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4.0;
                float cos = (float)Math.Cos(angle);
                float sin = (float)Math.Sin(angle);
                g.DrawLine(ray,
                    (16 + cos * 10.5f) * s, (16 + sin * 10.5f) * s,
                    (16 + cos * 14f) * s, (16 + sin * 14f) * s);
            }
        }
    }
}

// Hidden top-level window: receives the global hotkey and the
// WM_SETTINGCHANGE broadcast sent when the theme changes from anywhere.
internal sealed class MessageWindow : NativeWindow, IDisposable
{
    const int HotkeyId = 1;
    public event EventHandler HotkeyPressed;
    public event EventHandler ThemeChanged;
    public readonly bool HotkeyRegistered;

    public MessageWindow()
    {
        CreateHandle(new CreateParams());
        HotkeyRegistered = Native.RegisterHotKey(
            Handle, HotkeyId,
            Native.MOD_WIN | Native.MOD_SHIFT | Native.MOD_NOREPEAT,
            (uint)Keys.D);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
        {
            if (HotkeyPressed != null) HotkeyPressed(this, EventArgs.Empty);
        }
        else if (m.Msg == Native.WM_SETTINGCHANGE && m.LParam != IntPtr.Zero
            && Marshal.PtrToStringUni(m.LParam) == "ImmersiveColorSet")
        {
            if (ThemeChanged != null) ThemeChanged(this, EventArgs.Empty);
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            if (HotkeyRegistered) Native.UnregisterHotKey(Handle, HotkeyId);
            DestroyHandle();
        }
    }
}

internal sealed class TrayApp : ApplicationContext
{
    const string SettingsPath = @"SOFTWARE\awabkb\ThemeToggle";
    const string HotkeyText = "Win+Shift+D";
    readonly NotifyIcon tray;
    readonly MessageWindow window;
    Icon currentIcon;

    public TrayApp()
    {
        tray = new NotifyIcon();
        tray.Visible = true;
        RefreshIcon();

        window = new MessageWindow();
        window.HotkeyPressed += delegate { Toggle(); };
        window.ThemeChanged += delegate { RefreshIcon(); };

        ContextMenuStrip menu = new ContextMenuStrip();
        ToolStripMenuItem toggleItem = new ToolStripMenuItem(
            "Toggle dark / light mode", null, delegate { Toggle(); });
        if (window.HotkeyRegistered) toggleItem.ShortcutKeyDisplayString = HotkeyText;
        menu.Items.Add(toggleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, delegate { Exit(); });
        tray.ContextMenuStrip = menu;

        tray.MouseClick += delegate(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Toggle();
            }
        };

        if (!window.HotkeyRegistered)
        {
            tray.BalloonTipTitle = "Theme Toggle";
            tray.BalloonTipText = HotkeyText + " is already used by another app, so the shortcut is off. Clicking the icon still works.";
            tray.ShowBalloonTip(5000);
        }
        else if (ShouldShowTip())
        {
            tray.BalloonTipTitle = "Theme Toggle";
            tray.BalloonTipText = "Click this icon or press " + HotkeyText + " to switch dark and light mode.";
            tray.ShowBalloonTip(4000);
        }
    }

    static bool ShouldShowTip()
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsPath))
        {
            if (key == null) return true;
            object shown = key.GetValue("ShownTip", 0);
            if (Convert.ToInt32(shown) == 1) return false;
            key.SetValue("ShownTip", 1, RegistryValueKind.DWord);
            return true;
        }
    }

    void Toggle()
    {
        Theme.SetLight(!Theme.IsLight());
        RefreshIcon();
    }

    void RefreshIcon()
    {
        bool light = Theme.IsLight();
        Icon next = Icons.Create(!light);
        tray.Icon = next;
        tray.Text = light
            ? "Light mode - click to switch to dark"
            : "Dark mode - click to switch to light";
        if (currentIcon != null)
        {
            currentIcon.Dispose();
        }
        currentIcon = next;
    }

    void Exit()
    {
        window.Dispose();
        tray.Visible = false;
        tray.Dispose();
        if (currentIcon != null) currentIcon.Dispose();
        Application.Exit();
    }
}

internal static class Program
{
    [STAThread]
    static void Main()
    {
        bool created;
        using (Mutex mutex = new Mutex(true, "AwabkThemeToggleTray", out created))
        {
            if (!created)
            {
                return;
            }

            // Without this, Windows reports 16px icons at every scale and
            // stretches them, which is what made the old icon blurry.
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp());
        }
    }
}
