# Factory backup API (Railway)

Minimal Express service: **`POST /upload-backup`** accepts `multipart/form-data` with fields **`file`** and **`deviceId`**. Files are stored under **`{DATA_ROOT}/backups/{deviceId}/`** with a timestamped filename.

## Railway

1. Create a **Volume** and mount it at **`/data`** (service root path).
2. Set the service **root directory** to **`backup-api`** if deploying from a monorepo.
3. **Start command:** `npm start` (default after `npm install`).
4. **`PORT`** is set automatically by Railway.

Production uses **`DATA_ROOT=/data`** by default (no env var needed if the volume is at `/data`).

## Local development

Without a `/data` directory (e.g. on Windows), set:

```bash
set DATA_ROOT=./data
npm install
npm start
```

Or on Unix: `DATA_ROOT=./data npm start`

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/health` | `{ "ok": true }` |
| POST | `/upload-backup` | Multipart: `file`, `deviceId` |

## Example

```bash
curl -X POST http://localhost:3000/upload-backup ^
  -F "file=@./factory.db" ^
  -F "deviceId=550e8400-e29b-41d4-a716-446655440000"
```

## Security

No authentication in this minimal setup. Keep the deployment URL private or add auth later.
