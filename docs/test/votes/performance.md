# Performance Tests — Votes (< 500 ms)

This document defines response-time assertions for Votes endpoints using Postman.

## Threshold

Default: **< 500 ms** controlled by:
- `perf_threshold_ms` (collection variable, default `500`)

## Postman Snippet (copy-paste)

```javascript
const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});
```

## Endpoints Covered

- `POST /api/{{api_version}}/votes`
- `GET /api/{{api_version}}/votes`
- `GET /api/{{api_version}}/votes/{{vote_id}}`
- `GET /api/{{api_version}}/votes/idea/{{idea_id}}`
- `PUT /api/{{api_version}}/votes/{{vote_id}}`
- `DELETE /api/{{api_version}}/votes/{{vote_id}}`

## Expected Results

- Each endpoint responds under `{{perf_threshold_ms}}` ms in normal dev conditions.

