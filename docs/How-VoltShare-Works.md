# How VoltShare Works

A plain-language guide to the domain behind the **Smart Solar Microgrid Trading System** — what a microgrid is, what the three roles do, what a booking is, and the business rules the system enforces.

Read this before the viva: you will be asked to explain the *domain*, not just the code.

---

## Contents

1. [The real-world idea](#1-the-real-world-idea)
2. [Injection and withdrawal](#2-injection-and-withdrawal)
3. [What a booking (reservation) is](#3-what-a-booking-reservation-is)
4. [The three roles](#4-the-three-roles)
5. [The business rules](#5-the-business-rules)
6. [The whole thing as one story](#6-the-whole-thing-as-one-story)
7. [What the system does not do](#7-what-the-system-does-not-do)

---

## 1. The real-world idea

**A prosumer** = **pro**ducer + con**sumer**. A household with solar panels on the roof. At midday the panels produce more than the house uses; at night they produce nothing and the house needs power. So the same home is sometimes a *producer* and sometimes a *consumer*.

**A microgrid** = a small, local energy network — a neighbourhood or community — with its own generation and battery storage, rather than one giant national grid.

In VoltShare each microgrid is represented by a **node** (the code calls it a *station*): a physical hub with

- a GPS location
- a total energy capacity (kWh)
- a number of **battery storage slots**
- opening hours

Two are seeded in the database: **Colombo Fort Solar Hub** and **Malabe Community Grid** — the two pins on the map.

The point of the system: let prosumers **trade energy through these local hubs** instead of it going to waste.

---

## 2. Injection and withdrawal

A common mix-up is to get these the wrong way round.

| Type | In the app | Direction | When a prosumer does it |
|---|---|---|---|
| **Injection** | "Deliver energy" | Home → **into** the microgrid | They have **surplus** solar energy |
| **Withdrawal** | "Draw energy" | Microgrid → **out** to the home | They **need more** energy than they have |

An easy way to remember it: you *inject* something **into** something; you *withdraw* money **from** a bank.

The assignment brief's own wording is energy **"drop-off / charging"** slots — drop-off is injection, charging is withdrawal.

---

## 3. What a booking (reservation) is

A hub does not have unlimited capacity. It has a limited number of battery slots and can only handle so much at once. So a prosumer cannot just turn up — they **book a time window** at a hub, the same way you would book a table at a restaurant.

The pieces:

- **Slot** — a time window the staff open at a hub, for example *Colombo Fort, 6 Sept, 19:30–21:30, 5 places, 25 kWh each*
- **Booking / reservation** — one prosumer claiming one place in one slot, for either injection or withdrawal

"Booking" and "reservation" mean the same thing. The code says *reservation*; the mobile app says *booking*.

### Lifecycle of a booking

```
                 staff approve            operator scans QR
   Pending  ───────────────►  Approved  ───────────────────►  Completed
      │                          │
      │ staff reject             │ cancelled (12h+ before start)
      ▼                          ▼
   Rejected                  Cancelled
```

| Status | Meaning |
|---|---|
| **Pending** | The prosumer requested it; waiting for staff |
| **Approved** | Staff accepted it. **This is when the QR code is generated** |
| **Completed** | The prosumer arrived, the operator scanned the QR, the transfer is recorded as done |
| **Rejected** | Staff turned it down; the place goes back into the slot |
| **Cancelled** | Withdrawn by the prosumer or by staff |

### Why the QR code exists

It proves the person at the hub has a genuine, approved booking.

- The QR is an opaque token **signed with a secret key only the server knows** (HMAC-SHA256)
- It contains **no personal data** — no name, no NIC
- The operator's phone sends it to the API, which checks the signature and the booking's status, then returns the booking details
- It works **once**. Scanning it a second time returns `QR_ALREADY_USED`

---

## 4. The three roles

The key idea: **the web app is for staff, the mobile app is mainly for prosumers, and grid operators use both.**

### Back office — the administrators

**Web app.** They run the system.

- Create, edit and deactivate **staff accounts** (back office and grid operators) — *only* they can
- Create and edit **prosumer** profiles, **activate** new registrations, **deactivate** accounts
- See **pending activations** and **deactivation requests**
- **Create, edit, activate and deactivate hubs** — *only* they can
- Everything a grid operator can do, below

### Grid operator — the people at the hubs

**Web app and mobile app.** They run day-to-day operations.

- **Approve or reject** bookings
- Open **slots** at hubs and update **battery slot availability**
- View prosumers and all bookings; search and filter them
- On mobile: **operator mode** — scan the prosumer's QR, verify it, and **finalise the transfer**

They **cannot** manage users, activate prosumers, or create or deactivate hubs.

### Prosumer — the households

**Mobile app only.** The web login deliberately turns them away with a message telling them to use the mobile app.

- **Register** using their NIC, which is their unique ID — the primary key
- Edit their profile and **request** account closure (they cannot close it themselves)
- Find nearby hubs on a **Google Map**
- **Book** injection or withdrawal windows, **change** them, or **cancel** them
- See their **QR code** once a booking is approved
- A dashboard with counts, booking history, search and filter

### Summary

| Capability | Back office | Grid operator | Prosumer |
|---|:---:|:---:|:---:|
| Web app | ✅ | ✅ | ❌ |
| Mobile app | — | ✅ | ✅ |
| Manage staff accounts | ✅ | ❌ | ❌ |
| Activate / deactivate prosumers | ✅ | ❌ | ❌ |
| Create / deactivate hubs | ✅ | ❌ | ❌ |
| Open slots, update battery availability | ✅ | ✅ | ❌ |
| Approve / reject bookings | ✅ | ✅ | ❌ |
| Scan QR and finalise transfer | ✅ | ✅ | ❌ |
| Register themselves | — | — | ✅ |
| Book, change, cancel own bookings | — | — | ✅ |
| Request own account closure | — | — | ✅ |

### Test accounts

| Role | Email | Password | Use on |
|---|---|---|---|
| Back office | `admin@voltshare.lk` | `Admin@123` | Web |
| Grid operator | `operator1@voltshare.lk` | `Operator@123` | Web and mobile |
| Prosumer | `kamal@example.lk` | `Prosumer@123` | Mobile |

Change the seeded passwords before submission.

---

## 5. The business rules

Every one of these is enforced **in the Web API** — the "FAT service" pattern the brief requires. The web and mobile apps only display what the API decides; they never work a rule out themselves. This is the most heavily marked part of the assignment.

| # | Rule | How it shows up |
|---|---|---|
| **R1** | A booking must be **in the future** and **within 7 days** | Booking 8 days out → *"Bookings can only be made up to 7 days in advance."* |
| **R2** | A booking can be changed or cancelled only **at least 12 hours** before it starts | Inside 12 hours the Cancel and Change buttons disappear |
| **R3** | A hub **cannot be deactivated** while it has upcoming active bookings | The web app refuses, showing the API's message |
| **R4** | A deactivated prosumer can only be **reactivated by back office** | Operators do not see the button |
| **R5** | Self-registered prosumers start **inactive** until back office approves them | They appear under *Pending Activations* and cannot sign in yet |
| **R6** | A prosumer can **request** closure but cannot close the account themselves | It sets a flag for back office to act on |
| **R7** | A slot **cannot be overbooked** | The "N free" badge; full slots are greyed out and refused |
| **R8** | A QR is issued only once a booking is **approved**, and is valid **once** | A second scan returns `QR_ALREADY_USED` |
| **R9** | An **inactive** prosumer or hub cannot take new bookings | The API refuses the request |

### R7 in detail — worth knowing for the viva

Overbooking is prevented with a single **atomic** database update:

> add one to `bookedCount` **only if** it is still below `capacity`

A naive approach — *"check whether there is space, then save"* — leaves a gap between the check and the save. Two people grabbing the last place at the same instant could both pass the check and both be saved. Doing the check and the increment as one indivisible operation closes that gap: exactly one of them succeeds.

---

## 6. The whole thing as one story

This also works as a script for the 5-minute demo video.

1. **Back office** (web) creates the **Malabe** hub and opens a slot: *Friday 10:00–12:00, 5 places*.
2. **Nimal**, a homeowner with solar panels, installs the app and **registers** with his NIC. His account is **inactive** (R5).
3. **Back office** sees him under **Pending Activations** and **activates** him.
4. On Friday morning Nimal's panels are producing more than his home uses. He opens the map, finds Malabe, and **books an injection** for 10:00. Status: **Pending**.
5. A **grid operator** (web) sees it and **approves** it. Nimal's app now shows a **QR code** (R8).
6. Nimal arrives at Malabe. The operator **scans his QR** with the mobile app. The API checks that it is genuine and approved.
7. The operator taps **Finalise energy transfer**. Status: **Completed**. Both dashboards update.

And the rules along the way:

- Had he tried to cancel an hour before 10:00 → refused (R2)
- Had he tried to book 9 days ahead → refused (R1)
- Had back office tried to deactivate Malabe while his booking was active → refused (R3)
- Had the operator scanned the same QR twice → refused (R8)

---

## 7. What the system does not do

**No electricity actually moves.** VoltShare is a booking and record-keeping system. *Completed* means *the system recorded that the transfer happened* — not that software controlled a battery.

There is also **no pricing**. The brief says "trading", but it never asks for money or credits, so the system tracks energy in kWh, not payment.

Both are exactly what the brief asks for. But if an examiner asks *"how does the energy get into the battery?"*, the right answer is:

> That is the physical hub's job. Our system manages who is allowed to transfer, when, and verifies that it happened.

Do not claim more than the system does.

---

## Try it

Sign in as **admin** on the web app and as **kamal** on an Android phone, then walk through the story in [section 6](#6-the-whole-thing-as-one-story) with the two screens side by side. After one run-through every screen will make sense.
