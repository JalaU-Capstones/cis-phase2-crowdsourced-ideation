// 02_create_topic_for_idea_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Topic created successfully (prerequisite for ideas)", function () {
  pm.response.to.have.status(201);
  const json = pm.response.json();
  pm.expect(json.id).to.be.a("string").and.not.empty;
  pm.collectionVariables.set("topic_id", json.id);
});

pm.test("Topic fields match request variables", function () {
  const json = pm.response.json();
  pm.expect(json.title).to.equal(pm.collectionVariables.get("topic_title"));
  pm.expect(json.description).to.equal(pm.collectionVariables.get("topic_description"));
  pm.expect(json.status).to.equal("OPEN");
});

