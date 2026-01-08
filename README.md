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
- Trust & safety
  - driver identity verification workflow (admin review + verified badge surfaced in search/trip details)
  - emergency contact + panic flow with admin alerts
  - messaging on bookings with blocklist checks
- Logging
  - Serilog console + rolling file logs under `TripShare.Api/Logs/`
  - correlation id header: `X-Correlation-Id`
- SMS OTP via Text.lk (phone-based login / verification)
- Admin toggle for driver verification gate (require admin-approved drivers before creating trips)
- Ads
  - configurable ad slots + per-session frequency caps (admin UI), telemetry-backed impressions
- Reliability
  - durable background job queue for notifications with retry/backoff

## Run locally (Visual Studio 2022)
### 1) Database
Install SQL Server (LocalDB or full SQL Server).

Set the connection string in `src/TripShare.Api/appsettings.Development.json` (key: `ConnectionStrings:DefaultConnection`), or via environment variables / User Secrets:

`Server=localhost;Database=TripShareDb;Trusted_Connection=True;TrustServerCertificate=True;`

Tip: you can also drop a `.env.local` (or `.env`) file in the repo root for local runs; keys like `ConnectionStrings__DefaultConnection` and `Jwt__SigningKey` will be picked up automatically before `appsettings.json`.

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

### Local env & secrets
- Use User Secrets or `.env.local` to set:
  - `ConnectionStrings__DefaultConnection`
  - `Jwt__SigningKey`
  - `Cors__AllowedOrigins__0=http://localhost:5173`
  - `Sms__Provider` (`TextLk`, `Acs`, or `DevFile`), `Sms__TextLk:*` or `Sms__Acs:*`
  - `Email__Mode` (`DevFile`, `Smtp`, or `Acs`)
  - `BackgroundJobs__Provider` (`StorageQueue` recommended; uses Azurite if `UseDevelopmentStorage=true` — run `azurite --queue` or set `BackgroundJobs__Provider=Sql` to avoid the emulator)
  - `ApplicationInsights__ConnectionString` (optional)
  - `Telemetry__ApiKey` (optional; if set, the web client should set `VITE_TELEMETRY_KEY`)

## Default admin access
The API seeds a default admin account on startup:
- **Email/username:** `admin@tripshare.local`
- **Password:** `TripShareAdmin!2025`

To access the admin dashboard:
1. Open the web app and sign in with **Email & Password** on `/sign-in`.
2. Navigate to `/admin` (or use the Admin link in the top navigation).

> Tip: Change the default admin password in production by updating the stored hash/salt or replacing the account.

### Azure deployment (free-tier by default)
1) Provision infra with `azure-deploy.bicep`. The template defaults to **Free** tier (App Service plan F1, SQL Basic, in-memory cache, dev-file email) and only deploys paid components (Redis, Communication Services, App Insights ingestion) when you opt in.
```
az group create -n tripshare-rg -l eastus
az deployment group create -g tripshare-rg -f azure-deploy.bicep \
  -p sqlAdminLogin=tripshareadmin sqlAdminPassword=<StrongPassword> \
  storageName=<uniqueStorage> deploymentTier=Free
```
   - Switch to paid features by setting `deploymentTier=Paid` (creates Redis, Communication Services, paid App Insights, and uses the provided `sku` for the App Service plan).
2) Build/push API container: `docker build -t <registry>/tripshare-api:latest -f src/TripShare.Api/Dockerfile .` then push.
3) App settings to align with the chosen tier (Bicep sets defaults):
   - `Deployment__Tier=Free` (default) uses in-memory cache, dev-file email, Text.lk SMS; set to `Paid` to use Redis, ACS email/SMS, and Application Insights.
   - `ConnectionStrings__DefaultConnection`
   - `Jwt__SigningKey`
   - `Cors__AllowedOrigins__0=https://<your-web>`
   - `BackgroundJobs__Provider=StorageQueue`
   - `BackgroundJobs__StorageQueue__ConnectionString=<storage-conn>`
   - `Cache__RedisConnection=<redis-conn>` (only when using `Deployment__Tier=Paid`)
   - `ApplicationInsights__ConnectionString=<ai>` (only when using `Deployment__Tier=Paid`)
   - `Email__Mode=Acs` + `Email__Acs__ConnectionString` + `Email__Acs__FromEmail` (Paid), otherwise `DevFile`/`Smtp`
   - `Sms__Provider=Acs` + `Sms__Acs__ConnectionString` + `Sms__Acs__Sender` (Paid) or `TextLk` (default)
4) Frontend: build and host `src/tripshare-web/dist` via Static Web Apps/App Service/Front Door with `VITE_API_BASE` pointing to the API. Align CORS with the web host.

## Email verification gate
- After signing in, the API sends a verification email.
- In dev mode it writes a HTML file under:
  `TripShare.Api/bin/Debug/net8.0/App_Data/dev-emails/` (or the publish folder)
- Open that file and click the verification link.
- Once verified (and your phone number is verified), you can create trips and book.

## Phone verification gate
- Phone verification is required for creating trips and booking seats.
- Verify from the **Profile** page (send SMS code and confirm).

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

### Local verification testing tips
- **Email verification & password reset:** set `Email__Mode=DevFile` and open the HTML files written to `TripShare.Api/App_Data/dev-emails`.
- **Phone verification / SMS OTP:** set `Sms__Provider=DevFile` to write SMS payloads to `TripShare.Api/App_Data/dev-sms`.

## Password reset
- Request a reset link from `/reset-password` (Email & Password users).
- The API emails a reset link via the configured email provider.

## Branding (admin-managed)
- Admins can upload the logo, hero banner, map overlay, and login illustration from the Admin dashboard.

## Ads (non-invasive, configurable)
Ads are controlled centrally via the admin UI and enforced on both server and client:
- Server: `/api/ads/config` for settings, `/api/ads/impression` for server-side frequency enforcement. Settings include per-session frequency caps, per-page density caps (`MaxSlotsPerPage`), and sanitized HTML (scripts/iframes/event handlers are rejected).
- Client: page-level caps, session caps, deterministic slot variant selection, telemetry impressions, and a user “reduce ads” toggle surfaced via the ad-block notice.
- Admin UI: edit slots, enable/disable ads, set per-session cap, and set max slots per page. Default config keeps ads disabled unless turned on.

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
- With `Database:AutoCreate=true` (default for Development) the API will create the database and SQL login/user on first run if they don't exist.
- In `src/tripshare-web`, copy `.env.example` to `.env`, set API base + Google keys, then `npm install && npm run dev`.

## Production readiness upgrades (included)
- Deterministic DB schema setup using **DbUp** embedded SQL scripts (see `src/TripShare.Api/DbUp/Scripts`).
  - Production uses DbUp by default.
  - Development falls back to `EnsureCreated` if scripts are not embedded/available.
- Configurable **CORS** via `Cors:AllowedOrigins` (no more `AllowAnyOrigin`).
- Health endpoint: `GET /health`
- Logging: Serilog (console + rolling file logs) with correlation ids.
- Durable notification jobs with retry/backoff stored in SQL (see `BackgroundJobs` table and worker service).
- Ad slot configuration + frequency capping via `/api/admin/ads/config` and client fetch `/api/ads/config`.
- Driver identity verification workflow and badges; search supports `verifiedDriversOnly` to surface trusted drivers.

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
