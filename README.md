# QuoteFlow — Mini Pricing Platform

A backend service that calculates service pricing (quote) based on configurable rules, built with .NET 10 and Clean Architecture.

---

## Architecture Overview

```
src/
  QuoteFlow.Core/           # Domain models & interfaces (no dependencies)
    ├── Pricing/            # QuoteRequest, QuoteResult, IPricingEngine
    ├── Rules/              # PricingRule, IRuleRepository, Parameters
    └── Jobs/               # BulkJob, BulkJobItem, IJobRepository

  QuoteFlow.Application/    # Business logic (depends on Core only)
    ├── Pricing/            # PricingService
    ├── Rules/              # RuleService
    └── Jobs/               # JobService

  QuoteFlow.Infrastructure/ # Implementations (depends on Core + Application)
    ├── Pricing/            # PricingEngine
    ├── Rules/              # InMemoryRuleRepository
    └── Jobs/               # InMemoryJobRepository, BulkJobWorker

  QuoteFlow.API/            # HTTP layer (depends on Application + Infrastructure)
    └── Endpoints/
        ├── Pricing/        # POST /quotes/price, POST /quotes/bulk
        ├── Rules/          # CRUD /rules
        ├── Jobs/           # GET /jobs/{id}
        └── Health/         # GET /health

tests/
  QuoteFlow.UnitTests/      # PricingEngine & RuleService tests
  QuoteFlow.IntegrationTests/ # API endpoint tests via WebApplicationFactory
```

### Pricing Flow

```
Request (weight, origin, destination, basePrice)
  │
  ├── 1. WeightTier rules   → override BasePrice if weight matches
  ├── 2. RemoteAreaSurcharge → add Surcharge if destination matches
  └── 3. TimeWindowPromotion → apply Discount if time & day match

FinalPrice = BasePrice + Surcharge - Discount (minimum 0)
```

---

## Rule Types

| Type | Parameters | Effect |
|---|---|---|
| `WeightTier` | minWeight, maxWeight, price | Sets base price by weight range |
| `RemoteAreaSurcharge` | areaCodes[], surchargeAmount | Adds flat surcharge for remote areas |
| `TimeWindowPromotion` | startHour, endHour, daysOfWeek[], discountPercent | Applies % discount in time window |

Rules have: `priority` (lower = applied first), `effectiveFrom`, `effectiveTo`, `isActive`

---

## Setup & Run

### With Docker (recommended)

```bash
docker-compose up --build
```

API available at: `http://localhost:8080`

### Local Development

```bash
dotnet run --project src/QuoteFlow.API
```

API available at: `https://localhost:7xxx` (see launchSettings.json)

### Run Tests

```bash
dotnet test
```

---

## API Documentation

OpenAPI/Swagger available at: `http://localhost:8080/openapi/v1.json`

### Endpoints

| Method | Path | Description |
|---|---|---|
| GET | /health | System health check |
| POST | /quotes/price | Calculate price immediately |
| POST | /quotes/bulk | Submit JSON list, get job_id |
| POST | /quotes/bulk/csv | Upload CSV file, get job_id |
| GET | /jobs/{job_id} | Track job status & results |
| GET | /rules | List all pricing rules |
| GET | /rules/{id} | Get rule by ID |
| POST | /rules | Create new rule |
| PUT | /rules/{id} | Update rule |
| DELETE | /rules/{id} | Delete rule |

---

## Sample Requests

### Calculate price immediately

```bash
curl -X POST http://localhost:8080/quotes/price \
  -H "Content-Type: application/json" \
  -d '{
    "originCode": "BKK",
    "destinationCode": "CNX",
    "weight": 3.0,
    "basePrice": 100
  }'
```

Response:
```json
{
  "originCode": "BKK",
  "destinationCode": "CNX",
  "weight": 3.0,
  "inputBasePrice": 100,
  "basePrice": 100,
  "surcharge": 50,
  "discount": 0,
  "finalPrice": 150,
  "appliedRules": ["Standard Weight Tier 0-5kg", "Remote Area Surcharge - North"],
  "calculatedAt": "2024-01-01T10:00:00+00:00"
}
```

### Bulk submission (JSON)

```bash
curl -X POST http://localhost:8080/quotes/bulk \
  -H "Content-Type: application/json" \
  -d '[
    {"originCode": "BKK", "destinationCode": "CNX", "weight": 3.0, "basePrice": 100},
    {"originCode": "BKK", "destinationCode": "HKT", "weight": 7.5, "basePrice": 180}
  ]'
```

Response:
```json
{ "jobId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" }
```

### Bulk submission (CSV)

```bash
curl -X POST http://localhost:8080/quotes/bulk/csv \
  -F "file=@sample-data/bulk_quotes.csv"
```

### Track job status

```bash
curl http://localhost:8080/jobs/{job_id}
```

### Create a pricing rule

```bash
curl -X POST http://localhost:8080/rules \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Weekend Promotion",
    "ruleType": "TimeWindowPromotion",
    "priority": 30,
    "isActive": true,
    "effectiveFrom": "2024-01-01T00:00:00+00:00",
    "effectiveTo": null,
    "parameters": "{\"startHour\":0,\"endHour\":23,\"daysOfWeek\":[0,6],\"discountPercent\":15}"
  }'
```

---

## Default Seed Data

The system starts with pre-loaded rules:

| Rule | Type | Detail |
|---|---|---|
| Weight Tier 0-5kg | WeightTier | 100 THB |
| Weight Tier 5-15kg | WeightTier | 180 THB |
| Weight Tier 15-30kg | WeightTier | 300 THB |
| Remote North (CNX, LPG, PYY) | RemoteAreaSurcharge | +50 THB |
| Remote South (SGZ, NWT, PTN) | RemoteAreaSurcharge | +60 THB |
| Flash Sale Friday 8:00-12:00 | TimeWindowPromotion | -20% |

---

## Tech Stack

- .NET 10 / ASP.NET Core Minimal APIs
- In-memory storage (ConcurrentDictionary)
- Background worker (IHostedService + Channel)
- OpenAPI built-in
- xUnit + NSubstitute + FluentAssertions
- Docker + docker-compose
