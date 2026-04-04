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

> ⚠️ **If you already have the `cis-mysql-phase1` container running from Phase 1**, run the script manually:
> ```powershell
> Get-Content init.sql | docker exec -i cis-mysql-phase1 mysql -u sd3user -psd3pass sd3
> ```
> Then verify the tables were created:
> ```bash
> docker exec -i cis-mysql-phase1 mysql -u sd3user -psd3pass sd3 -e "SHOW TABLES;"
> ```
> You should see: `ideas`, `topics`, `users`, `votes`.

If starting fresh:
```bash
docker compose up -d
```

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

3. Copy the returned token and use it in the `Authorization: Bearer <token>` header for all Topics endpoints.

## 6. Testing the API

All `/topics` endpoints require a valid JWT token. Use Swagger UI or the curl examples below.

### 6.1. POST /topics — Create a Topic
```bash
TOKEN="your_jwt_token_here"

curl -X POST http://localhost:5257/topics \
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
  "createdBy": "user-uuid",
  "createdAt": "2026-03-30T00:00:00Z",
  "updatedAt": "2026-03-30T00:00:00Z"
}
```

### 6.2. GET /topics — Get All Topics
```bash
TOKEN="your_jwt_token_here"

curl http://localhost:5257/topics \
     -H "Authorization: Bearer $TOKEN"
```

**Expected Response (200 OK):**
```json
[
  {
    "id": "generated-uuid",
    "title": "My First Topic",
    "description": "A topic for new ideas",
    "status": "OPEN",
    "createdBy": "user-uuid",
    "createdAt": "2026-03-30T00:00:00Z",
    "updatedAt": "2026-03-30T00:00:00Z"
  }
]
```

### 6.3. GET /topics/{id} — Get Topic by ID
```bash
TOKEN="your_jwt_token_here"
TOPIC_ID="generated-uuid"

curl http://localhost:5257/topics/$TOPIC_ID \
     -H "Authorization: Bearer $TOKEN"
```

**Expected Response (200 OK):** Topic object.
**Expected Response (404 Not Found):** Topic does not exist.

### 6.4. PUT /topics/{id} — Update a Topic
```bash
TOKEN="your_jwt_token_here"
TOPIC_ID="generated-uuid"

curl -X PUT http://localhost:5257/topics/$TOPIC_ID \
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
**Expected Response (400 Bad Request):** Invalid data.

### 6.5. DELETE /topics/{id} — Delete a Topic
```bash
TOKEN="your_jwt_token_here"
TOPIC_ID="generated-uuid"

curl -X DELETE http://localhost:5257/topics/$TOPIC_ID \
     -H "Authorization: Bearer $TOKEN"
```

**Expected Response (204 No Content)**
**Expected Response (404 Not Found):** Topic does not exist.

## 7. Running Tests
```bash
dotnet test
```

To generate a coverage report:
```bash
dotnet test --collect:"XPlat Code Coverage"
dotnet tool restore
dotnet tool run reportgenerator \
  -reports:"test/**/TestResults/**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html
```

Open `coverage-report/index.html` to view the report.

## 8. Common Issues

- **401 Unauthorized**: Verify your token is valid and not expired. Check the `Authorization: Bearer <token>` header format.
- **404 Not Found on Topics**: Ensure the topic ID exists in the database.
- **400 Bad Request**: Check that `title` is not empty and does not exceed 200 characters. For updates, `status` must be `OPEN` or `CLOSED`.
- **Database Connection Error**: Ensure the Docker container `cis-mysql-phase1` is running on port `3307`.
- **Port Conflict**: If port `5257` is in use, check `launchSettings.json` to update the port.

## 9. Voting on Ideas

### 9.1. POST /api/ideas/{ideaId}/votes — Cast a Vote

Cast a vote on a specific idea. Each user can only vote once per idea.

```bash
TOKEN="your_jwt_token_here"
IDEA_ID="idea-uuid-here"

curl -X POST http://localhost:5257/api/ideas/$IDEA_ID/votes \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json"
