# QuoteFlow API Documentation

| Environment | Base URL |
| --- | --- |
| Local (`dotnet run`) | `http://localhost:5292` |
| Docker | `http://localhost:8080` |

Interactive Docs (Swagger UI):

- Local: `http://localhost:5292/swagger`
- Docker: `http://localhost:8080/swagger`

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

ตรวจสอบสถานะของ API server ว่ายังทำงานปกติอยู่ไหม ใช้สำหรับ healthcheck ของ Docker / load balancer

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

คำนวณราคาขนส่งทันทีแบบ synchronous โดยนำ request ไปผ่าน Pricing Pipeline ซึ่งประกอบด้วย rule ที่ active อยู่ทั้งหมด เรียงตาม priority(สามารถปิดหรือเปิดได้)

ระบบจะ:
1. แปลงสกุลเงินเป็น THB (ถ้ามี ExchangeRate rule)
2. กำหนด basePrice ตามน้ำหนัก (WeightTier)
3. คูณราคาตามประเภทรถ (VehicleType)
4. บวกค่าน้ำมัน คำนวณจากระยะทางอัตโนมัติ (FuelSurcharge)
5. บวกค่าพื้นที่ห่างไกล (RemoteAreaSurcharge)
6. หักส่วนลดตามช่วงเวลา (TimeWindowPromotion)
7. แปลงราคากลับเป็นสกุลเงินที่ขอ

**Request Body**

| Field | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| originCode | string | Yes | - | รหัสต้นทาง เช่น `"BKK"` ต้องไม่ว่าง |
| destinationCode | string | Yes | - | รหัสปลายทาง เช่น `"CNX"` ต้องไม่ว่าง |
| weight | decimal | Yes | - | น้ำหนัก (kg) ต้องมากกว่า 0 |
| basePrice | decimal | Yes | - | ราคาเริ่มต้น (THB) ต้องมากกว่า 0 |
| currency | string | No | `"THB"` | สกุลเงิน: THB, USD, EUR, SGD, JPY |
| vehicleType | string | No | null | ประเภทรถ: Motorcycle, Car, Van, Truck |
| distance | decimal | No | null | ระยะทาง (km) — ถ้าไม่ใส่จะดึงอัตโนมัติจาก OSRM |
| requestedAt | datetime | No | now | เวลาที่ขอ quote (ใช้ตรวจสอบ TimeWindowPromotion) |

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

| Field | Description |
| --- | --- |
| originCode / destinationCode | ต้นทาง / ปลายทาง |
| weight | น้ำหนัก (kg) |
| inputBasePrice | ราคาเริ่มต้นที่ส่งเข้ามา |
| basePrice | ราคาหลังผ่าน WeightTier / VehicleType |
| surcharge | ค่าเพิ่มรวม (FuelSurcharge + RemoteAreaSurcharge) |
| discount | ส่วนลดรวม (TimeWindowPromotion) |
| finalPrice | ราคาสุดท้าย = basePrice + surcharge - discount (ขั้นต่ำ 0) |
| currency | สกุลเงินของผลลัพธ์ |
| appliedRules | รายชื่อ rule ที่ถูกใช้งาน |
| calculatedAt | เวลาที่คำนวณ |

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

**Error 400** — เมื่อข้อมูลไม่ครบหรือไม่ถูกต้อง เช่น originCode ว่าง, weight = 0

---

#### POST /quotes/bulk

ส่ง list ของ QuoteRequest เพื่อประมวลผลใน background แบบ asynchronous ระบบจะสร้าง Job และ enqueue ทันที แล้วคืน `jobId` เพื่อติดตามสถานะ

ใช้เมื่อต้องการคำนวณหลาย quote พร้อมกัน โดยไม่ต้องรอผล

**Request Body** — array ของ QuoteRequest

```json
[
  { "originCode": "BKK", "destinationCode": "CNX", "weight": 3, "basePrice": 50 },
  { "originCode": "BKK", "destinationCode": "HKT", "weight": 10, "basePrice": 100, "currency": "USD" }
]
```

**Response 202** — รับคำสั่งแล้ว กำลังประมวลผล

```json
{
  "jobId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

**Error 400** — เมื่อส่ง array ว่าง

---

#### POST /quotes/bulk/csv

อัปโหลดไฟล์ CSV เพื่อประมวลผลแบบ bulk ใช้เมื่อมีข้อมูลจำนวนมากในรูปแบบ spreadsheet

ระบบจะอ่าน CSV ทีละแถว ข้ามแถวที่ format ผิด แล้วสร้าง Job สำหรับแถวที่ valid

**Request** — `multipart/form-data` field ชื่อ `file`

**CSV Format** — header แถวแรก, ข้อมูลตั้งแต่แถวที่ 2

```text
originCode,destinationCode,weight,basePrice
BKK,CNX,3,50
BKK,HKT,10,100
CNX,BKK,5,80
```

**Response 202**

| Field | Description |
| --- | --- |
| jobId | ID สำหรับติดตามสถานะ |
| validRows | จำนวนแถวที่ valid และถูก enqueue |
| skippedRows | จำนวนแถวที่ถูกข้าม |
| errors | รายละเอียดแถวที่มีปัญหา |

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

**Error 400** — เมื่อไม่ได้แนบไฟล์ หรือไม่มีแถวที่ valid เลย

---

### Jobs

#### GET /jobs/{id}

ติดตามสถานะและผลลัพธ์ของ bulk job ที่สร้างจาก `POST /quotes/bulk` หรือ `POST /quotes/bulk/csv`

ใช้ polling endpoint นี้จนกว่า `status` จะเป็น `Completed` หรือ `Failed`

**Path Parameter**

| Parameter | Description |
| --- | --- |
| id | jobId ที่ได้จาก bulk endpoint |

**Response 200**

| Field | Description |
| --- | --- |
| id | Job ID |
| status | สถานะรวมของ job |
| totalItems | จำนวน quote ทั้งหมดใน job |
| processedItems | จำนวนที่ประมวลผลแล้ว |
| failedItems | จำนวนที่ล้มเหลว |
| createdAt | เวลาที่สร้าง job |
| completedAt | เวลาที่เสร็จสิ้น (null ถ้ายังไม่เสร็จ) |
| items | ผลลัพธ์รายแถว |

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
      "rowIndex": 1,
      "originCode": "BKK",
      "destinationCode": "CNX",
      "weight": 3,
      "inputBasePrice": 50,
      "basePrice": 100,
      "finalPrice": 150,
      "surcharge": 50,
      "discount": 0,
      "appliedRules": ["Standard Weight Tier 0-5kg", "Remote Area Surcharge - North"],
      "status": "Completed",
      "errorMessage": null,
      "processedAt": "2026-04-11T09:00:02Z"
    }
  ]
}
```

**Job Status Values**

| Status | ความหมาย |
| --- | --- |
| `Pending` | รอ worker รับไปประมวลผล |
| `Processing` | กำลังประมวลผลอยู่ |
| `Completed` | เสร็จสมบูรณ์ทั้งหมด |
| `Failed` | ล้มเหลวทุก item |

**Item Status Values**

| Status | ความหมาย |
| --- | --- |
| `Pending` | รอประมวลผล |
| `Completed` | คำนวณสำเร็จ |
| `Failed` | คำนวณล้มเหลว ดู `errorMessage` |

**Error 404** — ไม่พบ job ที่ระบุ

---

### Rules

#### GET /rules

ดึง pricing rule ทั้งหมดในระบบ เรียงตาม priority (น้อยไปมาก) รวมทั้ง rule ที่ปิดอยู่ (`isActive: false`)

**Response 200** — array ของ PricingRule

```json
[
  {
    "id": "11111111-0000-0000-0000-000000000001",
    "name": "Standard Weight Tier 0-5kg",
    "description": "Base price for shipments up to 5kg",
    "ruleType": 2,
    "priority": 10,
    "isActive": true,
    "effectiveFrom": "2026-01-01T00:00:00Z",
    "effectiveTo": null,
    "parameters": "{\"minWeight\":0,\"maxWeight\":5,\"price\":100}"
  }
]
```

---

#### GET /rules/{id}

ดึง rule เดี่ยวตาม ID ใช้เพื่อตรวจสอบ parameters หรือสถานะของ rule นั้นๆ

**Path Parameter**

| Parameter | Description |
| --- | --- |
| id | GUID ของ rule |

**Response 200** — PricingRule object

**Error 404** — ไม่พบ rule ที่ระบุ

---

#### POST /rules

สร้าง pricing rule ใหม่ ระบบจะ validate ทั้ง field หลักและ parameters ตาม ruleType ก่อนบันทึก

**Validation**

- `name` — ต้องไม่ว่าง
- `ruleType` — ต้องเป็น 0–5 เท่านั้น
- `priority` — ต้องมากกว่าหรือเท่ากับ 0
- `effectiveFrom` — ต้องระบุ
- `effectiveTo` — ถ้าระบุต้องมากกว่า `effectiveFrom`
- `parameters` — ต้องเป็น JSON ที่ถูกต้องตาม ruleType

**Rule fields**

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| name | string | Yes | ชื่อ rule |
| description | string | No | คำอธิบาย |
| ruleType | int | Yes | ประเภท (0–5) |
| priority | int | Yes | ลำดับ (ยิ่งน้อยยิ่งทำก่อน) |
| isActive | bool | Yes | เปิด/ปิด (`false` = ข้ามการคำนวณ) |
| effectiveFrom | datetime | Yes | วันเริ่มต้น |
| effectiveTo | datetime | No | วันสิ้นสุด (null = ไม่มีกำหนด) |
| parameters | string | Yes | JSON string ของ parameters ตาม ruleType |

**Response 201** — PricingRule ที่สร้างแล้ว พร้อม id ที่ระบบ generate ให้

**Error 400** — validation ไม่ผ่าน พร้อม detail ว่า field ไหนผิด

---

#### PUT /rules/{id}

แก้ไข rule ที่มีอยู่ ใช้เพื่อ:
- เปลี่ยน parameters (เช่น อัปเดตราคาน้ำมัน)
- ปิด rule ชั่วคราว (`isActive: false`) โดยไม่ต้องลบ
- กำหนดวันหมดอายุ (`effectiveTo`)

ต้องส่ง rule object ครบทุก field (ไม่ใช่ partial update)

**Response 200** — PricingRule ที่อัปเดตแล้ว

**Error 404** — ไม่พบ rule ที่ระบุ

**Error 400** — validation ไม่ผ่าน

---

#### DELETE /rules/{id}

ลบ rule ออกจากระบบถาวร หากต้องการแค่หยุดการคำนวณชั่วคราวให้ใช้ `isActive: false` แทน

**Response 204** — No Content

**Error 404** — ไม่พบ rule ที่ระบุ

---

#### Rule Types (กฎราคา 6 ประเภท)

##### ruleType = 0 — TimeWindowPromotion

หักส่วนลด % จาก (basePrice + surcharge) เมื่อเวลาที่ขอ quote อยู่ในช่วงที่กำหนด

```json
{
  "name": "Flash Sale Friday Morning",
  "ruleType": 0,
  "priority": 30,
  "isActive": true,
  "effectiveFrom": "2026-01-01T00:00:00Z",
  "parameters": "{\"startHour\":8,\"endHour\":12,\"daysOfWeek\":[5],\"discountPercent\":20}"
}
```

| Parameter | Type | Validation | Description |
| --- | --- | --- | --- |
| startHour | int | 0–23 | ชั่วโมงเริ่มต้น |
| endHour | int | > startHour, ≤ 24 | ชั่วโมงสิ้นสุด |
| daysOfWeek | int[] | ต้องไม่ว่าง | วัน (0=อาทิตย์ … 5=ศุกร์, 6=เสาร์) |
| discountPercent | decimal | > 0 | เปอร์เซ็นต์ส่วนลด |

Seed: Flash Sale Friday Morning — ศุกร์ 08:00–12:00 ลด 20%

---

##### ruleType = 1 — RemoteAreaSurcharge

บวกค่าธรรมเนียมเพิ่มเติม เมื่อ `destinationCode` อยู่ในรายการ `areaCodes`

```json
{
  "name": "Remote Area Surcharge - North",
  "ruleType": 1,
  "priority": 20,
  "isActive": true,
  "effectiveFrom": "2026-01-01T00:00:00Z",
  "parameters": "{\"areaCodes\":[\"CNX\",\"LPG\",\"PYY\",\"NAN\",\"PYO\",\"CMR\"],\"surchargeAmount\":50}"
}
```

| Parameter | Type | Validation | Description |
| --- | --- | --- | --- |
| areaCodes | string[] | ต้องไม่ว่าง | รหัสจังหวัดที่เข้าเงื่อนไข |
| surchargeAmount | decimal | > 0 | ค่าเพิ่ม (THB) |

Seed:

| Rule | พื้นที่ | ค่าเพิ่ม |
| --- | --- | --- |
| Remote North | CNX, LPG, PYY, NAN, PYO, CMR | +50 THB |
| Remote Northeast | MDH, LEI, BKN, NKP | +55 THB |
| Remote South | STN, YLA, PTN, NWT | +60 THB |
| Remote West | TAK, TRT | +45 THB |

---

##### ruleType = 2 — WeightTier

แทนที่ `basePrice` ด้วยราคาที่กำหนด เมื่อน้ำหนักอยู่ใน range `minWeight`–`maxWeight` เลือก rule ที่ match ก่อน (priority ต่ำสุด) เพียงอันเดียว

```json
{
  "name": "Standard Weight Tier 0-5kg",
  "ruleType": 2,
  "priority": 10,
  "isActive": true,
  "effectiveFrom": "2026-01-01T00:00:00Z",
  "parameters": "{\"minWeight\":0,\"maxWeight\":5,\"price\":100}"
}
```

| Parameter | Type | Validation | Description |
| --- | --- | --- | --- |
| minWeight | decimal | ≥ 0 | น้ำหนักขั้นต่ำ (kg) |
| maxWeight | decimal | > minWeight | น้ำหนักสูงสุด (kg) |
| price | decimal | > 0 | ราคาสำหรับช่วงนี้ (THB) |

Seed:

| Rule | น้ำหนัก | ราคา |
| --- | --- | --- |
| Standard 0–5kg | 0–5 kg | 100 THB |
| Standard 5–15kg | 5.01–15 kg | 180 THB |
| Standard 15–30kg | 15.01–30 kg | 300 THB |

---

##### ruleType = 3 — ExchangeRate

แปลงสกุลเงิน ใช้ 2 ครั้งต่อ request: ครั้งแรกแปลงเข้า THB ก่อนคำนวณ ครั้งสุดท้ายแปลงกลับเป็นสกุลเงินที่ขอ ถ้าไม่มี rule นี้หรือปิดอยู่ ระบบจะคำนวณเป็น THB ตลอด

```json
{
  "name": "USD → THB",
  "ruleType": 3,
  "priority": 1,
  "isActive": true,
  "effectiveFrom": "2026-01-01T00:00:00Z",
  "parameters": "{\"fromCurrency\":\"USD\",\"toCurrency\":\"THB\",\"rate\":36.00}"
}
```

| Parameter | Type | Validation | Description |
| --- | --- | --- | --- |
| fromCurrency | string | ต้องไม่ว่าง | สกุลเงินต้นทาง |
| toCurrency | string | ต้องไม่ว่าง | สกุลเงินปลายทาง |
| rate | decimal | > 0 | อัตราแลกเปลี่ยน |

Seed: USD, EUR, SGD, JPY ↔ THB (8 rules)

---

##### ruleType = 4 — FuelSurcharge

บวกค่าน้ำมันโดยคำนวณจาก: `ระยะทาง ÷ kmPerLiter × pricePerLiter`

ต้องมี VehicleType rule match ด้วย เพื่อให้ทราบอัตราสิ้นเปลือง (`kmPerLiter`) ถ้าไม่มี VehicleType ข้าม FuelSurcharge ทั้งหมด

ราคาน้ำมันสามารถ sync อัตโนมัติจาก external API ทุก 24 ชั่วโมง โดยตั้งค่า `FuelPrice:ApiUrl` ใน appsettings.json

```json
{
  "name": "Fuel Price (THB/Liter)",
  "ruleType": 4,
  "priority": 40,
  "isActive": true,
  "effectiveFrom": "2026-01-01T00:00:00Z",
  "parameters": "{\"pricePerLiter\":40.50}"
}
```

| Parameter | Type | Validation | Description |
| --- | --- | --- | --- |
| pricePerLiter | decimal | > 0 | ราคาน้ำมันต่อลิตร (THB) |

Seed: 40.50 THB/L (ID: `22222222-0000-0000-0000-000000000001`)

อัปเดตราคาน้ำมันด้วยตัวเอง:

```bash
curl -X PUT http://localhost:8080/rules/22222222-0000-0000-0000-000000000001 \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Fuel Price (THB/Liter)",
    "ruleType": 4,
    "priority": 40,
    "isActive": true,
    "effectiveFrom": "2026-01-01T00:00:00Z",
    "parameters": "{\"pricePerLiter\":42.50}"
  }'
```

> **ปิด rule นี้ (`isActive: false`) → ข้ามการคำนวณค่าน้ำมันทั้งหมด**

---

##### ruleType = 5 — VehicleType

กำหนด price multiplier และ fuel efficiency ตามประเภทรถที่ส่งมาใน `vehicleType` field ถ้า `vehicleType` ใน request ไม่ตรงกับ rule ไหนเลย จะข้าม

```json
{
  "name": "Vehicle: Truck",
  "ruleType": 5,
  "priority": 35,
  "isActive": true,
  "effectiveFrom": "2026-01-01T00:00:00Z",
  "parameters": "{\"vehicleType\":\"Truck\",\"kmPerLiter\":8.0,\"priceMultiplier\":1.5}"
}
```

| Parameter | Type | Validation | Description |
| --- | --- | --- | --- |
| vehicleType | string | ต้องไม่ว่าง | ชื่อประเภทรถ (ต้องตรงกับ request) |
| kmPerLiter | decimal | > 0 | อัตราสิ้นเปลือง (km/L) ใช้โดย FuelSurcharge |
| priceMultiplier | decimal | > 0 | ตัวคูณ basePrice |

Seed:

| ประเภทรถ | km/L | Multiplier | ผล |
| --- | --- | --- | --- |
| Motorcycle | 35 | 0.8x | ราคาถูกลง ประหยัดน้ำมัน |
| Car | 14 | 1.0x | ราคาปกติ |
| Van | 10 | 1.2x | ราคาสูงขึ้นเล็กน้อย |
| Truck | 8 | 1.5x | ราคาสูงสุด กินน้ำมันมาก |

---

#### การเปิด/ปิด Rule

ทุก rule สามารถปิดได้โดยไม่ต้องลบ เมื่อ `isActive: false` rule นั้นจะถูกข้ามในทุก request

ปิดทันที:

```bash
curl -X PUT http://localhost:8080/rules/{id} \
  -H "Content-Type: application/json" \
  -d '{
    "name": "...",
    "ruleType": 4,
    "priority": 40,
    "isActive": false,
    "effectiveFrom": "2026-01-01T00:00:00Z",
    "parameters": "{...}"
  }'
```

กำหนดวันหมดอายุ (rule จะหยุดทำงานหลัง `effectiveTo`):

```json
{
  "effectiveTo": "2026-04-30T23:59:59Z"
}
```

---

### Locations

#### GET /locations

ดึง locations ทั้งหมด (77 จังหวัดของไทย) หรือกรองตามภาคด้วย query parameter `region`
ใช้ดูรหัสจังหวัด (`code`) เพื่อนำไปใส่ใน `originCode` / `destinationCode`

**Query Parameters**

| Parameter | Type | Required | Description |
| --- | --- | --- | --- |
| region | integer | No | กรองตามภาค (0–5) — ถ้าไม่ใส่จะคืนทั้งหมด 77 จังหวัด |

**Region Values**

| Value | ภาค | จำนวนจังหวัด |
| --- | --- | --- |
| 0 | Central (ภาคกลาง) | 15 |
| 1 | North (ภาคเหนือ) | 16 |
| 2 | Northeast (ภาคอีสาน) | 20 |
| 3 | East (ภาคตะวันออก) | 7 |
| 4 | West (ภาคตะวันตก) | 5 |
| 5 | South (ภาคใต้) | 14 |

**ตัวอย่าง**

```text
GET /locations          → คืนทั้งหมด 77 จังหวัด
GET /locations?region=1 → คืนเฉพาะจังหวัดภาคเหนือ 16 จังหวัด
```

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

---

#### GET /locations/{code}

ดึงข้อมูล location เดี่ยวตามรหัสจังหวัด ใช้ตรวจสอบพิกัด, ภาค, หรือสถานะ isRemoteArea

**Path Parameter**

| Parameter | Description |
| --- | --- |
| code | รหัสจังหวัด เช่น `BKK`, `CNX`, `HKT` |

**Response 200**

```json
{
  "code": "CNX",
  "name": "Chiang Mai",
  "province": "Chiang Mai",
  "region": 1,
  "distanceFromBkk": 696,
  "isRemoteArea": true,
  "latitude": 18.7883,
  "longitude": 98.9853
}
```

**Error 404** — ไม่พบรหัสจังหวัดที่ระบุ

---

## Error Responses

ทุก error ใช้รูปแบบ RFC 7807 Problem Details:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Bad Request",
  "status": 400,
  "detail": "weight must be > 0",
  "instance": "/quotes/price"
}
```

| Status | ความหมาย |
| --- | --- |
| 400 | Bad Request — ข้อมูลผิดรูปแบบหรือ validation ไม่ผ่าน |
| 404 | Not Found — ไม่พบ resource ที่ระบุ |
| 429 | Too Many Requests — เกิน rate limit (100 req/min) |
| 500 | Internal Server Error |

---

## Pricing Pipeline

```text
POST /quotes/price
        |
        v
[Validation] originCode, destinationCode, weight > 0, basePrice > 0
        |
        v
[Active Rules] กรองเฉพาะ rule ที่ isActive=true และอยู่ในช่วง effectiveFrom–effectiveTo
        |
        v
1. ExchangeRate        → แปลง basePrice จาก currency ที่ขอ → THB
2. WeightTier          → แทนที่ basePrice ตามช่วงน้ำหนัก
3. VehicleType         → คูณ basePrice ด้วย priceMultiplier
4. FuelSurcharge       → surcharge += distance / kmPerLiter × pricePerLiter
5. RemoteAreaSurcharge → surcharge += surchargeAmount (ถ้า destination match)
6. TimeWindowPromotion → discount += (basePrice + surcharge) × discountPercent%
        |
        v
FinalPrice = BasePrice + Surcharge - Discount  (ขั้นต่ำ 0)
        |
        v
ExchangeRate (reverse) → แปลงผลกลับเป็น currency ที่ขอ
```

- ระยะทางดึงอัตโนมัติจาก **OSRM API** โดยใช้พิกัด latitude/longitude ของ location
- **ทุกขั้นตอนข้ามได้** โดยปิด rule ที่เกี่ยวข้อง (`isActive: false`)

---

## Fuel Price Sync

ระบบ sync ราคาน้ำมันจาก external API อัตโนมัติ:

```text
FuelPriceSyncWorker (Background Service)
  +-- startup  → fetch ราคาจาก FuelPrice:ApiUrl
  +-- ทุก 24h  → fetch ราคาใหม่ → อัปเดต FuelSurcharge rule ใน memory
  +-- fetch ล้มเหลว → log warning, ใช้ราคาเดิมใน rule ต่อไป
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

---

## Testing

### รัน Tests ทั้งหมด

```bash
dotnet test
```

### Unit Tests — `tests/QuoteFlow.UnitTests`

ทดสอบ logic ภายในโดยไม่ต้องรัน server ใช้ in-memory dependencies ทั้งหมด

| Test Class | ครอบคลุม |
| --- | --- |
| `PricingEngineTests` | Pricing Pipeline ทุก handler |
| `RuleServiceTests` | Rule CRUD และ validation |

**PricingEngineTests** — ทดสอบ handler แต่ละตัวและการทำงานร่วมกัน:

| Test | สิ่งที่ตรวจสอบ |
| --- | --- |
| `Calculate_NoRules_ReturnInputBasePrice` | ไม่มี rule → คืน basePrice เดิม |
| `Calculate_WeightTierMatches_OverridesBasePrice` | WeightTier match → แทนที่ basePrice |
| `Calculate_WeightTierNotMatched_UsesInputBasePrice` | WeightTier ไม่ match → ใช้ basePrice เดิม |
| `Calculate_RemoteAreaSurcharge_AddsSurcharge` | destination อยู่ใน areaCodes → บวก surcharge |
| `Calculate_RemoteAreaNotMatched_NoSurcharge` | destination ไม่ match → surcharge = 0 |
| `Calculate_TimeWindowPromotion_AppliesDiscount` | อยู่ในช่วงเวลา → หักส่วนลด |
| `Calculate_AllRulesCombined_CorrectFinalPrice` | ทุก rule ทำงานร่วมกัน → ราคาถูกต้อง |
| `Calculate_FinalPriceNeverNegative` | ส่วนลดเกิน → finalPrice = 0 |
| `Calculate_MultipleWeightTiers_OnlyFirstMatchApplied` | หลาย WeightTier match → ใช้ priority ต่ำสุดอันเดียว |
| `Calculate_ExchangeRate_ConvertsAndReturnsInRequestCurrency` | แปลง USD→THB คำนวณ→แปลงกลับ USD |
| `Calculate_CurrencyPropagatedToResult` | currency ถูก propagate ไปใน result |
| `Calculate_VehicleType_AppliesMultiplier` | vehicleType match → คูณ priceMultiplier |
| `Calculate_VehicleTypeNotMatched_NoMultiplier` | vehicleType ไม่ match → basePrice เดิม |
| `Calculate_FuelSurcharge_AddsCostBasedOnDistance` | คำนวณ fuel cost จาก distance/kmPerLiter×price |
| `Calculate_NoVehicleType_SkipsFuelSurcharge` | ไม่มี vehicleType → ข้าม FuelSurcharge |
| `Calculate_NullOrEmptyCurrency_DefaultsToTHB` | currency ว่าง → default THB |

**RuleServiceTests** — ทดสอบ CRUD และ validation ของ RuleService:

| Test | สิ่งที่ตรวจสอบ |
| --- | --- |
| `CreateAsync_SetsIdAndTimestamps` | สร้าง rule → ระบบ generate Id, CreatedAt, UpdatedAt |
| `UpdateAsync_SetsUpdatedAt` | อัปเดต rule → UpdatedAt เปลี่ยน |
| `GetByIdAsync_NotFound_ReturnsNull` | ไม่พบ rule → คืน null |
| `DeleteAsync_CallsRepository` | ลบ rule → เรียก repository ถูกต้อง |

---

### Integration Tests — `tests/QuoteFlow.IntegrationTests`

ทดสอบ HTTP endpoints จริงผ่าน `WebApplicationFactory` รัน server จำลองใน memory ใช้ seed data จริง (rules.json, locations.json) ไม่ต้องรัน Docker

**QuoteEndpointsTests**:

| Test | สิ่งที่ตรวจสอบ |
| --- | --- |
| `PostQuotePrice_ValidRequest_Returns200WithResult` | `POST /quotes/price` → 200, finalPrice > 0 |
| `PostQuotePrice_WeightTierMatches_OverridesBasePrice` | weight=3kg → basePrice แทนที่เป็น 100 (จาก seed) |
| `PostQuoteBulk_ValidRequests_ReturnsAcceptedWithJobId` | `POST /quotes/bulk` → 202, มี jobId |
| `PostQuoteBulk_EmptyList_Returns400` | bulk ว่าง → 400 |
| `GetHealth_Returns200` | `GET /health` → 200 |

**JobEndpointsTests**:

| Test | สิ่งที่ตรวจสอบ |
| --- | --- |
| `GetJob_AfterBulkSubmit_ReturnsJobStatus` | submit bulk → `GET /jobs/{id}` → 200, totalItems ถูกต้อง |
| `GetJob_NotFound_Returns404` | jobId ไม่มี → 404 |

---

### รัน Tests แยกตาม project

```bash
# Unit Tests เท่านั้น
dotnet test tests/QuoteFlow.UnitTests

# Integration Tests เท่านั้น
dotnet test tests/QuoteFlow.IntegrationTests
```
