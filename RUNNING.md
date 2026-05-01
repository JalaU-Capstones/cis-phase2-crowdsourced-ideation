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

Verify the containers are running:
```bash
docker ps
# You should see: cis-mysql-phase1  and  cis-mongo-phase1
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

### Switching the default provider (optional)

The `Persistence:Provider` key in `appsettings.json` controls the default `IRepositoryAdapter` used internally. It does **not** change endpoint routing — `/api/v1/` always uses MySQL and `/api/v2/` always uses MongoDB regardless of this setting.

```json
"Persistence": {
  "Provider": "MySQL"
}
```

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

To filter by category:
```bash
# Only MongoDB repository unit tests
dotnet test --filter "FullyQualifiedName~Mongo"

# Only V2 endpoint integration tests
dotnet test --filter "FullyQualifiedName~V2"

# Verbose output
dotnet test --logger "console;verbosity=detailed"
```

> **Note:** Migration tests use Testcontainers and require Docker running locally. They are excluded from the CI pipeline. To run them locally:
> ```bash
> dotnet test --filter "FullyQualifiedName~Migration"
> ```

## 11. Migrating Data from MySQL to MongoDB (US 2.2)

> **Responsibility boundary:**
> - The **Java Phase 1** migration is solely responsible for migrating the `users` collection.
> - The **C# Phase 2** migration (this script) only migrates `topics`, `ideas`, and `votes`.
> - The C# script will **fail with a clear error** if any referenced user ID is missing from MongoDB, ensuring you always run Phase 1 first.

### Migration order — mandatory

**Step 1:** Run the Java Phase 1 user migration first (populates the `users` collection in MongoDB).

**Step 2:** Run the C# Phase 2 migration below (migrates topics, ideas and votes).

> Skipping Step 1 will cause the C# script to abort with:
> `Missing users in MongoDB. Please run Phase 1 user migration first.`

### About MigrationService.cs vs migrate-to-mongo.csx

Both implement the same upsert + validation logic but serve different purposes:
- `MigrationService.cs` — testable C# class used by the Testcontainers integration tests.
- `migrate-to-mongo.csx` — standalone executable script for developers to run manually.

They are independent: the script does not call the service class.

### Install dotnet-script (one time)
```bash
dotnet tool install -g dotnet-script
```

### Run the migration
```bash
dotnet script migration/migrate-to-mongo.csx -- \
  --mysql "Server=localhost;Port=3307;Database=sd3;User Id=sd3user;Password=sd3pass;SslMode=None;AllowPublicKeyRetrieval=true;" \
  --mongo "mongodb://localhost:27017" \
  --db    "sd3"
```

### Expected output
```
=== CIS Phase 2 -- Migration MySQL to MongoDB ===
  MySQL : Server=localhost;Port=3307;Database=sd3;User Id=sd3user;Passwor...
  Mongo : mongodb://localhost:27017
  DB    : sd3
  Scope : topics, ideas, votes (users owned by Java Phase 1)

-- [0/4] Validating Phase 1 users in MongoDB...
   OK - all referenced users exist in MongoDB
-- [1/3] Migrating topics...
   OK 18 topics migrated
-- [2/3] Migrating ideas...
   OK 95 ideas migrated
-- [3/3] Migrating votes...
   OK 310 votes migrated

-- Validating integrity...
   OK       topics     MySQL=    18  MongoDB=    18
   OK       ideas      MySQL=    95  MongoDB=    95
   OK       votes      MySQL=   310  MongoDB=   310

Migration completed. 100% data consistency verified.
```

### If Phase 1 has not been run yet
```
-- [0/4] Validating Phase 1 users in MongoDB...
   ERROR: 42 user ID(s) referenced in MySQL are missing from MongoDB.
   Missing IDs: abc123, def456 ...

   Please run Phase 1 Java user migration first, then retry.
```

### Rollback to MySQL

No redeployment needed. `/api/v1/` endpoints never stopped using MySQL. To roll back, simply point clients back to `/api/v1/`.

## 12. Complete API Examples (V1 + V2) with HATEOAS `_links`

Notes:
- All read endpoints are public unless explicitly marked as authenticated.
- All write endpoints require `Authorization: Bearer $TOKEN`.
- All resource responses include `_links` and these links stay in the same API version (`/api/v1/*` links in v1 responses, `/api/v2/*` links in v2 responses).

### 12.1. Topics

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

### 12.2. Ideas

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

### 12.3. Votes

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

### 12.4. Statistics

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


## 13. Automated ELT Migration — US 2.3 (Blue-Green / API Sunsetting)

### Overview
The migration follows a three‑phase Blue‑Green strategy coordinated via **command‑line flags** (no HTTP endpoints). Each phase requires restarting the Java Phase 1 API with different JVM arguments, plus a one‑time execution of the C# worker.

| Phase | Java flag | C# state | v1 behavior | v2 behavior |
|-------|-----------|----------|-------------|-------------|
| 1 — Normal | *(none)* | `IsMigrationRunning=false` <br> `HasMigrated=false` | Full read+write | Full read+write |
| 2 — Migration running | `-Dmigration.maintenance=true` | `IsMigrationRunning=true` | GET only (writes → **503**) | GET only (writes → **503**) |
| 3 — Post‑migration | `-Dsunset.v1=true` | `HasMigrated=true` | GET + `Warning: 299` header; writes → **410 Gone** | Full read+write |

### Mandatory execution order

1. **Migrate users (Java) — offline**   
   Stop any running Java API instance. Use the `migrate` Spring profile to perform the MySQL → MongoDB user migration without exposing the API:
   ```bash
   mvn spring-boot:run -Dspring-boot.run.profiles=migrate
   ```
If you don’t have the Maven wrapper, install it with `mvn wrapper:wrapper` or use `mvn`.

2. **Start Java in maintenance mode**
   ```bash
   mvn spring-boot:run -Dspring-boot.run.jvmArguments="-Dmigration.maintenance=true"
   ```
   This blocks all writes on both `/api/v1/**` and `/api/v2/**` with **503 Service Unavailable**.

3. **Run the C# migration (topics, ideas, votes)**
   ```bash
   dotnet run --project src/CIS-Phase2-Crowdsourced-Ideation \
     -- \
     --MigrationSettings:RunOnStartup=true \
     --MigrationSettings:DowntimeSeconds=30
   ```
  * `RunOnStartup` – set to `true` to execute the worker immediately (default: `false`).
  * `DowntimeSeconds` – seconds the C# API stays in maintenance mode after its own migration finishes, before activating the `HasMigrated` flag.

   The worker will:
  * Read all topics, ideas and votes from MySQL.
  * Upsert them into MongoDB (idempotent).
  * Validate that MySQL and MongoDB counts match exactly.
  * Wait the configured downtime, then set `HasMigrated = true`.

   If validation fails, the worker logs the error and clears `IsMigrationRunning` so the system reverts to dual‑API mode.

4. **Restart Java in sunset mode**
   Stop the Java process (Ctrl+C) and start it with:
   ```bash
   mvn spring-boot:run -Dspring-boot.run.jvmArguments="-Dsunset.v1=true"
   ```
   Now:
  * POST/PUT/DELETE `/api/v1/**` → **410 Gone**
  * GET `/api/v1/**` → **200** with `Warning: 299` header
  * `/api/v2/**` operates normally

### C# worker behaviour (internal)
The `AutomatedMigrationWorker` no longer calls any Java HTTP endpoint. It only manages the C#‑side `MigrationStateManager` flags. The `MigrationSunsettingMiddleware` enforces the same rules as Java:
* During `IsMigrationRunning` → blocks writes on both API versions with **503**.
* After `HasMigrated` → permanently disables v1 writes (**410**) and adds the **Warning** header to v1 reads.

### Configuration reference
`appsettings.json` / CLI arguments:
```json
"MigrationSettings": {
  "RunOnStartup": false,
  "DowntimeSeconds": 30
}
```

| Key | Default | Description |
|-----|---------|-------------|
| RunOnStartup | false | When `true`, the worker runs immediately on application startup. |
| DowntimeSeconds | 30 | Seconds to hold the C# API in maintenance mode after its own migration succeeds. |

### Testing the full flow
```bash
# 1. Start from scratch
docker compose down -v && docker compose up -d

# 2. Populate MySQL with some test data (use the C# API v1 endpoints)

# 3. Java: migrate users
mvn spring-boot:run -Dspring-boot.run.profiles=migrate

# 4. Java: start in maintenance
mvn spring-boot:run -Dspring-boot.run.jvmArguments="-Dmigration.maintenance=true"

# 5. C#: run migration
dotnet run --project src/CIS-Phase2-Crowdsourced-Ideation \
  -- \
  --MigrationSettings:RunOnStartup=true \
  --MigrationSettings:DowntimeSeconds=10

# 6. Java: restart in sunset
#    (stop the maintenance instance first)
mvn spring-boot:run -Dspring-boot.run.jvmArguments="-Dsunset.v1=true"

# 7. Verify
#    - POST /api/v1/topics → 410 (C#) / POST /api/v1/users → 410 (Java)
#    - GET  /api/v1/topics → 200 + Warning header
#    - POST /api/v2/topics → 201 (back to normal)
```

### Rollback
To revert to dual‑API mode before the sunset phase, simply restart the Java API without any special flags and restart the C# API. The MySQL database is never modified by the migration, so `/api/v1/` data remains intact.
