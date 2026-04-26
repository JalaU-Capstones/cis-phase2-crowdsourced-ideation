# Connectivity Tests — Statistics (v1/v2)

This document defines **connectivity-level** integration checks for all Statistics endpoints:
- Validate the endpoint is reachable
- Validate the **expected HTTP status code**
- Avoid deep payload validation (covered in `structure.md`)

The same checks apply to both:
- `{{api_version}} = v1` (MySQL)
- `{{api_version}} = v2` (MongoDB)

## Endpoints And Expected Status Codes

Notes:
- `limit` must be `>= 1`
- `offset` must be `>= 0`
- Statistics endpoints are **public** (no auth required)

| Endpoint | Auth | Expected (happy path) | Common negatives |
|---|---|---:|---|
| `GET /api/{{api_version}}/statistics/top-topics?limit=10&offset=0` | No | `200 OK` | `400` invalid `limit` (`<= 0`), invalid `offset` (`< 0`) |
| `GET /api/{{api_version}}/statistics/most-voted-ideas?limit=10&offset=0` | No | `200 OK` | `400` invalid `limit` (`<= 0`), invalid `offset` (`< 0`) |
| `GET /api/{{api_version}}/statistics/topic/{topicId}/summary` | No | `200 OK` | `404` topic not found, `400` missing/blank `topicId` |

## Postman Connectivity Scripts (Minimal)

Use the provided copy-paste request scripts in:
- `docs/test/statistics/postman-scripts/`

They already validate reachability and status codes as part of the automated flow.

### Example: Invalid `limit` Returns 400

```javascript
pm.sendRequest({
  url: `${pm.collectionVariables.get("base_url")}/api/${pm.collectionVariables.get("api_version")}/statistics/top-topics?limit=0&offset=0`,
  method: "GET"
}, function (err, res) {
  pm.expect(err).to.equal(null);
  pm.expect(res.code).to.equal(400);
});
```

## Expected Results

- All endpoints return the expected status codes shown above.
- When switching `{{api_version}}` between `v1` and `v2`, the same suite passes without modifications.

