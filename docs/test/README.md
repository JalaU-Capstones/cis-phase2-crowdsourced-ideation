# Integration Tests (Postman) — CIS Phase 2 (v1/v2)

This `docs/test/` directory contains **integration-level Postman test suites** for all CIS Phase 2 features. The same collections/scripts run against both API versions by changing `{{api_version}}`:

- `v1` → MySQL persistence (`/api/v1/*`)
- `v2` → MongoDB persistence (`/api/v2/*`)

## Feature Suites

- [Topics](topics/README.md)
- [Ideas](ideas/README.md)
- [Votes](votes/README.md)
- [Statistics](statistics/README.md)

## Quick Setup (Postman)

1. Start services:
   - Phase 2 API (this repo): default `http://localhost:5257`
   - Phase 1 User Management API (JWT issuance): `http://localhost:8080`
2. Create a Postman Collection (or 4 separate collections, one per feature).
3. Add these **collection variables** (shared across suites):

| Variable | Example | Required | Used for |
|---|---:|:---:|---|
| `base_url` | `http://localhost:5257` | Yes | Phase 2 API base |
| `api_version` | `v1` or `v2` | Yes | Switch persistence version |
| `phase1_base_url` | `http://localhost:8080` | Yes | Phase 1 token issuance |
| `seed_login` | `testuser` | Yes | Phase 1 login |
| `seed_password` | `password123` | Yes | Phase 1 login |
| `perf_threshold_ms` | `500` | No | Response time threshold (ms) |

Each suite documents additional runtime variables it sets (for example: `seed_token`, `topic_id`, `idea_id`).

## Running All Tests

Run each suite end-to-end in the Postman Collection Runner:

1. Set `api_version=v1` and run the full request sequence for Topics, then Ideas, then Votes, then Statistics.
2. Change to `api_version=v2` and repeat.

## Newman (CLI)

You can also run collections using Newman. Example (run once for `v1`, then again for `v2`):

```bash
newman run <your_collection.json> \
  --env-var base_url=http://localhost:5257 \
  --env-var phase1_base_url=http://localhost:8080 \
  --env-var api_version=v1 \
  --env-var seed_login=testuser \
  --env-var seed_password=password123
```

