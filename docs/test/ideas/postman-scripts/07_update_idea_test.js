// 07_update_idea_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Update idea succeeded", function () {
  pm.response.to.have.status(200);
});

pm.test("Idea fields were updated", function () {
  const json = pm.response.json();
  pm.expect(json.title).to.equal(pm.collectionVariables.get("idea_title_updated"));
  pm.expect(json.description).to.equal(pm.collectionVariables.get("idea_description_updated"));
});

pm.test("Response has HATEOAS _links", function () {
  const json = pm.response.json();
  pm.expect(json._links).to.be.an("array").and.not.empty;
  const self = json._links.find(l => l.rel === "self");
  pm.expect(self).to.exist;
});

