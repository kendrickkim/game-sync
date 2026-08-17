# Game Sync Server

Node.js 20+ Express API that stores game save directory zip files and metadata in **SQLite**.

## Setup

1. Copy `.env.example` to `.env` (optional; defaults work for local use).
2. Install and start — DB file is created automatically under `data/game_sync.sqlite`:

```bash
npm install
npm start
```

Development:

```bash
npm run dev
```

## Config

| Variable | Default | Description |
|----------|---------|-------------|
| `PORT` | `3000` | API listen port |
| `JWT_SECRET` | (dev fallback) | JWT signing secret |
| `SQLITE_PATH` | `data/game_sync.sqlite` | SQLite DB file path |
| `UPLOAD_DIR` | `uploads` | Stored zip directory |

## API overview

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/auth/register` | No | Create account |
| POST | `/auth/login` | No | Login, returns JWT |
| GET/POST | `/games` | Yes | List / create games |
| DELETE | `/games/:id` | Yes | Delete game |
| GET/POST | `/computers` | Yes | List / register computer |
| GET | `/sync/list` | Yes | List sync entries (`?gameId=`) |
| POST | `/sync/upload` | Yes | Upload zip (`multipart`: file, gameId, computerName, localPath, contentMtime) |
| GET | `/sync/download/:entryId` | Yes | Download zip |
