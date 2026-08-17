using GameSync.Models;
using GameSync.Services;

namespace GameSync.Forms;

public sealed class MainForm : Form
{
    private readonly AppConfig _config;
    private readonly ApiClient _api;
    private readonly string _computerName = Environment.MachineName;

    private readonly ComboBox _cmbGames;
    private readonly TextBox _txtLocalPath;
    private readonly ListView _lvEntries;
    private readonly TextBox _txtLog;
    private readonly Label _lblUser;
    private readonly Label _lblComputer;

    private List<GameInfo> _games = new();
    private List<SyncEntry> _entries = new();

    public MainForm(AppConfig config, ApiClient api)
    {
        _config = config;
        _api = api;

        Text = "Game Sync";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(960, 680);
        ClientSize = new Size(980, 700);
        Icon = LoadAppIcon();

        const int controlHeight = 36;
        const int actionHeight = 40;

        _lblUser = new Label
        {
            AutoSize = true,
            Location = new Point(16, 18),
            Text = $"사용자: {config.Username}",
        };

        _lblComputer = new Label
        {
            AutoSize = true,
            Location = new Point(220, 18),
            Text = $"컴퓨터: {_computerName}",
        };

        var btnLogout = new Button
        {
            Text = "로그아웃",
            Location = new Point(852, 12),
            Size = new Size(112, controlHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        btnLogout.Click += (_, _) => Logout();

        var lblGame = new Label { Text = "게임", Location = new Point(16, 58), AutoSize = true };
        _cmbGames = new ComboBox
        {
            Location = new Point(16, 82),
            Size = new Size(280, controlHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.System,
        };
        _cmbGames.SelectedIndexChanged += (_, _) => OnGameSelected();

        var btnAddGame = new Button { Text = "게임 추가", Location = new Point(308, 82), Size = new Size(120, controlHeight) };
        btnAddGame.Click += async (_, _) => await AddGameAsync();

        var btnDeleteGame = new Button { Text = "게임 삭제", Location = new Point(436, 82), Size = new Size(120, controlHeight) };
        btnDeleteGame.Click += async (_, _) => await DeleteGameAsync();

        var btnRefresh = new Button { Text = "새로고침", Location = new Point(564, 82), Size = new Size(120, controlHeight) };
        btnRefresh.Click += async (_, _) => await RefreshAllAsync();

        var lblPath = new Label { Text = "로컬 디렉토리", Location = new Point(16, 132), AutoSize = true };
        _txtLocalPath = new TextBox
        {
            Location = new Point(16, 156),
            Size = new Size(680, controlHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        var btnBrowse = new Button
        {
            Text = "찾아보기",
            Location = new Point(708, 156),
            Size = new Size(112, controlHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        btnBrowse.Click += (_, _) => BrowseFolder();

        var btnSavePath = new Button
        {
            Text = "경로 저장",
            Location = new Point(828, 156),
            Size = new Size(112, controlHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        btnSavePath.Click += (_, _) => SaveCurrentPath();

        var btnUpload = new Button
        {
            Text = "업로드",
            Location = new Point(16, 208),
            Size = new Size(110, actionHeight),
        };
        btnUpload.Click += async (_, _) => await UploadAsync();

        var btnDownload = new Button
        {
            Text = "선택 다운로드",
            Location = new Point(138, 208),
            Size = new Size(160, actionHeight),
        };
        btnDownload.Click += async (_, _) => await DownloadSelectedAsync();

        var btnDeleteEntry = new Button
        {
            Text = "기록 삭제",
            Location = new Point(310, 208),
            Size = new Size(120, actionHeight),
        };
        btnDeleteEntry.Click += async (_, _) => await DeleteSelectedAsync();

        var btnRefreshList = new Button
        {
            Text = "기록 새로고침",
            Location = new Point(442, 208),
            Size = new Size(160, actionHeight),
        };
        btnRefreshList.Click += async (_, _) => await LoadEntriesAsync();

        var lblHistory = new Label
        {
            Text = "업로드 기록 (행을 선택한 뒤 다운로드)",
            Location = new Point(16, 256),
            AutoSize = true,
        };

        _lvEntries = new ListView
        {
            Location = new Point(16, 282),
            Size = new Size(948, 212),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            GridLines = true,
            HideSelection = false,
        };
        _lvEntries.Columns.Add("ID", 60);
        _lvEntries.Columns.Add("업로드 시각", 150);
        _lvEntries.Columns.Add("컴퓨터", 140);
        _lvEntries.Columns.Add("크기", 90);
        _lvEntries.Columns.Add("콘텐츠 mtime", 160);
        _lvEntries.Columns.Add("로컬 경로(업로드 시)", 300);
        _lvEntries.DoubleClick += async (_, _) => await DownloadSelectedAsync();

        var lblLog = new Label
        {
            Text = "로그",
            Location = new Point(16, 508),
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
        };

        _txtLog = new TextBox
        {
            Location = new Point(16, 532),
            Size = new Size(948, 148),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };

        Controls.AddRange(new Control[]
        {
            _lblUser, _lblComputer, btnLogout,
            lblGame, _cmbGames, btnAddGame, btnDeleteGame, btnRefresh,
            lblPath, _txtLocalPath, btnBrowse, btnSavePath,
            btnUpload, btnDownload, btnDeleteEntry, btnRefreshList,
            lblHistory, _lvEntries, lblLog, _txtLog,
        });

        Shown += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _api.RegisterComputerAsync(_computerName);
            await RefreshAllAsync();
            Log($"준비 완료. 컴퓨터 '{_computerName}' 등록됨.");
        }
        catch (Exception ex)
        {
            Log("초기화 실패: " + ex.Message);
            MessageBox.Show(this, ex.Message, "초기화 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RefreshAllAsync()
    {
        try
        {
            var selectedId = SelectedGame?.Id;
            _games = await _api.GetGamesAsync();
            _cmbGames.Items.Clear();
            foreach (var game in _games)
            {
                _cmbGames.Items.Add(game);
            }

            if (_games.Count == 0)
            {
                _txtLocalPath.Text = "";
                _entries = new List<SyncEntry>();
                BindEntries();
                return;
            }

            var index = selectedId is null
                ? 0
                : Math.Max(0, _games.FindIndex(g => g.Id == selectedId));
            _cmbGames.SelectedIndex = index >= 0 ? index : 0;
            await LoadEntriesAsync();
        }
        catch (Exception ex)
        {
            Log("새로고침 실패: " + ex.Message);
        }
    }

    private GameInfo? SelectedGame => _cmbGames.SelectedItem as GameInfo;

    private SyncEntry? SelectedEntry =>
        _lvEntries.SelectedItems.Count == 0
            ? null
            : _lvEntries.SelectedItems[0].Tag as SyncEntry;

    private void OnGameSelected()
    {
        var game = SelectedGame;
        if (game is null)
        {
            _txtLocalPath.Text = "";
            return;
        }

        _txtLocalPath.Text = _config.GameLocalPaths.TryGetValue(game.Id, out var path) ? path : "";
        _ = LoadEntriesAsync();
    }

    private async Task LoadEntriesAsync()
    {
        var game = SelectedGame;
        if (game is null)
        {
            _entries = new List<SyncEntry>();
            BindEntries();
            return;
        }

        try
        {
            _entries = await _api.GetSyncListAsync(game.Id);
            BindEntries();
            Log($"업로드 기록 {_entries.Count}건 로드됨.");
        }
        catch (Exception ex)
        {
            Log("목록 로드 실패: " + ex.Message);
        }
    }

    private void BindEntries()
    {
        _lvEntries.Items.Clear();
        foreach (var entry in _entries)
        {
            var uploadedAt = entry.CreatedAt ?? entry.UpdatedAt;
            var item = new ListViewItem(entry.Id.ToString());
            item.SubItems.Add(uploadedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-");
            item.SubItems.Add(entry.ComputerName);
            item.SubItems.Add(FormatSize(entry.FileSize));
            item.SubItems.Add(FormatMtime(entry.ContentMtime));
            item.SubItems.Add(entry.LocalPath);
            item.Tag = entry;
            if (string.Equals(entry.ComputerName, _computerName, StringComparison.OrdinalIgnoreCase))
            {
                item.BackColor = Color.Honeydew;
            }

            _lvEntries.Items.Add(item);
        }
    }

    private async Task AddGameAsync()
    {
        var name = Prompt("새 게임 이름", "게임 추가");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            var game = await _api.CreateGameAsync(name.Trim());
            Log($"게임 추가: {game.Name}");
            await RefreshAllAsync();
            _cmbGames.SelectedItem = _games.FirstOrDefault(g => g.Id == game.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "게임 추가 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DeleteGameAsync()
    {
        var game = SelectedGame;
        if (game is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"게임 '{game.Name}'과 관련 동기화 데이터를 삭제할까요?",
            "게임 삭제",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await _api.DeleteGameAsync(game.Id);
            _config.GameLocalPaths.Remove(game.Id);
            ConfigStore.Save(_config);
            Log($"게임 삭제: {game.Name}");
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "삭제 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "게임 세이브 디렉토리 선택",
            UseDescriptionForTitle = true,
        };

        if (!string.IsNullOrWhiteSpace(_txtLocalPath.Text) && Directory.Exists(_txtLocalPath.Text))
        {
            dialog.SelectedPath = _txtLocalPath.Text;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _txtLocalPath.Text = dialog.SelectedPath;
            SaveCurrentPath();
        }
    }

    private void SaveCurrentPath()
    {
        var game = SelectedGame;
        if (game is null)
        {
            MessageBox.Show(this, "게임을 먼저 선택하세요.", "경로 저장", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var path = _txtLocalPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            _config.GameLocalPaths.Remove(game.Id);
        }
        else
        {
            _config.GameLocalPaths[game.Id] = path;
        }

        ConfigStore.Save(_config);
        Log($"경로 저장: {game.Name} -> {path}");
    }

    private async Task UploadAsync()
    {
        var game = SelectedGame;
        if (game is null)
        {
            MessageBox.Show(this, "게임을 선택하세요.", "업로드", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var localPath = _txtLocalPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(localPath))
        {
            MessageBox.Show(this, "유효한 로컬 디렉토리를 지정하세요.", "업로드", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SaveCurrentPath();

        try
        {
            var localMtime = ZipHelper.GetDirectoryContentMtime(localPath);
            await DoUploadAsync(game, localPath, localMtime);
        }
        catch (Exception ex)
        {
            Log("업로드 실패: " + ex.Message);
            MessageBox.Show(this, ex.Message, "업로드 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DoUploadAsync(GameInfo game, string localPath, long localMtime)
    {
        Log($"압축 중: {localPath}");
        var zipPath = ZipHelper.CreateZipFromDirectory(localPath);
        try
        {
            Log("업로드 중... (새 기록으로 저장)");
            var entry = await _api.UploadAsync(game.Id, _computerName, localPath, localMtime, zipPath);
            Log($"업로드 완료. 기록 ID={entry.Id}, size={FormatSize(entry.FileSize)}");
            await LoadEntriesAsync();
            SelectEntryById(entry.Id);
        }
        finally
        {
            try { File.Delete(zipPath); } catch { /* ignore */ }
        }
    }

    private async Task DownloadSelectedAsync()
    {
        var game = SelectedGame;
        var entry = SelectedEntry;
        if (game is null)
        {
            MessageBox.Show(this, "게임을 선택하세요.", "다운로드", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (entry is null)
        {
            MessageBox.Show(this, "다운로드할 업로드 기록을 목록에서 선택하세요.", "다운로드", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var localPath = _txtLocalPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            MessageBox.Show(this, "로컬 디렉토리를 지정하세요.", "다운로드", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SaveCurrentPath();

        if (Directory.Exists(localPath) && Directory.EnumerateFileSystemEntries(localPath).Any())
        {
            var confirm = MessageBox.Show(
                this,
                $"선택한 기록(ID={entry.Id}, {entry.ComputerName})을 다운로드하면\n로컬 폴더 내용이 덮어써집니다.\n\n계속할까요?\n{localPath}",
                "다운로드 확인",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                Log("다운로드 취소됨.");
                return;
            }
        }

        try
        {
            await DownloadEntryAsync(entry, localPath);
        }
        catch (Exception ex)
        {
            Log("다운로드 실패: " + ex.Message);
            MessageBox.Show(this, ex.Message, "다운로드 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        var entry = SelectedEntry;
        if (entry is null)
        {
            MessageBox.Show(this, "삭제할 업로드 기록을 선택하세요.", "기록 삭제", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"기록 ID={entry.Id} ({entry.ComputerName})을(를) 삭제할까요?\n서버 zip 파일도 함께 삭제됩니다.",
            "기록 삭제",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await _api.DeleteSyncEntryAsync(entry.Id);
            Log($"기록 삭제 완료. ID={entry.Id}");
            await LoadEntriesAsync();
        }
        catch (Exception ex)
        {
            Log("기록 삭제 실패: " + ex.Message);
            MessageBox.Show(this, ex.Message, "삭제 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DownloadEntryAsync(SyncEntry entry, string localPath)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), $"gamesync-dl-{Guid.NewGuid():N}.zip");
        try
        {
            Log($"다운로드 중: 기록 ID={entry.Id} ({entry.ComputerName})");
            await _api.DownloadToFileAsync(entry.Id, zipPath);
            Log($"압축 해제 중: {localPath}");
            ZipHelper.ExtractZipToDirectory(zipPath, localPath, clearExisting: true);
            Log("다운로드 완료.");
        }
        finally
        {
            try { File.Delete(zipPath); } catch { /* ignore */ }
        }
    }

    private void SelectEntryById(int entryId)
    {
        foreach (ListViewItem item in _lvEntries.Items)
        {
            if (item.Tag is SyncEntry entry && entry.Id == entryId)
            {
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
                break;
            }
        }
    }

    private void Logout()
    {
        _config.Token = null;
        ConfigStore.Save(_config);
        Hide();

        using var login = new LoginForm(_config);
        if (login.ShowDialog() == DialogResult.OK && login.Api is not null)
        {
            var next = new MainForm(login.Config, login.Api);
            next.FormClosed += (_, _) => Close();
            next.Show();
            return;
        }

        Close();
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        if (_txtLog.InvokeRequired)
        {
            _txtLog.Invoke(() => _txtLog.AppendText(line));
        }
        else
        {
            _txtLog.AppendText(line);
        }
    }

    private static string Prompt(string text, string caption)
    {
        using var form = new Form
        {
            Width = 440,
            Height = 190,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = caption,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
        };

        var label = new Label { Left = 16, Top = 16, Text = text, AutoSize = true };
        var input = new TextBox { Left = 16, Top = 48, Width = 380, Height = 36 };
        var ok = new Button { Text = "확인", Left = 200, Width = 90, Height = 36, Top = 100, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "취소", Left = 306, Width = 90, Height = 36, Top = 100, DialogResult = DialogResult.Cancel };
        form.Controls.AddRange(new Control[] { label, input, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK ? input.Text : "";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024.0):0.##} MB";
    }

    private static string FormatMtime(long ms)
    {
        if (ms <= 0) return "-";
        return DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static Icon? LoadAppIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(path))
            {
                return new Icon(path);
            }

            // Fallback: embedded beside exe via ApplicationIcon is already on the process;
            // use executable icon when Assets copy is missing.
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            return null;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _api.Dispose();
        base.OnFormClosed(e);
    }
}
