## Game Sync Client

.NET 8 WinForms app that syncs game save directories with the Game Sync server.

### Requirements
- .NET 8 SDK / Desktop runtime
- Running Game Sync server (`../server`)

### Build & Run

```bash
dotnet build
dotnet run
```

### Features
- Account login / register
- Per-game local directory mapping (saved under `%AppData%/GameSync/config.json`)
- Manual upload creates history records; select a record to download
- Computer name taken from `Environment.MachineName`
