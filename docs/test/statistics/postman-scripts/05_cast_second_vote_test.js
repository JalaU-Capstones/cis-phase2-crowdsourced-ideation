// 05_cast_second_vote_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Idea #2 id and token exist", function () {
  pm.expect(!!pm.collectionVariables.get("idea2_id")).to.equal(true);
  pm.expect(pm.variables.get("__seed_token_present")).to.equal(true);
});

pm.test("Vote #2 cast successfully", function () {
  pm.response.to.have.status(201);
  const json = pm.response.json();
  pm.expect(String(json.id)).to.not.equal("");
  pm.collectionVariables.set("vote2_id", String(json.id));
});

pm.test("Vote #2 response links and references are correct", function () {
  const json = pm.response.json();
  pm.expect(String(json.ideaId)).to.equal(pm.collectionVariables.get("idea2_id"));
  pm.expect(json._links).to.be.an("array").and.not.empty;

  const rels = json._links.map(l => l.rel);
  ["self", "idea", "remove"].forEach(r => pm.expect(rels, `missing rel=${r}`).to.include(r));
});

