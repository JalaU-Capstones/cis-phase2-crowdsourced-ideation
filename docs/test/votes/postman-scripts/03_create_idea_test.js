// 03_create_idea_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Idea created successfully (prerequisite for votes)", function () {
  pm.response.to.have.status(201);
  const json = pm.response.json();
  pm.expect(String(json.id)).to.not.equal("");
  pm.collectionVariables.set("idea_id", String(json.id));
});

pm.test("Idea belongs to created topic", function () {
  const json = pm.response.json();
  pm.expect(json.topicId).to.equal(pm.collectionVariables.get("topic_id"));
  pm.expect(json.title).to.equal(pm.collectionVariables.get("idea_title"));
});

