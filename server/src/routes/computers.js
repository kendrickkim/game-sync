const express = require('express');
const { db } = require('../db/sqlite');
const { authRequired } = require('../middleware/auth');

const router = express.Router();

router.use(authRequired);

router.get('/', (req, res) => {
  try {
    const rows = db
      .prepare(
        `SELECT id, name,
                replace(created_at, ' ', 'T') AS createdAt,
                replace(last_seen_at, ' ', 'T') AS lastSeenAt,
                CASE
                  WHEN last_seen_at >= datetime('now', '-30 seconds') THEN 1
                  ELSE 0
                END AS isOnline
         FROM computers
         WHERE user_id = @userId
         ORDER BY name ASC`
      )
      .all({ userId: req.user.id });
    return res.json(rows);
  } catch (err) {
    console.error('list computers error', err);
    return res.status(500).json({ error: 'Failed to list computers' });
  }
});

router.post('/', (req, res) => {
  try {
    const name = String(req.body.name || '').trim();
    if (!name) {
      return res.status(400).json({ error: 'Computer name is required' });
    }

    const existing = db
      .prepare(
        `SELECT id, name,
                replace(created_at, ' ', 'T') AS createdAt,
                replace(last_seen_at, ' ', 'T') AS lastSeenAt
         FROM computers
         WHERE user_id = @userId AND name = @name
         LIMIT 1`
      )
      .get({ userId: req.user.id, name });

    if (existing) {
      db.prepare(
        `UPDATE computers SET last_seen_at = datetime('now')
         WHERE id = @id AND user_id = @userId`
      ).run({ id: existing.id, userId: req.user.id });
      return res.json({ ...existing, lastSeenAt: new Date().toISOString(), isOnline: 1 });
    }

    const result = db
      .prepare(
        `INSERT INTO computers (user_id, name, last_seen_at)
         VALUES (@userId, @name, datetime('now'))`
      )
      .run({ userId: req.user.id, name });

    return res.status(201).json({
      id: Number(result.lastInsertRowid),
      name,
      createdAt: new Date().toISOString(),
      lastSeenAt: new Date().toISOString(),
      isOnline: 1,
    });
  } catch (err) {
    console.error('create computer error', err);
    return res.status(500).json({ error: 'Failed to register computer' });
  }
});

router.post('/heartbeat', (req, res) => {
  try {
    const name = String(req.body.name || '').trim();
    if (!name) {
      return res.status(400).json({ error: 'Computer name is required' });
    }

    const result = db.prepare(
      `UPDATE computers
       SET last_seen_at = datetime('now')
       WHERE user_id = @userId AND name = @name`
    ).run({ userId: req.user.id, name });

    if (result.changes === 0) {
      return res.status(404).json({ error: 'Computer not registered' });
    }

    return res.json({ ok: true, lastSeenAt: new Date().toISOString() });
  } catch (err) {
    console.error('computer heartbeat error', err);
    return res.status(500).json({ error: 'Heartbeat failed' });
  }
});

module.exports = router;
