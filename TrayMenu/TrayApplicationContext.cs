namespace TrayMenu;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly NoTaskbarContextMenuStrip _appsMenu;
    private readonly NoTaskbarContextMenuStrip _systemMenu;
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

        _appsMenu = new NoTaskbarContextMenuStrip();
        _appsMenu.Opening += (_, _) => RebuildAppsMenu();

        _systemMenu = new NoTaskbarContextMenuStrip();
        BuildSystemMenu();

        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "TrayMenu",
            Visible = true,
            // Right click → system menu only
            ContextMenuStrip = _systemMenu
        };
        _notifyIcon.MouseUp += OnNotifyIconMouseUp;

        _rebuildTimer = new System.Windows.Forms.Timer { Interval = 400 };
        _rebuildTimer.Tick += (_, _) =>
        {
            _rebuildTimer.Stop();
            RebuildAppsMenu();
        };

        RebuildAppsMenu();
        AttachWatcher();
    }

    private void OnNotifyIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        // Left click → programs only
        RebuildAppsMenu();
        _appsMenu.Show(Cursor.Position);
    }

    private void RebuildAppsMenu()
    {
        DisposeMenuImages(_appsMenu.Items);
        ShortcutMenuBuilder.Populate(_appsMenu.Items, _config.ShortcutsFolder);
    }

    private void BuildSystemMenu()
    {
        _systemMenu.Items.Clear();

        var settingsItem = new ToolStripMenuItem("Настройки…");
        settingsItem.Click += (_, _) => OpenSettings();
        _systemMenu.Items.Add(settingsItem);

        var editMenuItem = new ToolStripMenuItem("Редактировать меню…");
        editMenuItem.Click += (_, _) => OpenMenuEditor();
        _systemMenu.Items.Add(editMenuItem);

        _systemMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (_, _) => Exit();
        _systemMenu.Items.Add(exitItem);
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_config);
        form.ShowDialog();
        _config = ConfigStore.Load();
        AttachWatcher();
        RebuildAppsMenu();
    }

    private void OpenMenuEditor()
    {
        var folder = _config.ShortcutsFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            MessageBox.Show(
                "Сначала укажите папку с ярлыками в настройках.",
                "TrayMenu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            OpenSettings();
            return;
        }

        using var editor = new MenuEditorForm(folder);
        editor.ShowDialog();
        RebuildAppsMenu();
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
            DisposeMenuImages(_appsMenu.Items);
            _appsMenu.Dispose();
            _systemMenu.Dispose();
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
