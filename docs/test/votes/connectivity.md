# Connectivity Tests — Votes (v1/v2)

This document defines **connectivity-level** integration checks for all Votes endpoints:
- Validate endpoint reachability
- Validate expected HTTP status codes
- Avoid deep payload validation (covered in `structure.md`)

Applies to both:
- `{{api_version}}=v1` (MySQL)
- `{{api_version}}=v2` (MongoDB)

## Endpoints And Expected Status Codes

Notes:
- `DELETE /votes/{id}` returns `200 OK` (not `204`).
- Conflicts for duplicate votes return `409 Conflict`.

| Endpoint | Auth | Expected (happy path) | Common negatives |
|---|---|---:|---|
| `POST /api/{{api_version}}/votes` | Bearer | `201 Created` | `401` no token, `403` topic CLOSED, `404` idea not found, `409` duplicate vote |
| `GET /api/{{api_version}}/votes` | No | `200 OK` | — |
| `GET /api/{{api_version}}/votes/idea/{ideaId}` | No | `200 OK` | — |
| `GET /api/{{api_version}}/votes/{voteId}` | No | `200 OK` | `404` not found |
| `PUT /api/{{api_version}}/votes/{voteId}` | Bearer (owner) | `200 OK` | `401` no token, `403` not owner or topic CLOSED, `404` vote/idea not found, `409` duplicate vote on target idea |
| `DELETE /api/{{api_version}}/votes/{voteId}` | Bearer (owner) | `200 OK` | `401` no token, `403` not owner or topic CLOSED, `404` not found |

## Expected Results

- All endpoints return the expected status codes above.
- Changing only `{{api_version}}` (`v1` ↔ `v2`) runs the same connectivity suite.

