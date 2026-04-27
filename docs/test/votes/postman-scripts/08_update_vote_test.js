// 08_update_vote_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Update vote succeeded", function () {
  pm.response.to.have.status(200);
});

pm.test("Vote moved to new idea (ideaId changed)", function () {
  const json = pm.response.json();
  const newIdeaId = pm.collectionVariables.get("new_idea_id");
  pm.expect(String(json.ideaId)).to.equal(String(newIdeaId));
});

pm.test("Vote response reflects new idea title (best-effort)", function () {
  const json = pm.response.json();
  const expectedTitle = pm.collectionVariables.get("new_idea_title");
  if (expectedTitle) {
    pm.expect(String(json.ideaTitle)).to.equal(String(expectedTitle));
  }
});

pm.test("Vote membership moved between votes-by-idea lists", function (done) {
  const base = pm.collectionVariables.get("base_url");
  const v = pm.collectionVariables.get("api_version");
  const voteId = pm.collectionVariables.get("vote_id");
  const oldIdeaId = pm.collectionVariables.get("idea_id");
  const newIdeaId = pm.collectionVariables.get("new_idea_id");

  pm.sendRequest({
    url: `${base}/api/${v}/votes/idea/${oldIdeaId}`,
    method: "GET"
  }, function (err1, res1) {
    pm.expect(err1).to.equal(null);
    pm.expect(res1.code).to.equal(200);
    const arr1 = res1.json();
    const stillThere = (arr1 || []).some(x => x && String(x.id) === String(voteId));
    pm.expect(stillThere, "vote should not be in old idea list").to.equal(false);

    pm.sendRequest({
      url: `${base}/api/${v}/votes/idea/${newIdeaId}`,
      method: "GET"
    }, function (err2, res2) {
      pm.expect(err2).to.equal(null);
      pm.expect(res2.code).to.equal(200);
      const arr2 = res2.json();
      const nowThere = (arr2 || []).some(x => x && String(x.id) === String(voteId));
      pm.expect(nowThere, "vote should be in new idea list").to.equal(true);
      done();
    });
  });
});

