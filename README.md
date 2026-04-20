# Smart Pet Care A — Setup Guide

## Clone the repository

```bash
git clone https://github.com/VolodymyrBiletskyi/online-shop-backend.git
cd online-shop-backend
```

---

## Run locally

### 1. Restore dependencies

```bash
dotnet restore
```

### 2. Configure environment

Before launch, make sure your configuration contains:

- PostgreSQL connection string
- JWT settings

For example, in `appsettings.Development.json` or through environment variables:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=AppDb;Username=postgres;Password=your_password"
  },
  "JwtOptions": {
    "SecretKey": "your_long_secret_key_here",
    "Issuer": "OnlineShop",
    "Audience": "OnlineShopUsers"
  }
}
```

### 3. Apply migrations

```bash
dotnet ef database update
```

---

## Run locally

### 2. Restore dependencies

```bash
dotnet restore
```

### 3. Configure environment

Before launch, make sure your configuration contains:

- PostgreSQL connection string
- JWT settings

For example, in `appsettings.Development.json` or through environment variables:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=AppDb;Username=postgres;Password=your_password"
  },
  "JwtOptions": {
    "SecretKey": "your_long_secret_key_here",
    "Issuer": "OnlineShop",
    "Audience": "OnlineShopUsers"
  }
}
```


### 4. Run the application

```bash
dotnet watch run
```

The API should be available at:

```text
http://localhost:<YOURPORT>
```

### Swagger

After startup, open Swagger in the browser:

```text
http://localhost:<YOURPORT>/swagger
```

---

## Run with Docker Compose

### 1. Go to the project root

Make sure you are in the root folder, where these files are located:

- `Dockerfile`
- `docker-compose.yml`
- `.env`
- `OnlineShopBackend.sln`

### 2. Prepare the `.env` file

Example:

```env
POSTGRES_DB=AppDb
POSTGRES_USER=postgres
POSTGRES_PASSWORD=your_db_password

JWT_SECRET=your_long_secret_key_here
JWT_ISSUER=OnlineShop
JWT_AUDIENCE=OnlineShopUsers
```

### 3. Start containers

```bash
docker compose up --build
```

To run in background:

```bash
docker compose up --build -d
```

### 4. Open Swagger

After the containers start, Swagger should be available at:

```text
http://localhost:8080/swagger
```

---

## Stop Docker containers

```bash
docker compose down
```

This stops and removes containers, but keeps PostgreSQL data if a Docker volume is used.

To remove containers **and** database data:

```bash
docker compose down -v
```

---

## Useful commands

### View logs

```bash
docker compose logs -f
```

### View only API logs

```bash
docker compose logs -f api
```

### Rebuild from scratch

```bash
docker compose down
docker compose up --build --force-recreate
```

---:

```bash
dotnet ef database update
```

### 4. Run the application

```bash
dotnet run
```

The API should be available at:

```text
http://localhost:<YOURPORT>
```

### Swagger

After startup, open Swagger in the browser:

```text
http://localhost:<YOURPORT>/swagger
```

---

## Run with Docker Compose

### 1. Go to the project root

Make sure you are in the root folder, where these files are located:

- `Dockerfile`
- `docker-compose.yml`
- `.env`
- `OnlineShopBackend.sln`

### 2. Prepare the `.env` file

Example:

```env
POSTGRES_DB=AppDb
POSTGRES_USER=postgres
POSTGRES_PASSWORD=your_db_password

JWT_SECRET=your_long_secret_key_here
JWT_ISSUER=OnlineShop
JWT_AUDIENCE=OnlineShopUsers
```

### 3. Start containers

```bash
docker compose up --build
```

To run in background:

```bash
docker compose up --build -d
```

### 4. Open Swagger

After the containers start, Swagger should be available at:

```text
http://localhost:8080/swagger
```

---

## Stop Docker containers

```bash
docker compose down
```

This stops and removes containers, but keeps PostgreSQL data if a Docker volume is used.

To remove containers **and** database data:

```bash
docker compose down -v
```

---

## Useful commands

### View logs

```bash
docker compose logs -f
```

### View only API logs

```bash
docker compose logs -f api
```

### Rebuild from scratch

```bash
docker compose down
docker compose up --build --force-recreate
```

---
