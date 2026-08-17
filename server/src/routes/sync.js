const express = require('express');
const fs = require('fs');
const path = require('path');
const multer = require('multer');
const { db } = require('../db/sqlite');
const { authRequired } = require('../middleware/auth');

const router = express.Router();
router.use(authRequired);

const serverRoot = path.join(__dirname, '..', '..');
const configuredUploadDir = process.env.UPLOAD_DIR || 'uploads';
const uploadDir = path.isAbsolute(configuredUploadDir)
  ? configuredUploadDir
  : path.resolve(serverRoot, configuredUploadDir);
fs.mkdirSync(uploadDir, { recursive: true });

const storage = multer.diskStorage({
  destination: (_req, _file, cb) => cb(null, uploadDir),
  filename: (_req, _file, cb) => {
    const safe = `${Date.now()}-${Math.round(Math.random() * 1e9)}.zip`;
    cb(null, safe);
  },
});

const upload = multer({
  storage,
  limits: { fileSize: 512 * 1024 * 1024 },
  fileFilter: (_req, file, cb) => {
    if (file.mimetype === 'application/zip' || file.originalname.toLowerCase().endsWith('.zip')) {
      cb(null, true);
    } else {
      cb(new Error('Only zip files are allowed'));
    }
  },
});

function ensureComputer(userId, computerName) {
  const existing = db
    .prepare('SELECT id FROM computers WHERE user_id = @userId AND name = @name LIMIT 1')
    .get({ userId, name: computerName });
  if (existing) {
    return existing.id;
  }
  const result = db
    .prepare('INSERT INTO computers (user_id, name) VALUES (@userId, @name)')
    .run({ userId, name: computerName });
  return Number(result.lastInsertRowid);
}

function assertGameOwned(userId, gameId) {
  const row = db
    .prepare('SELECT id FROM games WHERE id = @gameId AND user_id = @userId LIMIT 1')
    .get({ gameId, userId });
  return Boolean(row);
}

router.get('/list', (req, res) => {
  try {
    const gameId = req.query.gameId ? Number(req.query.gameId) : null;
    const params = { userId: req.user.id };

    let sql = `
      SELECT
        se.id,
        se.game_id AS gameId,
        g.name AS gameName,
        se.computer_id AS computerId,
        c.name AS computerName,
        se.local_path AS localPath,
        se.zip_filename AS zipFilename,
        se.content_mtime AS contentMtime,
        se.file_size AS fileSize,
        replace(se.created_at, ' ', 'T') AS createdAt,
        replace(se.updated_at, ' ', 'T') AS updatedAt
      FROM sync_entries se
      INNER JOIN games g ON g.id = se.game_id
      INNER JOIN computers c ON c.id = se.computer_id
      WHERE se.user_id = @userId
    `;

    if (gameId) {
      sql += ' AND se.game_id = @gameId';
      params.gameId = gameId;
    }

    sql += ' ORDER BY se.id DESC';

    const rows = db.prepare(sql).all(params);
    return res.json(rows);
  } catch (err) {
    console.error('sync list error', err);
    return res.status(500).json({ error: 'Failed to list sync entries' });
  }
});

router.post('/upload', upload.single('file'), (req, res) => {
  try {
    if (!req.file) {
      return res.status(400).json({ error: 'Zip file is required' });
    }

    const gameId = Number(req.body.gameId);
    const computerName = String(req.body.computerName || '').trim();
    const localPath = String(req.body.localPath || '').trim();
    const contentMtime = Number(req.body.contentMtime);

    if (!gameId || !computerName || !localPath || Number.isNaN(contentMtime)) {
      fs.unlink(req.file.path, () => {});
      return res.status(400).json({
        error: 'gameId, computerName, localPath, contentMtime are required',
      });
    }

    if (!assertGameOwned(req.user.id, gameId)) {
      fs.unlink(req.file.path, () => {});
      return res.status(404).json({ error: 'Game not found' });
    }

    const computerId = ensureComputer(req.user.id, computerName);

    // Always insert a new history record (do not overwrite previous uploads)
    const result = db
      .prepare(
        `INSERT INTO sync_entries
          (user_id, game_id, computer_id, local_path, zip_filename, content_mtime, file_size)
         VALUES
          (@userId, @gameId, @computerId, @localPath, @zipFilename, @contentMtime, @fileSize)`
      )
      .run({
        userId: req.user.id,
        gameId,
        computerId,
        localPath,
        zipFilename: req.file.filename,
        contentMtime,
        fileSize: req.file.size,
      });

    const id = Number(result.lastInsertRowid);
    const created = db
      .prepare(
        `SELECT replace(created_at, ' ', 'T') AS createdAt
         FROM sync_entries WHERE id = @id`
      )
      .get({ id });

    return res.status(201).json({
      id,
      gameId,
      computerId,
      localPath,
      zipFilename: req.file.filename,
      contentMtime,
      fileSize: req.file.size,
      createdAt: created?.createdAt,
    });
  } catch (err) {
    if (req.file) {
      fs.unlink(req.file.path, () => {});
    }
    console.error('sync upload error', err);
    return res.status(500).json({ error: err.message || 'Upload failed' });
  }
});

router.get('/download/:entryId', (req, res) => {
  try {
    const entryId = Number(req.params.entryId);
    const entry = db
      .prepare(
        `SELECT id, zip_filename AS zipFilename, local_path AS localPath,
                content_mtime AS contentMtime, file_size AS fileSize
         FROM sync_entries
         WHERE id = @entryId AND user_id = @userId
         LIMIT 1`
      )
      .get({ entryId, userId: req.user.id });

    if (!entry) {
      return res.status(404).json({ error: 'Sync entry not found' });
    }

    const filePath = path.join(uploadDir, entry.zipFilename);
    if (!fs.existsSync(filePath)) {
      return res.status(404).json({ error: 'Zip file missing on server' });
    }

    res.setHeader('Content-Type', 'application/zip');
    res.setHeader('Content-Disposition', `attachment; filename="${entry.zipFilename}"`);
    res.setHeader('X-Content-Mtime', String(entry.contentMtime));
    res.setHeader('X-File-Size', String(entry.fileSize));
    return fs.createReadStream(filePath).pipe(res);
  } catch (err) {
    console.error('sync download error', err);
    return res.status(500).json({ error: 'Download failed' });
  }
});

router.delete('/:entryId', (req, res) => {
  try {
    const entryId = Number(req.params.entryId);
    const entry = db
      .prepare(
        `SELECT id, zip_filename AS zipFilename
         FROM sync_entries
         WHERE id = @entryId AND user_id = @userId
         LIMIT 1`
      )
      .get({ entryId, userId: req.user.id });

    if (!entry) {
      return res.status(404).json({ error: 'Sync entry not found' });
    }

    db.prepare('DELETE FROM sync_entries WHERE id = @entryId').run({ entryId });
    const zipPath = path.join(uploadDir, entry.zipFilename);
    fs.unlink(zipPath, () => {});

    return res.status(204).send();
  } catch (err) {
    console.error('sync delete error', err);
    return res.status(500).json({ error: 'Failed to delete sync entry' });
  }
});

module.exports = router;
