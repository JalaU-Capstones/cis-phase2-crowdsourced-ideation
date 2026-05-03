# Functionality Tests — Ideas (Business Rules)

This document covers **Ideas business rules** and how to validate them with Postman.

Applies to both `{{api_version}} = v1` and `v2`.

## Rule 1: Only Owner Can Update/Delete An Idea

### Expected behavior

- Owner token can `PUT` and `DELETE` the idea.
- A different valid token must be rejected:
  - `PUT /ideas/{id}` → `403 Forbidden`
  - `DELETE /ideas/{id}` → `403 Forbidden`

### Setup

You need a second Phase 1 user:
- `{{alt_login}}`
- `{{alt_password}}`

### Postman negative test (copy-paste)

Run this after step 3 creates `{{idea_id}}`:

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
  pm.test("Alt token exists", function () {
    pm.expect(altToken).to.be.a("string").and.not.empty;
  });

  const base = pm.collectionVariables.get("base_url");
  const v = pm.collectionVariables.get("api_version");
  const ideaId = pm.collectionVariables.get("idea_id");

  pm.sendRequest({
    url: `${base}/api/${v}/ideas/${ideaId}`,
    method: "PUT",
    header: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${altToken}`
    },
    body: {
      mode: "raw",
      raw: JSON.stringify({
        title: `Not owner update ${Date.now()}`,
        description: "Should be forbidden"
      })
    }
  }, function (err2, res2) {
    pm.test("Non-owner cannot update idea", function () {
      pm.expect(err2).to.equal(null);
      pm.expect(res2.code).to.equal(403);
    });
  });
});
```

## Rule 2: Cannot Create/Modify Ideas When Topic Is CLOSED

### Expected behavior

- Creating an idea under a `CLOSED` topic returns `403 Forbidden`.
- Updating/deleting an idea under a `CLOSED` topic returns `403 Forbidden`.

### Postman negative test (copy-paste)

This script:
1. Closes the topic (`PUT /topics/{topicId}` with `status=CLOSED`)
2. Attempts to create a new idea under that topic → expects `403`

```javascript
const base = pm.collectionVariables.get("base_url");
const v = pm.collectionVariables.get("api_version");
const token = pm.collectionVariables.get("seed_token");
const topicId = pm.collectionVariables.get("topic_id");

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
      title: `Close topic for idea rule ${Date.now()}`,
      description: "Closing topic to test idea rule",
      status: "CLOSED"
    })
  }
}, function (err, res) {
  pm.test("Topic closed for rule test", function () {
    pm.expect(err).to.equal(null);
    pm.expect(res.code).to.equal(200);
  });

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
        title: `Should fail ${Date.now()}`,
        description: "Cannot create idea on CLOSED topic"
      })
    }
  }, function (err2, res2) {
    pm.test("Cannot create idea when topic is CLOSED", function () {
      pm.expect(err2).to.equal(null);
      pm.expect(res2.code).to.equal(403);
    });
  });
});
```

## Rule 3: Duplicate Idea Title Per Topic

Status in current implementation:
- There is **no server-side rule** in `IdeaService` enforcing unique idea titles per topic.
- If the product requires uniqueness later, add a constraint at the persistence layer and a `409 Conflict` or `400 Bad Request` rule in the service.

## Rule 4: `isWinning` Becomes True When Topic Closes (Winner Selection)

### Expected behavior

When a topic transitions from `OPEN` to `CLOSED`, the service:
- counts votes per idea
- marks exactly one idea as winner (`isWinning=true`)

Note:
- This is executed by the **topic closing** logic (TopicService), but it affects the **IdeaResponse** field `isWinning`.

### Suggested validation approach (optional, uses Votes)

To validate end-to-end:
1. Create 2 ideas under the same topic
2. Cast 1+ votes for one idea
3. Close the topic
4. `GET /ideas/{id}` and assert `isWinning=true` on the winner

This requires calling `/votes` and `/topics` endpoints and is best run as a separate optional check (not part of the core 9-step Ideas CRUD flow).

## Expected Results

- Owner-only enforcement returns `403` for a different user.
- Creating/updating/deleting ideas under a CLOSED topic returns `403`.
- Duplicate titles are currently allowed (documented as not enforced).
- Winning selection sets `isWinning` when the topic closes (optional to validate with votes).

