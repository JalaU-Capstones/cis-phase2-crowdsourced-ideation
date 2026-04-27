// 07_verify_deletion_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Deleted topic returns 404 Not Found", function () {
  pm.response.to.have.status(404);
});

