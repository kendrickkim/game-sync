const path = require('path');
const fs = require('fs');
const dotenv = require('dotenv');

dotenv.config({ path: path.join(__dirname, '..', '.env') });

const express = require('express');
const cors = require('cors');
const { dbPath } = require('./db/sqlite');

const authRoutes = require('./routes/auth');
const gamesRoutes = require('./routes/games');
const computersRoutes = require('./routes/computers');
const syncRoutes = require('./routes/sync');
const remoteUploadRoutes = require('./routes/remoteUploads');

if (!process.env.JWT_SECRET) {
  console.warn('Warning: JWT_SECRET is not set. Using insecure default for development only.');
  process.env.JWT_SECRET = 'dev-insecure-secret-change-me';
}

const uploadDir = path.resolve(process.env.UPLOAD_DIR || 'uploads');
fs.mkdirSync(uploadDir, { recursive: true });

const app = express();
app.use(cors());
app.use(express.json({ limit: '2mb' }));

app.get('/health', (_req, res) => {
  res.json({ ok: true, db: 'sqlite', dbPath });
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
app.listen(port, () => {
  console.log(`Game Sync server listening on http://localhost:${port}`);
  console.log(`SQLite: ${dbPath}`);
});
