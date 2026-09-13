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
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

internal static class Native
{
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint SMTO_ABORTIFHUNG = 0x0002;
    public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
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
            Native.WM_SETTINGCHANGE,
            IntPtr.Zero,
            "ImmersiveColorSet",
            Native.SMTO_ABORTIFHUNG,
            1000,
            out result);
    }
}

internal static class Icons
{
    public static Icon Create(bool darkMode)
    {
        using (Bitmap bmp = new Bitmap(32, 32))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            if (darkMode)
            {
                using (SolidBrush gold = new SolidBrush(Color.FromArgb(255, 220, 80)))
                using (SolidBrush cut = new SolidBrush(Color.FromArgb(40, 40, 40)))
                {
                    g.FillEllipse(gold, 5, 5, 22, 22);
                    g.FillEllipse(cut, 13, 1, 22, 22);
                }
            }
            else
            {
                using (SolidBrush gold = new SolidBrush(Color.FromArgb(255, 200, 40)))
                using (Pen ray = new Pen(Color.FromArgb(255, 170, 20), 2.4f))
                {
                    g.FillEllipse(gold, 9, 9, 14, 14);
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * 45.0 * Math.PI / 180.0;
                        int x1 = 16 + (int)(Math.Cos(angle) * 14);
                        int y1 = 16 + (int)(Math.Sin(angle) * 14);
                        int x2 = 16 + (int)(Math.Cos(angle) * 10);
                        int y2 = 16 + (int)(Math.Sin(angle) * 10);
                        g.DrawLine(ray, x1, y1, x2, y2);
                    }
                }
            }

            IntPtr handle = bmp.GetHicon();
            return Icon.FromHandle(handle);
        }
    }
}

internal sealed class TrayApp : ApplicationContext
{
    const string SettingsPath = @"SOFTWARE\awabkb\ThemeToggle";
    readonly NotifyIcon tray;
    Icon currentIcon;

    public TrayApp()
    {
        tray = new NotifyIcon();
        tray.Visible = true;
        RefreshIcon();

        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Toggle dark / light mode", null, delegate { Toggle(); });
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

        if (ShouldShowTip())
        {
            tray.BalloonTipTitle = "Theme Toggle";
            tray.BalloonTipText = "Click this sun/moon icon to switch dark and light mode.";
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

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp());
        }
    }
}
