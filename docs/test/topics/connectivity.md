# Connectivity Tests — Topics (v1/v2)

This document defines **connectivity-level** integration checks for all Topics endpoints:
- Validate the endpoint is reachable
- Validate the **expected HTTP status code**
- Avoid deep payload validation (covered in `structure.md`)

The same checks apply to both:
- `{{api_version}} = v1` (MySQL)
- `{{api_version}} = v2` (MongoDB)

## Endpoints And Expected Status Codes

Notes:
- `GET /topics` returns a **paged object** with `data[]` (not a raw array).
- `DELETE /topics/{id}` returns **`200 OK`** (not `204 No Content`).

| Endpoint | Auth | Expected (happy path) | Common negatives |
|---|---|---:|---|
| `POST /api/{{api_version}}/topics` | Bearer | `201 Created` | `401` missing/invalid token, `400` invalid title/status |
| `GET /api/{{api_version}}/topics` | No | `200 OK` | `400` invalid query params (`page < 0`, bad `status`, bad `sortBy`, bad `order`) |
| `GET /api/{{api_version}}/topics/{id}` | No | `200 OK` | `404` not found |
| `PUT /api/{{api_version}}/topics/{id}` | Bearer (owner) | `200 OK` | `401` no token, `403` not owner, `400` invalid transition, `404` not found |
| `DELETE /api/{{api_version}}/topics/{id}` | Bearer (owner) | `200 OK` | `401` no token, `403` not owner, `404` not found |

## Postman Connectivity Scripts (Minimal)

Use the provided copy-paste request scripts in:
- `docs/test/topics/postman-scripts/`

They already validate status codes for the 7-step e2e Topics flow:
1. Login (Phase 1)
2. Create topic
3. Get all topics
4. Get topic by id
5. Update topic (close it)
6. Delete topic
7. Verify `404`

### Example: GET Topic By Id (Public)

Postman Tests tab:

```javascript
pm.test("GET topic by id is reachable", function () {
  pm.response.to.have.status(200);
});
```

### Example: Verify 404 After Deletion

```javascript
pm.test("Deleted topic returns 404", function () {
  pm.response.to.have.status(404);
});
```

## Expected Results

- All endpoints return the expected status codes shown above.
- When switching `{{api_version}}` between `v1` and `v2`, the same suite passes without modifications.

