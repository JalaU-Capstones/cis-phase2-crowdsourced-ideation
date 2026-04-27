# Data Integrity Tests — Topics (CRUD + Cascade Delete)

This document defines **data integrity** integration tests for Topics:
- Create → Read → Update → Delete
- Verify deletion is final (`404`)
- Verify cascade delete behavior (ideas + votes removed)

These tests apply to both API versions by switching `{{api_version}}`.

## Primary CRUD Integrity Flow (Automated)

Use the 7-step Postman flow documented in [README.md](./README.md) and implemented in:
- `docs/test/topics/postman-scripts/*.js`

### Steps And Expected Results

1. **Login (Phase 1)**
   - `POST {{phase1_base_url}}/api/v1/auth/login`
   - Expect: `200 OK`
   - Output: `{{seed_token}}` (collection variable)

2. **Create Topic**
   - `POST {{base_url}}/api/{{api_version}}/topics`
   - Auth: `Bearer {{seed_token}}`
   - Expect: `201 Created`
   - Output: `{{topic_id}}`

3. **Read (List)**
   - `GET {{base_url}}/api/{{api_version}}/topics`
   - Expect: `200 OK`
   - Verify: `{{topic_id}}` exists in the returned page `data[]`

4. **Read (By Id)**
   - `GET {{base_url}}/api/{{api_version}}/topics/{{topic_id}}`
   - Expect: `200 OK`
   - Verify: response fields and `_links` exist (see `structure.md`)

5. **Update (Close Topic)**
   - `PUT {{base_url}}/api/{{api_version}}/topics/{{topic_id}}`
   - Auth: `Bearer {{seed_token}}`
   - Expect: `200 OK`
   - Verify:
     - status becomes `CLOSED`
     - `winningIdea` is returned (not null) and `winningIdea.isWinning === true`

   Implementation detail:
   - The pre-request script `05_update_topic_pre.js` creates 2 ideas and 1 vote via `pm.sendRequest()` to ensure `winningIdea` is deterministic.

6. **Delete**
   - `DELETE {{base_url}}/api/{{api_version}}/topics/{{topic_id}}`
   - Auth: `Bearer {{seed_token}}`
   - Expect: `200 OK`
   - Verify: response JSON includes cascade delete message

7. **Verify Deletion**
   - `GET {{base_url}}/api/{{api_version}}/topics/{{topic_id}}`
   - Expect: `404 Not Found`

## Cascade Delete Verification

The Topic service performs a cascade delete:
- deletes topic ideas
- deletes votes for each idea

### What we validate (Topics-only requirement)

The **required** assertion is the final `404` for the topic itself (step 7).

### Optional stronger validation (uses Ideas/Votes endpoints)

If you want to explicitly verify cascade behavior, add these optional checks after step 6 (or in step 6 Tests via `pm.sendRequest()`):

1. `GET {{base_url}}/api/{{api_version}}/ideas/{{idea1_id}}` → expect `404`
2. `GET {{base_url}}/api/{{api_version}}/votes/{{vote1_id}}` → expect `404`

This suite already stores `{{idea1_id}}` and `{{vote1_id}}` during the update preparation step, so you can run those checks without manual copying.

## Expected Results

- Created topic is visible in list and retrievable by id.
- Update persists changes and closes the topic.
- Closing triggers `winningIdea` computation.
- Deleting a topic makes it permanently unavailable (`404`).
- (Optional) Related ideas/votes also return `404` after topic deletion.

