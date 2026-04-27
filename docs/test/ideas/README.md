# Ideas Integration Tests (Postman) — CIS Phase 2 (v1/v2)

This folder documents and provides copy-paste Postman scripts for **integration-level** testing of the **Ideas** feature in the CIS Phase 2 Minimal API.

The API has two versions that expose the same Ideas contract:

- `v1` → MySQL persistence (`/api/v1/*`)
- `v2` → MongoDB persistence (`/api/v2/*`)

All Idea responses include HATEOAS `_links`. The same suite runs against both versions by changing `{{api_version}}`.

## Scope

Included:
- Ideas CRUD:
  - `POST/GET/PUT/DELETE /api/{{api_version}}/ideas`
  - `GET /api/{{api_version}}/ideas/topic/{{topic_id}}`
- Pagination and sorting connectivity for `GET /ideas` (paged response)
- HATEOAS `_links` validation (including conditional `vote` link)
- Key business rules that impact Ideas:
  - owner-only update/delete
  - cannot create/update/delete an idea when the parent topic is `CLOSED`
  - `isWinning` is set when the topic is closed and winner is calculated
- Basic response time assertion (`< {{perf_threshold_ms}} ms`)

Not included (beyond internal setup):
- Separate full test suites for Topics, Votes, and Statistics.

## Prerequisites

1. **Phase 2 API** running (this repo)
   - Default URL from `RUNNING.md`: `http://localhost:5257`
2. **Phase 1 User Management API** running (for JWT issuance)
   - `http://localhost:8080`
   - Login endpoint used by the scripts: `POST /api/v1/auth/login`
3. Datastores (if running locally):
   - MySQL for v1 (docker): `localhost:3307`
   - MongoDB for v2: `localhost:27017`

See repo root `RUNNING.md` (section “11.2. Ideas”) for canonical examples and HATEOAS link format.

## Postman Setup (Collection Variables)

Create a Postman Collection (for example: `CIS Phase 2 - Ideas`) and add these **collection variables**:

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

The scripts will set/overwrite runtime variables such as `seed_token`, `topic_id`, `idea_id`, etc.

## Requests To Create In Your Collection (9-Step E2E Flow)

Create these 9 requests in this order, and paste the matching scripts from `docs/test/ideas/postman-scripts/`.

### 1) Seed user login (Phase 1)

- Method: `POST`
- URL: `{{phase1_base_url}}/api/{{api_version}}/auth/login`
- Headers: `Content-Type: application/json`
- Body (raw JSON):
  ```json
  { "login": "{{seed_login}}", "password": "{{seed_password}}" }
  ```
- Scripts:
  - Pre-request: `postman-scripts/01_seed_user_login_pre.js`
  - Tests: `postman-scripts/01_seed_user_login_test.js`

### 2) Create topic for idea (authenticated)

Ideas must belong to a topic, so the suite creates one topic first.

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
  - Pre-request: `postman-scripts/02_create_topic_for_idea_pre.js`
  - Tests: `postman-scripts/02_create_topic_for_idea_test.js`

### 3) Create idea (authenticated)

- Method: `POST`
- URL: `{{base_url}}/api/{{api_version}}/ideas`
- Headers:
  - `Content-Type: application/json`
  - `Authorization: Bearer {{seed_token}}`
- Body (raw JSON):
  ```json
  { "topicId": "{{topic_id}}", "title": "{{idea_title}}", "description": "{{idea_description}}" }
  ```
- Scripts:
  - Pre-request: `postman-scripts/03_create_idea_pre.js`
  - Tests: `postman-scripts/03_create_idea_test.js`

### 4) Get all ideas (public, paged)

- Method: `GET`
- URL:
  - Minimal: `{{base_url}}/api/{{api_version}}/ideas`
  - Recommended: `{{base_url}}/api/{{api_version}}/ideas?page=0&size=10&sortBy=updatedAt&order=desc`
- Scripts:
  - Tests: `postman-scripts/04_get_all_ideas_public_test.js`

### 5) Get idea by id (public)

- Method: `GET`
- URL: `{{base_url}}/api/{{api_version}}/ideas/{{idea_id}}`
- Scripts:
  - Tests: `postman-scripts/05_get_idea_by_id_public_test.js`

### 6) Get ideas by topic (public)

- Method: `GET`
- URL: `{{base_url}}/api/{{api_version}}/ideas/topic/{{topic_id}}`
- Scripts:
  - Tests: `postman-scripts/06_get_ideas_by_topic_public_test.js`

### 7) Update idea (owner only)

- Method: `PUT`
- URL: `{{base_url}}/api/{{api_version}}/ideas/{{idea_id}}`
- Headers:
  - `Content-Type: application/json`
  - `Authorization: Bearer {{seed_token}}`
- Body (raw JSON):
  ```json
  { "title": "{{idea_title_updated}}", "description": "{{idea_description_updated}}" }
  ```
- Scripts:
  - Pre-request: `postman-scripts/07_update_idea_pre.js`
  - Tests: `postman-scripts/07_update_idea_test.js`

### 8) Delete idea (owner only)

- Method: `DELETE`
- URL: `{{base_url}}/api/{{api_version}}/ideas/{{idea_id}}`
- Headers: `Authorization: Bearer {{seed_token}}`
- Scripts:
  - Tests: `postman-scripts/08_delete_idea_test.js`

### 9) Verify deletion (public)

- Method: `GET`
- URL: `{{base_url}}/api/{{api_version}}/ideas/{{idea_id}}`
- Scripts:
  - Tests: `postman-scripts/09_verify_deletion_test.js`

## Running The Suite

### Postman Collection Runner

1. Open the collection → **Run collection**
2. Iterations: `1`
3. Ensure requests execute in the 1→9 order
4. Run once with `api_version=v1`, then again with `api_version=v2`

### Newman (CLI)

```bash
newman run <your_collection.json> \
  --env-var base_url=http://localhost:5257 \
  --env-var phase1_base_url=http://localhost:8080 \
  --env-var api_version=v1 \
  --env-var seed_login=testuser \
  --env-var seed_password=password123
```

## Expected Outcomes (Happy Path)

- Step 3 creates an idea and returns `_links`, including `vote` because the topic is `OPEN`.
- Step 4 returns a **paged** response and the created idea appears in `data[]`.
- Step 6 returns an **array** and includes the created idea.
- Step 7 updates title/description.
- Step 8 returns `200 OK` with an “Idea deleted…” message.
- Step 9 returns `404 Not Found`.

