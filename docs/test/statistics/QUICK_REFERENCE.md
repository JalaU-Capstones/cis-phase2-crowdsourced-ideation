# Statistics Tests — Quick Reference (v1/v2)

Collection variables used:
- `base_url` (Phase 2, default `http://localhost:5257`)
- `api_version` (`v1` or `v2`)
- `phase1_base_url` (Phase 1, default `http://localhost:8080`)
- `seed_login`, `seed_password` → used to get `seed_token`
- `perf_threshold_ms` (default `500`)

Runtime variables produced by the flow:
- `seed_token`
- `topic_id`, `topic_title`
- `idea1_id`, `idea1_title`
- `idea2_id`, `idea2_title`
- `vote1_id`, `vote2_id` (optional)

## Endpoints Summary

| # | Request | Method | URL | Auth | Expected |
|---:|---|---|---|---|---:|
| 1 | Seed user login (Phase 1) | POST | `{{phase1_base_url}}/api/{{api_version}}/auth/login` | No | 200 |
| 2 | Create topic | POST | `{{base_url}}/api/{{api_version}}/topics` | Bearer `{{seed_token}}` | 201 |
| 3 | Create idea #1 | POST | `{{base_url}}/api/{{api_version}}/ideas` | Bearer `{{seed_token}}` | 201 |
| 4 | Cast vote on idea #1 | POST | `{{base_url}}/api/{{api_version}}/votes` | Bearer `{{seed_token}}` | 201 |
| 5 | Create idea #2 + cast vote | POST | `{{base_url}}/api/{{api_version}}/votes` | Bearer `{{seed_token}}` | 201 |
| 6 | Get top topics | GET | `{{base_url}}/api/{{api_version}}/statistics/top-topics?limit=10&offset=0` | No | 200 |
| 7 | Get most voted ideas | GET | `{{base_url}}/api/{{api_version}}/statistics/most-voted-ideas?limit=10&offset=0` | No | 200 |
| 8 | Get topic summary (+ close topic + cleanup) | GET | `{{base_url}}/api/{{api_version}}/statistics/topic/{{topic_id}}/summary` | No | 200 |

## Query Params

Statistics list endpoints support:
- `limit` (default `10`, must be `>= 1`)
- `offset` (default `0`, must be `>= 0`)

## Required Fields (DTO Summary)

### TopTopicDto (`GET /statistics/top-topics`)

Each array item should contain:
- `topicId`, `topicTitle`, `status`
- `ideasCount`, `votesCount`
- `_links[]` with `href`, `method`, `rel` (includes `topic`, `summary`)

### MostVotedIdeaDto (`GET /statistics/most-voted-ideas`)

Each array item should contain:
- `ideaId`, `ideaTitle`, `votesCount`
- `topicId`, `topicTitle`
- `_links[]` (includes `idea`, `topic`)

### TopicSummaryDto (`GET /statistics/topic/{topicId}/summary`)

Response should contain:
- `topicId`, `topicTitle`, `status`
- `ideasCount`, `votesCount`
- `winningIdea` (nullable; present after closing)
- `mostVotedIdea` (nullable; present when ideas exist)
- `_links[]` (includes `self`, `topic`)

