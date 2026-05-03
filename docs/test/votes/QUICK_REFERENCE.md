# Votes Tests — Quick Reference (v1/v2)

Collection variables required:
- `base_url` (Phase 2, default `http://localhost:5257`)
- `api_version` (`v1` or `v2`)
- `phase1_base_url` (Phase 1, default `http://localhost:8080`)
- `seed_login`, `seed_password`
- `perf_threshold_ms` (default `500`, optional)

Runtime variables produced:
- `seed_token`
- `topic_id`, `topic_title`
- `idea_id`, `idea_title`
- `vote_id`
- `new_idea_id`, `new_idea_title`

## Endpoints Summary (E2E Flow)

| # | Request | Method | URL | Auth | Expected |
|---:|---|---|---|---|---:|
| 1 | Seed user login (Phase 1) | POST | `{{phase1_base_url}}/api/v1/auth/login` | No | 200 |
| 2 | Create topic | POST | `{{base_url}}/api/{{api_version}}/topics` | Bearer `{{seed_token}}` | 201 |
| 3 | Create idea | POST | `{{base_url}}/api/{{api_version}}/ideas` | Bearer `{{seed_token}}` | 201 |
| 4 | Cast vote | POST | `{{base_url}}/api/{{api_version}}/votes` | Bearer `{{seed_token}}` | 201 |
| 5 | Get all votes | GET | `{{base_url}}/api/{{api_version}}/votes` | No | 200 |
| 6 | Get vote by id | GET | `{{base_url}}/api/{{api_version}}/votes/{{vote_id}}` | No | 200 |
| 7 | Get votes by idea | GET | `{{base_url}}/api/{{api_version}}/votes/idea/{{idea_id}}` | No | 200 |
| 8 | Update vote (move) | PUT | `{{base_url}}/api/{{api_version}}/votes/{{vote_id}}` | Bearer `{{seed_token}}` | 200 |
| 9 | Delete vote | DELETE | `{{base_url}}/api/{{api_version}}/votes/{{vote_id}}` | Bearer `{{seed_token}}` | 200 |
| 10 | Verify deletion | GET | `{{base_url}}/api/{{api_version}}/votes/{{vote_id}}` | No | 404 |

## Required Fields (VoteResponse)

Each vote response includes:
- `id`
- `ideaId`, `ideaTitle`
- `topicId`, `topicTitle`
- `_links[]` with `href`, `method`, `rel`

HATEOAS relations:
- `self` (GET votes-by-idea resource)
- `idea` (GET idea)
- `remove` (DELETE vote)

