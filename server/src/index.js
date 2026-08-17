const path = require('path');
const dotenv = require('dotenv');

dotenv.config({ path: path.join(__dirname, '..', '.env') });

const express = require('express');
const cors = require('cors');
const { db, dbPath } = require('./db/sqlite');
const { serverRoot, getUploadDir, ensureUploadDir } = require('./config/paths');

const authRoutes = require('./routes/auth');
const gamesRoutes = require('./routes/games');
const computersRoutes = require('./routes/computers');
const syncRoutes = require('./routes/sync');
const remoteUploadRoutes = require('./routes/remoteUploads');

if (!process.env.JWT_SECRET) {
  console.warn('Warning: JWT_SECRET is not set. Using insecure default for development only.');
  process.env.JWT_SECRET = 'dev-insecure-secret-change-me';
}

const uploadDir = ensureUploadDir();

const app = express();
app.use(cors());
app.use(express.json({ limit: '2mb' }));

app.get('/health', (_req, res) => {
  res.json({
    ok: true,
    db: 'sqlite',
    dbPath,
    uploadDir: getUploadDir(),
    serverRoot,
    cwd: process.cwd(),
  });
});

app.use('/auth', authRoutes);
app.use('/games', gamesRoutes);
app.use('/computers', computersRoutes);
app.use('/sync', syncRoutes);
app.use('/remote-uploads', remoteUploadRoutes);

app.use((err, _req, res, _next) => {
  console.error('unhandled error', err);
  res.status(500).json({ error: err.message || 'Internal server error' });
});

const port = Number(process.env.PORT || 3000);
const server = app.listen(port, () => {
  console.log(`Game Sync server listening on http://localhost:${port}`);
  console.log(`PID:     ${process.pid}`);
  console.log(`SQLite:  ${dbPath}`);
  console.log(`Uploads: ${uploadDir}`);
  console.log(`Cwd:     ${process.cwd()}`);
});

let shuttingDown = false;

function shutdown(reason) {
  if (shuttingDown) {
    return;
  }
  shuttingDown = true;

  const uptime = process.uptime().toFixed(1);
  console.log(`Shutting down: ${reason} (uptime ${uptime}s, pid ${process.pid})`);

  server.close(() => {
    try {
      db.close();
    } catch (err) {
      console.error('failed to close database', err);
    }
    process.exit(0);
  });

  // Do not hang forever on lingering keep-alive connections.
  setTimeout(() => process.exit(0), 5000).unref();
}

for (const signal of ['SIGINT', 'SIGTERM', 'SIGHUP', 'SIGQUIT']) {
  process.on(signal, () => shutdown(`received ${signal}`));
}

process.on('uncaughtException', (err) => {
  console.error('uncaughtException', err);
  shutdown('uncaughtException');
});

process.on('unhandledRejection', (err) => {
  console.error('unhandledRejection', err);
});
