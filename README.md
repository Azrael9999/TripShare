# TripShare (Backend + Vue Frontend)

A production-oriented starter for a ride sharing / carpool app.

## Stack
- Backend: ASP.NET Core (net10.0), EF Core, SQL Server
- Auth: Google ID token login + JWT access + refresh token rotation
- Email verification gate: **create trip** and **book trip** require verified email
- Frontend: Vue 3 + TypeScript + Vite + Tailwind + Pinia + Heroicons

## What’s implemented
- Google login (`/api/auth/google`) and token refresh (`/api/auth/refresh`)
- Email verification flow
  - backend creates verification token and sends email
  - dev mode writes emails to: `TripShare.Api/App_Data/dev-emails`
  - verify endpoint: `POST /api/users/verify-email?token=...`
- Trips
  - create trip with route points and per-segment prices
  - search + trip details
- Bookings
  - create booking with seat allocation across selected segments
  - driver accept/reject/complete, passenger cancel
  - concurrency protection via serializable transaction + retry
- Ratings
  - allow rating only after booking is completed, one rating per user per booking
- Logging
  - Serilog console + rolling file logs under `TripShare.Api/Logs/`
  - correlation id header: `X-Correlation-Id`
- SMS OTP via Text.lk (phone-based login / verification)
- Admin toggle for driver verification gate (require admin-approved drivers before creating trips)

## Run locally (Visual Studio 2022)
### 1) Database
Install SQL Server (LocalDB or full SQL Server).

Set the connection string in `src/TripShare.Api/appsettings.Development.json` (key: `ConnectionStrings:DefaultConnection`):

`Server=localhost;Database=TripShareDb;Trusted_Connection=True;TrustServerCertificate=True;`

Or use SQL auth as needed.

### 2) Backend
Open `TripShare.sln` in VS 2022.
Set startup project: `TripShare.Api`
Run. Swagger opens at:
- `http://localhost:8080/swagger`

### 3) Frontend (Vue)
Open folder `src/tripshare-web` in VS (or any terminal).

Copy `.env.example` to `.env` and set:
- `VITE_API_BASE=http://localhost:8080`
- `VITE_GOOGLE_CLIENT_ID=...`
- `VITE_GOOGLE_MAPS_API_KEY=...` (for Places autocomplete + details; debounced and cached)

Then:
```bash
npm install
npm run dev
```
Frontend runs on:
- `http://localhost:5173`

## Email verification gate
- After signing in, the API sends a verification email.
- In dev mode it writes a HTML file under:
  `TripShare.Api/bin/Debug/net8.0/App_Data/dev-emails/` (or the publish folder)
- Open that file and click the verification link.
- Once verified, you can create trips and book.

## SMS OTP (Text.lk)
- Configure `Sms` in `src/TripShare.Api/appsettings*.json` (or environment variables):
  - `Sms:Provider=TextLk`
  - `Sms:TextLk:ApiKey`, `Sms:TextLk:SenderId`, optional `Sms:TextLk:Endpoint`
  - `Sms:OtpMinutes` controls OTP expiry.
- Endpoints:
  - `POST /api/auth/sms/request` `{ phoneNumber }`
  - `POST /api/auth/sms/verify` `{ phoneNumber, otp }`
- Frontend login modal now supports SMS alongside Google.
- Driver verification (admin-controlled): enable/disable globally from the Admin dashboard; when enabled, only admin-verified drivers can create trips.

## Ads (non-invasive)
The UI includes **placeholder ad slots** in:
- Home sidebar
- Trip details bottom
- Profile sidebar
- Bookings sidebar

Replace `AdSlot.vue` with your ad provider integration (AdSense, etc.). Keep it subtle (no popups).

## Google Maps / Places efficiency
- Frontend search uses a debounced + cached Places autocomplete (per-query TTL ~5 minutes) and Place Details cache (per-place TTL ~10 minutes) to reduce calls without hurting UX.
- Selecting a suggestion reuses the same session token to keep billing optimized, then rotates to a fresh token for the next interaction.
- Configure `VITE_GOOGLE_MAPS_API_KEY` in `src/tripshare-web/.env` for local runs and in your Azure environment for production.

## Security notes
- Refresh tokens are stored as SHA-256 hashes
- Refresh token rotation is enabled
- Actions requiring verified email are protected by `RequireVerifiedEmailAttribute`
- Do not commit secrets. Use environment variables / user secrets for:
  - `Jwt:SigningKey`
  - SMTP credentials (if switching Email Mode to SMTP)
  - Google Client IDs
  - Text.lk API credentials (`Sms:TextLk:ApiKey`, `Sms:TextLk:SenderId`)

## Docker (optional)
`docker-compose.yml` brings up:
- SQL Server 2022
- API on 8080
- Web on 5173

```bash
docker compose up --build
```

## Deploying to Azure (API + Web)
### API (App Service or Container Apps)
1. Build & push Docker image (optional): `docker build -t <registry>/tripshare-api:latest -f src/TripShare.Api/Dockerfile .` then push to ACR; or let App Service build from GitHub.
2. Create SQL Database/Server and capture the connection string.
3. Create an App Service (Linux, container recommended) or Container App and set environment variables:
   - `ConnectionStrings__DefaultConnection=<SQL connection string>`
   - `Jwt__SigningKey=<long random secret>`
   - `Cors__AllowedOrigins__0=https://<your-web-domain>`
   - `Email__Mode=Smtp`, `Email__SmtpHost`, `Email__SmtpPort`, `Email__SmtpUser`, `Email__SmtpPass`, `Email__FromEmail`, `Email__FromName`
   - `Sms__Provider=TextLk`, `Sms__TextLk__ApiKey`, `Sms__TextLk__SenderId` (optional)
   - `ApplicationInsights__ConnectionString=<AI connection string>` (optional)
4. Expose port 8080 (already set in Dockerfile) and configure a health probe at `/health`.

### Web (Azure Static Web Apps or Azure App Service)
1. Build locally: `cd src/tripshare-web && npm install && npm run build` (outputs `dist/`).
2. For Static Web Apps: app location `src/tripshare-web`, output location `dist`, environment variables:
   - `VITE_API_BASE=https://<api-host>`
   - `VITE_GOOGLE_CLIENT_ID=<web client id>`
   - `VITE_GOOGLE_MAPS_API_KEY=<maps key>`
3. For App Service (Linux): serve the `dist` folder via `nginx` or similar static host, with the same env variables at build time.
4. Align API CORS origins with your web host.

### Local (Visual Studio / VS Code)
- Open `TripShare.sln`, ensure `ConnectionStrings:DefaultConnection` is set, and run `TripShare.Api`.
- In `src/tripshare-web`, copy `.env.example` to `.env`, set API base + Google keys, then `npm install && npm run dev`.

## Production readiness upgrades (included)
- Deterministic DB schema setup using **DbUp** embedded SQL scripts (see `src/TripShare.Api/DbUp/Scripts`).
  - Production uses DbUp by default.
  - Development falls back to `EnsureCreated` if scripts are not embedded/available.
- Configurable **CORS** via `Cors:AllowedOrigins` (no more `AllowAnyOrigin`).
- Health endpoint: `GET /health`
- Logging: Serilog (console + rolling file logs) with correlation ids.

## Running locally (developer friendly)
### Backend
- Open `TripShare.sln` and run `TripShare.Api`.
- Update `src/TripShare.Api/appsettings.Development.json` connection string.
- Swagger: `http://localhost:8080/swagger`

### Frontend
- `cd src/tripshare-web`
- `cp .env.example .env`
- `npm install`
- `npm run dev`
- UI: `http://localhost:5173`

## Running production-like with Docker
- Copy `.env.prod.example` to `.env.prod` and fill values.
- Run:
  - `docker compose -f docker-compose.prod.yml --env-file .env.prod up --build`
- Web: `http://localhost/`
- API: `http://localhost:8080/swagger`

## Deploy notes
- Put secrets in environment variables / Key Vault (JWT signing key, SMTP password).
- Restrict CORS to your web domain(s).
- Use a real email provider (`Email:Mode=Smtp`) for verification emails.
