namespace GameSync.Forms;

public enum ConflictChoice
{
    Cancel,
    UploadLocal,
    DownloadServer,
}

public sealed class ConflictDialog : Form
{
    public ConflictChoice Choice { get; private set; } = ConflictChoice.Cancel;

    public ConflictDialog(string gameName, long localMtime, long serverMtime)
    {
        Text = "동기화 충돌";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(480, 250);

        var localTime = DateTimeOffset.FromUnixTimeMilliseconds(localMtime).LocalDateTime;
        var serverTime = DateTimeOffset.FromUnixTimeMilliseconds(serverMtime).LocalDateTime;

        var label = new Label
        {
            AutoSize = false,
            Location = new Point(16, 16),
            Size = new Size(440, 110),
            Text =
                $"게임 '{gameName}'의 로컬 데이터와 서버 데이터가 다릅니다.\n\n" +
                $"로컬 최신: {localTime:yyyy-MM-dd HH:mm:ss}\n" +
                $"서버 최신: {serverTime:yyyy-MM-dd HH:mm:ss}\n\n" +
                "어떻게 처리할까요?",
        };

        var btnUpload = new Button
        {
            Text = "로컬 업로드",
            Location = new Point(16, 150),
            Size = new Size(130, 42),
        };
        btnUpload.Click += (_, _) =>
        {
            Choice = ConflictChoice.UploadLocal;
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnDownload = new Button
        {
            Text = "서버 다운로드",
            Location = new Point(160, 150),
            Size = new Size(140, 42),
        };
        btnDownload.Click += (_, _) =>
        {
            Choice = ConflictChoice.DownloadServer;
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnCancel = new Button
        {
            Text = "취소",
            Location = new Point(320, 150),
            Size = new Size(120, 42),
        };
        btnCancel.Click += (_, _) =>
        {
            Choice = ConflictChoice.Cancel;
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.Add(label);
        Controls.Add(btnUpload);
        Controls.Add(btnDownload);
        Controls.Add(btnCancel);
        CancelButton = btnCancel;
    }
}
