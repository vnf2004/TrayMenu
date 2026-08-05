namespace TrayMenu;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _rebuildTimer;
    private readonly SynchronizationContext _ui;
    private AppConfig _config;
    private FileSystemWatcher? _watcher;
    private Icon _trayIcon;

    public TrayApplicationContext()
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _config = ConfigStore.Load();
        if (_config.Autostart != Autostart.IsEnabled())
        {
            // Keep registry in sync with saved preference on startup
            try
            {
                Autostart.SetEnabled(_config.Autostart);
            }
            catch
            {
                // ignore registry issues at startup
            }
        }

        _trayIcon = CreateTrayIcon();
        _menu = new ContextMenuStrip();
        _menu.Opening += (_, _) => RebuildMenu();

        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "TrayMenu",
            Visible = true,
            ContextMenuStrip = _menu
        };
        _notifyIcon.MouseUp += OnNotifyIconMouseUp;

        _rebuildTimer = new System.Windows.Forms.Timer { Interval = 400 };
        _rebuildTimer.Tick += (_, _) =>
        {
            _rebuildTimer.Stop();
            RebuildMenu();
        };

        RebuildMenu();
        AttachWatcher();
    }

    private void OnNotifyIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        // Left click also opens the menu (right click uses ContextMenuStrip).
        _menu.Show(Cursor.Position);
    }

    private void RebuildMenu()
    {
        DisposeMenuImages(_menu.Items);
        ShortcutMenuBuilder.Populate(_menu.Items, _config.ShortcutsFolder);

        _menu.Items.Add(new ToolStripSeparator());

        var settingsItem = new ToolStripMenuItem("Настройки…");
        settingsItem.Click += (_, _) => OpenSettings();
        _menu.Items.Add(settingsItem);

        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (_, _) => Exit();
        _menu.Items.Add(exitItem);
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_config);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _config = ConfigStore.Load();
        AttachWatcher();
        RebuildMenu();
    }

    private void AttachWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;

        var folder = _config.ShortcutsFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            _watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.CreationTime
            };
            _watcher.Changed += (_, _) => ScheduleRebuild();
            _watcher.Created += (_, _) => ScheduleRebuild();
            _watcher.Deleted += (_, _) => ScheduleRebuild();
            _watcher.Renamed += (_, _) => ScheduleRebuild();
            _watcher.EnableRaisingEvents = true;
        }
        catch
        {
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    private void ScheduleRebuild()
    {
        _ui.Post(_ =>
        {
            if (_rebuildTimer.Enabled)
            {
                _rebuildTimer.Stop();
            }

            _rebuildTimer.Start();
        }, null);
    }

    private void Exit()
    {
        _notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rebuildTimer.Dispose();
            _watcher?.Dispose();
            DisposeMenuImages(_menu.Items);
            _menu.Dispose();
            _notifyIcon.Dispose();
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void DisposeMenuImages(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            if (item is ToolStripMenuItem menuItem)
            {
                DisposeMenuImages(menuItem.DropDownItems);
            }

            item.Image?.Dispose();
            item.Image = null;
        }
    }

    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            using (var brush = new SolidBrush(Color.FromArgb(30, 90, 160)))
            {
                g.FillEllipse(brush, 1, 1, 14, 14);
            }

            using var pen = new Pen(Color.White, 1.5f);
            g.DrawLine(pen, 5, 8, 8, 11);
            g.DrawLine(pen, 8, 11, 12, 5);
        }

        var hIcon = bitmap.GetHicon();
        using var temp = Icon.FromHandle(hIcon);
        var icon = (Icon)temp.Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}
