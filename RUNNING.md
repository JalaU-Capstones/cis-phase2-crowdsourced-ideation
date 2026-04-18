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
