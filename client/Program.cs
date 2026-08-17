using GameSync.Forms;
using GameSync.Services;

namespace GameSync;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var config = ConfigStore.Load();

        // Try silent login with saved token by opening main after validating via games list.
        if (!string.IsNullOrWhiteSpace(config.Token) && !string.IsNullOrWhiteSpace(config.ServerUrl))
        {
            var api = new ApiClient(config.ServerUrl);
            api.SetToken(config.Token);
            try
            {
                // Fire-and-forget validation in UI thread with wait
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
