using System.Threading;
using GameSync.Forms;
using GameSync.Services;

namespace GameSync;

static class Program
{
    private const string SingleInstanceMutexName = "Local\\GameSync.WinForms.SingleInstance";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Game Sync가 이미 실행 중입니다.",
                "Game Sync",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        var config = ConfigStore.Load();

        // Try silent login with saved token by opening main after validating via games list.
        if (!string.IsNullOrWhiteSpace(config.Token) && !string.IsNullOrWhiteSpace(config.ServerUrl))
        {
            var api = new ApiClient(config.ServerUrl);
            api.SetToken(config.Token);
            try
            {
                var task = api.GetGamesAsync();
                task.GetAwaiter().GetResult();
                Application.Run(new MainForm(config, api));
                return;
            }
            catch
            {
                api.Dispose();
                config.Token = null;
                ConfigStore.Save(config);
            }
        }

        using var login = new LoginForm(config);
        if (login.ShowDialog() != DialogResult.OK || login.Api is null)
        {
            return;
        }

        Application.Run(new MainForm(login.Config, login.Api));
    }
}
