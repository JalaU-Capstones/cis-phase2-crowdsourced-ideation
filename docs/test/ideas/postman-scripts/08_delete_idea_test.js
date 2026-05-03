// 08_delete_idea_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Delete idea succeeded", function () {
  pm.response.to.have.status(200);
});

pm.test("Delete response mentions related votes deleted", function () {
  const json = pm.response.json();
  pm.expect(json).to.have.property("message");
  pm.expect(String(json.message).toLowerCase()).to.include("idea deleted");
  pm.expect(String(json.message).toLowerCase()).to.include("votes");
});

