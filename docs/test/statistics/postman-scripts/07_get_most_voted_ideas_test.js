// 07_get_most_voted_ideas_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Most voted ideas returns 200", function () {
  pm.response.to.have.status(200);
});

pm.test("Response is an array", function () {
  const jsonData = pm.response.json();
  pm.expect(jsonData).to.be.an("array");
});

pm.test("Contains idea #1 and idea #2 with correct vote counts", function () {
  const arr = pm.response.json();
  const idea1 = arr.find(i => String(i.ideaId) === String(pm.collectionVariables.get("idea1_id")));
  const idea2 = arr.find(i => String(i.ideaId) === String(pm.collectionVariables.get("idea2_id")));

  pm.expect(idea1, "idea #1 not found").to.exist;
  pm.expect(idea2, "idea #2 not found").to.exist;

  pm.expect(idea1.votesCount).to.equal(1);
  pm.expect(idea2.votesCount).to.equal(1);

  [idea1, idea2].forEach((i) => {
    pm.expect(i._links).to.be.an("array").and.not.empty;
    const rels = i._links.map(l => l.rel);
    ["idea", "topic"].forEach(r => pm.expect(rels, `missing rel=${r}`).to.include(r));
  });
});

function compareMostVotedIdeas(a, b) {
  if (a.votesCount !== b.votesCount) return b.votesCount - a.votesCount;
  return String(a.ideaTitle).localeCompare(String(b.ideaTitle));
}

pm.test("Most voted ideas sorted by votesCount desc then ideaTitle asc", function () {
  const arr = pm.response.json();
  for (let i = 1; i < arr.length; i++) {
    pm.expect(compareMostVotedIdeas(arr[i - 1], arr[i]) <= 0, `order violated at i=${i}`).to.equal(true);
  }
});

pm.test("Invalid limit/offset return 400", function (done) {
  const base = pm.collectionVariables.get("base_url");
  const v = pm.collectionVariables.get("api_version");

  pm.sendRequest(`${base}/api/${v}/statistics/most-voted-ideas?limit=0&offset=0`, function (err1, res1) {
    pm.expect(err1).to.equal(null);
    pm.expect(res1.code).to.equal(400);

    pm.sendRequest(`${base}/api/${v}/statistics/most-voted-ideas?limit=10&offset=-1`, function (err2, res2) {
      pm.expect(err2).to.equal(null);
      pm.expect(res2.code).to.equal(400);
      done();
    });
  });
});

