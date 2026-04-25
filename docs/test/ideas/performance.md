# Performance Tests — Ideas (< 500 ms)

This document defines response-time assertions for Ideas endpoints using Postman.

## Threshold

The default requirement is **< 500 ms**, controlled by a collection variable:

- `perf_threshold_ms` (default: `500`)

## Postman Script Snippet (copy-paste)

Add this to each Ideas request in the “Tests” tab (or keep it where already included in the provided scripts):

```javascript
const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});
```

## Endpoints Covered

- `POST /api/{{api_version}}/ideas`
- `GET /api/{{api_version}}/ideas`
- `GET /api/{{api_version}}/ideas/{{idea_id}}`
- `GET /api/{{api_version}}/ideas/topic/{{topic_id}}`
- `PUT /api/{{api_version}}/ideas/{{idea_id}}`
- `DELETE /api/{{api_version}}/ideas/{{idea_id}}`

## Expected Results

- Each endpoint response time is below `{{perf_threshold_ms}}` ms.
- If an endpoint is slower, the test fails and reports the measured response time.

