# GoalStream 

## Project Overview

GoalStream is a small **event-driven sports highlights backend**.

It demonstrates how an incoming match event flows:

1. Into an HTTP API (`Events.Api`).
2. Through **Kafka**.
3. Into a **Postgres** read model (`Highlights.Api`).
4. Gets enriched by a **background worker**.
5. And is finally served via **REST endpoints**, with optional **Redis caching**.

This is a **learning**, not a production system:
- No authentication/authorization.
- Minimal error handling and observability.
- Single-instance Kafka/Postgres/Redis, tuned for local development.

---

## Architecture 

- **Events.Api**
  - `POST /events`  
    - Accepts a match event payload (`matchId`, `occurredAt`, `eventType`, `team`, `player`, `description`).
    - Does basic validation.
    - Publishes a JSON message to Kafka topic `match-events` using Confluent.Kafka.
  - Swagger UI and `/health` endpoint exposed.

- **Highlights.Api**
  - **Kafka consumer**
    - Background service subscribes to `match-events`.
    - For GOAL events, inserts a row into the `highlights` table (Postgres) with status `PENDING_AI`.
  - **Enrichment worker**
    - Background service periodically scans for `PENDING_AI` highlights.
    - Uses a stub `IHighlightEnricher` to generate a deterministic title/summary.
    - Updates highlights to `READY`.
  - **Read APIs**
    - `GET /highlights`  
      - Returns a list of Highlight DTOs.
      - Optional filters: `matchId`, `status`, plus simple paging (`page`, `pageSize`).
      - Ordered by event time (newest first).
    - `GET /highlights/{id}`  
      - Returns a single highlight by `Guid`, or `404` if not found.
  - **Redis cache**
    - Uses Redis and `IHighlightCache` to cache `GET /highlights` responses **only** when:
      - `matchId` is specified,
      - (optionally) `status` is the default/READY,
      - and it’s the **first page**.
    - Cache TTL is short (around 60 seconds).
    - If Redis is down, the API still reads from Postgres; the cache is best-effort only.

---

## Tech Stack

- **.NET**: `net10.0` (Minimal APIs)
- **Web**: ASP.NET Core + Swagger / OpenAPI
- **Messaging**: Kafka (Confluent.Kafka client)
- **Database**: Postgres + EF Core (code-first migrations)
- **Caching**: Redis (StackExchange.Redis)
- **Infra**: Docker + docker-compose

---

## Running the Project

From the repository root:

```bash
docker compose up --build
```

Once everything is up:

- Events API Swagger: http://localhost:5001/swagger
- Highlights API Swagger: http://localhost:5002/swagger

The docker-compose file also starts:

- Postgres (single instance).
- Kafka (single broker, KRaft mode).
- Redis.

### Trying the End-to-End Flow

You can exercise the whole pipeline with just a few calls.

1. Send a match event to Events.Api
   Use Swagger or a tool like curl / PowerShell to POST to Events.Api:
   - Endpoint: `POST http://localhost:5001/events`
   - Body (example):

   ```json
   {
     "matchId": "00000000-0000-0000-0000-000000000001",
     "occurredAt": "2025-12-08T20:16:00Z",
     "eventType": "goal",
     "team": "home",
     "player": "John Doe",
     "description": "Curled it into the top corner from outside the box."
   }
   ```

2. Kafka → Postgres
   Events.Api publishes the event to Kafka topic `match-events`.
   Highlights.Api consumes from Kafka and inserts a new row in the highlights table with status = `"PENDING_AI"`.

3. Enrichment worker
   A background worker in Highlights.Api picks up `PENDING_AI` highlights.
   It uses a stub enrichment implementation to generate:
   - A title (e.g. "Home GOAL by John Doe").
   - A friendly summary.
   The highlight is updated to status = `"READY"`.

4. Fetch highlights
   List highlights:

   ```text
   GET http://localhost:5002/highlights
   ```

   Fetch a specific highlight by its id:

   ```text
   GET http://localhost:5002/highlights/{id}
   ```

5. See Redis caching in action
   Call:

   ```text
   GET http://localhost:5002/highlights?matchId=00000000-0000-0000-0000-000000000001&page=1&pageSize=10
   ```

   Call the same URL again within ~60 seconds.

   The second call should be served from Redis (you can confirm via logs or Redis CLI).

### Notes & Limitations

- 
  - No authentication or authorization.
  - Error handling is minimal and mostly developer-focused.
  - No metrics/monitoring, no multi-region or HA design.
- Enrichment is stubbed:
  - The “AI” is a deterministic stub implementation.
  - A real LLM/AI endpoint can be wired in later.
- Local-only infra:
  - Kafka, Postgres, and Redis are single-instance containers.
  - Config is tuned for local development, not production.
- This repository is meant to showcase architecture, messaging, background workers, EF Core, and caching in a compact, readable way.
