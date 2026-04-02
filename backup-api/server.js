'use strict';

const express = require('express');
const multer = require('multer');
const fs = require('fs/promises');
const path = require('path');

const port = Number(process.env.PORT) || 3000;
/** Railway: mount volume at /data. Local: set DATA_ROOT=./data */
const dataRoot = process.env.DATA_ROOT || '/data';

const maxFileBytes = Number(process.env.MAX_UPLOAD_MB || 200) * 1024 * 1024;

const upload = multer({
  storage: multer.memoryStorage(),
  limits: { fileSize: maxFileBytes },
});

const app = express();

function sanitizeDeviceId(raw) {
  if (raw == null || typeof raw !== 'string') return null;
  const s = raw.trim();
  if (!s || s.length > 128) return null;
  // GUID "D" format and similar: hex + hyphens only
  if (!/^[a-fA-F0-9-]+$/.test(s)) return null;
  if (s.includes('..') || s.includes('/') || s.includes('\\')) return null;
  return s;
}

function safeBackupBasename(originalname) {
  const base = path.basename(originalname || 'backup.db');
  if (!base || base === '.' || base === '..') return 'backup.db';
  const cleaned = base.replace(/[^a-zA-Z0-9._-]/g, '_');
  return cleaned.length > 200 ? cleaned.slice(0, 200) : cleaned;
}

app.get('/health', (_req, res) => {
  res.json({ ok: true });
});

app.get('/', (_req, res) => {
  res.json({ ok: true });
});

app.post('/upload-backup', upload.single('file'), async (req, res) => {
  try {
    const deviceId = sanitizeDeviceId(req.body?.deviceId);
    if (!deviceId) {
      return res.status(400).json({
        success: false,
        error: 'Invalid or missing deviceId',
      });
    }

    if (!req.file || !req.file.buffer) {
      return res.status(400).json({
        success: false,
        error: 'Missing file field',
      });
    }

    const dir = path.join(dataRoot, 'backups', deviceId);
    await fs.mkdir(dir, { recursive: true });

    const basename = safeBackupBasename(req.file.originalname);
    const filename = `${Date.now()}_${basename}`;
    const destPath = path.join(dir, filename);

    await fs.writeFile(destPath, req.file.buffer);

    return res.status(200).json({
      success: true,
      filename,
      deviceId,
    });
  } catch (err) {
    console.error('upload-backup error:', err);
    return res.status(500).json({
      success: false,
      error: 'Upload failed',
    });
  }
});

app.use((err, _req, res, _next) => {
  if (err instanceof multer.MulterError) {
    if (err.code === 'LIMIT_FILE_SIZE') {
      return res.status(400).json({
        success: false,
        error: 'File too large',
      });
    }
  }
  console.error(err);
  return res.status(500).json({ success: false, error: 'Server error' });
});

app.listen(port, () => {
  console.log(`backup-api listening on port ${port}, dataRoot=${dataRoot}`);
});
