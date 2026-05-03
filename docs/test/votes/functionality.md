# Functionality Tests — Votes (Business Rules)

This document covers Votes business rules and how to validate them with Postman scripts.

Applies to `{{api_version}} = v1` and `v2`.

## Rule 1: One Vote Per User Per Idea

Expected behavior:
- First `POST /votes` for an idea succeeds (`201`).
- Second `POST /votes` for the same idea by the same user fails (`409 Conflict`).

Where validated:
- `postman-scripts/04_cast_vote_test.js` sends a second `POST /votes` and asserts `409`.

## Rule 2: Cannot Vote On Closed Topics

Expected behavior:
- If the idea’s topic is `CLOSED`, voting is forbidden (`403 Forbidden`).

Copy-paste negative test (uses Topics endpoint to close the topic):

```javascript
const base = pm.collectionVariables.get("base_url");
const v = pm.collectionVariables.get("api_version");
const token = pm.collectionVariables.get("seed_token");
const topicId = pm.collectionVariables.get("topic_id");
const ideaId = pm.collectionVariables.get("idea_id");

// 1) Close topic
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
      title: `Close topic for vote rule ${Date.now()}`,
      description: "Closing topic to test vote restriction",
      status: "CLOSED"
    })
  }
}, function (err1, res1) {
  pm.test("Topic closed", function () {
    pm.expect(err1).to.equal(null);
    pm.expect(res1.code).to.equal(200);
  });

  // 2) Attempt to vote on idea (should be forbidden)
  pm.sendRequest({
    url: `${base}/api/${v}/votes`,
    method: "POST",
    header: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${token}`
    },
    body: {
      mode: "raw",
      raw: JSON.stringify({ ideaId: ideaId })
    }
  }, function (err2, res2) {
    pm.test("Cannot vote on CLOSED topic", function () {
      pm.expect(err2).to.equal(null);
      pm.expect(res2.code).to.equal(403);
    });
  });
});
```

## Rule 3: Only Vote Owner Can Update/Delete

Expected behavior:
- Vote owner can `PUT` and `DELETE`.
- A different valid user gets `403 Forbidden`.

Copy-paste negative test (requires `alt_login/alt_password`):

```javascript
pm.test("Alt user credentials are configured", function () {
  pm.expect(pm.collectionVariables.get("alt_login")).to.be.a("string").and.not.empty;
  pm.expect(pm.collectionVariables.get("alt_password")).to.be.a("string").and.not.empty;
});

const phase1 = pm.collectionVariables.get("phase1_base_url");
pm.sendRequest({
  url: `${phase1}/api/v1/auth/login`,
  method: "POST",
  header: { "Content-Type": "application/json" },
  body: {
    mode: "raw",
    raw: JSON.stringify({
      login: pm.collectionVariables.get("alt_login"),
      password: pm.collectionVariables.get("alt_password")
    })
  }
}, function (err, res) {
  pm.test("Alt user can login", function () {
    pm.expect(err).to.equal(null);
    pm.expect(res.code).to.equal(200);
  });

  const json = res.json();
  const altToken = json.token || json.accessToken || json.jwt || (json.data && json.data.token);
  const base = pm.collectionVariables.get("base_url");
  const v = pm.collectionVariables.get("api_version");
  const voteId = pm.collectionVariables.get("vote_id");
  const newIdeaId = pm.collectionVariables.get("new_idea_id") || pm.collectionVariables.get("idea_id");

  pm.sendRequest({
    url: `${base}/api/${v}/votes/${voteId}`,
    method: "PUT",
    header: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${altToken}`
    },
    body: { mode: "raw", raw: JSON.stringify({ ideaId: newIdeaId }) }
  }, function (err2, res2) {
    pm.test("Non-owner cannot update vote", function () {
      pm.expect(err2).to.equal(null);
      pm.expect(res2.code).to.equal(403);
    });
  });
});
```

## Rule 4: Moving A Vote Changes `ideaId` And Vote Membership

Expected behavior:
- `PUT /votes/{voteId}` returns a `VoteResponse` with updated `ideaId`.
- The vote appears in `GET /votes/idea/{newIdeaId}` and no longer appears in `GET /votes/idea/{oldIdeaId}`.

Where validated:
- `postman-scripts/08_update_vote_test.js` validates `ideaId` and uses `GET /votes/idea/*` to verify membership change.

## Rule 5: User Cannot Vote On Their Own Idea (if applicable)

Status in current implementation:
- There is **no rule** in `VoteService.CastVoteAsync` preventing a user from voting on their own idea.
- If the product requires this later, add a check comparing `idea.OwnerId` and `userId`, returning `403`.

## Expected Results

- Duplicate vote attempt returns `409`.
- Voting on CLOSED topics returns `403`.
- Only owner can update/delete votes (`403` for other user).
- Vote move updates `ideaId` and changes votes-by-idea lists.

