const express = require('express');
const { db } = require('../db/sqlite');
const { authRequired } = require('../middleware/auth');

const router = express.Router();

router.use(authRequired);

router.get('/', (req, res) => {
  try {
    const rows = db
      .prepare(
        `SELECT id, name, created_at AS createdAt
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
        `SELECT id, name, created_at AS createdAt
         FROM computers
         WHERE user_id = @userId AND name = @name
         LIMIT 1`
      )
      .get({ userId: req.user.id, name });

    if (existing) {
      return res.json(existing);
    }

    const result = db
      .prepare('INSERT INTO computers (user_id, name) VALUES (@userId, @name)')
      .run({ userId: req.user.id, name });

    return res.status(201).json({
      id: Number(result.lastInsertRowid),
      name,
      createdAt: new Date().toISOString(),
    });
  } catch (err) {
    console.error('create computer error', err);
    return res.status(500).json({ error: 'Failed to register computer' });
  }
});

module.exports = router;
