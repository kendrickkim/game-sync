-- Reference schema (also auto-created on server start via better-sqlite3)
-- Each upload creates a new sync_entries row (history). Download by selecting an entry id.

CREATE TABLE IF NOT EXISTS users (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  username TEXT NOT NULL UNIQUE,
  password_hash TEXT NOT NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS games (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER NOT NULL,
  name TEXT NOT NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  UNIQUE (user_id, name),
  FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS computers (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER NOT NULL,
  name TEXT NOT NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  last_seen_at TEXT,
  UNIQUE (user_id, name),
  FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS sync_entries (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER NOT NULL,
  game_id INTEGER NOT NULL,
  computer_id INTEGER NOT NULL,
  local_path TEXT NOT NULL,
  zip_filename TEXT NOT NULL,
  content_mtime INTEGER NOT NULL,
  file_size INTEGER NOT NULL DEFAULT 0,
  updated_at TEXT NOT NULL DEFAULT (datetime('now')),
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
  FOREIGN KEY (game_id) REFERENCES games (id) ON DELETE CASCADE,
  FOREIGN KEY (computer_id) REFERENCES computers (id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_sync_entries_game_created
  ON sync_entries (user_id, game_id, created_at DESC);

CREATE TABLE IF NOT EXISTS remote_upload_requests (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER NOT NULL,
  requester_computer_id INTEGER NOT NULL,
  target_computer_id INTEGER NOT NULL,
  game_id INTEGER NOT NULL,
  status TEXT NOT NULL DEFAULT 'pending'
    CHECK (status IN ('pending', 'processing', 'completed', 'failed')),
  sync_entry_id INTEGER,
  message TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  started_at TEXT,
  completed_at TEXT,
  FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
  FOREIGN KEY (requester_computer_id) REFERENCES computers (id) ON DELETE CASCADE,
  FOREIGN KEY (target_computer_id) REFERENCES computers (id) ON DELETE CASCADE,
  FOREIGN KEY (game_id) REFERENCES games (id) ON DELETE CASCADE,
  FOREIGN KEY (sync_entry_id) REFERENCES sync_entries (id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_remote_upload_pending
  ON remote_upload_requests (user_id, target_computer_id, status, created_at);
