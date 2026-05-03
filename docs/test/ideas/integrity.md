# Data Integrity Tests — Ideas (CRUD + Cascade Delete)

This document defines **data integrity** integration tests for Ideas:
- Create → Read → Update → Delete
- Verify deletion is final (`404`)
- Verify cascade delete behaviors:
  - Deleting a **topic** deletes its ideas (and votes for those ideas)
  - Deleting an **idea** does **not** delete the topic

All tests apply to both versions by switching `{{api_version}}`.

## Primary CRUD Integrity Flow (Automated, 9 Steps)

Use the 9-step Postman flow documented in [README.md](./README.md) and implemented in:
- `docs/test/ideas/postman-scripts/*.js`

### Steps And Expected Results

1. **Login (Phase 1)** → `{{seed_token}}`
   - `POST {{phase1_base_url}}/api/v1/auth/login`
   - Expect: `200 OK`

2. **Create topic (for the idea)** → `{{topic_id}}`
   - `POST {{base_url}}/api/{{api_version}}/topics`
   - Expect: `201 Created`

3. **Create idea** → `{{idea_id}}`
   - `POST {{base_url}}/api/{{api_version}}/ideas`
   - Expect: `201 Created`

4. **Read list (paged)**
   - `GET {{base_url}}/api/{{api_version}}/ideas`
   - Expect: `200 OK`
   - Verify: created idea appears in `data[]`

5. **Read by id**
   - `GET {{base_url}}/api/{{api_version}}/ideas/{{idea_id}}`
   - Expect: `200 OK`

6. **Read by topic**
   - `GET {{base_url}}/api/{{api_version}}/ideas/topic/{{topic_id}}`
   - Expect: `200 OK`
   - Verify: created idea is returned in the array

7. **Update**
   - `PUT {{base_url}}/api/{{api_version}}/ideas/{{idea_id}}`
   - Expect: `200 OK`
   - Verify: title/description changed

8. **Delete**
   - `DELETE {{base_url}}/api/{{api_version}}/ideas/{{idea_id}}`
   - Expect: `200 OK`

9. **Verify deletion**
   - `GET {{base_url}}/api/{{api_version}}/ideas/{{idea_id}}`
   - Expect: `404 Not Found`

## Cascade Delete: Topic -> Ideas

Requirement:
- When the parent topic is deleted, all its ideas are deleted.

Implementation in this suite:
- After step 9 confirms the original idea is deleted, the script creates a **new idea** under the same `{{topic_id}}` (via `pm.sendRequest()`), deletes the topic, then verifies that the new idea returns `404`.

This cascade check is performed entirely automatically in:
- `postman-scripts/09_verify_deletion_test.js`

## Integrity: Deleting An Idea Does Not Delete The Topic

Requirement:
- Deleting an idea must not delete its topic.

Implementation in this suite:
- After step 8 deletes the idea, step 9 uses `pm.sendRequest()` to `GET /topics/{{topic_id}}` and expects `200 OK` before running the cascade deletion check.

## Expected Results

- Ideas support full CRUD.
- Deleting an idea does not remove its topic.
- Deleting the topic removes any ideas under that topic (verified via `404` on the idea after topic deletion).

