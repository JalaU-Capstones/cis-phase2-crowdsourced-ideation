// 07_get_votes_by_idea_public_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Get votes by idea is reachable", function () {
  pm.response.to.have.status(200);
});

pm.test("Response is an array and contains created vote", function () {
  const json = pm.response.json();
  pm.expect(json).to.be.an("array");

  const voteId = pm.collectionVariables.get("vote_id");
  const found = json.some(v => v && String(v.id) === String(voteId));
  pm.expect(found, `vote_id ${voteId} in votes-by-idea`).to.equal(true);
});

