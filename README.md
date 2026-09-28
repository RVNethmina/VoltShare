# VoltShare – Smart Solar Microgrid Trading System

SE4040 Enterprise Application Development – Assignment 1 (Year 4, Semester 2, 2026)

A client–server system that lets solar prosumers trade energy with local microgrid hubs. It has three parts:

- a **C# ASP.NET Core Web API** on Windows IIS, with MongoDB Atlas. All business logic lives here (FAT service).
- a **React web back-office** for back-office officers and grid operators.
- a **pure native Android app** with SQLite for prosumers and grid operators.

## Links

| | |
|---|---|
| **Git repository** | https://github.com/RVNethmina/VoltShare.git |
| **Demonstration video (YouTube, under 5 minutes)** | https://youtu.be/GNy57pkewY0 |

## Team and individual contributions

| IT Number | Name | Area of responsibility |
|---|---|---|
| IT22253958 | Nethmina W.P.R. | Platform, authentication, security and deployment |
| IT22140852 | Appuhami M.N.H. | User and prosumer account management, dashboards |
| IT22230942 | Madurapperuma H.A.S.I | Microgrid nodes, booking windows, maps and operator mode |
| IT22129376 | Wijesinghe W.A.C.S. | Energy reservations, business rules and transaction QR codes |

### IT22253958 – Nethmina W.P.R.
- **Web Service:** project architecture, `Program.cs`, configuration, MongoDB context, indexes and seed data
- **Web Service:** authentication (login, JWT issue and validation), BCrypt password hashing, role policies, global exception handling middleware, health check
- **Web Service:** IIS deployment of the API and the web site (deployment scripts, CORS, SPA rewrite)
- **Web App:** application shell, routing, API client, auth and theme contexts, shared UI components, sign-in page
- **Mobile App:** application setup, Retrofit API layer, SQLite database helper and local store, sign-in and splash screens, light and dark theme

### IT22140852 – Appuhami M.N.H.
- **Web Service:** user management (staff accounts) and prosumer management with NIC as the primary key
- **Web Service:** prosumer registration, activation, deactivation and closure-request rules; dashboard service and endpoints
- **Web App:** dashboard, system users, prosumers and pending activations pages
- **Mobile App:** prosumer registration, profile update, account closure request, prosumer dashboard

### IT22230942 – Madurapperuma H.A.S.I
- **Web Service:** microgrid node management (GPS location, capacity, battery slots, schedules, activation) and the deactivation-blocking rule
- **Web Service:** booking window (slot) management and the nearby-nodes geo query (2dsphere)
- **Web App:** microgrid nodes page and node detail page with booking windows
- **Mobile App:** nearby nodes list, Google Maps integration and device location
- **Mobile App:** operator mode: QR scanning, token verification and finalising the energy transfer

### IT22129376 – Wijesinghe W.A.C.S.
- **Web Service:** energy reservation management: create, modify, cancel, approve, reject and complete
- **Web Service:** 7-day and 12-hour rules, atomic no-overbooking, HMAC-signed single-use QR tokens
- **Web App:** reservations list with filters and the reservation detail page
- **Mobile App:** create booking, booking summary, booking history with search and filters, booking detail and transaction QR code

Each source file names its author in its header block.

## Technology

| Layer | Technology |
|---|---|
| Web Service | C#, ASP.NET Core Web API (.NET 10), JWT bearer auth, BCrypt.Net-Next, MongoDB.Driver, Swagger |
| Hosting | Windows IIS (API on port 8080, web site on port 8081) |
| Database | MongoDB Atlas – `users`, `solarStationInfo`, `energyBookingSlots`, `energyReservations` |
| Web App | React 19, TypeScript, React Router 7, Tailwind CSS 4, Vite |
| Mobile App | Native Android (Kotlin), Material Components, Retrofit, Coroutines, SQLite, Google Maps SDK, ZXing |

## Repository structure

```
VoltShare/
├── backend/            ASP.NET Core Web API (VoltShare.sln, src/VoltShare.Api)
├── web/                React + TypeScript back-office
├── mobile/             Native Android application (Kotlin)
├── scripts/            IIS deployment scripts for the API and the web site
└── docs/               Project notes
```

## Business rules (enforced by the Web API)

| # | Rule |
|---|---|
| R1 | A reservation must be in the future and within 7 days |
| R2 | A reservation can be changed or cancelled only with at least 12 hours' notice |
| R3 | A microgrid node cannot be deactivated while it has active reservations |
| R4 | Only a back-office officer can reactivate a deactivated prosumer |
| R5 | Prosumers who register from the mobile app start inactive until the back office activates them |
| R6 | A prosumer can request account closure; only the back office closes the account |
| R7 | A booking window cannot be overbooked (single atomic database update) |
| R8 | A transaction QR code is issued only when a booking is approved. It is HMAC-signed and valid once |
| R9 | Inactive prosumers, nodes or windows cannot take new bookings |

## Running the system

1. **Web API.** Put the MongoDB Atlas connection string, the JWT key and the QR key in `backend/src/VoltShare.Api/appsettings.Production.json`. Then deploy with `scripts/deploy-api-iis.ps1`, or run `dotnet run` in `backend/src/VoltShare.Api` for development. Health check: `GET /api/v1/health`.
2. **Web application.** Set `VITE_API_BASE_URL` in `web/.env.production`, then deploy with `scripts/deploy-web-iis.ps1`. For development, run `npm install` and then `npm run dev` in `web/`.
3. **Android application.** Open `mobile/` in Android Studio and add these lines to `mobile/local.properties`:
   ```
   MAPS_API_KEY=<Google Maps key>
   API_BASE_URL=http://<host IP>:8080/api/v1/
   ```
   Then run the app on an emulator or a phone on the same network.

### Demo accounts (seeded)

| Role | Email | Password |
|---|---|---|
| Back-office officer | admin@voltshare.lk | Admin@123 |
| Grid operator | operator1@voltshare.lk | Operator@123 |
| Prosumer | kamal@example.lk | Prosumer@123 |
