using GameSync.Services;

namespace GameSync.Forms;

public sealed class LoginForm : Form
{
    private readonly TextBox _txtServer;
    private readonly TextBox _txtUsername;
    private readonly TextBox _txtPassword;
    private readonly Button _btnLogin;
    private readonly Button _btnRegister;
    private readonly Label _lblStatus;

    public AppConfig Config { get; private set; }
    public ApiClient? Api { get; private set; }

    public LoginForm(AppConfig config)
    {
        Config = config;

        Text = "Game Sync - 로그인";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 330);
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
            }
            else
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
        }
        catch
        {
            // ignore icon load failures
        }

        const int fieldHeight = 36;
        const int buttonHeight = 40;

        var lblServer = new Label { Text = "서버 URL", Location = new Point(24, 20), AutoSize = true };
        _txtServer = new TextBox
        {
            Location = new Point(24, 44),
            Size = new Size(380, fieldHeight),
            Text = config.ServerUrl,
        };

        var lblUser = new Label { Text = "아이디", Location = new Point(24, 92), AutoSize = true };
        _txtUsername = new TextBox
        {
            Location = new Point(24, 116),
            Size = new Size(380, fieldHeight),
            Text = config.Username ?? "",
        };

        var lblPass = new Label { Text = "비밀번호", Location = new Point(24, 164), AutoSize = true };
        _txtPassword = new TextBox
        {
            Location = new Point(24, 188),
            Size = new Size(380, fieldHeight),
            UseSystemPasswordChar = true,
        };

        _btnLogin = new Button
        {
            Text = "로그인",
            Location = new Point(24, 244),
            Size = new Size(120, buttonHeight),
        };
        _btnLogin.Click += async (_, _) => await AuthenticateAsync(register: false);

        _btnRegister = new Button
        {
            Text = "회원가입",
            Location = new Point(156, 244),
            Size = new Size(120, buttonHeight),
        };
        _btnRegister.Click += async (_, _) => await AuthenticateAsync(register: true);

        _lblStatus = new Label
        {
            Location = new Point(24, 296),
            Size = new Size(380, 24),
            ForeColor = Color.DarkRed,
        };

        AcceptButton = _btnLogin;
        Controls.AddRange(new Control[]
        {
            lblServer, _txtServer,
            lblUser, _txtUsername,
            lblPass, _txtPassword,
            _btnLogin, _btnRegister, _lblStatus,
        });
    }

    private async Task AuthenticateAsync(bool register)
    {
        _lblStatus.Text = "";
        var serverUrl = _txtServer.Text.Trim().TrimEnd('/');
        var username = _txtUsername.Text.Trim();
        var password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(serverUrl) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            _lblStatus.Text = "서버 URL, 아이디, 비밀번호를 입력하세요.";
            return;
        }

        SetBusy(true);
        try
        {
            Api?.Dispose();
            Api = new ApiClient(serverUrl);

            var auth = register
                ? await Api.RegisterAsync(username, password)
                : await Api.LoginAsync(username, password);

            Api.SetToken(auth.Token);

            Config.ServerUrl = serverUrl;
            Config.Token = auth.Token;
            Config.Username = auth.User.Username;
            ConfigStore.Save(Config);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        _btnLogin.Enabled = !busy;
        _btnRegister.Enabled = !busy;
        _txtServer.Enabled = !busy;
        _txtUsername.Enabled = !busy;
        _txtPassword.Enabled = !busy;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (DialogResult != DialogResult.OK)
        {
            Api?.Dispose();
            Api = null;
        }

        base.OnFormClosed(e);
    }
}
