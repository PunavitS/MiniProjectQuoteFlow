# QuoteFlow API Documentation

Base URL: `http://localhost:8080`
Interactive Docs: `http://localhost:8080/scalar/v1`

---

## Rate Limiting

- **100 requests / นาที** ต่อ IP
- เมื่อเกินจะได้รับ `429 Too Many Requests`

---

## Correlation ID

ทุก request จะมี header `X-Correlation-ID` กลับมา ใช้สำหรับ trace log

```text
X-Correlation-ID: dc47bedc-510e-416e-ad86-e263b1f12713
```

---

## Endpoints

### Health

#### GET /health

ตรวจสอบสถานะระบบ

**Response 200**

```json
{
  "status": "healthy",
  "timestamp": "2026-04-13T08:33:31Z"
}
```

---

### Quotes

#### POST /quotes/price

คำนวณราคาทันที

**Request Body**

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| originCode | string | Yes | - | รหัสต้นทาง เช่น `"BKK"` |
| destinationCode | string | Yes | - | รหัสปลายทาง เช่น `"CNX"` |
| weight | decimal | Yes | - | น้ำหนัก (kg) |
| basePrice | decimal | Yes | - | ราคาเริ่มต้น (THB) |
| currency | string | No | `"THB"` | สกุลเงิน: THB, USD, EUR, SGD, JPY |
| vehicleType | string | No | null | ประเภทรถ: Motorcycle, Car, Van, Truck |
| requestedAt | datetime | No | now | เวลาที่ขอ quote (ใช้ตรวจ TimeWindowPromotion) |

**ตัวอย่าง Request**

```json
{
  "originCode": "BKK",
  "destinationCode": "CNX",
  "weight": 5.0,
  "basePrice": 100,
  "vehicleType": "Truck",
  "currency": "THB"
}
```

**Response 200**

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

---

#### POST /quotes/bulk

ส่ง list ของ quotes เพื่อประมวลผลใน background

**Request Body** — array ของ QuoteRequest

```json
[
  { "originCode": "BKK", "destinationCode": "CNX", "weight": 3, "basePrice": 50 },
  { "originCode": "BKK", "destinationCode": "HKT", "weight": 10, "basePrice": 100, "currency": "USD" }
]
```

**Response 202**

```json
{
  "jobId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

---

#### POST /quotes/bulk/csv

อัปโหลด CSV สำหรับ bulk processing

**Request** — `multipart/form-data` field ชื่อ `file`

**CSV Format**

```text
originCode,destinationCode,weight,basePrice
BKK,CNX,3,50
BKK,HKT,10,100
CNX,BKK,5,80
```

**Response 202**

```json
{
  "jobId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "validRows": 3,
  "skippedRows": 0,
  "errors": []
}
```

**cURL example**

```bash
curl -X POST http://localhost:8080/quotes/bulk/csv \
  -F "file=@sample-data/bulk-quotes.csv"
```

---

### Jobs

#### GET /jobs/{id}

ติดตามสถานะของ bulk job

**Response 200**

```json
{
  "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "status": "Completed",
  "totalItems": 3,
  "processedItems": 3,
  "failedItems": 0,
  "createdAt": "2026-04-11T09:00:00Z",
  "completedAt": "2026-04-11T09:00:05Z",
  "items": [
    {
      "id": "...",
      "originCode": "BKK",
      "destinationCode": "CNX",
      "weight": 3,
      "inputBasePrice": 50,
      "basePrice": 100,
      "finalPrice": 150,
      "surcharge": 50,
      "discount": 0,
      "currency": "THB",
      "appliedRules": "[\"Standard Weight Tier 0-5kg\",\"Remote Area Surcharge - North\"]",
      "status": "Completed",
      "processedAt": "2026-04-11T09:00:02Z"
    }
  ]
}
```

**Job Status Values**

| Status | ความหมาย |
| --- | --- |
| `Pending` | รอประมวลผล |
| `Processing` | กำลังประมวลผล |
| `Completed` | เสร็จสมบูรณ์ |
| `Failed` | ล้มเหลวทั้งหมด |

---

### Rules

#### GET /rules

ดึง rules ทั้งหมด เรียงตาม priority

**Response 200**

```json
[
  {
    "id": "11111111-0000-0000-0000-000000000001",
    "name": "Standard Weight Tier 0-5kg",
    "description": "Base price for shipments up to 5kg",
    "ruleType": 2,
    "priority": 10,
    "isActive": true,
    "effectiveFrom": "2024-01-01T00:00:00Z",
    "effectiveTo": null,
    "parameters": "{\"minWeight\":0,\"maxWeight\":5,\"price\":100}"
  }
]
```

---

#### POST /rules

สร้าง rule ใหม่

**Rule fields**

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| name | string | Yes | ชื่อ rule |
| description | string | No | คำอธิบาย |
| ruleType | int | Yes | ประเภท (0-5) |
| priority | int | Yes | ลำดับ (ยิ่งน้อยยิ่งทำก่อน) |
| isActive | bool | Yes | เปิด/ปิด (`false` = ข้ามการคำนวณ) |
| effectiveFrom | datetime | Yes | วันเริ่มต้น |
| effectiveTo | datetime | No | วันสิ้นสุด (null = ไม่มีกำหนด) |
| parameters | string | Yes | JSON string ของ parameters ตาม ruleType |

---

#### Rule Types (กฎราคา 6 ประเภท)

##### ruleType = 0 — TimeWindowPromotion

ส่วนลด % ตามวัน/เวลา

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

| Parameter | Type | Description |
| --- | --- | --- |
| startHour | int | ชั่วโมงเริ่ม (0-23) |
| endHour | int | ชั่วโมงสิ้นสุด (0-23) |
| daysOfWeek | int[] | วัน (0=อาทิตย์, 1=จันทร์, ..., 5=ศุกร์, 6=เสาร์) |
| discountPercent | decimal | เปอร์เซ็นต์ส่วนลด |

Seed: Flash Sale Friday Morning — ศุกร์ 08:00-12:00 ลด 20%

---

##### ruleType = 1 — RemoteAreaSurcharge

บวกค่าเพิ่มเมื่อปลายทางอยู่ในพื้นที่ห่างไกล

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

| Parameter | Type | Description |
| --- | --- | --- |
| areaCodes | string[] | รหัสจังหวัดที่เข้าเงื่อนไข |
| surchargeAmount | decimal | ค่าเพิ่ม (THB) |

Seed:

| Rule | พื้นที่ | ค่าเพิ่ม |
| --- | --- | --- |
| Remote North | CNX, LPG, PYY, NAN, PYO, CMR | +50 THB |
| Remote Northeast | MDH, LEI, BKN, NKP | +55 THB |
| Remote South | STN, YLA, PTN, NWT | +60 THB |
| Remote West | TAK, TRT | +45 THB |

---

##### ruleType = 2 — WeightTier

กำหนด basePrice ตามช่วงน้ำหนัก (แทนที่ basePrice เดิม)

```json
{
  "name": "Heavy 30-50kg",
  "ruleType": 2,
  "priority": 13,
  "isActive": true,
  "effectiveFrom": "2026-01-01T00:00:00Z",
  "parameters": "{\"minWeight\":30.01,\"maxWeight\":50,\"price\":500}"
}
```

| Parameter | Type | Description |
| --- | --- | --- |
| minWeight | decimal | น้ำหนักขั้นต่ำ (kg) |
| maxWeight | decimal | น้ำหนักสูงสุด (kg) |
| price | decimal | ราคาสำหรับช่วงนี้ (THB) |

Seed:

| Rule | น้ำหนัก | ราคา |
| --- | --- | --- |
| Standard 0-5kg | 0 - 5 kg | 100 THB |
| Standard 5-15kg | 5.01 - 15 kg | 180 THB |
| Standard 15-30kg | 15.01 - 30 kg | 300 THB |

---

##### ruleType = 3 — ExchangeRate

แปลงสกุลเงิน — ใช้ตอนเริ่ม (แปลงเข้า THB) และตอนจบ (แปลงกลับ)

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

| Parameter | Type | Description |
| --- | --- | --- |
| fromCurrency | string | สกุลเงินต้นทาง |
| toCurrency | string | สกุลเงินปลายทาง |
| rate | decimal | อัตราแลกเปลี่ยน |

Seed: USD, EUR, SGD, JPY ↔ THB (8 rules)

> ปิด ExchangeRate rule ทั้งหมด → ระบบคำนวณเป็น THB ตลอด

---

##### ruleType = 4 — FuelSurcharge

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

| Parameter | Type | Description |
| --- | --- | --- |
| pricePerLiter | decimal | ราคาน้ำมันต่อลิตร (THB) |

Seed: 40.50 THB/L (ID: `22222222-0000-0000-0000-000000000001`)

อัปเดตราคาน้ำมัน:

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

> ตั้งค่า `FuelPrice:ApiUrl` เพื่อ sync ราคาจาก external API ทุก 24 ชั่วโมงอัตโนมัติ

> **ปิด rule นี้ (`isActive: false`) → ข้ามการคำนวณค่าน้ำมันทั้งหมด**

---

##### ruleType = 5 — VehicleType

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

| Parameter | Type | Description |
| --- | --- | --- |
| vehicleType | string | ชื่อประเภทรถ |
| kmPerLiter | decimal | อัตราสิ้นเปลือง (km/L) |
| priceMultiplier | decimal | ตัวคูณราคา |

Seed:

| ประเภทรถ | km/L | Multiplier | ผล |
| --- | --- | --- | --- |
| Motorcycle | 35 | 0.8x | ราคาถูกลง ประหยัดน้ำมัน |
| Car | 14 | 1.0x | ราคาปกติ |
| Van | 10 | 1.2x | ราคาสูงขึ้นเล็กน้อย |
| Truck | 8 | 1.5x | ราคาสูงสุด กินน้ำมันมาก |

---

#### การเปิด/ปิด Rule

**ทุก rule** สามารถปิดการคำนวณได้:

ปิดทันที:

```bash
curl -X PUT http://localhost:8080/rules/{id} \
  -H "Content-Type: application/json" \
  -d '{ "name": "...", "ruleType": 4, "priority": 40, "isActive": false, "effectiveFrom": "2024-01-01T00:00:00Z", "parameters": "{...}" }'
```

ตั้งวันหมดอายุ:

```json
{ "effectiveTo": "2026-04-30T23:59:59Z" }
```

---

#### PUT /rules/{id}

แก้ไข rule — ใช้ `isActive: false` เพื่อปิดโดยไม่ต้องลบ

---

#### DELETE /rules/{id}

ลบ rule

**Response 204** — No Content

---

### Locations

#### GET /locations

ดึง locations ทั้งหมด (77 จังหวัดของไทย)

**Response 200**

```json
[
  {
    "code": "BKK",
    "name": "Bangkok",
    "province": "Bangkok",
    "region": 0,
    "distanceFromBkk": 0,
    "isRemoteArea": false,
    "latitude": 13.7563,
    "longitude": 100.5018
  }
]
```

**Region Values**

| Value | ภาค |
| --- | --- |
| 0 | Central (ภาคกลาง) — 15 จังหวัด |
| 1 | North (ภาคเหนือ) — 16 จังหวัด |
| 2 | Northeast (ภาคอีสาน) — 20 จังหวัด |
| 3 | East (ภาคตะวันออก) — 7 จังหวัด |
| 4 | West (ภาคตะวันตก) — 5 จังหวัด |
| 5 | South (ภาคใต้) — 14 จังหวัด |

ดูรหัสจังหวัดทั้งหมดได้ที่ `GET /locations`

---

#### GET /locations/{code}

ดึง location ตามรหัส เช่น `GET /locations/CNX`

---

## Error Responses

ทุก error ใช้รูปแบบ RFC 7807 Problem Details:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Bad Request",
  "status": 400,
  "detail": "Request list cannot be empty",
  "instance": "/quotes/bulk"
}
```

| Status | ความหมาย |
| --- | --- |
| 400 | Bad Request — ข้อมูลผิดรูปแบบ |
| 404 | Not Found — ไม่พบ resource |
| 429 | Too Many Requests — เกิน rate limit |
| 500 | Internal Server Error |

---

## Pricing Pipeline

```text
Request
   |
   +-- 1. ExchangeRate        -> แปลง currency เป็น THB
   +-- 2. WeightTier          -> กำหนดราคาตามน้ำหนัก
   +-- 3. VehicleType         -> คูณราคาตามประเภทรถ
   +-- 4. FuelSurcharge       -> ค่าน้ำมัน = ระยะทาง / km/L x THB/L
   +-- 5. RemoteAreaSurcharge -> บวกค่าพื้นที่ห่างไกล
   +-- 6. TimeWindowPromotion -> หักส่วนลดตามเวลา
   +-- 7. ExchangeRate        -> แปลงกลับเป็น currency ที่ขอมา

FinalPrice = BasePrice + Surcharge - Discount  (ขั้นต่ำ 0)
```

- ระยะทางดึงอัตโนมัติจาก **OSRM API** โดยใช้พิกัดของ Location
- **ทุกขั้นตอนข้ามได้** โดยปิด rule ที่เกี่ยวข้อง (`isActive: false`)

---

## Fuel Price Sync

ระบบ sync ราคาน้ำมันจาก external API อัตโนมัติ:

```text
FuelPriceSyncWorker (Background Service)
  +-- startup  -> fetch ราคาจาก FuelPrice:ApiUrl
  +-- ทุก 24h  -> fetch ราคาใหม่ -> อัปเดต FuelSurcharge rule
  +-- fetch ล้มเหลว -> log warning, ใช้ราคาเดิมใน rule
```

ตั้งค่าใน `appsettings.json`:

```json
{
  "FuelPrice": {
    "ApiUrl": "https://api.example.com/fuel-price"
  }
}
```

Expected response format จาก API:

```json
{ "pricePerLiter": 40.50 }
```

ถ้า `ApiUrl` ว่าง → worker ไม่ fetch (ใช้ค่าจาก rules.json seed ตามเดิม)
