// 04_cast_vote_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Idea id and token exist", function () {
  pm.expect(pm.variables.get("__idea_id_present")).to.equal(true);
  pm.expect(pm.variables.get("__seed_token_present")).to.equal(true);
});

pm.test("Vote cast successfully", function () {
  pm.response.to.have.status(201);
  const json = pm.response.json();
  pm.expect(String(json.id)).to.not.equal("");
  pm.collectionVariables.set("vote_id", String(json.id));
});

pm.test("Vote response has required fields and HATEOAS _links", function () {
  const json = pm.response.json();
  ["id", "ideaId", "ideaTitle", "topicId", "topicTitle", "_links"].forEach((k) => pm.expect(json).to.have.property(k));
  pm.expect(json._links).to.be.an("array").and.not.empty;

  const rels = json._links.map(l => l.rel);
  ["self", "idea", "remove"].forEach(r => pm.expect(rels, `missing rel=${r}`).to.include(r));
});

pm.test("Duplicate vote returns 409 Conflict", function (done) {
  const base = pm.collectionVariables.get("base_url");
  const v = pm.collectionVariables.get("api_version");
  const token = pm.collectionVariables.get("seed_token");
  const ideaId = pm.collectionVariables.get("idea_id");

  pm.sendRequest({
    url: `${base}/api/${v}/votes`,
    method: "POST",
    header: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${token}`
    },
    body: { mode: "raw", raw: JSON.stringify({ ideaId: ideaId }) }
  }, function (err, res) {
    pm.expect(err).to.equal(null);
    pm.expect(res.code).to.equal(409);

    // Best-effort checks for the error shape.
    try {
      const j = res.json();
      pm.expect(j).to.have.property("message");
      pm.expect(j).to.have.property("errorCode");
      pm.expect(String(j.errorCode)).to.equal("DUPLICATE_VOTE");
    } catch (_) {
      // Tolerate non-JSON conflict bodies in some environments.
    }
    done();
  });
});

