# Running and Testing the CIS Phase 2 - Crowdsourced Ideation API

This guide provides step-by-step instructions for new developers to set up, run, and test the Crowdsourced Ideation API (Phase 2).

## 1. Prerequisites

Ensure you have the following installed:
- **.NET SDK 8** (pinned via `global.json`)
- **Docker** & **Docker Compose**
- **Git**

## 2. Cloning the Repository
```bash
git clone https://gitlab.com/jala-university1/cohort-5/ES.CO.CSSD-232.GA.T1.26.M2/secci-n-c/capstone-sd3/idea-flow/cis-phase2-crowdsourced-ideation-platform/cis-phase2-crowdsourced-ideation.git
cd cis-phase2-crowdsourced-ideation
```

## 3. Setting Up the Database

To start the database fresh:
```bash
docker compose up -d
```

> ⚠️ **To apply changes to init.sql, you must run:**
> ```bash
> docker compose down -v && docker compose up -d
> ```

Verify the container is running:
```bash
docker ps
# You should see: cis-mysql-phase1
```

Connection details:
- **MySQL (V1)**: `localhost:3307`
- **MongoDB (V2)**: `localhost:27017`

## 4. Running the Application
```bash
dotnet restore
dotnet run --project src/CIS-Phase2-Crowdsourced-Ideation
```

The API will be available at `http://localhost:5257`.
Swagger UI: `http://localhost:5257/swagger`

## 5. API Versioning and Dual Persistence

The API implements versioning to support different persistence layers (US 1.1):

- **V1** (`/api/v1/*`): Uses **MySQL** persistence.
- **V2** (`/api/v2/*`): Uses **MongoDB** persistence.

Persistence adapters are automatically resolved based on the route version.

## 6. Authentication

This API uses **JWT Bearer Token** authentication delegated from the Phase 1 User Management API. To obtain a token:

1. Ensure the Phase 1 API is running on `http://localhost:8080`
2. Create a user and login:
```bash
curl -X POST http://localhost:8080/api/v1/auth/login \
     -H "Content-Type: application/json" \
     -d '{
           "login": "testuser",
           "password": "password123"
         }'
```

3. Copy the returned token and use it in the `Authorization: Bearer <token>` header for protected endpoints.

## 7. API Examples (V1 - MySQL)

### 7.1. POST /api/v1/topics — Create a Topic
```bash
TOKEN="your_jwt_token_here"

curl -X POST http://localhost:5257/api/v1/topics \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
           "title": "V1 Topic",
           "description": "Stored in MySQL"
         }'
```

### 7.2. GET /api/v1/topics — Get All Topics
```bash
curl http://localhost:5257/api/v1/topics
```

## 8. API Examples (V2 - MongoDB)

### 8.1. POST /api/v2/topics — Create a Topic
```bash
TOKEN="your_jwt_token_here"

curl -X POST http://localhost:5257/api/v2/topics \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
           "title": "V2 Topic",
           "description": "Stored in MongoDB"
         }'
```

### 8.2. GET /api/v2/topics — Get All Topics
```bash
curl http://localhost:5257/api/v2/topics
```

## 9. Business Rules & HATEOAS

- **HATEOAS Links**: All responses include `_links`. These links are **dynamic** and point to the same API version as the request (e.g., V2 resources will have V2 links).
- **Winning Idea**: Automatically calculated when a topic is `CLOSED`.
- **Ownership**: Only the owner can `PUT` or `DELETE` resources.
- **Cascading Delete**: Deleting a topic deletes all its ideas and votes.

## 10. Running Tests
```bash
dotnet test
```

## 11. Complete API Examples (V1 + V2) with HATEOAS `_links`

Notes:
- All read endpoints are public unless explicitly marked as authenticated.
- All write endpoints require `Authorization: Bearer $TOKEN`.
- All resource responses include `_links` and these links stay in the same API version (`/api/v1/*` links in v1 responses, `/api/v2/*` links in v2 responses).

### 11.1. Topics

Create a topic (Authenticated):
```bash
TOKEN="your_jwt_token_here"

# V1 (MySQL)
curl -X POST http://localhost:5257/api/v1/topics \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "title": "V1 Topic", "description": "Stored in MySQL" }'

# V2 (MongoDB)
curl -X POST http://localhost:5257/api/v2/topics \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "title": "V2 Topic", "description": "Stored in MongoDB" }'
```

Example response (201 Created):
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "title": "V2 Topic",
  "description": "Stored in MongoDB",
  "status": "OPEN",
  "ownerId": "550e8400-e29b-41d4-a716-446655440001",
  "createdAt": "2026-03-30T10:00:00Z",
  "updatedAt": "2026-03-30T10:00:00Z",
  "winningIdea": null,
  "_links": [
    { "href": "api/v2/topics/550e8400-e29b-41d4-a716-446655440000", "method": "GET", "rel": "self" },
    { "href": "api/v2/ideas/topic/550e8400-e29b-41d4-a716-446655440000", "method": "GET", "rel": "ideas" },
    { "href": "api/v2/topics/550e8400-e29b-41d4-a716-446655440000", "method": "PUT", "rel": "update" },
    { "href": "api/v2/topics/550e8400-e29b-41d4-a716-446655440000", "method": "DELETE", "rel": "delete" }
  ]
}
```

Get all topics (Public):
```bash
# V1
curl "http://localhost:5257/api/v1/topics?page=0&size=10&status=OPEN&sortBy=createdAt&order=desc"

# V2
curl "http://localhost:5257/api/v2/topics?page=0&size=10&status=OPEN&sortBy=createdAt&order=desc"
```

Get a topic by id (Public):
```bash
TOPIC_ID="550e8400-e29b-41d4-a716-446655440000"

# V1
curl "http://localhost:5257/api/v1/topics/$TOPIC_ID"

# V2
curl "http://localhost:5257/api/v2/topics/$TOPIC_ID"
```

Update a topic (Owner only):
```bash
TOKEN="your_jwt_token_here"
TOPIC_ID="550e8400-e29b-41d4-a716-446655440000"

# V1
curl -X PUT "http://localhost:5257/api/v1/topics/$TOPIC_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "title": "Updated Title", "description": "Updated description", "status": "CLOSED" }'

# V2
curl -X PUT "http://localhost:5257/api/v2/topics/$TOPIC_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "title": "Updated Title", "description": "Updated description", "status": "CLOSED" }'
```

Delete a topic (Owner only):
```bash
TOKEN="your_jwt_token_here"
TOPIC_ID="550e8400-e29b-41d4-a716-446655440000"

# V1
curl -X DELETE "http://localhost:5257/api/v1/topics/$TOPIC_ID" \
  -H "Authorization: Bearer $TOKEN"

# V2
curl -X DELETE "http://localhost:5257/api/v2/topics/$TOPIC_ID" \
  -H "Authorization: Bearer $TOKEN"
```

### 11.2. Ideas

Create an idea (Authenticated):
```bash
TOKEN="your_jwt_token_here"
TOPIC_ID="550e8400-e29b-41d4-a716-446655440000"

# V1
curl -X POST http://localhost:5257/api/v1/ideas \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "topicId": "'"$TOPIC_ID"'", "title": "My Idea", "description": "Some details" }'

# V2
curl -X POST http://localhost:5257/api/v2/ideas \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "topicId": "'"$TOPIC_ID"'", "title": "My Idea", "description": "Some details" }'
```

Example response (201 Created):
```json
{
  "id": "660e8400-e29b-41d4-a716-446655440000",
  "topicId": "550e8400-e29b-41d4-a716-446655440000",
  "ownerId": "550e8400-e29b-41d4-a716-446655440001",
  "title": "My Idea",
  "description": "Some details",
  "createdAt": "2026-03-30T11:00:00Z",
  "updatedAt": "2026-03-30T11:00:00Z",
  "isWinning": false,
  "_links": [
    { "href": "api/v2/ideas/660e8400-e29b-41d4-a716-446655440000", "method": "GET", "rel": "self" },
    { "href": "api/v2/topics/550e8400-e29b-41d4-a716-446655440000", "method": "GET", "rel": "topic" },
    { "href": "api/v2/votes/idea/660e8400-e29b-41d4-a716-446655440000", "method": "GET", "rel": "votes" },
    { "href": "api/v2/ideas/660e8400-e29b-41d4-a716-446655440000", "method": "PUT", "rel": "update" },
    { "href": "api/v2/ideas/660e8400-e29b-41d4-a716-446655440000", "method": "DELETE", "rel": "delete" },
    { "href": "api/v2/votes", "method": "POST", "rel": "vote" }
  ]
}
```

Get all ideas (Public):
```bash
# V1
curl "http://localhost:5257/api/v1/ideas?page=0&size=10&sortBy=updatedAt&order=desc"

# V2
curl "http://localhost:5257/api/v2/ideas?page=0&size=10&sortBy=updatedAt&order=desc"
```

Get an idea by id (Public):
```bash
IDEA_ID="660e8400-e29b-41d4-a716-446655440000"

# V1
curl "http://localhost:5257/api/v1/ideas/$IDEA_ID"

# V2
curl "http://localhost:5257/api/v2/ideas/$IDEA_ID"
```

Get ideas by topic (Public):
```bash
TOPIC_ID="550e8400-e29b-41d4-a716-446655440000"

# V1
curl "http://localhost:5257/api/v1/ideas/topic/$TOPIC_ID"

# V2
curl "http://localhost:5257/api/v2/ideas/topic/$TOPIC_ID"
```

Update an idea (Owner only):
```bash
TOKEN="your_jwt_token_here"
IDEA_ID="660e8400-e29b-41d4-a716-446655440000"

# V1
curl -X PUT "http://localhost:5257/api/v1/ideas/$IDEA_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "title": "Updated title", "description": "Updated description" }'

# V2
curl -X PUT "http://localhost:5257/api/v2/ideas/$IDEA_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "title": "Updated title", "description": "Updated description" }'
```

Delete an idea (Owner only):
```bash
TOKEN="your_jwt_token_here"
IDEA_ID="660e8400-e29b-41d4-a716-446655440000"

# V1
curl -X DELETE "http://localhost:5257/api/v1/ideas/$IDEA_ID" \
  -H "Authorization: Bearer $TOKEN"

# V2
curl -X DELETE "http://localhost:5257/api/v2/ideas/$IDEA_ID" \
  -H "Authorization: Bearer $TOKEN"
```

### 11.3. Votes

Cast a vote (Authenticated):
```bash
TOKEN="your_jwt_token_here"
IDEA_ID="660e8400-e29b-41d4-a716-446655440000"

# V1
curl -X POST http://localhost:5257/api/v1/votes \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "ideaId": "'"$IDEA_ID"'" }'

# V2
curl -X POST http://localhost:5257/api/v2/votes \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "ideaId": "'"$IDEA_ID"'" }'
```

Example response (201 Created):
```json
{
  "id": "770e8400-e29b-41d4-a716-446655440000",
  "ideaId": "660e8400-e29b-41d4-a716-446655440000",
  "ideaTitle": "My Idea",
  "topicId": "550e8400-e29b-41d4-a716-446655440000",
  "topicTitle": "V2 Topic",
  "_links": [
    { "href": "api/v2/votes/idea/660e8400-e29b-41d4-a716-446655440000", "method": "GET", "rel": "self" },
    { "href": "api/v2/ideas/660e8400-e29b-41d4-a716-446655440000", "method": "GET", "rel": "idea" },
    { "href": "api/v2/votes/770e8400-e29b-41d4-a716-446655440000", "method": "DELETE", "rel": "remove" }
  ]
}
```

Get all votes (Public):
```bash
# V1
curl "http://localhost:5257/api/v1/votes"

# V2
curl "http://localhost:5257/api/v2/votes"
```

Get votes by idea (Public):
```bash
IDEA_ID="660e8400-e29b-41d4-a716-446655440000"

# V1
curl "http://localhost:5257/api/v1/votes/idea/$IDEA_ID"

# V2
curl "http://localhost:5257/api/v2/votes/idea/$IDEA_ID"
```

Get vote by id (Public):
```bash
VOTE_ID="770e8400-e29b-41d4-a716-446655440000"

# V1
curl "http://localhost:5257/api/v1/votes/$VOTE_ID"

# V2
curl "http://localhost:5257/api/v2/votes/$VOTE_ID"
```

Update a vote (Owner only):
```bash
TOKEN="your_jwt_token_here"
VOTE_ID="770e8400-e29b-41d4-a716-446655440000"
NEW_IDEA_ID="880e8400-e29b-41d4-a716-446655440000"

# V1
curl -X PUT "http://localhost:5257/api/v1/votes/$VOTE_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "ideaId": "'"$NEW_IDEA_ID"'" }'

# V2
curl -X PUT "http://localhost:5257/api/v2/votes/$VOTE_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "ideaId": "'"$NEW_IDEA_ID"'" }'
```

Delete a vote (Owner only):
```bash
TOKEN="your_jwt_token_here"
VOTE_ID="770e8400-e29b-41d4-a716-446655440000"

# V1
curl -X DELETE "http://localhost:5257/api/v1/votes/$VOTE_ID" \
  -H "Authorization: Bearer $TOKEN"

# V2
curl -X DELETE "http://localhost:5257/api/v2/votes/$VOTE_ID" \
  -H "Authorization: Bearer $TOKEN"
```

### 11.4. Statistics

Top topics (Public):
```bash
# V1
curl "http://localhost:5257/api/v1/statistics/top-topics?limit=10&offset=0"

# V2
curl "http://localhost:5257/api/v2/statistics/top-topics?limit=10&offset=0"
```

Example response (200 OK):
```json
[
  {
    "topicId": "550e8400-e29b-41d4-a716-446655440000",
    "topicTitle": "V2 Topic",
    "status": "OPEN",
    "ideasCount": 3,
    "votesCount": 12,
    "_links": [
      { "href": "api/v2/topics/550e8400-e29b-41d4-a716-446655440000", "method": "GET", "rel": "topic" },
      { "href": "api/v2/statistics/topic/550e8400-e29b-41d4-a716-446655440000/summary", "method": "GET", "rel": "summary" }
    ]
  }
]
```

Most voted ideas (Public):
```bash
# V1
curl "http://localhost:5257/api/v1/statistics/most-voted-ideas?limit=10&offset=0"

# V2
curl "http://localhost:5257/api/v2/statistics/most-voted-ideas?limit=10&offset=0"
```

Topic summary (Public):
```bash
TOPIC_ID="550e8400-e29b-41d4-a716-446655440000"

# V1
curl "http://localhost:5257/api/v1/statistics/topic/$TOPIC_ID/summary"

# V2
curl "http://localhost:5257/api/v2/statistics/topic/$TOPIC_ID/summary"
```
