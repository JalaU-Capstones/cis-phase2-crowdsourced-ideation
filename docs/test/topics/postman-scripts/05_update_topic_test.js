// 05_update_topic_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Update topic succeeded", function () {
  pm.response.to.have.status(200);
});

pm.test("Topic is CLOSED after update", function () {
  const json = pm.response.json();
  pm.expect(json.status).to.equal("CLOSED");
});

pm.test("Update response includes X-Info header when closing", function () {
  const xInfo = pm.response.headers.get("X-Info");
  pm.expect(xInfo, "X-Info header").to.be.a("string").and.not.empty;
});

pm.test("winningIdea is calculated and returned", function () {
  const json = pm.response.json();
  const expectedWinnerId = pm.collectionVariables.get("expected_winning_idea_id");

  pm.expect(json).to.have.property("winningIdea");
  pm.expect(json.winningIdea, "winningIdea").to.not.equal(null);
  pm.expect(json.winningIdea).to.be.an("object");
  pm.expect(json.winningIdea.isWinning).to.equal(true);

  if (expectedWinnerId) {
    pm.expect(String(json.winningIdea.id)).to.equal(String(expectedWinnerId));
  }
});

pm.test("CLOSED topic includes winner HATEOAS link", function () {
  const json = pm.response.json();
  pm.expect(json._links).to.be.an("array").and.not.empty;
  const winner = json._links.find(l => l.rel === "winner");
  pm.expect(winner, "winner rel").to.exist;
  pm.expect(winner.method).to.equal("GET");
});

