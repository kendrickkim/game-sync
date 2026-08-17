const fs = require('fs');
const path = require('path');

const serverRoot = path.join(__dirname, '..', '..');

// Relative values are always anchored to server/, never to the process cwd,
// so PM2 or systemd can launch the app from anywhere.
function resolveFromServerRoot(configured, fallback) {
  const value = String(configured ?? '').trim() || fallback;
  return path.isAbsolute(value) ? value : path.resolve(serverRoot, value);
}

function getUploadDir() {
  return resolveFromServerRoot(process.env.UPLOAD_DIR, 'uploads');
}

// Called before every write: the directory may be removed while the app runs.
function ensureUploadDir() {
  const dir = getUploadDir();
  fs.mkdirSync(dir, { recursive: true });
  return dir;
}

function getDbPath() {
  return resolveFromServerRoot(process.env.SQLITE_PATH, path.join('data', 'game_sync.sqlite'));
}

module.exports = {
  serverRoot,
  resolveFromServerRoot,
  getUploadDir,
  ensureUploadDir,
  getDbPath,
};
