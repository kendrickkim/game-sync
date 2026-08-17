/**
 * PM2 ecosystem for Game Sync server.
 *
 * Usage:
 *   pm2 start ecosystem.config.cjs
 *   pm2 save
 *
 * Do NOT enable bare --watch on the whole project: SQLite WAL/SHM files under
 * data/ change continuously and will restart the process in a loop (SIGINT).
 */
module.exports = {
  apps: [
    {
      name: 'gamesync-server',
      script: 'src/index.js',
      cwd: __dirname,
      instances: 1,
      exec_mode: 'fork',
      autorestart: true,
      max_restarts: 20,
      min_uptime: '5s',
      // Keep watch off in production. If you turn it on for deploys, ignore DB/uploads.
      watch: false,
      ignore_watch: [
        'node_modules',
        'data',
        'uploads',
        '.git',
        '*.sqlite',
        '*.sqlite-shm',
        '*.sqlite-wal',
        'logs',
      ],
      env: {
        NODE_ENV: 'production',
      },
    },
  ],
};
