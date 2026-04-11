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
- **Host**: `localhost`
- **Port**: `3307`
- **Database**: `sd3`
- **User**: `sd3user`
- **Password**: `sd3pass`

## 4. Running the Application
```bash
dotnet restore
dotnet run --project src/CIS-Phase2-Crowdsourced-Ideation
```

The API will be available at `http://localhost:5257`.
Swagger UI (Development only): `http://localhost:5257/swagger`

## 5. Authentication

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

3. Copy the returned token and use it in the `Authorization: Bearer <token>` header for protected Topics endpoints.

## 6. Testing the API

GET operations on `/topics` are **public**. All write operations (POST, PUT, DELETE) require a valid JWT token. Ownership rules apply to PUT and DELETE.

### 6.0. Note About Legacy Database Schema

The local MySQL schema is defined in `init.sql` and is treated as legacy-compatible.

- Topic columns map 1:1 to the API model.
- Ideas are stored in the `ideas` table with a single `content` (TEXT) column. The API still exposes `title`, `description`, and `isWinning`; these fields are serialized into `ideas.content` as JSON so the public API contract remains unchanged.

If you inspect the database directly, expect `ideas.content` to look like:
```json
{"title":"My Idea","description":"Some details","isWinning":false}
```

Ideas endpoints:
- `GET /api/ideas` and `GET /api/ideas/{id}` are **public** (no JWT required).
- `GET /api/ideas/topic/{topicId}` is **public** and returns all ideas for a given topic (or `[]` if none).
- All write operations on `/api/ideas` require JWT, and only the owner can update/delete their idea.
- You cannot create an idea for a `CLOSED` topic (403 Forbidden).
- If the related topic is `CLOSED`, updating or deleting an idea returns `403 Forbidden` with: `This topic is closed. No modifications allowed.`

### 6.1. POST /api/topics — Create a Topic
```bash
TOKEN="your_jwt_token_here"

curl -X POST http://localhost:5257/api/topics \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
           "title": "My First Topic",
           "description": "A topic for new ideas"
         }'
```

**Expected Response (201 Created):**
```json
{
  "id": "generated-uuid",
  "title": "My First Topic",
  "description": "A topic for new ideas",
  "status": "OPEN",
  "ownerId": "user-uuid",
  "createdAt": "2026-03-30T00:00:00Z",
  "updatedAt": "2026-03-30T00:00:00Z",
  "winningIdea": null
}
```

### 6.2. GET /api/topics — Get All Topics (Public)
```bash
curl http://localhost:5257/api/topics
```

**Expected Response (200 OK):**
```json
[
  {
    "id": "generated-uuid",
    "title": "My First Topic",
    "description": "A topic for new ideas",
    "status": "OPEN",
    "ownerId": "user-uuid",
    "createdAt": "2026-03-30T00:00:00Z",
    "updatedAt": "2026-03-30T00:00:00Z",
    "winningIdea": null
  }
]
```

### 6.3. GET /api/topics/{id} — Get Topic by ID (Public)
```bash
TOPIC_ID="generated-uuid"

curl http://localhost:5257/api/topics/$TOPIC_ID
```

**Expected Response (200 OK):** Topic object.
**Expected Response (404 Not Found):** Topic does not exist.

### 6.4. PUT /api/topics/{id} — Update a Topic (Owner only)
```bash
TOKEN="your_jwt_token_here"
TOPIC_ID="generated-uuid"

curl -X PUT http://localhost:5257/api/topics/$TOPIC_ID \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
           "title": "Updated Title",
           "description": "Updated description",
           "status": "CLOSED"
         }'
```

**Expected Response (200 OK):** Updated topic object.
**Expected Response (404 Not Found):** Topic does not exist.
**Expected Response (403 Forbidden):** You are not authorized to modify this topic.
**Expected Response (400 Bad Request):** Invalid data.

Note: If you update a topic and set `status` to `CLOSED`, the response includes an `X-Info` header indicating the topic cannot be reopened. Once closed, attempts to set `status` back to `OPEN` will return `400 Bad Request`.
When a topic is `CLOSED`, `GET /api/topics` and `GET /api/topics/{id}` will include `winningIdea` if an idea is marked with `isWinning=true`.

### 6.5. DELETE /api/topics/{id} — Delete a Topic (Owner only)
```bash
TOKEN="your_jwt_token_here"
TOPIC_ID="generated-uuid"

curl -X DELETE http://localhost:5257/api/topics/$TOPIC_ID \
     -H "Authorization: Bearer $TOKEN"
```

**Expected Response (200 OK):** A confirmation message indicating the topic (and all related ideas/votes) were deleted.
**Expected Response (404 Not Found):** Topic does not exist.
**Expected Response (403 Forbidden):** You are not authorized to modify this topic.

### 6.6. POST /api/ideas — Create an Idea (Authenticated)
```bash
TOKEN="your_jwt_token_here"
TOPIC_ID="generated-uuid"

curl -X POST http://localhost:5257/api/ideas \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
           "topicId": "'"$TOPIC_ID"'",
           "title": "My Idea",
           "description": "Some details"
         }'
```

### 6.6a. GET /api/ideas/topic/{topicId} — Get Ideas by Topic (Public)
```bash
TOPIC_ID="generated-uuid"

curl http://localhost:5257/api/ideas/topic/$TOPIC_ID
```

**Expected Response (200 OK):** An array of ideas. If the topic does not exist (or has no ideas), the response is `[]`.

### 6.7. PUT /api/ideas/{id} — Update an Idea (Owner only)
```bash
TOKEN="your_jwt_token_here"
IDEA_ID="generated-uuid"

curl -X PUT http://localhost:5257/api/ideas/$IDEA_ID \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
           "title": "Updated title",
           "description": "Updated description"
         }'
```

### 6.8. DELETE /api/ideas/{id} — Delete an Idea (Owner only)
```bash
TOKEN="your_jwt_token_here"
IDEA_ID="generated-uuid"

curl -X DELETE http://localhost:5257/api/ideas/$IDEA_ID \
     -H "Authorization: Bearer $TOKEN"
```

**Expected Response (200 OK):** A confirmation message indicating the idea (and all related votes) were deleted.
**Expected Response (404 Not Found):** Idea does not exist.
**Expected Response (403 Forbidden):** You are not authorized to modify this idea, or the topic is closed.

## 6.9. Votes (US 2.2)

Votes allow authenticated users to vote on one or more ideas of the same topic. A user can only vote once per idea (enforced by a unique constraint in the database).

Important rules:
- `GET` vote endpoints are **public** (no JWT required).
- `POST/PUT/DELETE` vote endpoints require JWT.
- If the idea's topic is `CLOSED`, voting (create/update/delete) is forbidden and returns `403 Forbidden` with: `This topic is closed. Voting is no longer allowed.`
- Only the owner of a vote can modify/delete it; otherwise returns `403 Forbidden` with: `You can only modify or delete your own vote.`

### 6.9.1. GET /api/votes — Get All Votes (Public)
```bash
curl http://localhost:5257/api/votes
```

### 6.9.2. GET /api/votes/idea/{ideaId} — Get Votes for an Idea (Public)
```bash
IDEA_ID="generated-uuid"
curl http://localhost:5257/api/votes/idea/$IDEA_ID
```

### 6.9.3. GET /api/votes/{voteId} — Get a Vote by ID (Public)
```bash
VOTE_ID="generated-uuid"
curl http://localhost:5257/api/votes/$VOTE_ID
```

### 6.9.4. POST /api/votes — Cast a Vote (Authenticated)
```bash
TOKEN="your_jwt_token_here"
IDEA_ID="generated-uuid"

curl -X POST http://localhost:5257/api/votes \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
           "ideaId": "'"$IDEA_ID"'"
         }'
```

### 6.9.5. PUT /api/votes/{voteId} — Update a Vote (Owner only)
This endpoint updates the vote to point to a different idea. If you already voted for the target idea, the API returns `409 Conflict`.
```bash
TOKEN="your_jwt_token_here"
VOTE_ID="generated-uuid"
NEW_IDEA_ID="generated-uuid"

curl -X PUT http://localhost:5257/api/votes/$VOTE_ID \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
           "ideaId": "'"$NEW_IDEA_ID"'"
         }'
```

### 6.9.6. DELETE /api/votes/{voteId} — Delete a Vote (Owner only)
```bash
TOKEN="your_jwt_token_here"
VOTE_ID="generated-uuid"

curl -X DELETE http://localhost:5257/api/votes/$VOTE_ID \
     -H "Authorization: Bearer $TOKEN"
```

## 7. Running Tests
```bash
dotnet test
```

To generate a coverage report:
```bash
dotnet clean
rm -rf test/TestResults coverage-report
dotnet test --collect:"XPlat Code Coverage" --results-directory test/TestResults
dotnet tool restore
dotnet tool run reportgenerator \
  -reports:"test/TestResults/**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html
```

Open `coverage-report/index.html` to view the report.

## 8. Common Issues

- **401 Unauthorized**: Verify your token is valid and not expired. Check the `Authorization: Bearer <token>` header format.
- **403 Forbidden**: You are trying to update or delete a topic that was created by another user.
- **404 Not Found on Topics**: Ensure the topic ID exists in the database.
- **400 Bad Request**: Check that `title` is not empty and does not exceed 200 characters. For updates, `status` must be `OPEN` or `CLOSED`.
- **Database Connection Error**: Ensure the Docker container `cis-mysql-phase1` is running on port `3307`.
- **Port Conflict**: If port `5257` is in use, check `launchSettings.json` to update the port.
