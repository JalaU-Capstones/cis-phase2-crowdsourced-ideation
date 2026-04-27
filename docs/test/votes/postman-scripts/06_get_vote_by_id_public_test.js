// 06_get_vote_by_id_public_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Get vote by id is reachable", function () {
  pm.response.to.have.status(200);
});

pm.test("Vote has required fields", function () {
  const json = pm.response.json();
  ["id", "ideaId", "ideaTitle", "topicId", "topicTitle", "_links"].forEach((k) => pm.expect(json).to.have.property(k));
  pm.expect(json._links).to.be.an("array").and.not.empty;
});

pm.test("Vote HATEOAS links are correct and versioned", function () {
  const json = pm.response.json();
  const v = pm.collectionVariables.get("api_version");
  const voteId = pm.collectionVariables.get("vote_id");
  const ideaId = pm.collectionVariables.get("idea_id");

  function link(rel) {
    return (json._links || []).find(l => l.rel === rel);
  }

  const self = link("self");
  pm.expect(self).to.exist;
  pm.expect(self.method).to.equal("GET");
  pm.expect(self.href).to.include(`api/${v}/votes/idea/${ideaId}`);

  const idea = link("idea");
  pm.expect(idea).to.exist;
  pm.expect(idea.method).to.equal("GET");
  pm.expect(idea.href).to.include(`api/${v}/ideas/${ideaId}`);

  const remove = link("remove");
  pm.expect(remove).to.exist;
  pm.expect(remove.method).to.equal("DELETE");
  pm.expect(remove.href).to.include(`api/${v}/votes/${voteId}`);
});

