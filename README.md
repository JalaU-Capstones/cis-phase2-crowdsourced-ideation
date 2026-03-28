# CIS Phase 2: Crowdsourced Ideation (Idea Flow)

## Project Description
Phase 2 of the Crowdsourced Ideation Solution (CIS). This service is a .NET 8 Minimal API following Vertical Slice Architecture (ADR-004), using EF Core 8 (ADR-003) and xUnit + FluentAssertions + Moq (ADR-005).

## How To Clone And Run
```bash
git clone https://gitlab.com/jala-university1/cohort-5/ES.CO.CSSD-232.GA.T1.26.M2/secci-n-c/capstone-sd3/idea-flow/cis-phase2-crowdsourced-ideation-platform/cis-phase2-crowdsourced-ideation.git
cd cis-phase2-crowdsourced-ideation
```

Prerequisites:
- .NET SDK 8 (pinned via `global.json`)
- Docker & Docker Compose

## How To Start The MySQL Database Using Docker (Shared With Phase 1)
We use Docker to run MySQL 8.0 with the exact legacy schema (no changes allowed).

1. Start the database:
```bash
docker compose up -d
```

> ⚠️ **If you already have the `cis-mysql-phase1` container running from Phase 1,**
> Docker will not re-run `init.sql` automatically. Run the script manually once:
> ```bash
> docker exec -i cis-mysql-phase1 mysql -u sd3user -psd3pass sd3 < init.sql
> ```
> Then verify the new tables were created:
> ```bash
> docker exec -i cis-mysql-phase1 mysql -u sd3user -psd3pass sd3 -e "SHOW TABLES;"
> ```
> You should see: `ideas`, `topics`, `users`, `votes`.

2. Verify it's running:
```bash
docker ps
# You should see container 'cis-mysql-phase1'
```

3. Connect locally (optional, for debugging):
- Host: `localhost`
- Port: `3307`
- Database: `sd3`
- User: `sd3user`
- Password: `sd3pass`

Note: The legacy CLI config uses port 3307.

## How To Run The API
```bash
dotnet restore
dotnet run --project src/CIS-Phase2-Crowdsourced-Ideation
```

Swagger (Development only): `http://localhost:5257/swagger` (see `launchSettings.json` for the current ports).

## How To Generate Test Coverage Report
1. Run tests and collect coverage:
```bash
dotnet test --collect:"XPlat Code Coverage"
```

2. Generate an HTML report (local tool):
```bash
dotnet tool restore
dotnet tool run reportgenerator \
  -reports:"test/**/TestResults/**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html
```

3. Open the report:
- `coverage-report/index.html`

## Architecture Summary
- Minimal APIs + Vertical Slice Architecture (ADR-004)
- EF Core 8 with MySQL (ADR-003)
- Testing: xUnit + FluentAssertions + Moq (ADR-005)
- Authentication: JWT Bearer (delegated from Phase 1 User Management API)

