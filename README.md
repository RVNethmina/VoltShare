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

Each member owns one complete feature across the Web API, the web application and the Android application. The four shares are about the same size (about 4,200 lines of source each).

| IT Number | Name | Area of responsibility |
|---|---|---|
| IT22253958 | Nethmina W.P.R. | Energy reservation engine, business rules, concurrency control and the transaction QR flow |
| IT22140852 | Appuhami M.N.H. | Authentication, security, account management and local user storage |
| IT22230942 | Madurapperuma H.A.S.I | Microgrid nodes, booking windows, Google Maps and dashboards |
| IT22129376 | Wijesinghe W.A.C.S. | Platform architecture, error handling, IIS deployment and the shared client foundation |

### IT22253958 – Nethmina W.P.R.
- **Web Service:** the reservation engine. It covers the complete lifecycle (create, modify, cancel, approve, reject, complete) and every reservation business rule: the 7-day booking window, 12-hour notice for changes and cancellations, inactive prosumer, node and window checks, and duplicate booking prevention
- **Web Service:** concurrency control. A single conditional MongoDB update claims or releases a place in a booking window, so a window can never be overbooked
- **Web Service:** transaction QR. Tokens are HMAC-SHA256 signed and single-use: issued on approval, verified by the server, then used to finalise the energy transfer
- **Web App:** reservations list with filters, and the reservation decision screen (approve, reject, QR verification, completion)
- **Mobile App:** create booking, booking detail with the transaction QR code, and operator mode (QR scanning, server verification, finalising the transfer)

### IT22140852 – Appuhami M.N.H.
- **Web Service:** authentication and security: JWT issue and validation, role claims, BCrypt password hashing
- **Web Service:** staff user management, and prosumer management with NIC as the primary key, including the registration, activation, deactivation and closure-request rules
- **Web App:** sign-in page, session context and protected routes, plus the system users, prosumers and pending activations pages
- **Mobile App:** splash, sign-in and registration screens, profile update and account closure request
- **Mobile App:** SQLite local database for local user management and caching, and the authenticated Retrofit client, which attaches the access token and maps API errors

### IT22230942 – Madurapperuma H.A.S.I
- **Web Service:** microgrid node management (GPS location, capacity, battery slots, schedules, activation), and the rule that blocks deactivating a node with active reservations
- **Web Service:** booking window (slot) management and the nearby-nodes geospatial query (2dsphere)
- **Web Service:** dashboard statistics for prosumers and operators
- **Web App:** dashboard, the microgrid nodes page, and the node detail page with its booking windows
- **Mobile App:** prosumer dashboard, nearby nodes list, Google Maps with device location, the booking window picker, and booking history with search and filters

### IT22129376 – Wijesinghe W.A.C.S.
- **Web Service:** platform architecture: application start-up, dependency injection, authorisation policies, CORS and Swagger (`Program.cs`), plus the MongoDB context, indexes and seed data
- **Web Service:** the global exception handling middleware, and the error-code model every endpoint shares
- **Deployment:** hosting the Web API and the web application on Windows IIS (deployment scripts, SPA rewrite)
- **Web App:** application foundation: routing, layout and navigation, the API client and resource functions, shared UI components, and the light and dark theme
- **Mobile App:** application setup, the Retrofit API interface and models, the light and dark theme, shared formatters and status styles, and the booking summary screen

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
