# QuoteFlow — Mini Pricing Platform

A backend service for calculating shipping quotes based on configurable pricing rules. Built with .NET 10 and Clean Architecture.

---

## Architecture

```
src/
  QuoteFlow.Core/             # Domain models & interfaces (no dependencies)
    ├── Pricing/              # QuoteRequest, QuoteResult, IPricingEngine, IDistanceService
    ├── Rules/                # PricingRule, RuleType, IRuleRepository, Parameters/
    ├── Jobs/                 # BulkJob, BulkJobItem, IJobRepository
    └── Locations/            # Location, Region, ILocationRepository

  QuoteFlow.Application/      # Use cases (depends on Core only)
    ├── Pricing/              # PricingService
    ├── Rules/                # RuleService
    ├── Jobs/                 # JobService
    └── Locations/            # LocationService

  QuoteFlow.Infrastructure/   # Implementations (depends on Core + Application)
    ├── Pricing/              # PricingEngine, OsrmDistanceService
    ├── Rules/                # RuleRepository (in-memory)
    ├── Jobs/                 # JobRepository, BulkJobWorker
    └── Locations/            # LocationRepository (31 Thai locations)

  QuoteFlow.API/              # HTTP layer (Controllers + Middleware)
    ├── Controllers/          # Quotes, Rules, Jobs, Locations, Health
    └── Middleware/           # GlobalExceptionHandler

tests/
  QuoteFlow.UnitTests/        # PricingEngine, RuleService
  QuoteFlow.IntegrationTests/ # API endpoints via WebApplicationFactory
```

### Pricing Pipeline

```
POST /quotes/price
        │
        ├── 1. ExchangeRate      → convert request currency to THB
        ├── 2. WeightTier        → override BasePrice by weight range
        ├── 3. VehicleType       → multiply BasePrice by vehicle multiplier
        ├── 4. FuelSurcharge     → add fuel cost (distance ÷ kmPerLiter × pricePerLiter)
        ├── 5. RemoteAreaSurcharge → add flat surcharge for remote destinations
        ├── 6. TimeWindowPromotion → apply % discount in time window
        └── 7. ExchangeRate      → convert result back to request currency

FinalPrice = BasePrice + Surcharge - Discount  (minimum 0)
```

Distance is fetched automatically from **OSRM** (free routing API) using coordinates stored per location.

---

## Rule Types

| ruleType | Value | Parameters | Effect |
|----------|-------|-----------|--------|
| `TimeWindowPromotion` | 0 | startHour, endHour, daysOfWeek[], discountPercent | % discount in time window |
| `RemoteAreaSurcharge` | 1 | areaCodes[], surchargeAmount | Flat surcharge for remote areas |
| `WeightTier` | 2 | minWeight, maxWeight, price | Override base price by weight |
| `ExchangeRate` | 3 | fromCurrency, toCurrency, rate | Currency conversion |
| `FuelSurcharge` | 4 | pricePerLiter | Fuel cost per liter (THB) |
| `VehicleType` | 5 | vehicleType, kmPerLiter, priceMultiplier | Vehicle efficiency & price factor |

Rules have: `priority` (lower = applied first), `effectiveFrom`, `effectiveTo`, `isActive`

---

## Setup & Run

### Docker (recommended)

```bash
docker-compose up --build
```

API: `http://localhost:8080`
API Docs: `http://localhost:8080/scalar/v1`

### Local Development

```bash
dotnet run --project src/QuoteFlow.API
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Runtime environment |
| `Osrm__BaseUrl` | `http://router.project-osrm.org` | OSRM routing API base URL |

### Run Tests

```bash
dotnet test
```

---

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | /health | Health check |
| POST | /quotes/price | Calculate price immediately |
| POST | /quotes/bulk | Submit JSON list → returns jobId |
| POST | /quotes/bulk/csv | Upload CSV file → returns jobId |
| GET | /jobs/{id} | Track job status & results |
| GET | /rules | List all pricing rules |
| POST | /rules | Create new rule |
| PUT | /rules/{id} | Update rule |
| DELETE | /rules/{id} | Delete rule |
| GET | /locations | List all locations |
| GET | /locations/{code} | Get location by code |

Full interactive docs: `http://localhost:8080/scalar/v1`

---

## Sample Requests

### Calculate price (with vehicle & currency)

```bash
curl -X POST http://localhost:8080/quotes/price \
  -H "Content-Type: application/json" \
  -d '{
    "originCode": "BKK",
    "destinationCode": "CNX",
    "weight": 5.0,
    "basePrice": 100,
    "vehicleType": "Truck",
    "currency": "THB"
  }'
```

Response:
```json
{
  "originCode": "BKK",
  "destinationCode": "CNX",
  "weight": 5.0,
  "inputBasePrice": 100,
  "basePrice": 150,
  "surcharge": 3517.63,
  "discount": 733.53,
  "finalPrice": 2934.10,
  "currency": "THB",
  "appliedRules": [
    "Standard Weight Tier 0-5kg",
    "Vehicle: Truck",
    "Fuel Price (685km × 40.50฿/L ÷ 8km/L)",
    "Remote Area Surcharge - North",
    "Flash Sale Friday Morning"
  ],
  "calculatedAt": "2026-04-11T09:00:00Z"
}
```

### Bulk (JSON)

```bash
curl -X POST http://localhost:8080/quotes/bulk \
  -H "Content-Type: application/json" \
  -d @sample-data/bulk-quotes.csv
```

### Bulk (CSV)

```bash
curl -X POST http://localhost:8080/quotes/bulk/csv \
  -F "file=@sample-data/bulk-quotes.csv"
```

CSV format:
```
originCode,destinationCode,weight,basePrice
BKK,CNX,3,50
BKK,HKT,10,100
```

### Update fuel price

```bash
curl -X PUT http://localhost:8080/rules/22222222-0000-0000-0000-000000000001 \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Fuel Price (THB/Liter)",
    "ruleType": 4,
    "priority": 40,
    "isActive": true,
    "effectiveFrom": "2024-01-01T00:00:00Z",
    "parameters": "{\"pricePerLiter\":42.50}"
  }'
```

---

## Default Seed Data

### Weight Tiers

| Rule | Weight | Price |
|------|--------|-------|
| Standard 0-5kg | 0–5 kg | 100 THB |
| Standard 5-15kg | 5–15 kg | 180 THB |
| Standard 15-30kg | 15–30 kg | 300 THB |

### Surcharges

| Rule | Area Codes | Amount |
|------|-----------|--------|
| Remote North | CNX, LPG, PYY | +50 THB |
| Remote South | SGZ, NWT, PTN | +60 THB |

### Promotions

| Rule | Condition | Discount |
|------|-----------|---------|
| Flash Sale Friday | Fri 08:00–12:00 | -20% |

### Vehicle Types

| Vehicle | km/L | Multiplier |
|---------|------|-----------|
| Motorcycle | 35 | 0.8× |
| Car | 14 | 1.0× |
| Van | 10 | 1.2× |
| Truck | 8 | 1.5× |

### Exchange Rates (THB base)

USD, EUR, SGD, JPY ↔ THB (update via `PUT /rules/{id}`)

---

## Tech Stack

- **Runtime**: .NET 10 / ASP.NET Core
- **Architecture**: Clean Architecture + Modular Monolith
- **Storage**: In-memory (ConcurrentDictionary)
- **Background Processing**: IHostedService + System.Threading.Channels
- **Distance API**: OSRM (free, no API key required)
- **API Docs**: Scalar (OpenAPI)
- **Testing**: xUnit + NSubstitute + FluentAssertions (29 tests)
- **Container**: Docker + docker-compose
