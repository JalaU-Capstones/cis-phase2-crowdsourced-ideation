# Votes Integration Tests (Postman) — CIS Phase 2 (v1/v2)

This folder documents and provides copy-paste Postman scripts for **integration-level** testing of the **Votes** feature in the CIS Phase 2 Minimal API.

The API has two versions that expose the same Votes contract:

- `v1` → MySQL persistence (`/api/v1/*`)
- `v2` → MongoDB persistence (`/api/v2/*`)

Votes responses include HATEOAS `_links`. The same suite runs against both versions by changing `{{api_version}}`.

## Scope

Included:
- Votes CRUD:
  - `POST /api/{{api_version}}/votes` (cast)
  - `GET /api/{{api_version}}/votes` (list)
  - `GET /api/{{api_version}}/votes/{{vote_id}}` (by id)
  - `GET /api/{{api_version}}/votes/idea/{{idea_id}}` (by idea)
  - `PUT /api/{{api_version}}/votes/{{vote_id}}` (move vote to another idea)
  - `DELETE /api/{{api_version}}/votes/{{vote_id}}`
- Business rules that affect voting:
  - unique vote per user per idea (`409 Conflict`)
  - cannot vote on a `CLOSED` topic (`403 Forbidden`)
  - only vote owner can update/delete (`403 Forbidden`)
- Basic response time assertion (`< {{perf_threshold_ms}} ms`)

Setup requirements:
- A vote needs an existing **idea**, which needs an existing **topic**.
  The suite creates a topic and idea as prerequisites (steps 2 and 3).

## Prerequisites

1. **Phase 2 API** running (this repo)
   - Default URL from `RUNNING.md`: `http://localhost:5257`
2. **Phase 1 User Management API** running (for JWT issuance)
   - `http://localhost:8080`
   - Login endpoint used by scripts: `POST /api/v1/auth/login`
3. Datastores (if running locally):
   - MySQL for v1: `localhost:3307`
   - MongoDB for v2: `localhost:27017`

See repo root `RUNNING.md` (section “11.3. Votes”) for canonical curl examples and HATEOAS link format.

## Postman Setup (Collection Variables)

Create a Postman Collection (for example: `CIS Phase 2 - Votes`) and add these **collection variables**:

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

The scripts will set/overwrite runtime variables such as `seed_token`, `topic_id`, `idea_id`, `vote_id`, `new_idea_id`.

## Requests To Create In Your Collection (10-Step E2E Flow)

Create these 10 requests in this order, and paste the matching scripts from `docs/test/votes/postman-scripts/`.

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

### 2) Create topic (authenticated)

- Method: `POST`
- URL: `{{base_url}}/api/{{api_version}}/topics`
- Headers:
  - `Content-Type: application/json`
  - `Authorization: Bearer {{seed_token}}`
- Body:
  ```json
  { "title": "{{topic_title}}", "description": "{{topic_description}}" }
  ```
- Scripts:
  - Pre-request: `postman-scripts/02_create_topic_for_vote_pre.js`
  - Tests: `postman-scripts/02_create_topic_for_vote_test.js`

### 3) Create idea (authenticated)

- Method: `POST`
- URL: `{{base_url}}/api/{{api_version}}/ideas`
- Headers:
  - `Content-Type: application/json`
  - `Authorization: Bearer {{seed_token}}`
- Body:
  ```json
  { "topicId": "{{topic_id}}", "title": "{{idea_title}}", "description": "{{idea_description}}" }
  ```
- Scripts:
  - Pre-request: `postman-scripts/03_create_idea_pre.js`
  - Tests: `postman-scripts/03_create_idea_test.js`

### 4) Cast vote (authenticated)

- Method: `POST`
- URL: `{{base_url}}/api/{{api_version}}/votes`
- Headers:
  - `Content-Type: application/json`
  - `Authorization: Bearer {{seed_token}}`
- Body:
  ```json
  { "ideaId": "{{idea_id}}" }
  ```
- Scripts:
  - Pre-request: `postman-scripts/04_cast_vote_pre.js`
  - Tests: `postman-scripts/04_cast_vote_test.js`

### 5) Get all votes (public)

- Method: `GET`
- URL: `{{base_url}}/api/{{api_version}}/votes`
- Scripts:
  - Tests: `postman-scripts/05_get_all_votes_public_test.js`

### 6) Get vote by id (public)

- Method: `GET`
- URL: `{{base_url}}/api/{{api_version}}/votes/{{vote_id}}`
- Scripts:
  - Tests: `postman-scripts/06_get_vote_by_id_public_test.js`

### 7) Get votes by idea (public)

- Method: `GET`
- URL: `{{base_url}}/api/{{api_version}}/votes/idea/{{idea_id}}`
- Scripts:
  - Tests: `postman-scripts/07_get_votes_by_idea_public_test.js`

### 8) Update vote (move to another idea, owner only)

Step 8 needs a second idea to move the vote to. The pre-request script creates it automatically and sets `{{new_idea_id}}`.

- Method: `PUT`
- URL: `{{base_url}}/api/{{api_version}}/votes/{{vote_id}}`
- Headers:
  - `Content-Type: application/json`
  - `Authorization: Bearer {{seed_token}}`
- Body:
  ```json
  { "ideaId": "{{new_idea_id}}" }
  ```
- Scripts:
  - Pre-request: `postman-scripts/08_update_vote_pre.js`
  - Tests: `postman-scripts/08_update_vote_test.js`

### 9) Delete vote (owner only)

- Method: `DELETE`
- URL: `{{base_url}}/api/{{api_version}}/votes/{{vote_id}}`
- Headers: `Authorization: Bearer {{seed_token}}`
- Scripts:
  - Tests: `postman-scripts/09_delete_vote_test.js`

### 10) Verify deletion (public)

- Method: `GET`
- URL: `{{base_url}}/api/{{api_version}}/votes/{{vote_id}}`
- Scripts:
  - Tests: `postman-scripts/10_verify_deletion_test.js`

## Running The Suite

### Postman Collection Runner

1. Open the collection → **Run collection**
2. Iterations: `1`
3. Ensure requests execute in the 1→10 order
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

- Casting a vote returns `201` and stores `{{vote_id}}`.
- Duplicate vote attempt on the same idea returns `409 Conflict` (validated in step 4 tests).
- Updating the vote changes `ideaId` to `{{new_idea_id}}` and moves the vote between “votes by idea” lists.
- Deleting the vote returns `200 OK`.
- `GET /votes/{{vote_id}}` returns `404` after deletion.

