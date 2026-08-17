const express = require('express');
const { db } = require('../db/sqlite');
const { authRequired } = require('../middleware/auth');

const router = express.Router();
router.use(authRequired);

function getComputer(userId, selector) {
  if (selector.id) {
    return db.prepare(
      `SELECT id, name FROM computers
       WHERE id = @id AND user_id = @userId LIMIT 1`
    ).get({ id: selector.id, userId });
  }

  return db.prepare(
    `SELECT id, name FROM computers
     WHERE name = @name AND user_id = @userId LIMIT 1`
  ).get({ name: selector.name, userId });
}

router.post('/', (req, res) => {
  try {
    const gameId = Number(req.body.gameId);
    const targetComputerId = Number(req.body.targetComputerId);
    const requesterComputerName = String(req.body.requesterComputerName || '').trim();

    if (!gameId || !targetComputerId || !requesterComputerName) {
      return res.status(400).json({
        error: 'gameId, targetComputerId and requesterComputerName are required',
      });
    }

    const requester = getComputer(req.user.id, { name: requesterComputerName });
    const target = getComputer(req.user.id, { id: targetComputerId });
    const game = db.prepare(
      `SELECT id, name FROM games
       WHERE id = @gameId AND user_id = @userId LIMIT 1`
    ).get({ gameId, userId: req.user.id });

    if (!requester || !target || !game) {
      return res.status(404).json({ error: 'Game or computer not found for this account' });
    }
    if (requester.id === target.id) {
      return res.status(400).json({ error: 'Choose another computer' });
    }

    const duplicate = db.prepare(
      `SELECT id FROM remote_upload_requests
       WHERE user_id = @userId
         AND target_computer_id = @targetComputerId
         AND game_id = @gameId
         AND status IN ('pending', 'processing')
       LIMIT 1`
    ).get({ userId: req.user.id, targetComputerId, gameId });

    if (duplicate) {
      return res.status(409).json({ error: 'An upload request is already pending' });
    }

    const result = db.prepare(
      `INSERT INTO remote_upload_requests
        (user_id, requester_computer_id, target_computer_id, game_id)
       VALUES
        (@userId, @requesterComputerId, @targetComputerId, @gameId)`
    ).run({
      userId: req.user.id,
      requesterComputerId: requester.id,
      targetComputerId,
      gameId,
    });

    return res.status(201).json({
      id: Number(result.lastInsertRowid),
      gameId,
      gameName: game.name,
      requesterComputerName: requester.name,
      targetComputerId: target.id,
      targetComputerName: target.name,
      status: 'pending',
      createdAt: new Date().toISOString(),
    });
  } catch (err) {
    console.error('create remote upload request error', err);
    return res.status(500).json({ error: 'Failed to create remote upload request' });
  }
});

router.get('/pending', (req, res) => {
  try {
    const computerName = String(req.query.computerName || '').trim();
    const target = getComputer(req.user.id, { name: computerName });
    if (!target) {
      return res.status(404).json({ error: 'Computer not registered' });
    }

    db.prepare(
      `UPDATE computers SET last_seen_at = datetime('now')
       WHERE id = @id AND user_id = @userId`
    ).run({ id: target.id, userId: req.user.id });

    // A client may terminate after claiming a request. Make stale work retryable.
    db.prepare(
      `UPDATE remote_upload_requests
       SET status = 'pending', started_at = NULL,
           message = 'Retried after stale processing timeout'
       WHERE user_id = @userId
         AND target_computer_id = @targetComputerId
         AND status = 'processing'
         AND started_at < datetime('now', '-10 minutes')`
    ).run({ userId: req.user.id, targetComputerId: target.id });

    const rows = db.prepare(
      `SELECT
         r.id,
         r.game_id AS gameId,
         g.name AS gameName,
         requester.name AS requesterComputerName,
         target.name AS targetComputerName,
         r.status,
         replace(r.created_at, ' ', 'T') AS createdAt
       FROM remote_upload_requests r
       INNER JOIN games g ON g.id = r.game_id
       INNER JOIN computers requester ON requester.id = r.requester_computer_id
       INNER JOIN computers target ON target.id = r.target_computer_id
       WHERE r.user_id = @userId
         AND r.target_computer_id = @targetComputerId
         AND r.status = 'pending'
       ORDER BY r.id ASC`
    ).all({ userId: req.user.id, targetComputerId: target.id });

    return res.json(rows);
  } catch (err) {
    console.error('pending remote uploads error', err);
    return res.status(500).json({ error: 'Failed to load remote upload requests' });
  }
});

router.post('/:id/claim', (req, res) => {
  try {
    const id = Number(req.params.id);
    const computerName = String(req.body.computerName || '').trim();
    const target = getComputer(req.user.id, { name: computerName });
    if (!target) {
      return res.status(404).json({ error: 'Computer not registered' });
    }

    const result = db.prepare(
      `UPDATE remote_upload_requests
       SET status = 'processing', started_at = datetime('now')
       WHERE id = @id
         AND user_id = @userId
         AND target_computer_id = @targetComputerId
         AND status = 'pending'`
    ).run({ id, userId: req.user.id, targetComputerId: target.id });

    if (result.changes === 0) {
      return res.status(409).json({ error: 'Request is no longer pending' });
    }
    return res.json({ ok: true });
  } catch (err) {
    console.error('claim remote upload error', err);
    return res.status(500).json({ error: 'Failed to claim remote upload request' });
  }
});

router.post('/:id/result', (req, res) => {
  try {
    const id = Number(req.params.id);
    const computerName = String(req.body.computerName || '').trim();
    const status = String(req.body.status || '');
    const message = req.body.message == null ? null : String(req.body.message).slice(0, 1000);
    const syncEntryId = req.body.syncEntryId == null ? null : Number(req.body.syncEntryId);
    const target = getComputer(req.user.id, { name: computerName });

    if (!target || !['completed', 'failed'].includes(status)) {
      return res.status(400).json({ error: 'Invalid result' });
    }

    if (status === 'completed') {
      const entry = db.prepare(
        `SELECT id FROM sync_entries
         WHERE id = @syncEntryId
           AND user_id = @userId
           AND computer_id = @targetComputerId
         LIMIT 1`
      ).get({ syncEntryId, userId: req.user.id, targetComputerId: target.id });
      if (!entry) {
        return res.status(400).json({ error: 'Uploaded sync entry not found' });
      }
    }

    const result = db.prepare(
      `UPDATE remote_upload_requests
       SET status = @status,
           sync_entry_id = @syncEntryId,
           message = @message,
           completed_at = datetime('now')
       WHERE id = @id
         AND user_id = @userId
         AND target_computer_id = @targetComputerId
         AND status = 'processing'`
    ).run({
      id,
      userId: req.user.id,
      targetComputerId: target.id,
      status,
      syncEntryId: status === 'completed' ? syncEntryId : null,
      message,
    });

    if (result.changes === 0) {
      return res.status(409).json({ error: 'Request is not processing' });
    }
    return res.json({ ok: true });
  } catch (err) {
    console.error('remote upload result error', err);
    return res.status(500).json({ error: 'Failed to save remote upload result' });
  }
});

router.get('/', (req, res) => {
  try {
    const rows = db.prepare(
      `SELECT
         r.id,
         r.game_id AS gameId,
         g.name AS gameName,
         requester.name AS requesterComputerName,
         r.target_computer_id AS targetComputerId,
         target.name AS targetComputerName,
         r.status,
         r.sync_entry_id AS syncEntryId,
         r.message,
         replace(r.created_at, ' ', 'T') AS createdAt,
         replace(r.completed_at, ' ', 'T') AS completedAt
       FROM remote_upload_requests r
       INNER JOIN games g ON g.id = r.game_id
       INNER JOIN computers requester ON requester.id = r.requester_computer_id
       INNER JOIN computers target ON target.id = r.target_computer_id
       WHERE r.user_id = @userId
       ORDER BY r.id DESC
       LIMIT 100`
    ).all({ userId: req.user.id });
    return res.json(rows);
  } catch (err) {
    console.error('remote upload history error', err);
    return res.status(500).json({ error: 'Failed to load remote upload history' });
  }
});

module.exports = router;
