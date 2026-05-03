# Ideas Postman Scripts (Copy/Paste)

This folder contains **ready-to-paste** JavaScript for each request’s:
- **Pre-request Script** tab (`*_pre.js`)
- **Tests** tab (`*_test.js`)

These scripts implement a fully automated **Ideas e2e flow** and work for:
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

## Request Mapping (9 Steps)

1. **01 Seed user login**
   - Pre-request: `01_seed_user_login_pre.js`
   - Tests: `01_seed_user_login_test.js`
2. **02 Create topic for idea**
   - Pre-request: `02_create_topic_for_idea_pre.js`
   - Tests: `02_create_topic_for_idea_test.js`
3. **03 Create idea**
   - Pre-request: `03_create_idea_pre.js`
   - Tests: `03_create_idea_test.js`
4. **04 Get all ideas (public)**
   - Tests: `04_get_all_ideas_public_test.js`
5. **05 Get idea by id (public)**
   - Tests: `05_get_idea_by_id_public_test.js`
6. **06 Get ideas by topic (public)**
   - Tests: `06_get_ideas_by_topic_public_test.js`
7. **07 Update idea**
   - Pre-request: `07_update_idea_pre.js`
   - Tests: `07_update_idea_test.js`
8. **08 Delete idea**
   - Tests: `08_delete_idea_test.js`
9. **09 Verify deletion**
   - Tests: `09_verify_deletion_test.js`

## Request URLs / Bodies (Quick)

1) `POST {{phase1_base_url}}/api/v1/auth/login`
```json
{ "login": "{{seed_login}}", "password": "{{seed_password}}" }
```

2) `POST {{base_url}}/api/{{api_version}}/topics`
```json
{ "title": "{{topic_title}}", "description": "{{topic_description}}" }
```

3) `POST {{base_url}}/api/{{api_version}}/ideas`
```json
{ "topicId": "{{topic_id}}", "title": "{{idea_title}}", "description": "{{idea_description}}" }
```

4) `GET {{base_url}}/api/{{api_version}}/ideas?page=0&size=10&sortBy=updatedAt&order=desc`

5) `GET {{base_url}}/api/{{api_version}}/ideas/{{idea_id}}`

6) `GET {{base_url}}/api/{{api_version}}/ideas/topic/{{topic_id}}`

7) `PUT {{base_url}}/api/{{api_version}}/ideas/{{idea_id}}`
```json
{ "title": "{{idea_title_updated}}", "description": "{{idea_description_updated}}" }
```

8) `DELETE {{base_url}}/api/{{api_version}}/ideas/{{idea_id}}`

9) `GET {{base_url}}/api/{{api_version}}/ideas/{{idea_id}}`

## Optional Helper

`v1_v2_runner_setup.js` is an optional helper you can paste at the **collection level** (Pre-request Script) to set default variables and a run id.

