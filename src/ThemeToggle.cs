using System;
using System.Device.Location;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Theme Toggle")]
[assembly: AssemblyDescription("System tray toggle for Windows dark and light mode")]
[assembly: AssemblyCompany("awabkb")]
[assembly: AssemblyProduct("Theme Toggle")]
[assembly: AssemblyCopyright("Copyright 2026 awabkb")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

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

internal static class Settings
{
    const string Path = @"SOFTWARE\awabkb\ThemeToggle";

    static object Get(string name)
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(Path))
        {
            return key == null ? null : key.GetValue(name);
        }
    }

    static void Set(string name, object value, RegistryValueKind kind)
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Path))
        {
            if (key != null) key.SetValue(name, value, kind);
        }
    }

    public static bool GetFlag(string name)
    {
        object v = Get(name);
        return v != null && Convert.ToInt32(v) == 1;
    }

    public static void SetFlag(string name, bool on)
    {
        Set(name, on ? 1 : 0, RegistryValueKind.DWord);
    }

    public static bool Auto
    {
        get { return GetFlag("Auto"); }
        set { SetFlag("Auto", value); }
    }

    // True when the user typed coordinates; we then never overwrite them
    // with the Windows location.
    public static bool ManualLocation
    {
        get { return GetFlag("ManualLocation"); }
    }

    public static bool TryGetLocation(out double lat, out double lon)
    {
        lat = lon = 0;
        object a = Get("Latitude"), b = Get("Longitude");
        return a != null && b != null
            && double.TryParse(a.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
            && double.TryParse(b.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out lon);
    }

    public static void SetLocation(double lat, double lon, bool manual)
    {
        Set("Latitude", lat.ToString("R", CultureInfo.InvariantCulture), RegistryValueKind.String);
        Set("Longitude", lon.ToString("R", CultureInfo.InvariantCulture), RegistryValueKind.String);
        SetFlag("ManualLocation", manual);
    }
}

// Sunrise and sunset from the NOAA/Wikipedia "sunrise equation".
// Accurate to about a minute, fully offline.
internal static class Sun
{
    const double Rad = Math.PI / 180.0;

    public const int Normal = 0, MidnightSun = 1, PolarNight = -1;

    // Times for the given calendar date at (lat, lon), longitude east-positive.
    public static int Times(DateTime date, double lat, double lon, out DateTime riseUtc, out DateTime setUtc)
    {
        riseUtc = setUtc = DateTime.MinValue;
        double n = (date.Date - new DateTime(2000, 1, 1)).Days;
        double jStar = n - lon / 360.0;
        double m = Mod(357.5291 + 0.98560028 * jStar, 360.0) * Rad;
        double c = 1.9148 * Math.Sin(m) + 0.02 * Math.Sin(2 * m) + 0.0003 * Math.Sin(3 * m);
        double l = Mod(m / Rad + c + 180.0 + 102.9372, 360.0) * Rad;
        double jTransit = 2451545.0 + jStar + 0.0053 * Math.Sin(m) - 0.0069 * Math.Sin(2 * l);
        double sinDec = Math.Sin(l) * Math.Sin(23.4397 * Rad);
        double cosDec = Math.Cos(Math.Asin(sinDec));
        double cosW = (Math.Sin(-0.833 * Rad) - Math.Sin(lat * Rad) * sinDec) / (Math.Cos(lat * Rad) * cosDec);
        if (cosW > 1) return PolarNight;
        if (cosW < -1) return MidnightSun;
        double w = Math.Acos(cosW) / Rad / 360.0;
        riseUtc = FromJulian(jTransit - w);
        setUtc = FromJulian(jTransit + w);
        return Normal;
    }

    // Is it daytime at local time `now`? `next` is the next local sunrise or
    // sunset, or null during midnight sun / polar night.
    public static bool IsDay(DateTime now, double lat, double lon, out DateTime? next)
    {
        DateTime rise, set;
        int kind = Times(now, lat, lon, out rise, out set);
        next = null;
        if (kind == MidnightSun) return true;
        if (kind == PolarNight) return false;

        rise = rise.ToLocalTime();
        set = set.ToLocalTime();
        if (now < rise) { next = rise; return false; }
        if (now < set) { next = set; return true; }

        DateTime rise2, set2;
        if (Times(now.AddDays(1), lat, lon, out rise2, out set2) == Normal)
        {
            next = rise2.ToLocalTime();
        }
        return false;
    }

    static double Mod(double x, double m) { return ((x % m) + m) % m; }

    static DateTime FromJulian(double j)
    {
        return new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddDays(j - 2451545.0);
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

// Asks for coordinates when Windows location is off. Accepts what people
// paste from a search engine: "51.5074, -0.1278" or "51.5074° N, 0.1278° W".
internal sealed class LocationForm : Form
{
    readonly TextBox input;
    public double Latitude, Longitude;

    public LocationForm(string message, bool haveCurrent, double lat, double lon)
    {
        Text = "Theme Toggle - Location";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        TableLayoutPanel layout = new TableLayoutPanel();
        layout.AutoSize = true;
        layout.ColumnCount = 1;
        layout.Dock = DockStyle.Fill;

        Label text = new Label();
        text.AutoSize = true;
        text.MaximumSize = new Size(420, 0);
        text.Text = message + "\n\nSearch \"<your city> coordinates\" and paste the result here, for example 51.5074, -0.1278 or 51.5074° N, 0.1278° W.";
        text.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(text);

        input = new TextBox();
        input.Width = 420;
        if (haveCurrent)
        {
            input.Text = lat.ToString("0.####", CultureInfo.InvariantCulture) + ", "
                + lon.ToString("0.####", CultureInfo.InvariantCulture);
        }
        layout.Controls.Add(input);

        FlowLayoutPanel buttons = new FlowLayoutPanel();
        buttons.AutoSize = true;
        buttons.FlowDirection = FlowDirection.RightToLeft;
        buttons.Dock = DockStyle.Fill;
        buttons.Margin = new Padding(0, 12, 0, 0);
        Button cancel = new Button();
        cancel.Text = "Cancel";
        cancel.DialogResult = DialogResult.Cancel;
        cancel.AutoSize = true;
        Button ok = new Button();
        ok.Text = "Save";
        ok.AutoSize = true;
        ok.Click += delegate { Save(); };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    void Save()
    {
        double lat, lon;
        if (!TryParse(input.Text, out lat, out lon))
        {
            MessageBox.Show(this,
                "Couldn't read that. Use latitude, then longitude, e.g. 51.5074, -0.1278.",
                "Theme Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Latitude = lat;
        Longitude = lon;
        DialogResult = DialogResult.OK;
    }

    public static bool TryParse(string text, out double lat, out double lon)
    {
        lat = lon = 0;
        if (text == null) return false;
        // "51,5; -0,12" uses decimal commas; otherwise commas separate the pair.
        if (text.Contains(";")) text = text.Replace(',', '.').Replace(';', ' ');
        MatchCollection m = Regex.Matches(text,
            @"([-+]?\d+(?:\.\d+)?)\s*°?\s*([NSEWnsew])?");
        if (m.Count != 2) return false;
        if (!Parse(m[0], out lat) || !Parse(m[1], out lon)) return false;
        string h1 = m[0].Groups[2].Value.ToUpperInvariant();
        string h2 = m[1].Groups[2].Value.ToUpperInvariant();
        if (h1 == "E" || h1 == "W" || h2 == "N" || h2 == "S")
        {
            double t = lat; lat = lon; lon = t;
        }
        return lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;
    }

    static bool Parse(Match m, out double v)
    {
        if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return false;
        string h = m.Groups[2].Value.ToUpperInvariant();
        if ((h == "S" || h == "W") && v > 0) v = -v;
        return true;
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
    const string HotkeyText = "Win+Shift+D";
    readonly NotifyIcon tray;
    readonly MessageWindow window;
    readonly SynchronizationContext ui;
    readonly ToolStripMenuItem autoItem;
    readonly ToolStripMenuItem statusItem;
    readonly System.Windows.Forms.Timer clock;
    Icon currentIcon;

    // What automatic mode last decided (true = light). A manual switch leaves
    // this alone, so it lasts until the sun next rises or sets.
    bool? autoDecision;
    GeoCoordinateWatcher watcher;
    System.Windows.Forms.Timer locateTimeout;

    public TrayApp()
    {
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        ui = SynchronizationContext.Current;

        tray = new NotifyIcon();
        tray.Visible = true;

        window = new MessageWindow();
        window.HotkeyPressed += delegate { Toggle(); };
        window.ThemeChanged += delegate { RefreshIcon(); };

        ContextMenuStrip menu = new ContextMenuStrip();
        ToolStripMenuItem toggleItem = new ToolStripMenuItem(
            "Toggle dark / light mode", null, delegate { Toggle(); });
        if (window.HotkeyRegistered) toggleItem.ShortcutKeyDisplayString = HotkeyText;
        menu.Items.Add(toggleItem);
        menu.Items.Add(new ToolStripSeparator());

        autoItem = new ToolStripMenuItem("Automatic: dark from sunset to sunrise", null, delegate { SetAuto(!Settings.Auto); });
        autoItem.Checked = Settings.Auto;
        menu.Items.Add(autoItem);
        statusItem = new ToolStripMenuItem("");
        statusItem.Enabled = false;
        menu.Items.Add(statusItem);
        menu.Items.Add("Set location...", null, delegate { AskLocation("Where are you? Theme Toggle uses this only to work out sunrise and sunset. It never leaves your PC."); });
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

        // Every 30 s is plenty, and it also catches up after sleep or a clock change.
        clock = new System.Windows.Forms.Timer();
        clock.Interval = 30000;
        clock.Tick += delegate { Evaluate(); };
        clock.Start();

        RefreshIcon();
        if (Settings.Auto)
        {
            Evaluate();
            // Refresh the location quietly in case the user has travelled.
            if (!Settings.ManualLocation) Locate(false);
        }

        if (!window.HotkeyRegistered)
        {
            tray.BalloonTipTitle = "Theme Toggle";
            tray.BalloonTipText = HotkeyText + " is already used by another app, so the shortcut is off. Clicking the icon still works.";
            tray.ShowBalloonTip(5000);
        }
        else if (!Settings.GetFlag("ShownTip"))
        {
            Settings.SetFlag("ShownTip", true);
            tray.BalloonTipTitle = "Theme Toggle";
            tray.BalloonTipText = "Click this icon or press " + HotkeyText + " to switch dark and light mode. Right-click for automatic sunset/sunrise switching.";
            tray.ShowBalloonTip(5000);
        }
    }

    void Toggle()
    {
        Theme.SetLight(!Theme.IsLight());
        RefreshIcon();
    }

    void SetAuto(bool on)
    {
        Settings.Auto = on;
        autoItem.Checked = on;
        autoDecision = null;
        if (!on)
        {
            StopLocate();
            RefreshIcon();
            return;
        }

        double lat, lon;
        if (Settings.TryGetLocation(out lat, out lon) && Settings.ManualLocation)
        {
            Evaluate();
        }
        else
        {
            Evaluate();
            Locate(true);
        }
    }

    // Applies the theme the sun calls for, but only when that answer changes,
    // so a manual switch sticks until the next sunrise or sunset.
    void Evaluate()
    {
        double lat, lon;
        if (!Settings.Auto || !Settings.TryGetLocation(out lat, out lon))
        {
            RefreshIcon();
            return;
        }

        DateTime? next;
        bool day = Sun.IsDay(DateTime.Now, lat, lon, out next);
        if (autoDecision != day)
        {
            autoDecision = day;
            if (Theme.IsLight() != day) Theme.SetLight(day);
        }
        RefreshIcon();
    }

    void RefreshIcon()
    {
        bool light = Theme.IsLight();
        Icon next = Icons.Create(!light);
        tray.Icon = next;
        tray.Text = (light ? "Light mode" : "Dark mode")
            + (Settings.Auto ? " (automatic)" : "")
            + " - click to switch";
        if (currentIcon != null)
        {
            currentIcon.Dispose();
        }
        currentIcon = next;
        UpdateStatus(light);
    }

    void UpdateStatus(bool light)
    {
        double lat, lon;
        bool haveLoc = Settings.TryGetLocation(out lat, out lon);
        statusItem.Visible = Settings.Auto;
        if (!Settings.Auto) return;

        if (!haveLoc)
        {
            statusItem.Text = watcher != null ? "Finding your location..." : "Location needed: use Set location...";
            return;
        }

        DateTime? next;
        bool day = Sun.IsDay(DateTime.Now, lat, lon, out next);
        string when = next.HasValue ? next.Value.ToString("t") : null;
        if (!next.HasValue)
        {
            statusItem.Text = day ? "Sun is up all day: staying light" : "Sun stays down today: staying dark";
        }
        else if (light != day)
        {
            statusItem.Text = "Switched by hand: automatic again at " + when;
        }
        else
        {
            statusItem.Text = (day ? "Dark at " : "Light at ") + when + " (sun" + (day ? "set)" : "rise)");
        }
    }

    // Asks Windows for the location. `interactive` means the user just turned
    // automatic mode on, so if location is off we ask them for coordinates.
    void Locate(bool interactive)
    {
        StopLocate();
        watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default);
        watcher.PositionChanged += delegate(object s, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            GeoCoordinate c = e.Position.Location;
            ui.Post(delegate
            {
                if (watcher == null || c.IsUnknown) return;
                StopLocate();
                Settings.SetLocation(c.Latitude, c.Longitude, false);
                Evaluate();
            }, null);
        };
        watcher.StatusChanged += delegate(object s, GeoPositionStatusChangedEventArgs e)
        {
            if (e.Status == GeoPositionStatus.Disabled)
            {
                ui.Post(delegate { LocateFailed(interactive); }, null);
            }
        };

        locateTimeout = new System.Windows.Forms.Timer();
        locateTimeout.Interval = 15000;
        locateTimeout.Tick += delegate { LocateFailed(interactive); };
        locateTimeout.Start();

        watcher.Start(!interactive);
        RefreshIcon();
    }

    void LocateFailed(bool interactive)
    {
        if (watcher == null) return;
        StopLocate();
        double lat, lon;
        bool haveLoc = Settings.TryGetLocation(out lat, out lon);
        if (interactive && !haveLoc)
        {
            AskLocation("Windows location is turned off or unavailable, so Theme Toggle can't work out your sunrise and sunset by itself.");
        }
        RefreshIcon();
    }

    void StopLocate()
    {
        if (locateTimeout != null)
        {
            locateTimeout.Stop();
            locateTimeout.Dispose();
            locateTimeout = null;
        }
        if (watcher != null)
        {
            watcher.Stop();
            watcher.Dispose();
            watcher = null;
        }
    }

    void AskLocation(string message)
    {
        StopLocate();
        double lat, lon;
        bool have = Settings.TryGetLocation(out lat, out lon);
        using (LocationForm form = new LocationForm(message, have, lat, lon))
        {
            if (form.ShowDialog() == DialogResult.OK)
            {
                Settings.SetLocation(form.Latitude, form.Longitude, true);
                if (!Settings.Auto)
                {
                    SetAuto(true);
                    return;
                }
                autoDecision = null;
                Evaluate();
            }
            else if (!have && Settings.Auto)
            {
                SetAuto(false);
            }
        }
    }

    void Exit()
    {
        StopLocate();
        clock.Stop();
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
