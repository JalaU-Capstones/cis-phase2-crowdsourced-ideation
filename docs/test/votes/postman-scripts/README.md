# Votes Postman Scripts (Copy/Paste)

This folder contains **ready-to-paste** JavaScript for each request’s:
- **Pre-request Script** tab (`*_pre.js`)
- **Tests** tab (`*_test.js`)

These scripts implement the fully automated **Votes e2e flow** and work for:
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

## Request Mapping (10 Steps)

1. `01_seed_user_login_*`
2. `02_create_topic_for_vote_*`
3. `03_create_idea_*`
4. `04_cast_vote_*`
5. `05_get_all_votes_public_test.js`
6. `06_get_vote_by_id_public_test.js`
7. `07_get_votes_by_idea_public_test.js`
8. `08_update_vote_*` (pre script creates second idea)
9. `09_delete_vote_test.js`
10. `10_verify_deletion_test.js`

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

4) `POST {{base_url}}/api/{{api_version}}/votes`
```json
{ "ideaId": "{{idea_id}}" }
```

8) `PUT {{base_url}}/api/{{api_version}}/votes/{{vote_id}}`
```json
{ "ideaId": "{{new_idea_id}}" }
```

## Optional Helper

`v1_v2_runner_setup.js` can be pasted at the **collection level** (Pre-request Script) to set defaults and a run id.

