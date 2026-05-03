# Topics Tests — Quick Reference (v1/v2)

Collection variables used:
- `base_url` (Phase 2, default `http://localhost:5257`)
- `api_version` (`v1` or `v2`)
- `phase1_base_url` (Phase 1, default `http://localhost:8080`)
- `seed_login`, `seed_password` → used to get `seed_token`
- `perf_threshold_ms` (default `500`)

Runtime variables produced by the flow:
- `seed_token`
- `topic_id`, `topic_title`, `topic_ownerId`
- `topic_title_updated`, `topic_description_updated`
- `idea1_id`, `idea2_id`, `vote1_id`, `expected_winning_idea_id`

## Endpoints Summary

| # | Request | Method | URL | Auth | Expected |
|---:|---|---|---|---|---:|
| 1 | Seed user login (Phase 1) | POST | `{{phase1_base_url}}/api/v1/auth/login` | No | 200 |
| 2 | Create topic | POST | `{{base_url}}/api/{{api_version}}/topics` | Bearer `{{seed_token}}` | 201 |
| 3 | Get all topics (paged) | GET | `{{base_url}}/api/{{api_version}}/topics?page=0&size=10&sortBy=createdAt&order=desc` | No | 200 |
| 4 | Get topic by id | GET | `{{base_url}}/api/{{api_version}}/topics/{{topic_id}}` | No | 200 |
| 5 | Update topic (close) | PUT | `{{base_url}}/api/{{api_version}}/topics/{{topic_id}}` | Bearer `{{seed_token}}` | 200 |
| 6 | Delete topic | DELETE | `{{base_url}}/api/{{api_version}}/topics/{{topic_id}}` | Bearer `{{seed_token}}` | 200 |
| 7 | Verify deletion | GET | `{{base_url}}/api/{{api_version}}/topics/{{topic_id}}` | No | 404 |

## Topics Query Params (GET /topics)

All optional:
- `page` (default `0`, must be `>= 0`)
- `size` (default `10`, must be `>= 1`)
- `status` (`OPEN` or `CLOSED`)
- `ownerId` (string)
- `sortBy` (`createdAt`, `title`, `updatedAt`)
- `order` (`asc`, `desc`)

## Required Fields (TopicResponse)

Each topic response should contain:
- `id`, `title`, `description`
- `status` (`OPEN|CLOSED`)
- `ownerId`
- `createdAt`, `updatedAt`
- `winningIdea` (nullable; present when CLOSED and ideas exist)
- `_links[]` with `href`, `method`, `rel`

