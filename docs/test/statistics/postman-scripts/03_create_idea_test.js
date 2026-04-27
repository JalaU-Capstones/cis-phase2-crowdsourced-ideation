// 03_create_idea_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Idea #1 created successfully", function () {
  pm.response.to.have.status(201);
  const json = pm.response.json();

  pm.expect(json).to.have.property("id");
  pm.expect(String(json.id)).to.not.equal("");
  pm.collectionVariables.set("idea1_id", String(json.id));
});

pm.test("Idea #1 fields match request variables", function () {
  const json = pm.response.json();
  pm.expect(json.topicId).to.equal(pm.collectionVariables.get("topic_id"));
  pm.expect(json.title).to.equal(pm.collectionVariables.get("idea1_title"));
  pm.expect(json.description).to.equal(pm.collectionVariables.get("idea1_description"));
  pm.expect(json.isWinning).to.equal(false);
});

pm.test("HATEOAS _links are present (topic OPEN => vote link exists)", function () {
  const json = pm.response.json();
  pm.expect(json._links).to.be.an("array").and.not.empty;

  const rels = json._links.map(l => l.rel);
  ["self", "topic", "votes", "update", "delete"].forEach(r => pm.expect(rels, `missing rel=${r}`).to.include(r));
  pm.expect(rels, "vote rel should exist for OPEN topic").to.include("vote");
});

