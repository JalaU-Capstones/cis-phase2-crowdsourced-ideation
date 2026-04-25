# Performance Tests — Topics (< 500 ms)

This document defines response-time assertions for Topics endpoints using Postman.

## Threshold

The default requirement is **< 500 ms**. This is controlled by a collection variable:

- `perf_threshold_ms` (default: `500`)

## Postman Script Snippet (copy-paste)

Add this to each Topics request in the “Tests” tab (or keep it where already included in the provided scripts):

```javascript
const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});
```

## Endpoints Covered

These should all meet the threshold under normal local/dev conditions:
- `POST /api/{{api_version}}/topics`
- `GET /api/{{api_version}}/topics`
- `GET /api/{{api_version}}/topics/{{topic_id}}`
- `PUT /api/{{api_version}}/topics/{{topic_id}}`
- `DELETE /api/{{api_version}}/topics/{{topic_id}}`

## Expected Results

- Each endpoint response time is below `{{perf_threshold_ms}}` ms.
- If an endpoint exceeds the threshold, the request’s performance test fails and reports the measured response time.

