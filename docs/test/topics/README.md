# Topics Integration Tests (Postman) — CIS Phase 2 (v1/v2)

This folder documents and provides copy-paste Postman scripts for **integration-level** testing of the **Topics** feature in the CIS Phase 2 Minimal API.

The API has two versions that expose the same Topics contract:

- `v1` → MySQL persistence (`/api/v1/*`)
- `v2` → MongoDB persistence (`/api/v2/*`)

All Topics responses include HATEOAS `_links` and the same scripts work for both versions by changing `{{api_version}}`.

## Scope

Included:
- Topics CRUD (`POST/GET/PUT/DELETE /api/{{api_version}}/topics`)
- Pagination/filter/sort connectivity for `GET /topics`
- HATEOAS `_links` validation
- Business rules for Topics:
  - owner-only update/delete
  - status transition `OPEN -> CLOSED` only (no reopening)
  - `winningIdea` is calculated when a topic is closed
- Basic response time assertion (`< {{perf_threshold_ms}} ms`)

Not included (beyond internal setup):
- Separate documentation/test suites for Ideas/Votes/Statistics.

Note: To deterministically validate `winningIdea`, the Topics flow **creates 2 ideas and 1 vote** via internal `pm.sendRequest()` calls **as setup**, but the runnable request sequence remains Topics-only (7 steps).

## Prerequisites

1. **Phase 2 API** running (this repo)
   - Default URL from `RUNNING.md`: `http://localhost:5257`
2. **Phase 1 User Management API** running (for JWT issuance)
   - `http://localhost:8080`
   - Login endpoint used by the scripts: `POST /api/v1/auth/login`
3. Datastores (if running locally):
   - MySQL for v1 (docker): `localhost:3307`
   - MongoDB for v2: `localhost:27017`

See repo root `RUNNING.md` for the canonical setup steps and examples.

## Postman Setup (Collection Variables)

Create a Postman Collection (for example: `CIS Phase 2 - Topics`) and add these **collection variables**:

| Variable | Example | Required | Used for |
|---|---:|:---:|---|
| `base_url` | `http://localhost:5257` | Yes | Phase 2 API base |
| `api_version` | `v1` or `v2` | Yes | Switch persistence version |
| `phase1_base_url` | `http://localhost:8080` | Yes | Phase 1 token issuance |
| `seed_login` | `testuser` | Yes | Phase 1 login |
| `seed_password` | `password123` | Yes | Phase 1 login |
| `perf_threshold_ms` | `500` | No | Response time threshold (ms) |
| `alt_login` | `otheruser` | No | Negative ownership tests (optional) |
| `alt_password` | `password123` | No | Negative ownership tests (optional) |

The scripts will set/overwrite runtime variables such as `seed_token`, `topic_id`, `idea1_id`, etc.

## Requests To Create In Your Collection

Create these 7 requests in this order, and paste the matching scripts from `docs/test/topics/postman-scripts/`.

### 1) Seed user login (Phase 1)

- Method: `POST`
- URL: `{{phase1_base_url}}/api/v1/auth/login`
- Headers: `Content-Type: application/json`
- Body (raw JSON):
  ```json
  { "login": "{{seed_login}}", "password": "{{seed_password}}" }
  ```
- Scripts:
  - Pre-request: `postman-scripts/01_seed_user_login_pre.js`
  - Tests: `postman-scripts/01_seed_user_login_test.js`

### 2) Create topic (authenticated)

- Method: `POST`
- URL: `{{base_url}}/api/{{api_version}}/topics`
- Headers:
  - `Content-Type: application/json`
  - `Authorization: Bearer {{seed_token}}`
- Body (raw JSON):
  ```json
  { "title": "{{topic_title}}", "description": "{{topic_description}}" }
  ```
- Scripts:
  - Pre-request: `postman-scripts/02_create_topic_pre.js`
  - Tests: `postman-scripts/02_create_topic_test.js`

### 3) Get all topics (public)

- Method: `GET`
- URL:
  - Minimal: `{{base_url}}/api/{{api_version}}/topics`
  - Recommended (stable ordering): `{{base_url}}/api/{{api_version}}/topics?page=0&size=10&sortBy=createdAt&order=desc`
- Scripts:
  - Tests: `postman-scripts/03_get_all_topics_public_test.js`

### 4) Get topic by id (public)

- Method: `GET`
- URL: `{{base_url}}/api/{{api_version}}/topics/{{topic_id}}`
- Scripts:
  - Tests: `postman-scripts/04_get_topic_by_id_public_test.js`

### 5) Update topic (owner only) — close it

- Method: `PUT`
- URL: `{{base_url}}/api/{{api_version}}/topics/{{topic_id}}`
- Headers:
  - `Content-Type: application/json`
  - `Authorization: Bearer {{seed_token}}`
- Body (raw JSON):
  ```json
  { "title": "{{topic_title_updated}}", "description": "{{topic_description_updated}}", "status": "CLOSED" }
  ```
- Scripts:
  - Pre-request: `postman-scripts/05_update_topic_pre.js`
  - Tests: `postman-scripts/05_update_topic_test.js`

### 6) Delete topic (owner only)

- Method: `DELETE`
- URL: `{{base_url}}/api/{{api_version}}/topics/{{topic_id}}`
- Headers: `Authorization: Bearer {{seed_token}}`
- Scripts:
  - Tests: `postman-scripts/06_delete_topic_test.js`

### 7) Verify deletion (public)

- Method: `GET`
- URL: `{{base_url}}/api/{{api_version}}/topics/{{topic_id}}`
- Scripts:
  - Tests: `postman-scripts/07_verify_deletion_test.js`

## Running The Suite

### Postman Collection Runner

1. Open the collection → **Run collection**
2. Iterations: `1`
3. Ensure requests execute in the 1→7 order
4. Run once with `api_version=v1`, then again with `api_version=v2`

### Newman (CLI)

If you use Newman, run the same collection twice (or parameterize `api_version`):

```bash
newman run <your_collection.json> \
  --env-var base_url=http://localhost:5257 \
  --env-var phase1_base_url=http://localhost:8080 \
  --env-var api_version=v1 \
  --env-var seed_login=testuser \
  --env-var seed_password=password123
```

## Expected Outcomes (Happy Path)

- Step 2 creates a topic with:
  - `status: "OPEN"`
  - `_links` containing `self`, `ideas`, `update`, `delete`
- Step 3 list response is paged and includes the created topic in `data[]`
- Step 5 sets the topic to `CLOSED` and returns:
  - `status: "CLOSED"`
  - `winningIdea` not `null` and `winningIdea.isWinning === true`
  - `_links` includes `winner` when CLOSED
- Step 6 deletes the topic and returns `200 OK` with a cascade-delete message
- Step 7 returns `404 Not Found`

