// 04_get_all_ideas_public_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Get all ideas is reachable (paged)", function () {
  pm.response.to.have.status(200);
});

pm.test("Response is a paged object with data[]", function () {
  const json = pm.response.json();
  pm.expect(json).to.have.property("data");
  pm.expect(json.data).to.be.an("array");
  ["currentPage", "pageSize", "totalItems", "totalPages"].forEach((k) => pm.expect(json).to.have.property(k));
});

pm.test("Created idea appears in list data[]", function () {
  const json = pm.response.json();
  const ideaId = pm.collectionVariables.get("idea_id");
  pm.expect(ideaId, "idea_id").to.be.a("string").and.not.empty;

  const found = (json.data || []).some(i => i && String(i.id) === String(ideaId));
  pm.expect(found, `idea_id ${ideaId} in page data[]`).to.equal(true);
});

