// 06_delete_topic_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Delete topic succeeded", function () {
  pm.response.to.have.status(200);
});

pm.test("Delete response mentions cascade delete", function () {
  const json = pm.response.json();
  pm.expect(json).to.have.property("message");
  pm.expect(String(json.message).toLowerCase()).to.include("deleted");
  pm.expect(String(json.message).toLowerCase()).to.include("ideas");
  pm.expect(String(json.message).toLowerCase()).to.include("votes");
});

pm.test("Delete response includes deleted topicId", function () {
  const json = pm.response.json();
  pm.expect(String(json.topicId)).to.equal(String(pm.collectionVariables.get("topic_id")));
});

