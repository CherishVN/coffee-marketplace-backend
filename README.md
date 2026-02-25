# E-Commerce Platform for Local Brands - Backend API


**Project Code:** SP26SE114  
**Duration:** January 2026 - April 2026  
**Supervisor:** Phan Minh Tâm (tampm@fe.edu.vn)

## 📋 Project Overview

A specialized e-commerce platform designed for local Vietnamese brands (fashion, handmade crafts, organic food, and cosmetics). This backend API provides comprehensive services for sellers, customers, and administrators, with a key innovation: **AI-based product category tagging** to assist sellers in standardizing product classification and improving customer search experience.

### English Name
E-Commerce platform for local brands with AI-based product category tagging

### Vietnamese Name
Nền tảng thương mại điện tử dành cho các thương hiệu địa phương với AI phân loại và gắn nhãn sản phẩm

## 👥 Development Team

| Name | Student Code | Role |
|------|--------------|------|
| Nguyễn Hồ Quốc Thắng | SE183534 | Team Member |
| Lê Huỳnh Thiên Bảo | SE183554 | Team Member |
| Vũ An Khang | SE183550 | Team Member |
| Võ Thành Nam | SE183565 | Team Member |

## 🎯 Key Features

### 1. **User & Authentication**
- User registration and login
- Role-based access control (Customer, Seller, Admin)
- JWT token authentication

### 2. **Seller Portal**
- Create and manage shop profiles
- Product management (CRUD operations)
- Image upload with multiple variants
- Inventory tracking
- **AI-powered category & tag suggestions** during product upload
- Order management and status updates

### 3. **AI-based Product Category Tagging** 🤖
- Analyzes product title, description, and optional images
- Suggests main category, subcategory, and relevant tags
- Allows sellers to accept, override, or modify AI suggestions
- Logs suggestions for continuous model improvement

### 4. **Customer Portal**
- Browse products by category, tags, brand, or popularity
- Advanced search with AI-enhanced relevance
- Filter by price, category, shop, and attributes
- Shopping cart management
- Order placement and tracking

### 5. **Admin Portal**
- User, shop, and product management
- Seller and product approval workflow
- Category taxonomy maintenance
- System statistics and reports

### 6. **Order & Payment**
- Shopping cart operations
- Checkout workflow with shipping details
- Payment gateway integration
- Order tracking and history

## 🛠️ Technology Stack

### Backend
- **Framework:** ASP.NET Core 8.0 (Web API)
- **Language:** C# 12
- **Architecture:** RESTful API, Clean Architecture

### Database
- **Primary Database:** SQL Server / PostgreSQL
- **Caching:** Redis
- **ORM:** Entity Framework Core

### AI/ML
- **NLP:** Natural Language Processing for text classification
- **Framework:** ML.NET / Python integration
- **Microservice:** Separate AI service for category tagging

### DevOps
- **Containerization:** Docker
- **CI/CD:** GitHub Actions / Azure DevOps
- **Cloud:** Azure / AWS (planned)

### Security
- JWT Authentication
- Role-based Authorization
- Data encryption
- Input validation & sanitization

## 📦 Project Structure

```
ECommerceAPI/
├── Controllers/           # API endpoints
├── Models/               # Domain models
├── DTOs/                 # Data Transfer Objects
├── Services/             # Business logic
├── Repositories/         # Data access layer
├── Data/                 # Database context & configurations
├── Middleware/           # Custom middleware
├── Filters/              # Action filters
├── Helpers/              # Utility classes
├── AI/                   # AI integration module
└── appsettings.json      # Configuration
```

## 🚀 Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/sql-server) or [PostgreSQL](https://www.postgresql.org/)
- [Redis](https://redis.io/) (optional, for caching)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)
- [Git](https://git-scm.com/)

### Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd ECommerceAPI
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Update database connection string**
   
   Edit `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=ECommerceDB;Trusted_Connection=True;"
     }
   }
   ```

4. **Apply database migrations**
   ```bash
   dotnet ef database update
   ```

5. **Run the application**
   ```bash
   dotnet run
   ```

6. **Access the API**
   - API: `https://localhost:5001` or `http://localhost:5000`
   - Swagger UI: `https://localhost:5001/swagger`

## 📚 API Documentation

Once the application is running, visit the Swagger UI at:
```
https://localhost:5001/swagger
```

### Main Endpoints

- **Authentication:** `/api/auth/*`
- **Users:** `/api/users/*`
- **Shops:** `/api/shops/*`
- **Products:** `/api/products/*`
- **AI Tagging:** `/api/ai/categorize`
- **Orders:** `/api/orders/*`
- **Cart:** `/api/cart/*`
- **Admin:** `/api/admin/*`

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

## 📝 Development Workflow

### Task Packages

1. **Requirements & System Design** ✅
2. **Frontend Development** (Customer Portal) - *Separate Repository*
3. **Seller Portal Implementation** - *Separate Repository*
4. **Backend Development** ⚙️ *Current Repository*
5. **AI Module Development** 🤖
6. **Testing, Deployment & Documentation** 📋

## 🔧 Configuration

### Environment Variables

Create an `appsettings.Development.json` file (not tracked in Git):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "your-connection-string"
  },
  "JwtSettings": {
    "Secret": "your-secret-key",
    "Issuer": "ECommerceAPI",
    "Audience": "ECommerceClient",
    "ExpiryMinutes": 60
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "AI": {
    "ServiceUrl": "http://localhost:5002/api/categorize"
  }
}
```

## 🤝 Contributing

This is a Capstone Project. Contributions are managed by the development team listed above.

### Development Guidelines

1. Follow C# coding conventions
2. Write meaningful commit messages
3. Create feature branches: `feature/your-feature-name`
4. Submit pull requests for review
5. Ensure all tests pass before merging

## 📄 License

This project is developed as part of FPT University Capstone Project requirements.

## 📞 Contact

**Supervisor:** Phan Minh Tâm  
**Email:** tampm@fe.edu.vn

---

## 🎓 Academic Information

**Class:** SE1835  
**Specialty:** Software Engineering (ES/IS/JS)  
**Institution:** FPT University  
**Project Duration:** 01/01/2026 - 30/04/2026

---

**Last Updated:** 2026-02-25

---

## 🚀 Development Sprints Progress

### ✅ Sprint 0: Admin Portal (COMPLETED)
**Duration:** Tuần 0  
**Status:** ✅ Complete  
**APIs:** 44 endpoints  
**Documentation:** [ADMIN_PORTAL_APIs.md](ADMIN_PORTAL_APIs.md)

**Features:**
- User Management (suspend, unsuspend, audit logs)
- Seller Approval (approve, reject shops)
- Shop Management (activate, suspend, close)
- Category Management (CRUD, taxonomy tree, migrate products)
- Tag Management (CRUD)
- Product Moderation (hide, unhide, remove)
- Withdrawal Management (approve, reject)
- Dispute Management (approve refund, reject)
- Dashboard Statistics (comprehensive metrics)

---

### ✅ Sprint 2: User Profile Management (COMPLETED)
**Duration:** Tuần 3  
**Status:** ✅ Complete  
**APIs:** 8 endpoints  
**Documentation:** [USER_PROFILE_APIs.md](USER_PROFILE_APIs.md) | [Sprint Summary](SPRINT_2_SUMMARY.md)

**Features:**
- Get/Update user profile
- Register as seller (with business info)
- Address management (CRUD + set default)
- FluentValidation for all inputs
- Vietnamese slug generator
- Business rules enforcement

**Technical:**
- Service pattern implementation
- DTO pattern for clean API contracts
- Integration với Admin Portal (seller approval)

---

### ✅ Sprint 4: Seller Shop Management & Withdrawal (COMPLETED)
**Duration:** Tuần 6  
**Status:** ✅ Complete  
**APIs:** 5 endpoints  
**Documentation:** [SELLER_PORTAL_APIs.md](SELLER_PORTAL_APIs.md) | [Sprint Summary](SPRINT_4_SUMMARY.md)

**Features:**
- Shop management (get, update shop info)
- Wallet management (view balance, earnings, withdrawn)
- Withdrawal requests (create, view history)
- Auto reserve balance on withdrawal
- Prevent duplicate pending requests

**Technical:**
- Wallet entity integration
- Transaction ledger tracking
- FluentValidation for withdrawal requests
- Business rules: balance check, pending limit

---

### 🔄 Sprint 3: AI Microservice (SKIPPED - DO LATER)
**Duration:** TBD  
**Status:** ⏸️ Postponed  

**Planned Features:**
- AI Category Suggestion API
- AI Tag Suggestion API
- AI Material Suggestion API
- Product title/description analysis
- Optional image analysis
- Suggestion logging for model improvement
- Integration with backend

**Technology Stack:**
- Python + FastAPI
- PhoBERT for Vietnamese NLP
- TensorFlow/PyTorch for ML models
- Docker containerization

---

### 📊 Overall Progress

| Module | Progress | APIs | Status |
|--------|----------|------|--------|
| **Admin Portal** | 100% | 44/44 | ✅ Complete |
| **Authentication** | N/A | 0/0 | ✅ Supabase handles |
| **User Profile** | 100% | 8/8 | ✅ Complete |
| **Seller Shop & Withdrawal** | 100% | 5/5 | ✅ Complete |
| **Seller Products & Orders** | 0% | 0/10 | 🔲 Sprint 5 Next |
| **AI Service** | 0% | 0/4 | ⏸️ Postponed |
| **Customer Portal** | 0% | 0/20 | 🔲 Planned |
| **System Services** | 0% | 0/5 | 🔲 Planned |

**Total APIs Implemented:** 57 / ~100 endpoints (57% complete)

---

## 📚 Quick Links

- 📖 [Admin Portal API Documentation](ADMIN_PORTAL_APIs.md)
- 👤 [User Profile API Documentation](USER_PROFILE_APIs.md)
- 🏪 [Seller Portal API Documentation](SELLER_PORTAL_APIs.md)
- 🔐 [Authentication Strategy](AUTHENTICATION_STRATEGY.md)
- 🤖 [AI Service Integration Guide](AI_SERVICE_INTEGRATION.md)
- 📝 [Sprint 2 Summary](SPRINT_2_SUMMARY.md)
- 📝 [Sprint 4 Summary](SPRINT_4_SUMMARY.md)

---

**Project Status:** 🟢 Active Development  
**Current Sprint:** Sprint 2 (User Profile) ✅ COMPLETED  
**Next Sprint:** Sprint 3 (AI Microservice) 🤖 READY TO START
