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
    private readonly TextBox _txtExcludes;
    private readonly ListView _lvEntries;
    private readonly TextBox _txtLog;
    private readonly Label _lblUser;
    private readonly Label _lblComputer;
    private readonly ComboBox _cmbRemoteComputer;
    private readonly System.Windows.Forms.Timer _remoteCommandTimer;
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;

    private List<GameInfo> _games = new();
    private List<SyncEntry> _entries = new();
    private List<ComputerInfo> _computers = new();
    private bool _checkingRemoteCommands;
    private bool _exitRequested;

    public MainForm(AppConfig config, ApiClient api)
    {
        _config = config;
        _api = api;

        Text = "Game Sync";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(960, 800);
        ClientSize = new Size(980, 776);
        Icon = LoadAppIcon();

        const int controlHeight = 36;
        const int actionHeight = 40;

        _lblUser = new Label
        {
            AutoSize = true,
            UseCompatibleTextRendering = true,
            Location = new Point(16, 16),
            Text = $"사용자: {config.Username}",
        };

        _lblComputer = new Label
        {
            AutoSize = true,
            UseCompatibleTextRendering = true,
            Location = new Point(220, 16),
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

        var lblGame = new Label { Text = "게임", Location = new Point(16, 54), AutoSize = true, UseCompatibleTextRendering = true };
        _cmbGames = new ComboBox
        {
            Location = new Point(16, 92),
            Size = new Size(280, controlHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.System,
        };
        _cmbGames.SelectedIndexChanged += (_, _) => OnGameSelected();

        var btnAddGame = new Button { Text = "게임 추가", Location = new Point(308, 92), Size = new Size(120, controlHeight) };
        btnAddGame.Click += async (_, _) => await AddGameAsync();

        var btnDeleteGame = new Button { Text = "게임 삭제", Location = new Point(436, 92), Size = new Size(120, controlHeight) };
        btnDeleteGame.Click += async (_, _) => await DeleteGameAsync();

        var btnRefresh = new Button { Text = "새로고침", Location = new Point(564, 92), Size = new Size(120, controlHeight) };
        btnRefresh.Click += async (_, _) => await RefreshAllAsync();

        var lblPath = new Label { Text = "로컬 디렉토리", Location = new Point(16, 136), AutoSize = true, UseCompatibleTextRendering = true };
        _txtLocalPath = new TextBox
        {
            Location = new Point(16, 176),
            Size = new Size(680, controlHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        var btnBrowse = new Button
        {
            Text = "찾아보기",
            Location = new Point(708, 176),
            Size = new Size(112, controlHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        btnBrowse.Click += (_, _) => BrowseFolder();

        var btnSavePath = new Button
        {
            Text = "경로 저장",
            Location = new Point(828, 176),
            Size = new Size(112, controlHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        btnSavePath.Click += (_, _) => SaveCurrentPath();

        var lblExcludes = new Label { Text = "백업 제외", Location = new Point(16, 228), AutoSize = true, UseCompatibleTextRendering = true };
        _txtExcludes = new TextBox
        {
            Location = new Point(16, 266),
            Size = new Size(800, controlHeight),
            ReadOnly = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        var btnExcludes = new Button
        {
            Text = "제외 설정",
            Location = new Point(828, 266),
            Size = new Size(112, controlHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        btnExcludes.Click += (_, _) => EditExcludes();

        var btnUpload = new Button
        {
            Text = "업로드",
            Location = new Point(16, 312),
            Size = new Size(110, actionHeight),
        };
        btnUpload.Click += async (_, _) => await UploadAsync();

        var btnDownload = new Button
        {
            Text = "선택 다운로드",
            Location = new Point(138, 312),
            Size = new Size(160, actionHeight),
        };
        btnDownload.Click += async (_, _) => await DownloadSelectedAsync();

        var btnDeleteEntry = new Button
        {
            Text = "기록 삭제",
            Location = new Point(310, 312),
            Size = new Size(120, actionHeight),
        };
        btnDeleteEntry.Click += async (_, _) => await DeleteSelectedAsync();

        var btnRefreshList = new Button
        {
            Text = "기록 새로고침",
            Location = new Point(442, 312),
            Size = new Size(160, actionHeight),
        };
        btnRefreshList.Click += async (_, _) => await LoadEntriesAsync();

        var lblRemoteComputer = new Label
        {
            Text = "원격 업로드 대상",
            Location = new Point(16, 368),
            AutoSize = true,
            UseCompatibleTextRendering = true,
        };

        _cmbRemoteComputer = new ComboBox
        {
            Location = new Point(16, 408),
            Size = new Size(380, controlHeight),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };

        var btnRemoteUpload = new Button
        {
            Text = "원격 업로드 요청",
            Location = new Point(412, 396),
            Size = new Size(200, actionHeight),
        };
        btnRemoteUpload.Click += async (_, _) => await RequestRemoteUploadAsync();

        var lblHistory = new Label
        {
            Text = "업로드 기록 (행을 선택한 뒤 다운로드)",
            Location = new Point(16, 454),
            AutoSize = true,
            UseCompatibleTextRendering = true,
        };

        _lvEntries = new ListView
        {
            Location = new Point(16, 494),
            Size = new Size(948, 112),
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
            Location = new Point(16, 620),
            AutoSize = true,
            UseCompatibleTextRendering = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
        };

        _txtLog = new TextBox
        {
            Location = new Point(16, 658),
            Size = new Size(948, 96),
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
            lblExcludes, _txtExcludes, btnExcludes,
            btnUpload, btnDownload, btnDeleteEntry, btnRefreshList,
            lblRemoteComputer, _cmbRemoteComputer, btnRemoteUpload,
            lblHistory, _lvEntries, lblLog, _txtLog,
        });

        _remoteCommandTimer = new System.Windows.Forms.Timer
        {
            Interval = 10_000,
        };
        _remoteCommandTimer.Tick += async (_, _) => await CheckRemoteCommandsAsync();

        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("열기", null, (_, _) => RestoreFromTray());
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("종료", null, (_, _) => ExitApplication());

        _trayIcon = new NotifyIcon
        {
            Text = "Game Sync",
            Icon = Icon ?? SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = _trayMenu,
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        FormClosing += MainForm_FormClosing;
        Resize += MainForm_Resize;

        Shown += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _api.RegisterComputerAsync(_computerName);
            await RefreshAllAsync();
            _remoteCommandTimer.Start();
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
            await LoadComputersAsync();
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

    private async Task LoadComputersAsync()
    {
        var selectedId = (_cmbRemoteComputer.SelectedItem as ComputerInfo)?.Id;
        _computers = await _api.GetComputersAsync();

        _cmbRemoteComputer.Items.Clear();
        foreach (var computer in _computers.Where(c =>
                     !string.Equals(c.Name, _computerName, StringComparison.OrdinalIgnoreCase)))
        {
            _cmbRemoteComputer.Items.Add(computer);
        }

        if (_cmbRemoteComputer.Items.Count == 0)
        {
            return;
        }

        var selected = _computers.FirstOrDefault(c => c.Id == selectedId);
        _cmbRemoteComputer.SelectedItem = selected is not null &&
                                          !string.Equals(selected.Name, _computerName, StringComparison.OrdinalIgnoreCase)
            ? selected
            : _cmbRemoteComputer.Items[0];
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
            _txtExcludes.Text = "";
            return;
        }

        var settingPath = _config.GetGamePath(game.Id);
        _txtLocalPath.Text = settingPath;
        _txtExcludes.Text = BackupExclude.Summarize(_config.GetGameExcludes(game.Id));
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
            _config.RemoveGameSetting(game.Id);
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
            var previous = SelectedGame is null ? "" : _config.GetGamePath(SelectedGame.Id);
            _txtLocalPath.Text = dialog.SelectedPath;
            if (!string.Equals(previous, dialog.SelectedPath, StringComparison.OrdinalIgnoreCase) &&
                SelectedGame is not null)
            {
                var setting = _config.GetOrCreateGameSetting(SelectedGame.Id);
                setting.ExcludeRelativePaths = setting.ExcludeRelativePaths
                    .Where(item => File.Exists(Path.Combine(dialog.SelectedPath, item)) ||
                                   Directory.Exists(Path.Combine(dialog.SelectedPath, item)))
                    .ToList();
            }

            SaveCurrentPath();
            EditExcludes();
        }
    }

    private void EditExcludes()
    {
        var game = SelectedGame;
        if (game is null)
        {
            MessageBox.Show(this, "게임을 먼저 선택하세요.", "제외 설정", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var path = _txtLocalPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            MessageBox.Show(this, "유효한 로컬 디렉토리를 먼저 지정하세요.", "제외 설정", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SaveCurrentPath();
        var setting = _config.GetOrCreateGameSetting(game.Id);
        using var dialog = new ExcludeItemsDialog(path, setting.ExcludeRelativePaths);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _config.SetGameExcludes(game.Id, dialog.ExcludeRelativePaths);
        ConfigStore.Save(_config);
        _txtExcludes.Text = BackupExclude.Summarize(_config.GetGameExcludes(game.Id));
        var count = _config.GetGameExcludes(game.Id).Count;
        Log($"제외 항목 저장: {game.Name} ({count}개)");
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
            _config.RemoveGameSetting(game.Id);
            _txtExcludes.Text = "";
        }
        else
        {
            _config.SetGamePath(game.Id, path);
            _txtExcludes.Text = BackupExclude.Summarize(_config.GetGameExcludes(game.Id));
        }

        ConfigStore.Save(_config);
        Log($"경로 저장: {game.Name} -> {path}");
    }

    private async Task RequestRemoteUploadAsync()
    {
        var game = SelectedGame;
        var target = _cmbRemoteComputer.SelectedItem as ComputerInfo;
        if (game is null || target is null)
        {
            MessageBox.Show(
                this,
                "게임과 원격 컴퓨터를 선택하세요.",
                "원격 업로드",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var onlineWarning = target.IsOnline
            ? ""
            : "\n\n현재 오프라인입니다. 클라이언트가 실행되면 요청을 처리합니다.";
        var confirm = MessageBox.Show(
            this,
            $"'{target.Name}' 컴퓨터에 '{game.Name}' 업로드를 요청할까요?{onlineWarning}",
            "원격 업로드 요청",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var request = await _api.CreateRemoteUploadRequestAsync(
                game.Id,
                target.Id,
                _computerName);
            Log($"원격 업로드 요청 완료. 요청 ID={request.Id}, 대상={target.Name}, 게임={game.Name}");
        }
        catch (Exception ex)
        {
            Log("원격 업로드 요청 실패: " + ex.Message);
            MessageBox.Show(this, ex.Message, "원격 업로드 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CheckRemoteCommandsAsync()
    {
        if (_checkingRemoteCommands || IsDisposed)
        {
            return;
        }

        _checkingRemoteCommands = true;
        try
        {
            await _api.SendHeartbeatAsync(_computerName);
            var requests = await _api.GetPendingRemoteUploadsAsync(_computerName);
            foreach (var request in requests)
            {
                await ExecuteRemoteUploadAsync(request);
            }
        }
        catch (Exception ex)
        {
            Log("원격 명령 확인 실패: " + ex.Message);
        }
        finally
        {
            _checkingRemoteCommands = false;
        }
    }

    private async Task ExecuteRemoteUploadAsync(RemoteUploadRequest request)
    {
        try
        {
            await _api.ClaimRemoteUploadAsync(request.Id, _computerName);
        }
        catch
        {
            return;
        }

        Log(
            $"원격 업로드 요청 수신. 요청 ID={request.Id}, " +
            $"요청 PC={request.RequesterComputerName}, 게임={request.GameName}");

        try
        {
            var localPath = _config.GetGamePath(request.GameId);
            if (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(localPath))
            {
                throw new DirectoryNotFoundException(
                    $"'{request.GameName}'의 로컬 디렉토리가 이 컴퓨터에 설정되지 않았습니다.");
            }

            var game = _games.FirstOrDefault(g => g.Id == request.GameId)
                       ?? new GameInfo { Id = request.GameId, Name = request.GameName };
            var excludes = _config.GetGameExcludes(request.GameId);
            var localMtime = ZipHelper.GetDirectoryContentMtime(localPath, excludes);
            var entry = await DoUploadAsync(game, localPath, localMtime, excludes);
            await _api.CompleteRemoteUploadAsync(request.Id, _computerName, entry.Id);
            Log($"원격 업로드 완료. 요청 ID={request.Id}, 기록 ID={entry.Id}");
        }
        catch (Exception ex)
        {
            try
            {
                await _api.FailRemoteUploadAsync(request.Id, _computerName, ex.Message);
            }
            catch (Exception reportEx)
            {
                Log("원격 업로드 실패 상태 전송 실패: " + reportEx.Message);
            }

            Log($"원격 업로드 실패. 요청 ID={request.Id}: {ex.Message}");
        }
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
            var excludes = _config.GetGameExcludes(game.Id);
            var localMtime = ZipHelper.GetDirectoryContentMtime(localPath, excludes);
            await DoUploadAsync(game, localPath, localMtime, excludes);
        }
        catch (Exception ex)
        {
            Log("업로드 실패: " + ex.Message);
            MessageBox.Show(this, ex.Message, "업로드 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task<SyncEntry> DoUploadAsync(
        GameInfo game,
        string localPath,
        long localMtime,
        IReadOnlyCollection<string>? excludes = null)
    {
        var excludeCount = excludes?.Count ?? 0;
        Log(excludeCount > 0
            ? $"압축 중: {localPath} (제외 {excludeCount}개)"
            : $"압축 중: {localPath}");
        var zipPath = ZipHelper.CreateZipFromDirectory(localPath, excludes);
        try
        {
            Log("업로드 중... (새 기록으로 저장)");
            var entry = await _api.UploadAsync(game.Id, _computerName, localPath, localMtime, zipPath);
            Log($"업로드 완료. 기록 ID={entry.Id}, size={FormatSize(entry.FileSize)}");
            await LoadEntriesAsync();
            SelectEntryById(entry.Id);
            return entry;
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
                $"선택한 기록(ID={entry.Id}, {entry.ComputerName})을 다운로드하면\n로컬 폴더 내용이 덮어써집니다.\n백업 제외로 지정한 파일/폴더는 유지됩니다.\n\n계속할까요?\n{localPath}",
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
            var excludes = SelectedGame is null ? null : _config.GetGameExcludes(SelectedGame.Id);
            ZipHelper.ExtractZipToDirectory(zipPath, localPath, clearExisting: true, excludes);
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

        // Prevent tray hide while swapping session windows.
        _exitRequested = true;
        _trayIcon.Visible = false;
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

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
        }
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        if (WindowState == FormWindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        if (_trayIcon.Visible)
        {
            _trayIcon.ShowBalloonTip(
                2000,
                "Game Sync",
                "백그라운드에서 계속 실행 중입니다. 트레이 아이콘에서 열거나 종료할 수 있습니다.",
                ToolTipIcon.Info);
        }
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        _trayIcon.Visible = false;
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

        var label = new Label
        {
            Left = 16,
            Top = 16,
            Text = text,
            AutoSize = true,
            UseCompatibleTextRendering = true,
        };
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

    private static Icon? LoadAppIcon() => AppIcon.Value;

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _remoteCommandTimer.Stop();
        _remoteCommandTimer.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayMenu.Dispose();
        _api.Dispose();
        base.OnFormClosed(e);
    }
}
