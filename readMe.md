# 🏗️ Backend Project README

## 🚀 Getting Started

### 1. Clone repository

```bash
git clone  https://github.com/VolodymyrBiletskyi/smart-pet-care-api.git
cd smart-pet-care-api
```

### 2. Restore dependencies

```bash
dotnet restore
```

### 3. Configure database

Edit appsettings.json:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=app_db;Username=postgres;Password=yourpassword"
}
```

### 4. Apply migrations

```bash
dotnet ef database update
```

### 5. Run the project

```bash
dotnet run
```

---

## 🧠 Architecture Overview

Project uses **modular Clean Architecture (lightweight)**.

Structure is based on **features (modules)**.

---

## 🧱 Flow

HTTP → Controller → UseCase → Service → Repository → DB

---

## 📦 Structure

```
Modules/
  User/
    API/
      UserController.cs
    Application/
      CreateUserUseCase.cs
    Domain/
      UserService.cs
    Infrastructure/
      UserRepository.cs
    DTOs/
      CreateUserDto.cs
```

---

## 🔍 Layers

API → Controllers (no logic)  
Application → UseCases (orchestration)  
Domain → Services (business logic)  
Infrastructure → Repositories (DB)  
DTOs → data transfer objects

---

## ⚙️ Example Flow

POST /users  
→ Controller  
→ UseCase  
→ Service  
→ Repository → DB

---

## ❗ Rules

- Thin controllers
- Services = single responsibility
- UseCases = workflows
- Repositories = DB only
- Avoid service chains

---

## 🚫 Anti-patterns

- God services
- Mixed logic (DB + business)
- Flat structure
  f
  ffr
