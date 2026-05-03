# Statistics Integration Tests (Postman) — CIS Phase 2 (v1/v2)

This folder documents and provides copy-paste Postman scripts for **integration-level** testing of the **Statistics** feature in the CIS Phase 2 Minimal API.

The API has two versions that expose the same Statistics contract:

- `v1` → MySQL persistence (`/api/v1/*`)
- `v2` → MongoDB persistence (`/api/v2/*`)

Statistics endpoints are **public** (no authentication required), but the suite seeds data (topic + ideas + votes) using authenticated endpoints before executing statistics queries.

## Scope

Included:
- `GET /api/{{api_version}}/statistics/top-topics?limit=&offset=`
- `GET /api/{{api_version}}/statistics/most-voted-ideas?limit=&offset=`
- `GET /api/{{api_version}}/statistics/topic/{{topic_id}}/summary`
- Validation types:
  - connectivity/status codes (`connectivity.md`)
  - schema + HATEOAS `_links` (`structure.md`)
  - data integrity counts and cleanup verification (`integrity.md`)
  - sorting/limits/offsets and topic-closure behavior (`functionality.md`)
  - response time assertions (`performance.md`)

## Prerequisites

1. **Phase 2 API** running (this repo)
   - Default URL from `RUNNING.md`: `http://localhost:5257`
2. **Phase 1 User Management API** running (for JWT issuance)
   - `http://localhost:8080`
   - Login endpoint used by the scripts: `POST /api/v1/auth/login`
3. Datastores (if running locally):
   - MySQL for v1 (docker): `localhost:3307`
   - MongoDB for v2: `localhost:27017`

## Postman Setup (Collection Variables)

Create a Postman Collection (for example: `CIS Phase 2 - Statistics`) and add these **collection variables**:

| Variable | Example | Required | Used for |
|---|---:|:---:|---|
| `base_url` | `http://localhost:5257` | Yes | Phase 2 API base |
| `api_version` | `v1` or `v2` | Yes | Switch persistence version |
| `phase1_base_url` | `http://localhost:8080` | Yes | Phase 1 token issuance |
| `seed_login` | `testuser` | Yes | Phase 1 login |
| `seed_password` | `password123` | Yes | Phase 1 login |
| `perf_threshold_ms` | `500` | No | Response time threshold (ms) |

Runtime variables produced by the flow:
- `seed_token`
- `topic_id`
- `idea1_id`, `idea2_id`
- (optional) `vote1_id`, `vote2_id`

## Requests To Create In Your Collection (8 Requests, Fully Automated Cleanup)

Create these 8 requests in this order, and paste the matching scripts from `docs/test/statistics/postman-scripts/`.

Note on cleanup:
- The suite performs **topic closure** and **cascade cleanup** (topic delete) automatically via `pm.sendRequest()` inside step 8’s test script.

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

### 2) Create a topic (authenticated)

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

### 3) Create idea #1 (authenticated)

- Method: `POST`
- URL: `{{base_url}}/api/{{api_version}}/ideas`
- Headers:
  - `Content-Type: application/json`
  - `Authorization: Bearer {{seed_token}}`
- Body (raw JSON):
  ```json
  { "topicId": "{{topic_id}}", "title": "{{idea1_title}}", "description": "{{idea1_description}}" }
  ```
- Scripts:
  - Pre-request: `postman-scripts/03_create_idea_pre.js`
  - Tests: `postman-scripts/03_create_idea_test.js`

### 4) Cast vote on idea #1 (authenticated)

- Method: `POST`
- URL: `{{base_url}}/api/{{api_version}}/votes`
- Headers:
  - `Content-Type: application/json`
  - `Authorization: Bearer {{seed_token}}`
- Body (raw JSON):
  ```json
  { "ideaId": "{{idea1_id}}" }
  ```
- Scripts:
  - Pre-request: `postman-scripts/04_cast_vote_pre.js`
  - Tests: `postman-scripts/04_cast_vote_test.js`

### 5) Create idea #2 (setup) + cast vote on idea #2 (authenticated)

- Method: `POST`
- URL: `{{base_url}}/api/{{api_version}}/votes`
- Headers:
  - `Content-Type: application/json`
  - `Authorization: Bearer {{seed_token}}`
- Body (raw JSON):
  ```json
  { "ideaId": "{{idea2_id}}" }
  ```
- Scripts:
  - Pre-request: `postman-scripts/05_cast_second_vote_pre.js` (creates idea #2 via `pm.sendRequest()` and sets `{{idea2_id}}`)
  - Tests: `postman-scripts/05_cast_second_vote_test.js`

### 6) Get top topics (public)

- Method: `GET`
- URL: `{{base_url}}/api/{{api_version}}/statistics/top-topics?limit=10&offset=0`
- Scripts:
  - Tests: `postman-scripts/06_get_top_topics_test.js`

### 7) Get most voted ideas (public)

- Method: `GET`
- URL: `{{base_url}}/api/{{api_version}}/statistics/most-voted-ideas?limit=10&offset=0`
- Scripts:
  - Tests: `postman-scripts/07_get_most_voted_ideas_test.js`

### 8) Get topic summary (public) + close topic + cleanup (automated)

- Method: `GET`
- URL: `{{base_url}}/api/{{api_version}}/statistics/topic/{{topic_id}}/summary`
- Scripts:
  - Tests: `postman-scripts/08_get_topic_summary_test.js`

## Running The Suite

### Postman Collection Runner

1. Open the collection → **Run collection**
2. Iterations: `1`
3. Ensure requests execute in the 1→8 order
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

- After seeding, the created topic has `ideasCount=2` and `votesCount=2`.
- `GET /top-topics` includes the created topic and returns sorted data.
- `GET /most-voted-ideas` contains both created ideas and returns sorted data.
- `GET /topic/{topicId}/summary` returns:
  - `ideasCount=2`, `votesCount=2`
  - `winningIdea=null` while the topic is `OPEN`
  - after the suite closes the topic, `winningIdea` becomes non-null
- Cleanup deletes the topic and verifies the topic is removed from statistics results.
