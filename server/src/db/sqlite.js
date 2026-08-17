const fs = require('fs');
const path = require('path');
const Database = require('better-sqlite3');

const serverRoot = path.join(__dirname, '..', '..');
const configuredPath = process.env.SQLITE_PATH || path.join('data', 'game_sync.sqlite');
const dbPath = path.isAbsolute(configuredPath)
  ? configuredPath
  : path.resolve(serverRoot, configuredPath);

fs.mkdirSync(path.dirname(dbPath), { recursive: true });

const db = new Database(dbPath);
db.pragma('journal_mode = WAL');
db.pragma('foreign_keys = ON');

db.exec(`
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
`);

migrateRemoveSyncUniqueConstraint();
migrateComputerLastSeen();

function migrateComputerLastSeen() {
  const columns = db.prepare(`PRAGMA table_info(computers)`).all();
  if (!columns.some((column) => column.name === 'last_seen_at')) {
    db.exec(`ALTER TABLE computers ADD COLUMN last_seen_at TEXT`);
  }
}

function migrateRemoveSyncUniqueConstraint() {
  const row = db
    .prepare(`SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'sync_entries'`)
    .get();
  if (!row?.sql) {
    return;
  }

  const sql = String(row.sql);
  if (!/UNIQUE\s*\(\s*user_id\s*,\s*game_id\s*,\s*computer_id\s*\)/i.test(sql)) {
    return;
  }

  const migrate = db.transaction(() => {
    db.exec(`
      CREATE TABLE sync_entries_history (
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

      INSERT INTO sync_entries_history (
        id, user_id, game_id, computer_id, local_path, zip_filename,
        content_mtime, file_size, updated_at, created_at
      )
      SELECT
        id, user_id, game_id, computer_id, local_path, zip_filename,
        content_mtime, file_size, updated_at, created_at
      FROM sync_entries;

      DROP TABLE sync_entries;
      ALTER TABLE sync_entries_history RENAME TO sync_entries;

      CREATE INDEX IF NOT EXISTS idx_sync_entries_game_created
        ON sync_entries (user_id, game_id, created_at DESC);
    `);
  });

  migrate();
  console.log('Migrated sync_entries: unique constraint removed (upload history enabled)');
}

function isUniqueViolation(err) {
  return Boolean(err && (err.code === 'SQLITE_CONSTRAINT_UNIQUE' || String(err.message || '').includes('UNIQUE')));
}

module.exports = {
  db,
  dbPath,
  isUniqueViolation,
};
