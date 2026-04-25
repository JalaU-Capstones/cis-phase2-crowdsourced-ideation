# Structure Tests — Topics (JSON Schema + HATEOAS)

This document defines **structure/schema** validation for the Topics endpoints, including:
- Required fields and basic types
- ISO-8601 datetime strings
- HATEOAS `_links` structure and required relations

These validations are designed to work for both:
- `v1` (`/api/v1/topics`) and
- `v2` (`/api/v2/topics`)

## Shared JSON Schemas (Postman)

In Postman, you can validate JSON schema using:

```javascript
pm.response.to.have.jsonSchema(schemaObject);
```

Below is a reusable schema set you can paste into any Topics request “Tests” tab.

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

const winningIdeaSchema = {
  type: "object",
  additionalProperties: true,
  required: ["id", "topicId", "ownerId", "title", "description", "createdAt", "updatedAt", "isWinning"],
  properties: {
    id: { type: "string", minLength: 1 },          // GUID as string
    topicId: { type: "string", minLength: 1 },
    ownerId: { type: "string", minLength: 1 },     // GUID as string
    title: { type: "string", minLength: 1 },
    description: { type: "string" },
    createdAt: { type: "string", format: "date-time" },
    updatedAt: { type: "string", format: "date-time" },
    isWinning: { type: "boolean" }
  }
};

const topicSchema = {
  type: "object",
  additionalProperties: true,
  required: ["id", "title", "status", "ownerId", "createdAt", "updatedAt", "_links"],
  properties: {
    id: { type: "string", minLength: 1 },
    title: { type: "string", minLength: 1, maxLength: 200 },
    description: { type: ["string", "null"] },
    status: { type: "string", enum: ["OPEN", "CLOSED"] },
    ownerId: { type: "string", minLength: 1 },
    createdAt: { type: "string", format: "date-time" },
    updatedAt: { type: "string", format: "date-time" },
    winningIdea: { anyOf: [{ type: "null" }, winningIdeaSchema] },
    _links: {
      type: "array",
      minItems: 1,
      items: linkSchema
    }
  }
};

const pagedTopicsSchema = {
  type: "object",
  additionalProperties: true,
  required: ["data", "currentPage", "pageSize", "totalItems", "totalPages"],
  properties: {
    data: { type: "array", items: topicSchema },
    currentPage: { type: "integer", minimum: 0 },
    pageSize: { type: "integer", minimum: 1 },
    totalItems: { type: "integer", minimum: 0 },
    totalPages: { type: "integer", minimum: 0 }
  }
};
```

## HATEOAS `_links` Validation Rules

For a Topic response, `_links` must contain at least:
- `rel=self` → `href` ends with `api/{{api_version}}/topics/{{topic_id}}` and `method=GET`
- `rel=ideas` → `href` ends with `api/{{api_version}}/ideas/topic/{{topic_id}}` and `method=GET`
- `rel=update` → `PUT`
- `rel=delete` → `DELETE`

Additionally, when `status === "CLOSED"`:
- `_links` must contain `rel=winner` with `method=GET`

### Postman snippet (link relation assertions)

```javascript
function getLink(json, rel) {
  pm.expect(json).to.have.property("_links");
  pm.expect(json._links).to.be.an("array");
  return json._links.find(l => l.rel === rel);
}

pm.test("Topic has required HATEOAS links", function () {
  const json = pm.response.json();
  ["self", "ideas", "update", "delete"].forEach(rel => {
    pm.expect(getLink(json, rel), `missing rel=${rel}`).to.exist;
  });
});
```

## Endpoint-Specific Expectations

### `GET /api/{{api_version}}/topics` (paged list)

Expected shape:
- Root object: `{ data: [...], currentPage, pageSize, totalItems, totalPages }`
- Each element of `data[]` matches `topicSchema`

Postman Tests snippet:

```javascript
pm.response.to.have.jsonSchema(pagedTopicsSchema);
```

### `GET /api/{{api_version}}/topics/{{topic_id}}` (single topic)

Expected shape:
- Matches `topicSchema`
- For `OPEN` topic: `winningIdea` should be `null` or missing
- For `CLOSED` topic: `winningIdea` should be an object and `winningIdea.isWinning === true`

Postman Tests snippet:

```javascript
pm.response.to.have.jsonSchema(topicSchema);
```

### `POST /api/{{api_version}}/topics` and `PUT /api/{{api_version}}/topics/{{topic_id}}`

Expected shape:
- Response matches `topicSchema`
- `_links` exists and includes required relations

## Expected Results

- All Topics responses conform to the schemas above.
- `_links` relations match the topic state:
  - `OPEN` → no `winner` link
  - `CLOSED` → includes `winner` link and returns `winningIdea`

