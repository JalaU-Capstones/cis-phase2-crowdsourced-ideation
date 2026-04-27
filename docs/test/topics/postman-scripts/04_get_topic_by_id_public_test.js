// 04_get_topic_by_id_public_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Get topic by id is reachable", function () {
  pm.response.to.have.status(200);
});

pm.test("Topic has required fields", function () {
  const json = pm.response.json();
  ["id", "title", "status", "ownerId", "createdAt", "updatedAt", "_links"].forEach((k) => pm.expect(json).to.have.property(k));
  pm.expect(json._links).to.be.an("array").and.not.empty;
});

pm.test("HATEOAS _links contain required relations and correct version", function () {
  const json = pm.response.json();
  const v = pm.collectionVariables.get("api_version");
  const topicId = pm.collectionVariables.get("topic_id");

  function link(rel) {
    return (json._links || []).find(l => l.rel === rel);
  }

  const self = link("self");
  pm.expect(self, "self link").to.exist;
  pm.expect(self.method).to.equal("GET");
  pm.expect(self.href).to.include(`api/${v}/topics/${topicId}`);

  const ideas = link("ideas");
  pm.expect(ideas, "ideas link").to.exist;
  pm.expect(ideas.method).to.equal("GET");
  pm.expect(ideas.href).to.include(`api/${v}/ideas/topic/${topicId}`);

  const update = link("update");
  pm.expect(update, "update link").to.exist;
  pm.expect(update.method).to.equal("PUT");

  const del = link("delete");
  pm.expect(del, "delete link").to.exist;
  pm.expect(del.method).to.equal("DELETE");
});

