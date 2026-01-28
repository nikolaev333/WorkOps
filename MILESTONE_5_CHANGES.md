# Milestone 5: Quality & Ops - Промени и Описание

## Общо

Този milestone добавя production-ready функционалности за observability, security и operations: structured logging, correlation ID, rate limiting, security headers, Docker support и CI improvements.

---

## 1. Structured Logging (Serilog)

### Какво е направено:
- Добавени пакети: `Serilog.AspNetCore`, `Serilog.Sinks.Console`
- Конфигуриран Serilog като основен logger в `Program.cs`
- Добавено request logging middleware

### Файлове:
- **`Program.cs`** - Конфигурация на Serilog с enrichment (Environment, MachineName, ThreadId, CorrelationId)
- **`appsettings.json`** - Serilog настройки (log levels, output template, enrichment)

### За какво служи:
- **Structured logging** - Логовете са в структуриран формат (JSON), лесно за парсиране и анализ
- **Request logging** - Автоматично логване на всички HTTP заявки с method, path, status code, elapsed time
- **Enrichment** - Всеки log включва Environment, MachineName, ThreadId, CorrelationId за по-добро трасиране

### Как се използва:
- Логовете се извеждат в конзолата в структуриран формат
- Health endpoints (`/health/*`) са изключени от request logging за да не спамят логовете

---

## 2. Correlation ID Middleware

### Какво е направено:
- Създаден middleware който генерира/използва correlation ID за всяка заявка
- Добавени тестове

### Файлове:
- **`Middleware/CorrelationIdMiddleware.cs`** - Middleware който:
  - Чете `X-Correlation-Id` header от заявката (ако има)
  - Ако няма, генерира нов GUID
  - Добавя го в response header `X-Correlation-Id`
  - Добавя го в `HttpContext.Items` и Serilog LogContext
- **`Tests/CorrelationIdTests.cs`** - Тестове за correlation ID функционалността

### За какво служи:
- **Request tracing** - Можеш да проследиш цялата заявка от начало до край чрез correlation ID
- **Log correlation** - Всички логове за една заявка имат един и същ correlation ID
- **Debugging** - Лесно намиране на всички логове за конкретна заявка

### Как се използва:
- Клиентът може да изпрати `X-Correlation-Id` header (опционално)
- Ако не изпрати, API автоматично генерира нов
- Response header `X-Correlation-Id` винаги съдържа correlation ID
- Всички логове включват correlation ID в структурирания формат

---

## 3. Rate Limiting

### Какво е направено:
- Добавен пакет `Microsoft.AspNetCore.RateLimiting`
- Конфигурирани две политики:
  - Глобална: 60 заявки/минута per IP
  - Auth endpoints: 10 заявки/минута per IP
- Добавени тестове

### Файлове:
- **`Program.cs`** - Конфигурация на rate limiting policies и middleware
- **`Controllers/AuthController.cs`** - Добавен `[EnableRateLimiting("AuthPolicy")]` атрибут
- **`Tests/RateLimitingTests.cs`** - Тестове за rate limiting

### За какво служи:
- **Защита от abuse** - Предотвратява злоупотреба с API (brute force, DDoS)
- **Resource protection** - Защитава сървъра от претоварване
- **Auth protection** - По-строги лимити за login/register endpoints

### Как работи:
- Глобална политика: 60 заявки/минута per IP адрес (всички endpoints)
- Auth политика: 10 заявки/минута per IP адрес (само `/api/auth/*`)
- При надвишаване на лимита: връща `429 Too Many Requests` с ProblemDetails
- Използва IP адреса като partition key

---

## 4. Security Headers Middleware

### Какво е направено:
- Създаден middleware който добавя security headers към всички responses

### Файлове:
- **`Middleware/SecurityHeadersMiddleware.cs`** - Middleware който добавя:
  - `X-Content-Type-Options: nosniff` - Предотвратява MIME type sniffing
  - `Referrer-Policy: strict-origin-when-cross-origin` - Контролира referrer информация

### За какво служи:
- **Security hardening** - Подобрява сигурността на API-то
- **XSS protection** - Предотвратява някои XSS атаки чрез MIME sniffing
- **Privacy** - Контролира каква информация се изпраща в Referer header

### Как работи:
- Автоматично добавя headers към всички HTTP responses
- Приложено глобално за всички endpoints

---

## 5. Improved Exception Handling

### Какво е направено:
- Подобрен exception handler в `Program.cs`
- Добавено логване на unhandled exceptions
- По-добри ProblemDetails responses

### Файлове:
- **`Program.cs`** - Подобрен `UseExceptionHandler` middleware

### За какво служи:
- **Error logging** - Всички unhandled exceptions се логват с Serilog
- **Better error responses** - По-информативни ProblemDetails responses
- **Development vs Production** - В development показва exception message, в production общо съобщение

### Как работи:
- При unhandled exception:
  - Логва се с Serilog (включва correlation ID)
  - Връща се `500 Internal Server Error` с ProblemDetails
  - В development показва exception message, в production общо съобщение

---

## 6. Health Checks Enhancements

### Какво е направено:
- Health endpoints остават без промяна
- Request logging middleware изключва `/health/*` endpoints

### Файлове:
- **`Program.cs`** - Serilog request logging конфигурация изключва health endpoints

### За какво служи:
- **Reduced noise** - Health checks не спамят логовете
- **Performance** - Health checks са бързи и не генерират много логове

### Как работи:
- `/health/live` и `/health/ready` не се логват като нормални HTTP заявки
- Все още могат да се логват на Verbose level ако е необходимо

---

## 7. Docker Support

### Какво е направено:
- Създаден `Dockerfile` за API-то
- Обновен `docker-compose.yml` с API service

### Файлове:
- **`Dockerfile`** - Multi-stage build за API:
  - Build stage: компилира .NET приложението
  - Publish stage: публикува приложението
  - Final stage: минимален runtime image
- **`infra/docker-compose.yml`** - Добавен `api` service който:
  - Build-ва от Dockerfile
  - Expose-ва порт 8080
  - Зависи от `sqlserver` service
  - Използва environment variables за connection string и JWT key

### За какво служи:
- **Easy deployment** - Лесно стартиране на целия stack с една команда
- **Consistent environment** - Еднаква среда за всички разработчици
- **Production ready** - Готово за deployment в production

### Как се използва:
```bash
# Стартира SQL Server и API
docker compose -f infra/docker-compose.yml up -d

# API е достъпно на http://localhost:8080
# Swagger: http://localhost:8080/swagger
```

---

## 8. CI Improvements

### Какво е направено:
- Добавен format check в CI pipeline
- Добавено code coverage collection

### Файлове:
- **`.github/workflows/ci.yml`** - Добавени стъпки:
  - `Format check` - Проверява дали кодът е форматиран правилно
  - `Upload coverage` - Качва code coverage report като artifact

### За какво служи:
- **Code quality** - Гарантира че кодът е консистентно форматиран
- **Coverage tracking** - Позволява проследяване на code coverage във времето
- **Quality gates** - CI fail-ва ако кодът не е форматиран правилно

### Как работи:
- `dotnet format --verify-no-changes` - Проверява форматирането без да променя файловете
- Ако има промени, CI fail-ва
- Code coverage се събира и качва като artifact за анализ

---

## 9. Documentation Update

### Какво е направено:
- Обновен `README.md` с информация за новите функционалности

### Файлове:
- **`README.md`** - Добавени секции:
  - Docker Compose setup option
  - Observability (logging, correlation ID, rate limiting, health checks)
  - CI информация

### За какво служи:
- **Developer onboarding** - Нови разработчици могат лесно да разберат какво прави API-то
- **Reference** - Бърз reference за функционалностите

---

## Резюме на файлове

### Нови файлове:
1. `Middleware/CorrelationIdMiddleware.cs` - Correlation ID функционалност
2. `Middleware/SecurityHeadersMiddleware.cs` - Security headers
3. `Tests/CorrelationIdTests.cs` - Тестове за correlation ID
4. `Tests/RateLimitingTests.cs` - Тестове за rate limiting
5. `Dockerfile` - Docker image за API

### Променени файлове:
1. `Program.cs` - Serilog, correlation ID, rate limiting, security headers, exception handling
2. `WorkOps.Api.csproj` - Нови NuGet пакети
3. `appsettings.json` - Serilog конфигурация
4. `Controllers/AuthController.cs` - Rate limiting атрибут
5. `.github/workflows/ci.yml` - Format check и coverage
6. `infra/docker-compose.yml` - API service
7. `README.md` - Документация

---

## Как да тестваш промените

### Локално:
```powershell
cd M:\WorkOps.Api\WorkOps.Api
dotnet build WorkOps.Api.sln -c Release
dotnet test WorkOps.Api.sln -c Release
```

### С Docker:
```powershell
docker compose -f infra/docker-compose.yml up -d
# API: http://localhost:8080
```

### Тестване на correlation ID:
```bash
curl -H "X-Correlation-Id: test-123" http://localhost:8080/health/live
# Провери response header X-Correlation-Id
```

### Тестване на rate limiting:
```bash
# Направи 11 заявки бързо към /api/auth/login
# 11-тата трябва да върне 429
```

---

## Важни бележки

1. **Secrets** - Не комитвай secrets в git. Използвай user-secrets или environment variables
2. **Rate limiting** - Лимитите могат да се променят в `Program.cs` ако е необходимо
3. **Logging** - Health endpoints не се логват за да не спамят логовете
4. **Docker** - API service в docker-compose използва environment variables - промени ги за production
