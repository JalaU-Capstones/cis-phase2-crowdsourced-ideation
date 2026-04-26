# Structure Tests — Statistics (JSON Schema + HATEOAS)

This document defines **structure/schema** validation for the Statistics endpoints, including:
- Required fields and basic types
- Nullable fields (`winningIdea`, `mostVotedIdea`)
- HATEOAS `_links` structure and required relations

These validations are designed to work for both:
- `v1` (`/api/v1/statistics/*`) and
- `v2` (`/api/v2/statistics/*`)

## Shared JSON Schemas (Postman)

In Postman, you can validate JSON schema using:

```javascript
pm.response.to.have.jsonSchema(schemaObject);
```

Below is a reusable schema set you can paste into any Statistics request “Tests” tab.

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

const ideaBriefSchema = {
  type: "object",
  additionalProperties: true,
  required: ["ideaId", "ideaTitle", "votesCount"],
  properties: {
    ideaId: { type: "string", minLength: 1 },  // GUID as string
    ideaTitle: { type: "string", minLength: 1 },
    votesCount: { type: "integer", minimum: 0 }
  }
};

const topTopicSchema = {
  type: "object",
  additionalProperties: true,
  required: ["topicId", "topicTitle", "status", "ideasCount", "votesCount", "_links"],
  properties: {
    topicId: { type: "string", minLength: 1 },
    topicTitle: { type: "string", minLength: 1 },
    status: { type: "string", enum: ["OPEN", "CLOSED"] },
    ideasCount: { type: "integer", minimum: 0 },
    votesCount: { type: "integer", minimum: 0 },
    _links: { type: "array", minItems: 1, items: linkSchema }
  }
};

const mostVotedIdeaSchema = {
  type: "object",
  additionalProperties: true,
  required: ["ideaId", "ideaTitle", "votesCount", "topicId", "topicTitle", "_links"],
  properties: {
    ideaId: { type: "string", minLength: 1 },  // GUID as string
    ideaTitle: { type: "string", minLength: 1 },
    votesCount: { type: "integer", minimum: 0 },
    topicId: { type: "string", minLength: 1 },
    topicTitle: { type: "string", minLength: 1 },
    _links: { type: "array", minItems: 1, items: linkSchema }
  }
};

const topicSummarySchema = {
  type: "object",
  additionalProperties: true,
  required: ["topicId", "topicTitle", "status", "ideasCount", "votesCount", "_links"],
  properties: {
    topicId: { type: "string", minLength: 1 },
    topicTitle: { type: "string", minLength: 1 },
    status: { type: "string", enum: ["OPEN", "CLOSED"] },
    ideasCount: { type: "integer", minimum: 0 },
    votesCount: { type: "integer", minimum: 0 },
    winningIdea: { anyOf: [{ type: "null" }, ideaBriefSchema] },
    mostVotedIdea: { anyOf: [{ type: "null" }, ideaBriefSchema] },
    _links: { type: "array", minItems: 1, items: linkSchema }
  }
};
```

## HATEOAS `_links` Validation Rules

### `GET /statistics/top-topics`

Each item must include:
- `rel=topic` with `method=GET`
- `rel=summary` with `method=GET`

### `GET /statistics/most-voted-ideas`

Each item must include:
- `rel=idea` with `method=GET`
- `rel=topic` with `method=GET`

### `GET /statistics/topic/{topicId}/summary`

Response must include:
- `rel=self` with `method=GET`
- `rel=topic` with `method=GET`

## Endpoint-Specific Expectations

### `GET /api/{{api_version}}/statistics/top-topics`

Expected shape:
- Root JSON is an array
- Each element matches `topTopicSchema`

Postman snippet:

```javascript
pm.test("Top topics schema", function () {
  pm.response.to.have.jsonSchema({ type: "array", items: topTopicSchema });
});
```

### `GET /api/{{api_version}}/statistics/most-voted-ideas`

Expected shape:
- Root JSON is an array
- Each element matches `mostVotedIdeaSchema`

### `GET /api/{{api_version}}/statistics/topic/{{topic_id}}/summary`

Expected shape:
- Root JSON is an object matching `topicSummarySchema`
- `winningIdea` is typically `null` when the topic is `OPEN`
- `winningIdea` becomes non-null only after the topic is closed (topic update flow)

## Expected Results

- All Statistics responses conform to the schemas above.
- `_links` relations match the endpoint contract (topic/summary, idea/topic, self/topic).

