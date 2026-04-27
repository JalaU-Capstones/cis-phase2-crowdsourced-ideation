# Structure Tests — Votes (JSON Schema + HATEOAS)

This document defines **structure/schema** validation for Votes endpoints, including:
- Required fields and basic types
- HATEOAS `_links` format and required relations

Applies to both `v1` and `v2`.

## VoteResponse Fields (API Contract)

Each vote response includes:
- `id` (guid string)
- `ideaId` (guid string)
- `ideaTitle` (string)
- `topicId` (string)
- `topicTitle` (string)
- `_links` (array of `{ href, method, rel }`)

## Shared JSON Schemas (Postman)

```javascript
const linkSchema = {
  type: "object",
  additionalProperties: true,
  required: ["href", "method", "rel"],
  properties: {
    href: { type: "string", minLength: 1 },
    method: { type: "string", enum: ["GET", "POST", "PUT", "DELETE"] },
    rel: { type: "string", minLength: 1 }
  }
};

const voteSchema = {
  type: "object",
  additionalProperties: true,
  required: ["id", "ideaId", "ideaTitle", "topicId", "topicTitle", "_links"],
  properties: {
    id: { type: "string", minLength: 1 },
    ideaId: { type: "string", minLength: 1 },
    ideaTitle: { type: "string" },
    topicId: { type: "string", minLength: 1 },
    topicTitle: { type: "string" },
    _links: { type: "array", minItems: 1, items: linkSchema }
  }
};

const voteArraySchema = { type: "array", items: voteSchema };
```

Use in Postman:

```javascript
pm.response.to.have.jsonSchema(voteSchema);
```

## HATEOAS `_links` Rules For Votes

Vote HATEOAS links are generated as:
- `rel=self` → `GET api/{{api_version}}/votes/idea/{{ideaId}}` (note: **self for votes is the votes-by-idea resource**)
- `rel=idea` → `GET api/{{api_version}}/ideas/{{ideaId}}`
- `rel=remove` → `DELETE api/{{api_version}}/votes/{{voteId}}`

### Postman snippet (required relations)

```javascript
function getLink(json, rel) {
  pm.expect(json._links).to.be.an("array");
  return json._links.find(l => l.rel === rel);
}

pm.test("Vote has required HATEOAS links", function () {
  const json = pm.response.json();
  ["self", "idea", "remove"].forEach(rel => pm.expect(getLink(json, rel), `missing ${rel}`).to.exist);
});
```

## Endpoint-Specific Structure Expectations

### `POST /votes` and `PUT /votes/{voteId}`

- Response is a single `VoteResponse` (validate `voteSchema`)

### `GET /votes`

- Response is an array of `VoteResponse` (validate `voteArraySchema`)

### `GET /votes/idea/{ideaId}`

- Response is an array of `VoteResponse` (validate `voteArraySchema`)

## Expected Results

- All responses conform to the schemas above.
- `_links` relations are present and correctly versioned by `{{api_version}}`.

