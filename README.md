# QuoteFlow — Mini Pricing Platform

ระบบคำนวณราคาค่าจัดส่งพัสดุ รองรับกฎราคาที่ปรับแต่งได้ พร้อม bulk processing
สร้างด้วย .NET 10 และ Clean Architecture

---

## Architecture

```
src/
  QuoteFlow.Core/               # Domain models & interfaces
    ├── Pricing/                # QuoteRequest, QuoteResult, IPricingEngine
    ├── Rules/                  # PricingRule, RuleType, Parameters/
    ├── Jobs/                   # BulkJob, BulkJobItem
    └── Locations/              # Location, Region

  QuoteFlow.Application/        # Use cases (depends on Core only)
    ├── Pricing/                # PricingService, IPricingService
    ├── Rules/                  # RuleService
    ├── Jobs/                   # JobService
    └── Locations/              # LocationService

  QuoteFlow.Infrastructure/     # Implementations
    ├── Pricing/                # PricingEngine, OsrmDistanceService,
    │                           # ExternalFuelPriceService, FuelPriceSyncWorker
    ├── Rules/                  # RuleRepository (JSON seed → in-memory)
    ├── Jobs/                   # JobRepository, BulkJobWorker
    ├── Locations/              # LocationRepository (JSON seed → in-memory)
    └── Data/                   # locations.json (77 provinces), rules.json (seed)

  QuoteFlow.API/                # HTTP layer
    ├── Controllers/            # Quotes, Rules, Jobs, Locations, Health
    └── Middleware/             # GlobalExceptionHandler, CorrelationIdMiddleware

tests/
  QuoteFlow.UnitTests/          # PricingEngine, RuleService (22 tests)
  QuoteFlow.IntegrationTests/   # API endpoints via WebApplicationFactory (7 tests)
```

---

## Pricing Pipeline

ทุก request ผ่าน pipeline 7 ขั้นตอนตามลำดับ priority ของ rule:

```
POST /quotes/price
        │
        ├── 1. ExchangeRate        → แปลง currency เป็น THB (ถ้าไม่ใช่ THB)
        ├── 2. WeightTier          → กำหนด basePrice ตามช่วงน้ำหนัก
        ├── 3. VehicleType         → คูณ basePrice ด้วย multiplier ตามประเภทรถ
        ├── 4. FuelSurcharge       → บวกค่าน้ำมัน = ระยะทาง ÷ km/L × ฿/L
        ├── 5. RemoteAreaSurcharge → บวกค่าพื้นที่ห่างไกล
        ├── 6. TimeWindowPromotion → หักส่วนลด % ตามวัน/เวลา
        └── 7. ExchangeRate        → แปลงกลับเป็น currency ที่ request มา

FinalPrice = BasePrice + Surcharge - Discount  (ขั้นต่ำ 0)
```

- ระยะทางดึงอัตโนมัติจาก **OSRM API** (free routing) จากพิกัดของ Location
- ราคาน้ำมันสามารถ sync จาก external API ทุก 24 ชั่วโมงโดยอัตโนมัติ
- **ทุก rule สามารถปิด (`isActive: false`) เพื่อข้ามการคำนวณขั้นตอนนั้นได้**

---

## Rule Types (กฎราคา 6 ประเภท)

ทุก rule มี field ร่วม:
- `priority` — ลำดับความสำคัญ (ยิ่งน้อยยิ่งทำก่อน)
- `isActive` — เปิด/ปิด rule (`false` = ข้ามการคำนวณนี้)
- `effectiveFrom` / `effectiveTo` — ช่วงเวลาที่ rule มีผล

### 1. WeightTier (ruleType = 2)

กำหนด basePrice ตามช่วงน้ำหนัก ถ้าน้ำหนักตรงกับช่วงไหนจะ **แทนที่** basePrice

```json
{
  "name": "Standard Weight Tier 0-5kg",
  "ruleType": 2,
  "priority": 10,
  "isActive": true,
  "effectiveFrom": "2024-01-01T00:00:00Z",
  "parameters": "{\"minWeight\":0,\"maxWeight\":5,\"price\":100}"
}
```

**Seed data:**

| Rule | น้ำหนัก | ราคา (THB) |
|------|---------|-----------|
| Standard 0-5kg | 0 – 5 kg | 100 |
| Standard 5-15kg | 5.01 – 15 kg | 180 |
| Standard 15-30kg | 15.01 – 30 kg | 300 |

### 2. RemoteAreaSurcharge (ruleType = 1)

บวกค่าเพิ่มเมื่อ **ปลายทาง** อยู่ในพื้นที่ห่างไกล

```json
{
  "name": "Remote Area Surcharge - North",
  "ruleType": 1,
  "priority": 20,
  "isActive": true,
  "effectiveFrom": "2024-01-01T00:00:00Z",
  "parameters": "{\"areaCodes\":[\"CNX\",\"LPG\",\"PYY\",\"NAN\",\"PYO\",\"CMR\"],\"surchargeAmount\":50}"
}
```

**Seed data:**

| Rule | พื้นที่ | ค่าเพิ่ม (THB) |
|------|--------|---------------|
| Remote North | CNX, LPG, PYY, NAN, PYO, CMR | +50 |
| Remote Northeast | MDH, LEI, BKN, NKP | +55 |
| Remote South | STN, YLA, PTN, NWT | +60 |
| Remote West | TAK, TRT | +45 |

### 3. TimeWindowPromotion (ruleType = 0)

ให้ส่วนลด % เมื่อ request อยู่ในช่วงวัน/เวลาที่กำหนด

```json
{
  "name": "Flash Sale Friday Morning",
  "ruleType": 0,
  "priority": 30,
  "isActive": true,
  "effectiveFrom": "2024-01-01T00:00:00Z",
  "parameters": "{\"startHour\":8,\"endHour\":12,\"daysOfWeek\":[5],\"discountPercent\":20}"
}
```

> `daysOfWeek`: 0=Sunday, 1=Monday, ..., 5=Friday, 6=Saturday

**Seed data:**

| Rule | เงื่อนไข | ส่วนลด |
|------|---------|--------|
| Flash Sale Friday Morning | ศุกร์ 08:00–12:00 | -20% |

### 4. ExchangeRate (ruleType = 3)

แปลงสกุลเงิน — ใช้ตอนเริ่มต้น (แปลงเข้า THB) และตอนจบ (แปลงกลับ)

```json
{
  "name": "USD → THB",
  "ruleType": 3,
  "priority": 1,
  "isActive": true,
  "effectiveFrom": "2024-01-01T00:00:00Z",
  "parameters": "{\"fromCurrency\":\"USD\",\"toCurrency\":\"THB\",\"rate\":36.00}"
}
```

**Seed data:**

| คู่สกุลเงิน | Rate |
|------------|------|
| USD → THB | 36.00 |
| THB → USD | 0.0278 |
| EUR → THB | 38.50 |
| THB → EUR | 0.0260 |
| SGD → THB | 26.50 |
| THB → SGD | 0.0377 |
| JPY → THB | 0.240 |
| THB → JPY | 4.170 |

> ปิด ExchangeRate rule ทั้งหมด → ระบบคำนวณเป็น THB ตลอด

### 5. FuelSurcharge (ruleType = 4)

บวกค่าน้ำมัน คำนวณจาก: `ระยะทาง ÷ km/L × ราคาน้ำมัน/L`

```json
{
  "name": "Fuel Price (THB/Liter)",
  "ruleType": 4,
  "priority": 40,
  "isActive": true,
  "effectiveFrom": "2024-01-01T00:00:00Z",
  "parameters": "{\"pricePerLiter\":40.50}"
}
```

**Seed data:** 40.50 THB/ลิตร

- อัปเดตราคาน้ำมันผ่าน `PUT /rules/22222222-0000-0000-0000-000000000001`
- หรือตั้งค่า `FuelPrice:ApiUrl` เพื่อ sync จาก external API ทุก 24 ชั่วโมงอัตโนมัติ
- **ปิด rule นี้ → ข้ามการคำนวณค่าน้ำมันทั้งหมด**

### 6. VehicleType (ruleType = 5)

กำหนด price multiplier และ fuel efficiency ตามประเภทรถ

```json
{
  "name": "Vehicle: Truck",
  "ruleType": 5,
  "priority": 35,
  "isActive": true,
  "effectiveFrom": "2024-01-01T00:00:00Z",
  "parameters": "{\"vehicleType\":\"Truck\",\"kmPerLiter\":8.0,\"priceMultiplier\":1.5}"
}
```

**Seed data:**

| ประเภทรถ | km/L | Multiplier | ผล |
|---------|------|-----------|-----|
| Motorcycle | 35 | 0.8x | ราคาถูกลง ประหยัดน้ำมัน |
| Car | 14 | 1.0x | ราคาปกติ |
| Van | 10 | 1.2x | ราคาสูงขึ้นเล็กน้อย |
| Truck | 8 | 1.5x | ราคาสูงสุด กินน้ำมันมาก |

---

## การเปิด/ปิด Rule

ทุก rule สามารถปิดการคำนวณได้ 2 วิธี:

**วิธีที่ 1: ปิดทันที**
```bash
curl -X PUT http://localhost:8080/rules/{id} \
  -H "Content-Type: application/json" \
  -d '{ ..., "isActive": false }'
```

**วิธีที่ 2: ตั้งวันหมดอายุ**
```json
{ "effectiveTo": "2026-04-30T23:59:59Z" }
```

---

## Setup & Run

### Docker (recommended)

```bash
docker-compose up --build
```

- API: `http://localhost:8080`
- API Docs: `http://localhost:8080/scalar/v1`

### Local Development

```bash
dotnet run --project src/QuoteFlow.API
```

### Run Tests

```bash
dotnet test
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Runtime environment |
| `Osrm__BaseUrl` | `http://router.project-osrm.org` | OSRM routing API |
| `FuelPrice__ApiUrl` | (empty) | External fuel price API URL |

---

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | /health | Health check |
| POST | /quotes/price | คำนวณราคาทันที |
| POST | /quotes/bulk | ส่ง JSON list → returns jobId |
| POST | /quotes/bulk/csv | อัปโหลด CSV → returns jobId |
| GET | /jobs/{id} | ติดตามสถานะ bulk job |
| GET | /rules | ดู rules ทั้งหมด |
| GET | /rules/{id} | ดู rule ตาม ID |
| POST | /rules | สร้าง rule ใหม่ |
| PUT | /rules/{id} | แก้ไข/ปิด rule |
| DELETE | /rules/{id} | ลบ rule |
| GET | /locations | ดู 77 จังหวัด |
| GET | /locations/{code} | ดู location ตามรหัส |

Full interactive docs: `http://localhost:8080/scalar/v1`

---

## ตัวอย่างการใช้งาน

### 1. คำนวณราคา — กรุงเทพ → เชียงใหม่ ด้วยรถ Truck

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
  "surcharge": 3567.63,
  "discount": 743.53,
  "finalPrice": 2974.10,
  "currency": "THB",
  "appliedRules": [
    "Standard Weight Tier 0-5kg",
    "Vehicle: Truck",
    "Fuel Price (696km × 40.50฿/L ÷ 8km/L)",
    "Remote Area Surcharge - North",
    "Flash Sale Friday Morning"
  ],
  "calculatedAt": "2026-04-14T09:00:00Z"
}
```

### 2. คำนวณราคา — สกุลเงิน USD

```bash
curl -X POST http://localhost:8080/quotes/price \
  -H "Content-Type: application/json" \
  -d '{
    "originCode": "BKK",
    "destinationCode": "BKK",
    "weight": 3.0,
    "basePrice": 10,
    "currency": "USD"
  }'
```

### 3. คำนวณราคา — ไม่ระบุรถ (ไม่คิดค่าน้ำมัน)

```bash
curl -X POST http://localhost:8080/quotes/price \
  -H "Content-Type: application/json" \
  -d '{
    "originCode": "BKK",
    "destinationCode": "HKT",
    "weight": 8.0,
    "basePrice": 100
  }'
```

### 4. สร้าง rule ใหม่ — WeightTier 30-50kg

```bash
curl -X POST http://localhost:8080/rules \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Heavy 30-50kg",
    "ruleType": 2,
    "priority": 13,
    "isActive": true,
    "effectiveFrom": "2026-01-01T00:00:00Z",
    "parameters": "{\"minWeight\":30.01,\"maxWeight\":50,\"price\":500}"
  }'
```

### 5. อัปเดตราคาน้ำมัน

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

### 6. ปิด rule (ไม่คำนวณค่าน้ำมัน)

```bash
curl -X PUT http://localhost:8080/rules/22222222-0000-0000-0000-000000000001 \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Fuel Price (THB/Liter)",
    "ruleType": 4,
    "priority": 40,
    "isActive": false,
    "effectiveFrom": "2024-01-01T00:00:00Z",
    "parameters": "{\"pricePerLiter\":40.50}"
  }'
```

### 7. Bulk quotes — JSON

```bash
curl -X POST http://localhost:8080/quotes/bulk \
  -H "Content-Type: application/json" \
  -d '[
    { "originCode": "BKK", "destinationCode": "CNX", "weight": 3, "basePrice": 50 },
    { "originCode": "BKK", "destinationCode": "HKT", "weight": 10, "basePrice": 100 }
  ]'
```

### 8. Bulk quotes — CSV

```bash
curl -X POST http://localhost:8080/quotes/bulk/csv \
  -F "file=@sample-data/bulk-quotes.csv"
```

### 9. ติดตาม bulk job

```bash
curl http://localhost:8080/jobs/{jobId}
```

### 10. ดูรหัสจังหวัด

```bash
curl http://localhost:8080/locations
curl http://localhost:8080/locations/CNX
```

---

## Data Storage

ข้อมูลเก็บในรูปแบบ **JSON seed → in-memory**:

```
startup
  ├── Data/locations.json  → โหลด 77 จังหวัด เข้า ConcurrentDictionary
  └── Data/rules.json      → โหลด 21 rules เข้า ConcurrentDictionary

CRUD (Rules)
  ├── POST/PUT/DELETE → แก้ใน memory ทันที
  └── restart → โหลด JSON seed ใหม่ (กลับสู่ค่าเริ่มต้น)
```

JSON files ถูก embed เป็น `EmbeddedResource` ใน DLL — ทำงานได้ทุกที่โดยไม่ต้อง copy ไฟล์

---

## Fuel Price Sync

ระบบรองรับ sync ราคาน้ำมันจาก external API อัตโนมัติ:

```
FuelPriceSyncWorker (Background Service)
  ├── startup  → fetch ราคาจาก FuelPrice:ApiUrl
  ├── ทุก 24h  → fetch ราคาใหม่ → อัปเดต FuelSurcharge rule
  └── fetch ล้มเหลว → log warning, ใช้ราคาเดิมใน rule
```

ตั้งค่า:
```json
{ "FuelPrice": { "ApiUrl": "https://api.example.com/fuel-price" } }
```

ถ้า `ApiUrl` ว่าง → ไม่ fetch (ใช้ค่าจาก rules.json ตามเดิม)

---

## Observability

- **Serilog** structured logging + `X-Correlation-ID` header ทุก request
- **Rate Limiting** 100 requests/นาที ต่อ IP
- **RFC 7807** ProblemDetails error format
- **Retry Policy** OSRM API (3 attempts, 500ms delay)

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Runtime | .NET 10 / ASP.NET Core |
| Architecture | Clean Architecture |
| Storage | In-memory (JSON seed) |
| Background Processing | IHostedService + Channels |
| Distance API | OSRM (free, no API key) |
| Fuel Price | Configurable external API |
| API Docs | Scalar (OpenAPI) |
| Logging | Serilog |
| Testing | xUnit + NSubstitute + FluentAssertions (29 tests) |
| Container | Docker + docker-compose |
| CI | GitHub Actions |
