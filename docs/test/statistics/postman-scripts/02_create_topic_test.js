// 02_create_topic_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Topic created successfully", function () {
  pm.response.to.have.status(201);
  const json = pm.response.json();

  pm.expect(json).to.have.property("id");
  pm.expect(json.id).to.be.a("string").and.not.empty;

  pm.collectionVariables.set("topic_id", String(json.id));
});

pm.test("Topic fields match request variables", function () {
  const json = pm.response.json();
  pm.expect(json.title).to.equal(pm.collectionVariables.get("topic_title"));
  pm.expect(json.description).to.equal(pm.collectionVariables.get("topic_description"));
  pm.expect(json.status).to.equal("OPEN");
});

pm.test("Response has HATEOAS _links (OPEN topic)", function () {
  const json = pm.response.json();
  pm.expect(json._links).to.be.an("array").and.not.empty;

  const rels = json._links.map(l => l.rel);
  ["self", "ideas", "update", "delete"].forEach(r => pm.expect(rels, `missing rel=${r}`).to.include(r));
  pm.expect(rels, "winner rel should not exist for OPEN topic").to.not.include("winner");
});

