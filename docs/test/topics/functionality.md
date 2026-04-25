# Functionality Tests — Topics (Business Rules)

This document covers **Topics business rules** and how to validate them with Postman scripts.

Applies to both `{{api_version}} = v1` and `v2`.

## Rule 1: Only Owner Can Update/Delete

### Expected behavior

- Owner token can `PUT` and `DELETE` the topic.
- A different valid token must be rejected:
  - `PUT /topics/{id}` → `403 Forbidden`
  - `DELETE /topics/{id}` → `403 Forbidden`

### Setup

You need a second Phase 1 user:
- `{{alt_login}}`
- `{{alt_password}}`

### Postman negative test (copy-paste)

Add this as a one-off request in your collection (or run it manually after step 2 creates `{{topic_id}}`):

- Request: `PUT {{base_url}}/api/{{api_version}}/topics/{{topic_id}}`
- Body:
  ```json
  { "title": "Should fail", "description": "Not owner", "status": "OPEN" }
  ```

Tests tab:

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
    pm.expect(res).to.have.property("code");
    pm.expect(res.code).to.equal(200);
  });

  const json = res.json();
  const altToken = json.token || json.accessToken || json.jwt || (json.data && json.data.token);
  pm.test("Alt token exists", function () {
    pm.expect(altToken).to.be.a("string").and.not.empty;
  });

  const base = pm.collectionVariables.get("base_url");
  const v = pm.collectionVariables.get("api_version");
  const topicId = pm.collectionVariables.get("topic_id");

  pm.sendRequest({
    url: `${base}/api/${v}/topics/${topicId}`,
    method: "PUT",
    header: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${altToken}`
    },
    body: {
      mode: "raw",
      raw: JSON.stringify({
        title: `Not owner update ${Date.now()}`,
        description: "Should be forbidden",
        status: "OPEN"
      })
    }
  }, function (err2, res2) {
    pm.test("Non-owner cannot update topic", function () {
      pm.expect(err2).to.equal(null);
      pm.expect(res2.code).to.equal(403);
    });
  });
});
```

## Rule 2: Status Transition OPEN -> CLOSED Only (No Reopen)

### Expected behavior

1. `OPEN -> CLOSED` is allowed (owner-only).
2. `CLOSED -> OPEN` is rejected with `400 Bad Request`.

### Postman negative test (copy-paste)

Run this after the e2e flow step 5 closes the topic:

- Request: `PUT {{base_url}}/api/{{api_version}}/topics/{{topic_id}}`
- Auth: `Bearer {{seed_token}}`
- Body:
  ```json
  { "title": "{{topic_title_updated}}", "description": "{{topic_description_updated}}", "status": "OPEN" }
  ```

Tests tab:

```javascript
pm.test("Cannot reopen a CLOSED topic", function () {
  pm.response.to.have.status(400);
});
```

## Rule 3: Winning Idea Is Calculated When Topic Closes

### Expected behavior

When a topic is updated from `OPEN` to `CLOSED`:
- The service selects the idea with the most votes.
- The winning idea is marked `isWinning=true`.
- The Topic response includes `winningIdea` (object) and adds a `_links` relation `winner`.

### Where it’s validated

The e2e script `postman-scripts/05_update_topic_test.js` validates:
- status is `CLOSED`
- `winningIdea` is not null
- `winningIdea.isWinning === true`
- `winningIdea.id === {{expected_winning_idea_id}}` (seeded deterministically)
- `_links` includes `winner`

## Expected Results

- Owner-only enforcement returns `403` for a different user.
- Status cannot transition back to `OPEN` once closed (returns `400`).
- Closing a topic returns a computed `winningIdea` and includes the `winner` HATEOAS link.

