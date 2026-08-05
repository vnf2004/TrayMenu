namespace TrayMenu;

public sealed class SettingsForm : Form
{
    private readonly TextBox _folderBox = new();
    private readonly CheckBox _autostartCheck = new();
    private readonly AppConfig _config;

    public SettingsForm(AppConfig config)
    {
        _config = config;

        Text = "Настройки TrayMenu";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 150);
        ShowInTaskbar = true;

        var folderLabel = new Label
        {
            Text = "Папка с ярлыками:",
            AutoSize = true,
            Location = new Point(12, 16)
        };

        _folderBox.Location = new Point(12, 40);
        _folderBox.Width = 340;
        _folderBox.Text = config.ShortcutsFolder;

        var browseButton = new Button
        {
            Text = "Обзор…",
            Location = new Point(360, 38),
            Width = 80
        };
        browseButton.Click += (_, _) => BrowseFolder();

        _autostartCheck.Text = "Запускать с Windows";
        _autostartCheck.AutoSize = true;
        _autostartCheck.Location = new Point(12, 78);
        _autostartCheck.Checked = config.Autostart;

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(280, 110),
            Width = 75
        };
        okButton.Click += (_, _) => Apply();

        var cancelButton = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            Location = new Point(365, 110),
            Width = 75
        };

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(folderLabel);
        Controls.Add(_folderBox);
        Controls.Add(browseButton);
        Controls.Add(_autostartCheck);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Выберите папку с ярлыками",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_folderBox.Text) ? _folderBox.Text : string.Empty
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _folderBox.Text = dialog.SelectedPath;
        }
    }

    private void Apply()
    {
        _config.ShortcutsFolder = _folderBox.Text.Trim();
        _config.Autostart = _autostartCheck.Checked;

        try
        {
            Autostart.SetEnabled(_config.Autostart);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Не удалось изменить автозапуск:\n{ex.Message}",
                "TrayMenu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        ConfigStore.Save(_config);
    }
}
