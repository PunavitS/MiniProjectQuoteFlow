# QuoteFlow API Documentation

Base URL: `http://localhost:8080`  
Interactive Docs: `http://localhost:8080/scalar/v1`

---

## Authentication

ไม่จำเป็นต้องมี authentication สำหรับ version นี้

---

## Rate Limiting

- **100 requests / นาที** ต่อ IP
- เมื่อเกินจะได้รับ `429 Too Many Requests`

---

## Correlation ID

ทุก request จะมี header `X-Correlation-ID` กลับมา ใช้สำหรับ trace log

```
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
|-------|------|----------|---------|-------------|
| originCode | string | ✓ | - | รหัสต้นทาง เช่น `"BKK"` |
| destinationCode | string | ✓ | - | รหัสปลายทาง เช่น `"CNX"` |
| weight | decimal | ✓ | - | น้ำหนัก (kg) |
| basePrice | decimal | ✓ | - | ราคาเริ่มต้น (THB) |
| currency | string | | `"THB"` | สกุลเงิน: THB, USD, EUR, SGD, JPY |
| vehicleType | string | | null | ประเภทรถ: Motorcycle, Car, Van, Truck |
| requestedAt | datetime | | ตอนนี้ | เวลาที่ขอ quote (ใช้ตรวจ TimeWindowPromotion) |

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
```
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
|--------|---------|
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

**ruleType Values**

| Value | ชื่อ | Parameters |
|-------|------|-----------|
| 0 | TimeWindowPromotion | `{"startHour":8,"endHour":12,"daysOfWeek":[5],"discountPercent":20}` |
| 1 | RemoteAreaSurcharge | `{"areaCodes":["CNX","LPG"],"surchargeAmount":50}` |
| 2 | WeightTier | `{"minWeight":0,"maxWeight":5,"price":100}` |
| 3 | ExchangeRate | `{"fromCurrency":"USD","toCurrency":"THB","rate":36.0}` |
| 4 | FuelSurcharge | `{"pricePerLiter":40.50}` |
| 5 | VehicleType | `{"vehicleType":"Truck","kmPerLiter":8.0,"priceMultiplier":1.5}` |

**ตัวอย่าง — สร้าง WeightTier rule**
```json
{
  "name": "Heavy 30-50kg",
  "description": "Base price for heavy shipments",
  "ruleType": 2,
  "priority": 13,
  "isActive": true,
  "effectiveFrom": "2026-01-01T00:00:00Z",
  "effectiveTo": null,
  "parameters": "{\"minWeight\":30.01,\"maxWeight\":50,\"price\":500}"
}
```

**Response 201**
```json
{
  "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "name": "Heavy 30-50kg",
  ...
}
```

---

#### PUT /rules/{id}
แก้ไข rule — ใช้ `isActive: false` เพื่อปิดโดยไม่ต้องลบ

**ตัวอย่าง — อัปเดตราคาน้ำมัน**
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

#### DELETE /rules/{id}
ลบ rule

**Response 204** — No Content

---

### Locations

#### GET /locations
ดึง locations ทั้งหมด (31 จังหวัดของไทย)

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
|-------|-----|
| 0 | Central (ภาคกลาง) |
| 1 | North (ภาคเหนือ) |
| 2 | Northeast (ภาคอีสาน) |
| 3 | East (ภาคตะวันออก) |
| 4 | West (ภาคตะวันตก) |
| 5 | South (ภาคใต้) |

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
|--------|---------|
| 400 | Bad Request — ข้อมูลผิดรูปแบบ |
| 404 | Not Found — ไม่พบ resource |
| 429 | Too Many Requests — เกิน rate limit |
| 500 | Internal Server Error |

---

## Pricing Pipeline

```
Request
   │
   ├─ 1. ExchangeRate      → แปลง currency เป็น THB
   ├─ 2. WeightTier        → กำหนดราคาตามน้ำหนัก
   ├─ 3. VehicleType       → คูณราคาตามประเภทรถ
   ├─ 4. FuelSurcharge     → ค่าน้ำมัน = ระยะทาง ÷ km/L × ฿/L
   ├─ 5. RemoteAreaSurcharge → บวกค่าพื้นที่ห่างไกล
   ├─ 6. TimeWindowPromotion → หักส่วนลดตามเวลา
   └─ 7. ExchangeRate      → แปลงกลับเป็น currency ที่ขอมา

FinalPrice = BasePrice + Surcharge - Discount  (ขั้นต่ำ 0)
```

ระยะทางดึงอัตโนมัติจาก **OSRM API** โดยใช้พิกัดของ Location
