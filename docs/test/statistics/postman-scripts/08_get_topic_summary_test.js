// 08_get_topic_summary_test.js
//
// Validations:
// - counts match the seeded dataset (2 ideas, 2 votes)
// - winningIdea is null while topic is OPEN
// - after closing the topic (PUT /topics/{id}), winningIdea becomes non-null
// - cleanup deletes the topic and verifies it is removed from statistics

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Topic summary returns 200", function () {
  pm.response.to.have.status(200);
});

pm.test("Topic summary has correct counts for OPEN topic", function () {
  const json = pm.response.json();
  pm.expect(String(json.topicId)).to.equal(pm.collectionVariables.get("topic_id"));
  pm.expect(json.ideasCount).to.equal(2);
  pm.expect(json.votesCount).to.equal(2);

  // OPEN topic => winner is not computed yet
  pm.expect(json.status).to.equal("OPEN");
  pm.expect(json.winningIdea === null || json.winningIdea === undefined).to.equal(true);

  pm.expect(json.mostVotedIdea).to.exist;
  pm.expect(String(json.mostVotedIdea.ideaId)).to.equal(pm.collectionVariables.get("idea1_id"));
  pm.expect(json.mostVotedIdea.votesCount).to.equal(1);

  pm.expect(json._links).to.be.an("array").and.not.empty;
  const rels = json._links.map(l => l.rel);
  ["self", "topic"].forEach(r => pm.expect(rels, `missing rel=${r}`).to.include(r));
});

function isTrue(v) {
  return String(v || "").toLowerCase() === "true";
}

pm.test("Close topic => winningIdea exists => cleanup removes topic from statistics", function (done) {
  if (isTrue(pm.collectionVariables.get("stats_cleanup_done"))) return done();

  const base = pm.collectionVariables.get("base_url");
  const v = pm.collectionVariables.get("api_version");
  const token = pm.collectionVariables.get("seed_token");
  const topicId = pm.collectionVariables.get("topic_id");

  pm.expect(base).to.be.a("string").and.not.empty;
  pm.expect(v).to.be.a("string").and.not.empty;
  pm.expect(token).to.be.a("string").and.not.empty;
  pm.expect(topicId).to.be.a("string").and.not.empty;

  // 1) Close topic so winner is computed (OPEN -> CLOSED transition).
  pm.sendRequest({
    url: `${base}/api/${v}/topics/${topicId}`,
    method: "PUT",
    header: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${token}`
    },
    body: {
      mode: "raw",
      raw: JSON.stringify({
        title: pm.collectionVariables.get("topic_title"),
        description: pm.collectionVariables.get("topic_description"),
        status: "CLOSED"
      })
    }
  }, function (err1, res1) {
    pm.test("Topic close request succeeds", function () {
      pm.expect(err1).to.equal(null);
      pm.expect(res1.code).to.equal(200);
    });

    // 2) Re-fetch summary and validate winningIdea is present.
    pm.sendRequest({
      url: `${base}/api/${v}/statistics/topic/${topicId}/summary`,
      method: "GET"
    }, function (err2, res2) {
      pm.test("Summary after close returns 200", function () {
        pm.expect(err2).to.equal(null);
        pm.expect(res2.code).to.equal(200);
      });

      const json2 = res2.json();
      pm.test("CLOSED summary includes winningIdea", function () {
        pm.expect(json2.status).to.equal("CLOSED");
        pm.expect(json2.winningIdea).to.exist;
        pm.expect(String(json2.winningIdea.ideaId)).to.equal(pm.collectionVariables.get("idea1_id"));
        pm.expect(String(json2.winningIdea.ideaTitle)).to.equal(pm.collectionVariables.get("idea1_title"));
        pm.expect(json2.winningIdea.votesCount).to.equal(1);

        // In this seeded dataset, winner should match mostVotedIdea.
        pm.expect(String(json2.winningIdea.ideaId)).to.equal(String(json2.mostVotedIdea.ideaId));
      });

      // 3) Cleanup: delete topic (cascade deletes ideas and votes).
      pm.sendRequest({
        url: `${base}/api/${v}/topics/${topicId}`,
        method: "DELETE",
        header: {
          "Authorization": `Bearer ${token}`
        }
      }, function (err3, res3) {
        pm.test("Cleanup delete topic returns 200", function () {
          pm.expect(err3).to.equal(null);
          pm.expect(res3.code).to.equal(200);
        });

        // 4) Verify summary is 404 after deletion.
        pm.sendRequest({
          url: `${base}/api/${v}/statistics/topic/${topicId}/summary`,
          method: "GET"
        }, function (err4, res4) {
          pm.test("Deleted topic summary returns 404", function () {
            pm.expect(err4).to.equal(null);
            pm.expect(res4.code).to.equal(404);
          });

          // 5) Verify top-topics no longer contains the deleted topic.
          pm.sendRequest({
            url: `${base}/api/${v}/statistics/top-topics?limit=100&offset=0`,
            method: "GET"
          }, function (err5, res5) {
            pm.test("Top topics still reachable after cleanup", function () {
              pm.expect(err5).to.equal(null);
              pm.expect(res5.code).to.equal(200);
            });

            const arr = res5.json();
            const found = Array.isArray(arr) && arr.some(t => String(t.topicId) === String(topicId));
            pm.test("Deleted topic is not present in top topics", function () {
              pm.expect(found).to.equal(false);
            });

            pm.collectionVariables.set("stats_cleanup_done", "true");
            done();
          });
        });
      });
    });
  });
});

