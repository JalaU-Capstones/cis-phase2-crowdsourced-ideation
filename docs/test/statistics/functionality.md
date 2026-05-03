# Functionality Tests — Statistics (Business Rules)

This document covers **Statistics business rules** and how to validate them with Postman scripts.

Applies to both `{{api_version}} = v1` and `v2`.

## Rule 1: `top-topics` Sorting

Endpoint:
- `GET /api/{{api_version}}/statistics/top-topics?limit=&offset=`

Expected ordering:
1. `votesCount` descending
2. `ideasCount` descending
3. `topicTitle` ascending (alphabetical)

Postman snippet (validate response is sorted):

```javascript
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
```

## Rule 2: `most-voted-ideas` Sorting

Endpoint:
- `GET /api/{{api_version}}/statistics/most-voted-ideas?limit=&offset=`

Expected ordering:
1. `votesCount` descending
2. `ideaTitle` ascending

Postman snippet:

```javascript
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
```

## Rule 3: Limits And Offsets

Endpoints:
- `GET /top-topics?limit=&offset=`
- `GET /most-voted-ideas?limit=&offset=`

Expected behavior:
- `limit <= 0` → `400 Bad Request`
- `offset < 0` → `400 Bad Request`
- When omitted: default `limit=10`, `offset=0`

Postman snippet (negative checks via `pm.sendRequest`):

```javascript
const base = pm.collectionVariables.get("base_url");
const v = pm.collectionVariables.get("api_version");

pm.sendRequest(`${base}/api/${v}/statistics/top-topics?limit=0&offset=0`, function (err, res) {
  pm.expect(err).to.equal(null);
  pm.expect(res.code).to.equal(400);
});
```

## Rule 4: Topic Summary Accuracy + Winning Idea Visibility

Endpoint:
- `GET /api/{{api_version}}/statistics/topic/{{topic_id}}/summary`

Expected behavior:
- `ideasCount` and `votesCount` match the actual persisted data.
- `mostVotedIdea` is calculated whenever the topic has at least one idea.
- `winningIdea` is only present **after the topic is closed** (because the winner is computed when a topic transitions `OPEN → CLOSED`).

Where it’s validated:
- `postman-scripts/08_get_topic_summary_test.js` validates:
  - `winningIdea === null` when the topic is `OPEN`
  - it then closes the topic via `pm.sendRequest()` to `PUT /topics/{{topic_id}}`
  - re-fetches summary and validates `winningIdea` is non-null

## Expected Results

- Sorting and pagination rules are enforced consistently for both versions.
- Topic summary counts are correct and deterministic for the seeded dataset.
- `winningIdea` appears only after closing the topic.

