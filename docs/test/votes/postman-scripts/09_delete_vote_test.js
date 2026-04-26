// 09_delete_vote_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Delete vote succeeded", function () {
  pm.response.to.have.status(200);
});

pm.test("Delete response includes voteId", function () {
  const json = pm.response.json();
  pm.expect(String(json.voteId)).to.equal(String(pm.collectionVariables.get("vote_id")));
});

