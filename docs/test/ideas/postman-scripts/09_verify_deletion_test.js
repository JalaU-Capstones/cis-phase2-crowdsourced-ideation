// 09_verify_deletion_test.js
// This test also performs integrity checks using pm.sendRequest():
// - deleting an idea does not delete its topic
// - deleting the topic cascades and deletes its ideas

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Deleted idea returns 404 Not Found", function () {
  pm.response.to.have.status(404);
});

const base = pm.collectionVariables.get("base_url");
const v = pm.collectionVariables.get("api_version");
const token = pm.collectionVariables.get("seed_token");
const topicId = pm.collectionVariables.get("topic_id");

pm.test("Core variables exist for integrity checks", function () {
  pm.expect(base).to.be.a("string").and.not.empty;
  pm.expect(v).to.be.a("string").and.not.empty;
  pm.expect(token).to.be.a("string").and.not.empty;
  pm.expect(topicId).to.be.a("string").and.not.empty;
});

// 1) Deleting the idea must NOT delete the topic.
pm.sendRequest({
  url: `${base}/api/${v}/topics/${topicId}`,
  method: "GET"
}, function (errT, resT) {
  pm.test("Topic still exists after idea deletion", function () {
    pm.expect(errT).to.equal(null);
    pm.expect(resT.code).to.equal(200);
  });

  // 2) Create a new idea under the topic, then delete the topic and verify cascade deletion.
  const newIdeaTitle = `Cascade idea ${Date.now()}`;
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
        title: newIdeaTitle,
        description: "Created to validate cascade delete (topic -> ideas)."
      })
    }
  }, function (errI, resI) {
    pm.test("Setup: created idea for cascade test", function () {
      pm.expect(errI).to.equal(null);
      pm.expect(resI.code).to.equal(201);
    });

    const created = resI.json();
    const cascadeIdeaId = created && created.id ? String(created.id) : "";
    pm.test("Setup: cascade idea id exists", function () {
      pm.expect(cascadeIdeaId).to.be.a("string").and.not.empty;
    });

    pm.sendRequest({
      url: `${base}/api/${v}/topics/${topicId}`,
      method: "DELETE",
      header: { "Authorization": `Bearer ${token}` }
    }, function (errD, resD) {
      pm.test("Deleted topic for cascade test", function () {
        pm.expect(errD).to.equal(null);
        pm.expect(resD.code).to.equal(200);
      });

      pm.sendRequest({
        url: `${base}/api/${v}/ideas/${cascadeIdeaId}`,
        method: "GET"
      }, function (errG, resG) {
        pm.test("Cascade: idea is deleted after topic deletion (404)", function () {
          pm.expect(errG).to.equal(null);
          pm.expect(resG.code).to.equal(404);
        });
      });
    });
  });
});

