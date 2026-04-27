// 06_get_ideas_by_topic_public_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Get ideas by topic is reachable", function () {
  pm.response.to.have.status(200);
});

pm.test("Response is an array of ideas", function () {
  const json = pm.response.json();
  pm.expect(json).to.be.an("array");
});

pm.test("Created idea appears in ideas-by-topic list", function () {
  const json = pm.response.json();
  const ideaId = pm.collectionVariables.get("idea_id");
  pm.expect(ideaId, "idea_id").to.be.a("string").and.not.empty;

  const found = (json || []).some(i => i && String(i.id) === String(ideaId));
  pm.expect(found, `idea_id ${ideaId} in array`).to.equal(true);
});

