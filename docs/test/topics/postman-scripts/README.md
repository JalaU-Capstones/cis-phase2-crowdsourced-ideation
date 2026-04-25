# Topics Postman Scripts (Copy/Paste)

This folder contains **ready-to-paste** JavaScript for each request’s:
- **Pre-request Script** tab (`*_pre.js`)
- **Tests** tab (`*_test.js`)

These scripts implement the **fully automated Topics e2e flow** and work for:
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

## Request Mapping

Create 7 requests and paste scripts as follows:

1. **01 Seed user login**
   - Pre-request: `01_seed_user_login_pre.js`
   - Tests: `01_seed_user_login_test.js`
2. **02 Create topic**
   - Pre-request: `02_create_topic_pre.js`
   - Tests: `02_create_topic_test.js`
3. **03 Get all topics (public)**
   - Tests: `03_get_all_topics_public_test.js`
4. **04 Get topic by id (public)**
   - Tests: `04_get_topic_by_id_public_test.js`
5. **05 Update topic (close)**
   - Pre-request: `05_update_topic_pre.js`
   - Tests: `05_update_topic_test.js`
6. **06 Delete topic**
   - Tests: `06_delete_topic_test.js`
7. **07 Verify deletion**
   - Tests: `07_verify_deletion_test.js`

## Request URLs / Bodies (Quick)

1) `POST {{phase1_base_url}}/api/{{api_version}}/auth/login`
```json
{ "login": "{{seed_login}}", "password": "{{seed_password}}" }
```

2) `POST {{base_url}}/api/{{api_version}}/topics`
```json
{ "title": "{{topic_title}}", "description": "{{topic_description}}" }
```

3) `GET {{base_url}}/api/{{api_version}}/topics?page=0&size=10&sortBy=createdAt&order=desc`

4) `GET {{base_url}}/api/{{api_version}}/topics/{{topic_id}}`

5) `PUT {{base_url}}/api/{{api_version}}/topics/{{topic_id}}`
```json
{ "title": "{{topic_title_updated}}", "description": "{{topic_description_updated}}", "status": "CLOSED" }
```

6) `DELETE {{base_url}}/api/{{api_version}}/topics/{{topic_id}}`

7) `GET {{base_url}}/api/{{api_version}}/topics/{{topic_id}}`

## Optional Helper

`v1_v2_runner_setup.js` is an optional helper you can paste at the **collection level** (Pre-request Script) to set defaults and clear run variables.

