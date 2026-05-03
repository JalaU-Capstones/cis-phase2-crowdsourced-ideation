# Connectivity Tests — Ideas (v1/v2)

This document defines **connectivity-level** integration checks for all Ideas endpoints:
- Validate the endpoint is reachable
- Validate the **expected HTTP status code**
- Avoid deep payload validation (covered in `structure.md`)

The same checks apply to both:
- `{{api_version}} = v1` (MySQL)
- `{{api_version}} = v2` (MongoDB)

## Endpoints And Expected Status Codes

Notes:
- `GET /ideas` returns a **paged object** with `data[]`.
- `GET /ideas/topic/{topicId}` returns a **raw array** (not paged).
- `DELETE /ideas/{id}` returns **`200 OK`** (not `204 No Content`).

| Endpoint | Auth | Expected (happy path) | Common negatives |
|---|---|---:|---|
| `POST /api/{{api_version}}/ideas` | Bearer | `201 Created` | `401` missing/invalid token, `400` missing fields, `403` topic CLOSED, `400` topic not found |
| `GET /api/{{api_version}}/ideas` | No | `200 OK` | `400` invalid query params (`page < 0`, bad `sortBy`, bad `order`) |
| `GET /api/{{api_version}}/ideas/{id}` | No | `200 OK` | `404` not found |
| `GET /api/{{api_version}}/ideas/topic/{topicId}` | No | `200 OK` | (typically returns `200` with `[]` even if topic doesn’t exist) |
| `PUT /api/{{api_version}}/ideas/{id}` | Bearer (owner) | `200 OK` | `401` no token, `403` not owner or topic CLOSED, `404` not found, `400` missing fields |
| `DELETE /api/{{api_version}}/ideas/{id}` | Bearer (owner) | `200 OK` | `401` no token, `403` not owner or topic CLOSED, `404` not found |

## Postman Connectivity Scripts (Minimal)

Use the copy-paste request scripts in:
- `docs/test/ideas/postman-scripts/`

They validate status codes for the 9-step Ideas e2e flow:
1. Login (Phase 1)
2. Create topic (prerequisite for idea)
3. Create idea
4. Get all ideas (paged)
5. Get idea by id
6. Get ideas by topic
7. Update idea
8. Delete idea
9. Verify `404`

## Expected Results

- All endpoints return expected status codes shown above.
- Switching `{{api_version}}` between `v1` and `v2` runs the same suite with no script changes.

