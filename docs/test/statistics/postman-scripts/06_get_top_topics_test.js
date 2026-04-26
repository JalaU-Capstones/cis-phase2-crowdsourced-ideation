// 06_get_top_topics_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Top topics returns 200", function () {
  pm.response.to.have.status(200);
});

pm.test("Response is an array", function () {
  const jsonData = pm.response.json();
  pm.expect(jsonData).to.be.an("array");
});

pm.test("Contains the created topic with correct counts", function () {
  const jsonData = pm.response.json();
  const topicId = pm.collectionVariables.get("topic_id");
  const topic = jsonData.find(t => String(t.topicId) === String(topicId));

  pm.expect(topic).to.exist;
  pm.expect(topic.ideasCount).to.equal(2);
  pm.expect(topic.votesCount).to.equal(2);
  pm.expect(topic._links).to.be.an("array").and.not.empty;

  const rels = topic._links.map(l => l.rel);
  ["topic", "summary"].forEach(r => pm.expect(rels, `missing rel=${r}`).to.include(r));
});

function compareTopTopics(a, b) {
  if (a.votesCount !== b.votesCount) return b.votesCount - a.votesCount;
  if (a.ideasCount !== b.ideasCount) return b.ideasCount - a.ideasCount;
  return String(a.topicTitle).localeCompare(String(b.topicTitle));
}

pm.test("Top topics sorted by votesCount desc, ideasCount desc, topicTitle asc", function () {
  const arr = pm.response.json();
  for (let i = 1; i < arr.length; i++) {
    pm.expect(compareTopTopics(arr[i - 1], arr[i]) <= 0, `order violated at i=${i}`).to.equal(true);
  }
});

pm.test("Invalid limit/offset return 400", function (done) {
  const base = pm.collectionVariables.get("base_url");
  const v = pm.collectionVariables.get("api_version");

  pm.sendRequest(`${base}/api/${v}/statistics/top-topics?limit=0&offset=0`, function (err1, res1) {
    pm.expect(err1).to.equal(null);
    pm.expect(res1.code).to.equal(400);

    pm.sendRequest(`${base}/api/${v}/statistics/top-topics?limit=10&offset=-1`, function (err2, res2) {
      pm.expect(err2).to.equal(null);
      pm.expect(res2.code).to.equal(400);
      done();
    });
  });
});

