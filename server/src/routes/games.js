const express = require('express');
const { db, isUniqueViolation } = require('../db/sqlite');
const { authRequired } = require('../middleware/auth');

const router = express.Router();

router.use(authRequired);

router.get('/', (req, res) => {
  try {
    const rows = db
      .prepare(
        `SELECT id, name, created_at AS createdAt
         FROM games
         WHERE user_id = @userId
         ORDER BY name ASC`
      )
      .all({ userId: req.user.id });
    return res.json(rows);
  } catch (err) {
    console.error('list games error', err);
    return res.status(500).json({ error: 'Failed to list games' });
  }
});

router.post('/', (req, res) => {
  try {
    const name = String(req.body.name || '').trim();
    if (!name) {
      return res.status(400).json({ error: 'Game name is required' });
    }

    const result = db
      .prepare('INSERT INTO games (user_id, name) VALUES (@userId, @name)')
      .run({ userId: req.user.id, name });

    return res.status(201).json({
      id: Number(result.lastInsertRowid),
      name,
      createdAt: new Date().toISOString(),
    });
  } catch (err) {
    if (isUniqueViolation(err)) {
      return res.status(409).json({ error: 'Game already exists' });
    }
    console.error('create game error', err);
    return res.status(500).json({ error: 'Failed to create game' });
  }
});

router.delete('/:id', (req, res) => {
  try {
    const gameId = Number(req.params.id);
    const result = db
      .prepare('DELETE FROM games WHERE id = @gameId AND user_id = @userId')
      .run({ gameId, userId: req.user.id });

    if (result.changes === 0) {
      return res.status(404).json({ error: 'Game not found' });
    }
    return res.status(204).send();
  } catch (err) {
    console.error('delete game error', err);
    return res.status(500).json({ error: 'Failed to delete game' });
  }
});

module.exports = router;
