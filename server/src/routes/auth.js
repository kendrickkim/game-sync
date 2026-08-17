const express = require('express');
const bcrypt = require('bcryptjs');
const jwt = require('jsonwebtoken');
const { db, isUniqueViolation } = require('../db/sqlite');

const router = express.Router();

function signToken(user) {
  return jwt.sign(
    { username: user.username },
    process.env.JWT_SECRET,
    { subject: String(user.id), expiresIn: '30d' }
  );
}

router.post('/register', async (req, res) => {
  try {
    const username = String(req.body.username || '').trim();
    const password = String(req.body.password || '');

    if (username.length < 3 || password.length < 6) {
      return res.status(400).json({
        error: 'Username must be at least 3 chars and password at least 6 chars',
      });
    }

    const passwordHash = await bcrypt.hash(password, 10);
    const result = db
      .prepare('INSERT INTO users (username, password_hash) VALUES (@username, @passwordHash)')
      .run({ username, passwordHash });

    const user = { id: Number(result.lastInsertRowid), username };
    return res.status(201).json({
      token: signToken(user),
      user: { id: user.id, username: user.username },
    });
  } catch (err) {
    if (isUniqueViolation(err)) {
      return res.status(409).json({ error: 'Username already exists' });
    }
    console.error('register error', err);
    return res.status(500).json({ error: 'Registration failed' });
  }
});

router.post('/login', async (req, res) => {
  try {
    const username = String(req.body.username || '').trim();
    const password = String(req.body.password || '');

    const user = db
      .prepare('SELECT id, username, password_hash FROM users WHERE username = @username LIMIT 1')
      .get({ username });

    if (!user) {
      return res.status(401).json({ error: 'Invalid username or password' });
    }

    const ok = await bcrypt.compare(password, user.password_hash);
    if (!ok) {
      return res.status(401).json({ error: 'Invalid username or password' });
    }

    return res.json({
      token: signToken(user),
      user: { id: user.id, username: user.username },
    });
  } catch (err) {
    console.error('login error', err);
    return res.status(500).json({ error: 'Login failed' });
  }
});

module.exports = router;
