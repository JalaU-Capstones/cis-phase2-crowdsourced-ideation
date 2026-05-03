# Data Integrity Tests — Statistics (Counts + Cleanup)

This document defines **data integrity** integration tests for Statistics:
- counts returned by Statistics match the actual seeded data
- after cleanup (topic deletion), the statistics no longer reference the deleted topic

These tests apply to both API versions by switching `{{api_version}}`.

## Automated Integrity Flow (Seed → Query → Cleanup)

Use the Postman flow documented in [README.md](./README.md) and implemented in:
- `docs/test/statistics/postman-scripts/*.js`

### Seeded Dataset (Deterministic)

The suite creates:
- 1 topic → `{{topic_id}}`
- 2 ideas under that topic → `{{idea1_id}}`, `{{idea2_id}}`
- 2 votes (one per idea) → (optional) `{{vote1_id}}`, `{{vote2_id}}`

Expected resulting counts:
- topic has `ideasCount = 2`
- topic has `votesCount = 2`

## Integrity Assertions

### 1) `GET /statistics/topic/{{topic_id}}/summary` counts match data

Expected:
- `ideasCount === 2`
- `votesCount === 2`

Additionally:
- `mostVotedIdea` must be non-null (because there are ideas)
- `winningIdea` is `null` while the topic is `OPEN`

### 2) `GET /statistics/top-topics` reflects the same counts

Find the created topic by id in the array:
- `topicId === {{topic_id}}`
- `ideasCount === 2`
- `votesCount === 2`

### 3) Cleanup: deleting the topic removes it from statistics

The suite performs cleanup via `pm.sendRequest()` (authenticated `DELETE /topics/{{topic_id}}`).

After cleanup:
- `GET /statistics/topic/{{topic_id}}/summary` returns `404`
- `GET /statistics/top-topics` no longer contains `topicId === {{topic_id}}`

## Expected Results

- Statistics counts match the seeded dataset exactly.
- Cleanup is verified, ensuring the suite is repeatable without leaving test data behind.

