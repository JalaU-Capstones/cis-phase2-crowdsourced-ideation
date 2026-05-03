# Data Integrity Tests — Votes (CRUD + Uniqueness + Cascade)

This document defines **data integrity** integration tests for Votes:
- Cast → Read → Update (move) → Delete
- Verify unique vote per user per idea (duplicate vote → `409 Conflict`)
- Verify cascade deletes remove votes when the parent idea (or topic) is deleted

Applies to both `{{api_version}}`.

## Primary Integrity Flow (Automated, 10 Steps)

Use the 10-step Postman flow documented in [README.md](./README.md) and implemented in:
- `docs/test/votes/postman-scripts/*.js`

### Steps And Expected Results

1. **Login (Phase 1)** → `{{seed_token}}`
2. **Create topic** → `{{topic_id}}`
3. **Create idea** → `{{idea_id}}`
4. **Cast vote** → `{{vote_id}}`
   - Expect: `201 Created`
   - Duplicate vote attempt on the same idea (same user) should return `409 Conflict` (validated in step 4 tests).
5. **Get all votes** → includes `{{vote_id}}`
6. **Get vote by id** → `200 OK` and `_links` present
7. **Get votes by idea** → includes `{{vote_id}}`
8. **Update vote (move to another idea)** → `200 OK`, `ideaId` becomes `{{new_idea_id}}`
9. **Delete vote** → `200 OK`
10. **Verify deletion** → `GET /votes/{{vote_id}}` returns `404`

## Uniqueness: One Vote Per User Per Idea

Rule:
- A user can only vote once for the same idea.

Implementation:
- In `postman-scripts/04_cast_vote_test.js`, the test issues a second `POST /votes` to the same `ideaId` and expects:
  - `409 Conflict`
  - response includes `{ message, errorCode: "DUPLICATE_VOTE" }` (best-effort assertion)

## Cascade Delete: Idea/Topic → Votes

Requirement:
- When the parent idea is deleted (or the parent topic is deleted), votes for that idea should be deleted.

Implementation in this suite:
- In `postman-scripts/10_verify_deletion_test.js`, after confirming the main `{{vote_id}}` is deleted:
  1. Creates a fresh idea under the topic
  2. Casts a vote for that idea
  3. Deletes the idea
  4. Verifies `GET /votes/{{cascade_vote_id}}` returns `404`

This validates the “idea → votes” cascade behavior without adding extra runner steps.

## Expected Results

- Votes can be cast, read, moved, and deleted.
- Duplicate vote returns `409`.
- Deleting the parent idea deletes its votes (verified by `404`).

