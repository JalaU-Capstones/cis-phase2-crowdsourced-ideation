// 01_seed_user_login_test.js

const threshold = parseInt(pm.collectionVariables.get("perf_threshold_ms") || "500", 10);
pm.test(`Response time < ${threshold} ms`, function () {
  pm.expect(pm.response.responseTime).to.be.below(threshold);
});

pm.test("Seed login/password are configured", function () {
  pm.expect(pm.variables.get("__seed_login_present")).to.equal(true);
  pm.expect(pm.variables.get("__seed_password_present")).to.equal(true);
});

pm.test("Seed user login succeeded", function () {
  // Phase 1 commonly returns 200 OK for login.
  pm.response.to.have.status(200);
});

pm.test("Seed token is returned and stored", function () {
  const json = pm.response.json();
  const token =
    json.token ||
    json.accessToken ||
    json.jwt ||
    (json.data && (json.data.token || json.data.accessToken));

  pm.expect(token, "token field (token/accessToken/jwt)").to.be.a("string").and.not.empty;
  pm.collectionVariables.set("seed_token", token);
});

