# Structure Tests — Ideas (JSON Schema + HATEOAS)

This document defines **structure/schema** validation for the Ideas endpoints, including:
- Required fields and basic types
- ISO-8601 datetime strings
- HATEOAS `_links` structure, including conditional `vote` link (only when the parent topic is `OPEN`)

These validations are designed to work for both:
- `v1` (`/api/v1/ideas`) and
- `v2` (`/api/v2/ideas`)

## Shared JSON Schemas (Postman)

In Postman, validate schemas with:

```javascript
pm.response.to.have.jsonSchema(schemaObject);
```

Reusable schemas:

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

const ideaSchema = {
  type: "object",
  additionalProperties: true,
  required: ["id", "topicId", "ownerId", "title", "description", "createdAt", "updatedAt", "isWinning", "_links"],
  properties: {
    id: { type: "string", minLength: 1 },        // GUID serialized as string
    topicId: { type: "string", minLength: 1 },
    ownerId: { type: "string", minLength: 1 },   // GUID serialized as string
    title: { type: "string", minLength: 1 },
    description: { type: "string", minLength: 1 },
    createdAt: { type: "string", format: "date-time" },
    updatedAt: { type: "string", format: "date-time" },
    isWinning: { type: "boolean" },
    _links: { type: "array", minItems: 1, items: linkSchema }
  }
};

const pagedIdeasSchema = {
  type: "object",
  additionalProperties: true,
  required: ["data", "currentPage", "pageSize", "totalItems", "totalPages"],
  properties: {
    data: { type: "array", items: ideaSchema },
    currentPage: { type: "integer", minimum: 0 },
    pageSize: { type: "integer", minimum: 1 },
    totalItems: { type: "integer", minimum: 0 },
    totalPages: { type: "integer", minimum: 0 }
  }
};
```

## HATEOAS `_links` Validation Rules (IdeaResponse)

For an Idea response, `_links` must include:
- `rel=self` (GET) → `api/{{api_version}}/ideas/{{idea_id}}`
- `rel=topic` (GET) → `api/{{api_version}}/topics/{{topic_id}}`
- `rel=votes` (GET) → `api/{{api_version}}/votes/idea/{{idea_id}}`
- `rel=update` (PUT)
- `rel=delete` (DELETE)

Additionally:
- `rel=vote` (POST to `api/{{api_version}}/votes`) is included **only when the topic is OPEN**.

### Postman snippet (link relation assertions)

```javascript
function getLink(json, rel) {
  pm.expect(json).to.have.property("_links");
  pm.expect(json._links).to.be.an("array");
  return json._links.find(l => l.rel === rel);
}

pm.test("Idea has required HATEOAS links", function () {
  const json = pm.response.json();
  ["self", "topic", "votes", "update", "delete"].forEach(rel => {
    pm.expect(getLink(json, rel), `missing rel=${rel}`).to.exist;
  });
});
```

## Endpoint-Specific Expectations

### `GET /api/{{api_version}}/ideas` (paged list)

Expected shape:
- `{ data: [...], currentPage, pageSize, totalItems, totalPages }`

Postman Tests snippet:

```javascript
pm.response.to.have.jsonSchema(pagedIdeasSchema);
```

### `GET /api/{{api_version}}/ideas/{{idea_id}}` (single idea)

Expected shape:
- Matches `ideaSchema`
- `_links` includes `vote` when the topic is OPEN

### `GET /api/{{api_version}}/ideas/topic/{{topic_id}}` (ideas by topic)

Expected shape:
- Raw array of ideas: `IdeaResponse[]`

Postman Tests snippet:

```javascript
pm.expect(pm.response.json()).to.be.an("array");
pm.response.to.have.jsonSchema({ type: "array", items: ideaSchema });
```

## Expected Results

- All Idea responses conform to the schemas above.
- `_links` includes `vote` only when the parent topic is OPEN (and is omitted when CLOSED).

