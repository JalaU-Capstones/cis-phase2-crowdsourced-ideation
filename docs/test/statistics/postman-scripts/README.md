# Statistics Postman Scripts (Copy/Paste)

This folder contains **ready-to-paste** JavaScript for each request’s:
- **Pre-request Script** tab (`*_pre.js`)
- **Tests** tab (`*_test.js`)

These scripts implement the fully automated **Statistics e2e flow** and work for:
- `api_version=v1` (MySQL)
- `api_version=v2` (MongoDB)

## Collection Variables Required

Set these in your collection before running:
- `base_url` (example: `http://localhost:5257`)
- `api_version` (`v1` or `v2`)
- `phase1_base_url` (example: `http://localhost:8080`)
- `seed_login`
- `seed_password`

Optional:
- `perf_threshold_ms` (default `500`)

## Request Mapping (8 Requests + Automated Cleanup)

Create 8 requests and paste scripts as follows:

1. **01 Seed user login**
   - Pre-request: `01_seed_user_login_pre.js`
   - Tests: `01_seed_user_login_test.js`
2. **02 Create topic**
   - Pre-request: `02_create_topic_pre.js`
   - Tests: `02_create_topic_test.js`
3. **03 Create idea #1**
   - Pre-request: `03_create_idea_pre.js`
   - Tests: `03_create_idea_test.js`
4. **04 Cast vote (idea #1)**
   - Pre-request: `04_cast_vote_pre.js`
   - Tests: `04_cast_vote_test.js`
5. **05 Create idea #2 (setup) + cast vote (idea #2)**
   - Pre-request: `05_cast_second_vote_pre.js` (creates idea #2 via `pm.sendRequest`)
   - Tests: `05_cast_second_vote_test.js`
6. **06 Get top topics (public)**
   - Tests: `06_get_top_topics_test.js`
7. **07 Get most voted ideas (public)**
   - Tests: `07_get_most_voted_ideas_test.js`
8. **08 Get topic summary (public)**
   - Tests: `08_get_topic_summary_test.js` (closes topic + cleanup via `pm.sendRequest`)

## Request URLs / Bodies (Quick)

1) `POST {{phase1_base_url}}/api/{{api_version}}/auth/login`
```json
{ "login": "{{seed_login}}", "password": "{{seed_password}}" }
```

2) `POST {{base_url}}/api/{{api_version}}/topics`
```json
{ "title": "{{topic_title}}", "description": "{{topic_description}}" }
```

3) `POST {{base_url}}/api/{{api_version}}/ideas`
```json
{ "topicId": "{{topic_id}}", "title": "{{idea1_title}}", "description": "{{idea1_description}}" }
```

4) `POST {{base_url}}/api/{{api_version}}/votes`
```json
{ "ideaId": "{{idea1_id}}" }
```

5) `POST {{base_url}}/api/{{api_version}}/votes`
```json
{ "ideaId": "{{idea2_id}}" }
```

6) `GET {{base_url}}/api/{{api_version}}/statistics/top-topics?limit=10&offset=0`

7) `GET {{base_url}}/api/{{api_version}}/statistics/most-voted-ideas?limit=10&offset=0`

8) `GET {{base_url}}/api/{{api_version}}/statistics/topic/{{topic_id}}/summary`

## Optional Helper

`v1_v2_runner_setup.js` can be pasted at the **collection level** (Pre-request Script) to set defaults and a run id.

