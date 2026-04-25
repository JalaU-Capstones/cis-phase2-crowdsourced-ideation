# Ideas Tests — Quick Reference (v1/v2)

Collection variables required:
- `base_url` (Phase 2, default `http://localhost:5257`)
- `api_version` (`v1` or `v2`)
- `phase1_base_url` (Phase 1, default `http://localhost:8080`)
- `seed_login`, `seed_password`
- `perf_threshold_ms` (default `500`, optional)

Runtime variables produced:
- `seed_token`
- `topic_id`, `topic_title`
- `idea_id`, `idea_title`, `idea_ownerId`
- `idea_title_updated`, `idea_description_updated`

## Endpoints Summary (E2E Flow)

| # | Request | Method | URL | Auth | Expected |
|---:|---|---|---|---|---:|
| 1 | Seed user login (Phase 1) | POST | `{{phase1_base_url}}/api/v1/auth/login` | No | 200 |
| 2 | Create topic for idea | POST | `{{base_url}}/api/{{api_version}}/topics` | Bearer `{{seed_token}}` | 201 |
| 3 | Create idea | POST | `{{base_url}}/api/{{api_version}}/ideas` | Bearer `{{seed_token}}` | 201 |
| 4 | Get all ideas (paged) | GET | `{{base_url}}/api/{{api_version}}/ideas?page=0&size=10&sortBy=updatedAt&order=desc` | No | 200 |
| 5 | Get idea by id | GET | `{{base_url}}/api/{{api_version}}/ideas/{{idea_id}}` | No | 200 |
| 6 | Get ideas by topic | GET | `{{base_url}}/api/{{api_version}}/ideas/topic/{{topic_id}}` | No | 200 |
| 7 | Update idea | PUT | `{{base_url}}/api/{{api_version}}/ideas/{{idea_id}}` | Bearer `{{seed_token}}` | 200 |
| 8 | Delete idea | DELETE | `{{base_url}}/api/{{api_version}}/ideas/{{idea_id}}` | Bearer `{{seed_token}}` | 200 |
| 9 | Verify deletion | GET | `{{base_url}}/api/{{api_version}}/ideas/{{idea_id}}` | No | 404 |

## Ideas Query Params (GET /ideas)

Optional:
- `page` (default `0`, must be `>= 0`)
- `size` (default `10`, must be `>= 1`)
- `sortBy` (`updatedAt` only)
- `order` (`asc`, `desc`)

## Required Fields (IdeaResponse)

Each idea response includes:
- `id`, `topicId`, `ownerId`
- `title`, `description`
- `createdAt`, `updatedAt`
- `isWinning`
- `_links[]` with `href`, `method`, `rel`

