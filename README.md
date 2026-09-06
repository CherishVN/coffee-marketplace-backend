# Coffee Local Brand Marketplace — Backend

**Project Code:** SP26SE114  
**Duration:** January 2026 – April 2026  
**Supervisor:** Phan Minh Tâm (tampm@fe.edu.vn)

## Tổng quan

Backend cho **sàn thương mại điện tử cà phê đặc sản / thương hiệu địa phương Việt Nam** — nơi người bán đăng bán cà phê theo vùng trồng (Robusta Buôn Ma Thuột, Arabica Cầu Đất, …) và người mua có thể tin cậy xuất xứ sản phẩm.

Điểm khác biệt cốt lõi của dự án là **xác thực & phân biệt thương hiệu cà phê địa phương bằng AI**: seller khai báo vùng xuất xứ và đặc tính hương vị; hệ thống (rule + Gemini) kiểm tra tên/mô tả có thực sự là cà phê vùng đó hay không — ví dụ phát hiện *"cà phê bún bò Huế"* là không hợp lệ.

Ngoài luồng cà phê đặc sản, platform vẫn hỗ trợ các nghiệp vụ marketplace chung (giỏ hàng, thanh toán, vận chuyển, ví, tranh chấp…) để vận hành sàn; **phạm vi ưu tiên và tính năng AI phân biệt thương hiệu tập trung vào sản phẩm cà phê**.

**English name:** Coffee Local Brand Marketplace with AI-based origin verification  
**Vietnamese name:** Sàn thương mại điện tử cà phê thương hiệu địa phương với AI xác thực xuất xứ  
**Capstone title (SP26SE114):** E-Commerce platform for local brands with AI-based product category tagging

## Nhóm phát triển

| Họ tên | MSSV | Vai trò |
|--------|------|---------|
| Nguyễn Hồ Quốc Thắng | SE183534 | Team Leader |
| Lê Huỳnh Thiên Bảo | SE183554 | Team Member |
| Vũ An Khang | SE183550 | Team Member |
| Võ Thành Nam | SE183565 | Team Member |

## Kiến trúc hệ thống

Repository gồm **2 service ASP.NET Core 8** trong solution `ECommerceAPI.sln`:

| Service | Mô tả | Port mặc định (dev) |
|---------|--------|---------------------|
| **ECommerceAPI** | Main API — marketplace cà phê & nghiệp vụ TMĐT | `http://localhost:5153` |
| **ECommerceAI** | Microservice AI — xác thực xuất xứ cà phê, chat, gợi ý seller | `http://localhost:5001` |

```
┌─────────────┐     JWT (Supabase)     ┌──────────────────┐
│   Frontend  │ ─────────────────────► │   ECommerceAPI   │
└─────────────┘                        │  (Main API)      │
       │                               └────────┬─────────┘
       │                                        │
       │         JWT + Internal API Key         │ PostgreSQL
       └──────────────────────────────────────► │ ECommerceAI
                                                └────────┬─────────┘
                                                         │
                                              Google Gemini API
```

- **Xác thực:** Supabase Auth (JWKS), không tự triển khai `/api/auth/*`
- **Cơ sở dữ liệu:** PostgreSQL (EF Core), mỗi service có DbContext riêng
- **Realtime:** SignalR hub `/hubs/order-tracking`
- **Triển khai:** Docker + GitHub Actions → Google Cloud Run

## Phạm vi sản phẩm

| Phạm vi | Mô tả |
|---------|--------|
| **Trọng tâm** | Cà phê đặc sản Việt Nam — hạt rang xay, bột, drip bag, quà tặng cà phê theo vùng trồng |
| **Local Brand AI** | Chỉ áp dụng cho luồng đăng ký/xác thực **cà phê vùng** (`validate-local-brand`, `LocalSpecialtyProfile`) |
| **Marketplace chung** | Seller vẫn có thể bán sản phẩm khác qua category thông thường; AI gợi ý category/tag/material dùng chung |
| **Hồ sơ vùng** | Admin quản lý danh mục vùng cà phê (tỉnh, archetype, đặc tính hương vị) — `CategoryCode: ca_phe` |

## Tính năng chính

### Cà phê Local Brand (điểm nhấn dự án)

- **Hồ sơ đặc sản vùng** (`/api/local-specialty-profiles/*`) — Robusta Buôn Ma Thuột, Arabica Cầu Đất, …
- Seller gắn sản phẩm với profile vùng + chọn đặc tính hương vị (`ProductLocalMeta`)
- **AI xác thực xuất xứ** (`POST /api/ai/seller/validate-local-brand`) — Gemini phân tích ngữ nghĩa tên & mô tả có phải cà phê vùng đăng ký
- Admin duyệt sản phẩm kèm thông tin Local Brand; cảnh báo mâu thuẫn (`MismatchWarning`) nếu rule/AI phát hiện sai lệch

### Xác thực & hồ sơ người dùng
- Đăng ký/đăng nhập qua **Supabase** (frontend); backend validate JWT
- Phân quyền theo role: Customer, Seller, Admin
- Quản lý profile, địa chỉ giao hàng, đăng ký seller, đổi email (OTP)

### Cổng người bán (`/api/seller/*`)
- Quản lý shop, xem phí sàn hiện hành
- CRUD sản phẩm, biến thể, tồn kho
- Quản lý đơn hàng, duyệt/từ chối yêu cầu hủy
- Ví seller, yêu cầu rút tiền, trả lời review
- Xử lý tranh chấp (dispute) phía seller
- Gợi ý AI qua ECommerceAI (category, tags, materials, phân tích ảnh)
- Đăng ký **Local Brand cà phê** — chọn hồ sơ vùng, xác thực AI trước khi gửi duyệt

### Cổng khách hàng
- Duyệt cà phê theo vùng, shop, danh mục, bộ sưu tập, trang chủ
- Giỏ hàng & checkout (`/api/cart/*`)
- Đặt hàng, theo dõi, xác nhận nhận hàng, yêu cầu hủy (`/api/orders/*`)
- Yêu thích, review sản phẩm/shop
- Ví khách hàng & rút tiền (`/api/customer-wallet/*`)
- Tranh chấp & hoàn tiền (`/api/disputes/*`)
- Tin nhắn (`/api/conversations/*`), thông báo in-app

### Cổng quản trị (`/api/admin/*`)
- Quản lý user (suspend, audit log, reset password)
- Duyệt/từ chối seller, shop, sản phẩm
- CRUD category, tag, material; migrate sản phẩm giữa category
- Quản lý đơn hàng, tranh chấp, rút tiền seller & customer
- Dashboard thống kê, cấu hình & báo cáo phí sàn

### Thanh toán & vận chuyển
- **VNPay** và **MoMo** (single + batch payment, IPN/return URL)
- Tích hợp **GHN** — webhook cập nhật trạng thái giao hàng
- SignalR theo dõi đơn realtime

### AI Microservice (`ECommerceAI`)
- **Chat assistant** — tư vấn chọn cà phê & đặt hàng qua AI (`/api/ai/chat/*`)
- **Seller AI** — gợi ý category/tags/materials, phân tích text/ảnh
- **Xác thực cà phê vùng** — `validate-local-brand`: prompt chuyên biệt cho cà phê đặc sản VN (xem `ECommerceAI/Prompts/LocalBrandValidationPrompt.txt`)
- **Admin AI** — báo cáo, phân tích xu hướng, anomaly detection, dự báo metrics
- Lưu lịch sử gợi ý & phản hồi seller để cải thiện chất lượng
- Model: **Google Gemini** (C#, không dùng Python/FastAPI)

### Dịch vụ nền (background jobs)
- Timeout thanh toán, timeout yêu cầu hủy đơn
- Tự động hoàn thành đơn, giải phóng ví seller
- Auto refund khi trả hàng, timeout tranh chấp
- Gửi email thông báo đơn hàng (SMTP queue)

### Khác
- OCR căn cước VN qua FPT AI (`/api/ocr/vnm-id-card`) — xác minh seller
- Rate limiting cho tạo payment
- Health check & public config (`/api/system/*`)

## Công nghệ

| Thành phần | Công nghệ |
|------------|-----------|
| Backend | ASP.NET Core 8, C# 12, Clean Architecture |
| Database | PostgreSQL, Entity Framework Core 8 |
| Cache | `IMemoryCache` (in-process) |
| Auth | Supabase Auth + JWT Bearer (JWKS) |
| Validation | FluentValidation |
| AI | Google Gemini API (microservice C#) |
| Realtime | SignalR |
| Payment | VNPay, MoMo |
| Shipping | GHN (Giao Hàng Nhanh) |
| Email | SMTP |
| Container | Docker |
| CI/CD | GitHub Actions → Google Cloud Run |

## Cấu trúc project

```
E-Commerce-BE/
├── ECommerceAPI.sln
├── ECommerceAPI/                 # Main API
│   ├── Application/              # Services, DTOs, Interfaces
│   ├── Domain/                   # Entities, Enums
│   ├── Infrastructure/           # Data, Background, Payment, Notifications
│   ├── Controllers/
│   ├── Middleware/
│   ├── Hubs/                     # SignalR (OrderTrackingHub)
│   ├── migrations/
│   ├── Dockerfile
│   └── appsettings.Example.json
├── ECommerceAI/                  # AI Microservice
│   ├── Controllers/
│   ├── Services/
│   ├── Data/
│   ├── Prompts/
│   ├── Dockerfile
│   └── appsettings.Example.json
└── .github/workflows/
    └── deploy-cloud-run.yml
```

## Bắt đầu (Development)

### Yêu cầu

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL](https://www.postgresql.org/)
- Tài khoản [Supabase](https://supabase.com/) (auth)
- API key [Google Gemini](https://ai.google.dev/) (cho AI service)
- (Tuỳ chọn) Tài khoản VNPay/MoMo/GHN sandbox cho test payment & shipping

### Cài đặt

1. **Clone repository**
   ```bash
   git clone <repository-url>
   cd E-Commerce-BE
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore ECommerceAPI.sln
   ```

3. **Cấu hình**

   Copy file mẫu cho từng service:
   ```bash
   cp ECommerceAPI/appsettings.Example.json ECommerceAPI/appsettings.json
   cp ECommerceAI/appsettings.Example.json ECommerceAI/appsettings.json
   ```

   Điền các giá trị quan trọng:
   - `ConnectionStrings:DefaultConnection` — PostgreSQL
   - `Supabase:Url`, `Supabase:ServiceRoleKey` (Main API)
   - `AiService:BaseUrl` → `http://localhost:5001` (Main API trỏ tới AI service)
   - `Gemini:ApiKey` (AI service)
   - `InternalAuth:ApiKey`, `MainApi:BaseUrl` (AI service trỏ ngược Main API)
   - `FrontendUrl`, `Cors:AllowedOrigins`
   - (Tuỳ chọn) `VNPay`, `MoMo`, `GHN`, `Smtp`

4. **Chạy migration**
   ```bash
   dotnet ef database update --project ECommerceAPI
   dotnet ef database update --project ECommerceAI
   ```

5. **Chạy ứng dụng** (2 terminal)

   ```bash
   # Terminal 1 — Main API
   dotnet run --project ECommerceAPI

   # Terminal 2 — AI Service
   dotnet run --project ECommerceAI
   ```

6. **Truy cập Swagger**
   - Main API: http://localhost:5153/swagger
   - AI Service: http://localhost:5001/swagger

## Tài liệu API

Swagger là nguồn tài liệu chính khi chạy local hoặc trên môi trường deploy (`EnableSwagger: true`).

### Nhóm endpoint chính

| Nhóm | Route | Ghi chú |
|------|-------|---------|
| Profile | `/api/user/*` | Profile, địa chỉ, đăng ký seller |
| Coffee Local Brand | `/api/local-specialty-profiles/*` | Hồ sơ vùng cà phê (public) |
| AI — Coffee validation | `POST /api/ai/seller/validate-local-brand` | Xác thực xuất xứ cà phê (AI service) |
| Storefront | `/api/products`, `/api/shops`, `/api/home`, `/api/collections` | Public / authenticated |
| Cart & Orders | `/api/cart/*`, `/api/orders/*` | Checkout, tracking |
| Payments | `/api/payments/vnpay/*`, `/api/payments/momo/*` | |
| Seller | `/api/seller/*` | Shop, products, orders, wallet |
| Admin | `/api/admin/*` | Users, sellers, products, disputes, fees |
| Disputes | `/api/disputes/*`, `/api/seller/disputes/*` | Customer & seller |
| Wallet | `/api/customer-wallet/*` | Ví khách hàng |
| Chat | `/api/conversations/*` | Tin nhắn user-to-user |
| Notifications | `/api/notifications/*` | |
| AI (microservice) | `/api/ai/chat/*`, `/api/ai/seller/*`, `/api/ai/admin/*` | Chạy trên port 5001 |
| Webhooks | `/api/webhooks/ghn/*` | GHN shipping |
| Realtime | `/hubs/order-tracking` | SignalR |

> **Lưu ý:** Không có endpoint `/api/auth/*` — frontend xác thực trực tiếp với Supabase, gửi JWT Bearer cho backend.

## Triển khai (Production)

- Mỗi service có `Dockerfile` riêng
- Workflow `.github/workflows/deploy-cloud-run.yml` deploy lên **Google Cloud Run** (region `asia-southeast1`)
- Auto-deploy khi push branch `dev`; có thể chạy thủ công qua GitHub Actions

```bash
# Build Docker (ví dụ)
docker build -f ECommerceAPI/Dockerfile -t ecommerce-api .
docker build -f ECommerceAI/Dockerfile -t ecommerce-ai .
```

## Tiến độ phát triển

| Module | Trạng thái | Ghi chú |
|--------|------------|---------|
| Admin Portal | ✅ Hoàn thành | Users, sellers, products, categories, tags, materials, disputes, withdrawals, dashboard, platform fees |
| Authentication | ✅ Supabase | Không build auth API riêng |
| User Profile | ✅ Hoàn thành | Profile, addresses, register seller |
| Seller Portal | ✅ Hoàn thành | Shop, products, orders, wallet, reviews, disputes |
| Customer Portal | ✅ Hoàn thành | Browse, cart, checkout, orders, favorites, reviews, wallet, disputes |
| Order & Payment | ✅ Hoàn thành | VNPay, MoMo, GHN, SignalR tracking |
| Coffee Local Brand | ✅ Hoàn thành | Hồ sơ vùng, ProductLocalMeta, AI validate-local-brand, admin duyệt |
| AI Microservice | ✅ Hoàn thành | Chat, seller suggestions, coffee origin validation, admin analytics |
| Messaging & Notifications | ✅ Hoàn thành | Conversations, in-app + email |
| Background Services | ✅ Hoàn thành | 7 hosted services |
| Unit/Integration Tests | 🔲 Chưa có | Chưa có test project trong solution |

**Tổng số endpoint HTTP:** ~232 (Main API ~200 + AI ~32)

## Quy trình phát triển

1. Requirements & System Design ✅
2. Frontend (Customer/Seller Portal) — *repository riêng*
3. Backend Development (repository này) ⚙️
4. AI Module (ECommerceAI) 🤖 ✅
5. Testing, Deployment & Documentation 📋 *đang tiếp tục*

### Hướng dẫn đóng góp

1. Tuân thủ quy ước C# và cấu trúc Clean Architecture hiện có
2. Commit message rõ ràng
3. Tạo branch: `feature/ten-tinh-nang`
4. Tạo pull request để review

## Thông tin học thuật

**Lớp:** SE1835  
**Chuyên ngành:** Software Engineering (ES/IS/JS)  
**Trường:** FPT University  
**Thời gian dự án:** 01/01/2026 – 30/04/2026

## Liên hệ

**Supervisor:** Phan Minh Tâm  
**Email:** tampm@fe.edu.vn

---

**License:** Dự án Capstone — FPT University  
**Project Status:** 🟢 Active Development  
**Last Updated:** 2026-09-06 (Coffee Local Brand focus)
