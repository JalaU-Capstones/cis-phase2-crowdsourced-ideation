// 05_get_idea_by_id_public_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Get idea by id is reachable", function () {
  pm.response.to.have.status(200);
});

pm.test("Idea has required fields", function () {
  const json = pm.response.json();
  ["id", "topicId", "ownerId", "title", "description", "createdAt", "updatedAt", "isWinning", "_links"].forEach((k) => pm.expect(json).to.have.property(k));
  pm.expect(json._links).to.be.an("array").and.not.empty;
});

pm.test("HATEOAS _links contain required relations and correct version", function () {
  const json = pm.response.json();
  const v = pm.collectionVariables.get("api_version");
  const ideaId = pm.collectionVariables.get("idea_id");
  const topicId = pm.collectionVariables.get("topic_id");

  function link(rel) {
    return (json._links || []).find(l => l.rel === rel);
  }

  const self = link("self");
  pm.expect(self, "self link").to.exist;
  pm.expect(self.method).to.equal("GET");
  pm.expect(self.href).to.include(`api/${v}/ideas/${ideaId}`);

  const topic = link("topic");
  pm.expect(topic, "topic link").to.exist;
  pm.expect(topic.method).to.equal("GET");
  pm.expect(topic.href).to.include(`api/${v}/topics/${topicId}`);

  const votes = link("votes");
  pm.expect(votes, "votes link").to.exist;
  pm.expect(votes.method).to.equal("GET");
  pm.expect(votes.href).to.include(`api/${v}/votes/idea/${ideaId}`);

  const vote = link("vote");
  pm.expect(vote, "vote link (topic OPEN)").to.exist;
  pm.expect(vote.method).to.equal("POST");
  pm.expect(vote.href).to.include(`api/${v}/votes`);

  const update = link("update");
  pm.expect(update, "update link").to.exist;
  pm.expect(update.method).to.equal("PUT");

  const del = link("delete");
  pm.expect(del, "delete link").to.exist;
  pm.expect(del.method).to.equal("DELETE");
});

