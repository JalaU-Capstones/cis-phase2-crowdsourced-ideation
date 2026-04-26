// 10_verify_deletion_test.js
// This test also validates cascade delete behavior (idea -> votes) via pm.sendRequest().

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Deleted vote returns 404 Not Found", function () {
  pm.response.to.have.status(404);
});

const base = pm.collectionVariables.get("base_url");
const v = pm.collectionVariables.get("api_version");
const token = pm.collectionVariables.get("seed_token");
const topicId = pm.collectionVariables.get("topic_id");

pm.test("Core variables exist for cascade checks", function () {
  pm.expect(base).to.be.a("string").and.not.empty;
  pm.expect(v).to.be.a("string").and.not.empty;
  pm.expect(token).to.be.a("string").and.not.empty;
  pm.expect(topicId).to.be.a("string").and.not.empty;
});

// Cascade test: create idea -> cast vote -> delete idea -> vote must be 404
pm.sendRequest({
  url: `${base}/api/${v}/ideas`,
  method: "POST",
  header: {
    "Content-Type": "application/json",
    "Authorization": `Bearer ${token}`
  },
  body: {
    mode: "raw",
    raw: JSON.stringify({
      topicId: topicId,
      title: `Vote cascade idea ${Date.now()}`,
      description: "Idea created to validate cascade delete (idea -> votes)."
    })
  }
}, function (errI, resI) {
  pm.test("Setup: created idea for cascade test", function () {
    pm.expect(errI).to.equal(null);
    pm.expect(resI.code).to.equal(201);
  });

  const idea = resI.json();
  const cascadeIdeaId = idea && idea.id ? String(idea.id) : "";
  pm.test("Setup: cascade idea id exists", function () {
    pm.expect(cascadeIdeaId).to.be.a("string").and.not.empty;
  });

  pm.sendRequest({
    url: `${base}/api/${v}/votes`,
    method: "POST",
    header: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${token}`
    },
    body: { mode: "raw", raw: JSON.stringify({ ideaId: cascadeIdeaId }) }
  }, function (errV, resV) {
    pm.test("Setup: cast vote for cascade test", function () {
      pm.expect(errV).to.equal(null);
      pm.expect(resV.code).to.equal(201);
    });

    const vote = resV.json();
    const cascadeVoteId = vote && vote.id ? String(vote.id) : "";
    pm.test("Setup: cascade vote id exists", function () {
      pm.expect(cascadeVoteId).to.be.a("string").and.not.empty;
    });

    pm.sendRequest({
      url: `${base}/api/${v}/ideas/${cascadeIdeaId}`,
      method: "DELETE",
      header: { "Authorization": `Bearer ${token}` }
    }, function (errD, resD) {
      pm.test("Deleted idea for cascade test", function () {
        pm.expect(errD).to.equal(null);
        pm.expect(resD.code).to.equal(200);
      });

      pm.sendRequest({
        url: `${base}/api/${v}/votes/${cascadeVoteId}`,
        method: "GET"
      }, function (errG, resG) {
        pm.test("Cascade: vote is deleted after idea deletion (404)", function () {
          pm.expect(errG).to.equal(null);
          pm.expect(resG.code).to.equal(404);
        });
      });
    });
  });
});

