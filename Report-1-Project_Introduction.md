# Project Report

## Report 1 – Project Introduction

**Student Accommodation Booking System**

---

**Hanoi, December 2024**

---

## Table of Contents

I. [Record of Changes](#i-record-of-changes)  
II. [Definition and Acronyms](#ii-definition-and-acronyms)  
III. [Project Introduction](#iii-project-introduction)  
&nbsp;&nbsp;&nbsp;&nbsp;1. [Overview](#1-overview)  
&nbsp;&nbsp;&nbsp;&nbsp;2. [Product Background](#2-product-background)  
&nbsp;&nbsp;&nbsp;&nbsp;3. [Existing Solutions](#3-existing-solutions)  
&nbsp;&nbsp;&nbsp;&nbsp;4. [Proposed Solution](#4-proposed-solution)  
&nbsp;&nbsp;&nbsp;&nbsp;5. [Project Scope & Limitations](#5-project-scope--limitations)

---

## I. Record of Changes

| Date | Action | In charge | Change Description |
|------|--------|-----------|-------------------|
| 2024-12-15 | A | Team | Initial document creation |
| 2024-12-20 | M | Team | Added JWT authentication scope |
| 2024-12-22 | M | Team | Updated admin features and SignalR |
|  |  |  |  |

*A - Added, M - Modified, D - Deleted*

---

## II. Definition and Acronyms

| Acronym | Definition |
|---------|------------|
| SABS | Student Accommodation Booking System |
| JWT | JSON Web Token |
| MVC | Model-View-Controller |
| RBAC | Role-Based Access Control |
| ATS | Applicant Tracking System (referenced in comparison) |
| API | Application Programming Interface |
| SignalR | Real-time web communication library for ASP.NET |
| EF Core | Entity Framework Core |

---

## III. Project Introduction

### 1. Overview

#### 1.1 Project Information

- **English name**: Student Accommodation Booking System
- **Vietnamese name**: Hệ thống Đặt Phòng Ký Túc Xá Sinh Viên
- **Project code**: SABS
- **Group name**: SEP490-G4
- **Software type**: Web Application (ASP.NET Core MVC)
- **Technology stack**: 
  - Backend: ASP.NET Core 8.0 (C#)
  - Frontend: Razor Pages, Bootstrap 5, JavaScript
  - Database: SQL Server
  - Authentication: Cookie-based + JWT Bearer
  - Real-time: SignalR

#### 1.2 Project Purpose

The purpose of this project is to develop the Student Accommodation Booking System (SABS), a comprehensive web-based platform designed to streamline the process of managing student dormitory room bookings at educational institutions. The system provides role-based access for students, administrators, facilities staff, and security guards, enabling efficient room reservation management, real-time occupancy tracking, asset management, and student reputation scoring. This document serves as a guideline for the implementation of the system's core features and defines the project scope, limitations, and expected outcomes.

#### 1.3 Project Stakeholders

| Full Name | Role | Email | Mobile |
|-----------|------|-------|--------|
| Nguyen Trung Kien | Lecturer/Advisor | kien.nt@fe.edu.vn | 0912345678 |
| Nguyen Tien Thanh | Team Leader / Admin Module Developer | thanh.nt@student.edu.vn | - |
| Team Member 1 | Student Booking Module | member1@student.edu.vn | - |
| Team Member 2 | Attendance & Check-in Module | member2@student.edu.vn | - |
| Team Member 3 | Security Guard Module | member3@student.edu.vn | - |
| Team Member 5 | Reputation System Module | member5@student.edu.vn | - |

---

### 2. Product Background

Student accommodation management at universities in Vietnam currently relies on fragmented manual processes involving paper forms, Excel spreadsheets, and informal communication channels (email, Zalo groups). A typical accommodation officer managing a 200-300 student dormitory spends 4-6 hours daily coordinating room bookings, manually checking room availability in spreadsheets, tracking student check-ins on paper logs, and responding to individual booking requests via messages or phone calls.

At the beginning of each semester, when 150+ students request room bookings simultaneously, the manual system breaks down. Officers cannot efficiently verify room availability in real-time, leading to double-bookings. A recent review at a representative university found that approximately 8-12% of bookings each semester contain errors: duplicate room assignments, bookings made for rooms already at capacity, or students assigned to rooms without valid enrollment status. When these errors surface—often only when students physically arrive to check in—resolution requires hours of phone calls, manual spreadsheet corrections, and frustrated students waiting in hallways.

Security guards face parallel challenges. They maintain paper-based check-in/check-out logs that are difficult to audit and never synchronized with the booking system. When a student reports a missing asset (damaged furniture, lost equipment), there is no structured workflow: reports are submitted via email or verbal notification, often lost in inboxes, with no tracking of review status, no record of which students were occupying the room, and no mechanism to apply reputation penalties automatically.

Facilities staff manage asset inventories and room statuses in separate spreadsheets, completely disconnected from booking records. When a room requires maintenance, there is no automated way to mark it unavailable in the booking system, leading to bookings being made for rooms that should be offline.

The lack of an integrated system causes:
- **Time waste**: 20-30 hours/week of manual coordination across all staff
- **Data integrity issues**: Conflicting records between booking sheets, check-in logs, and asset inventories
- **Student dissatisfaction**: Long wait times, booking errors, unclear application status
- **Accountability gaps**: No audit trail, no reputation tracking, no automated enforcement of booking policies

---

### 3. Existing Solutions

#### 3.1 StarRez (Enterprise Housing Management System)

StarRez is a comprehensive cloud-based housing and accommodation management system used by universities worldwide to manage student housing, room assignments, billing, and maintenance requests.

| Website / Link | https://www.starrez.com |
|----------------|-------------------------|
| **Target Users** | Large universities and housing departments, primarily in English-speaking countries |
| **Core Features** | Room assignments, billing integration, work order management, occupancy reporting, roommate matching, meal plan management |
| **Strengths** | Enterprise-grade feature set; extensive reporting capabilities; integrates with university ERP systems; mobile app available |
| **Weaknesses** | Extremely high cost (USD 20,000+ annual licensing); UI and documentation in English only; requires dedicated IT staff for implementation and maintenance; overkill for institutions with < 500 beds |
| **GAP for this project** | StarRez is designed for large-scale housing operations with complex billing, meal plans, and multi-building management. For a mid-sized Vietnamese dormitory with 200-300 students that needs basic booking, attendance tracking, and asset incident reporting, the cost and complexity are disproportionate. The institution needs a lightweight, self-hosted system deployable without vendor dependency. |

#### 3.2 Google Sheets + Google Forms + Zalo

Many Vietnamese universities without a dedicated housing system use Google Sheets (to track room availability and bookings), Google Forms (to collect booking requests), and Zalo groups (for coordination) as a free, flexible workaround.

| Website / Link | https://sheets.google.com / https://forms.google.com / https://zalo.me |
|----------------|-------------------------------------------------------------------------|
| **Target Users** | Small institutions that prioritize cost over structure |
| **Core Features** | Free; real-time collaborative editing; form-based data collection; basic filtering and sorting; instant messaging |
| **Strengths** | Zero software cost; no training required; accessible from any device; familiar tools in Vietnam |
| **Weaknesses** | No access control—any collaborator can edit or delete any row; no workflow enforcement; no audit trail; no automated business rules (reputation scoring, booking limits); unmanageable beyond 50 concurrent bookings; cannot enforce data integrity (double-booking prevention requires manual checking); no role-based views (students see all bookings, including others' data) |
| **GAP for this project** | Google Sheets cannot prevent double-bookings through database-level constraints, cannot enforce student reputation thresholds automatically, cannot hide sensitive check-in logs from students, and cannot integrate real-time occupancy tracking for security guards. As booking volume approaches 200+ students per semester, the sheet-based approach becomes error-prone and unscalable—the exact threshold the target institution is facing. |

#### 3.3 Manual Paper-Based System

Traditional paper logbooks and printed forms remain in use at smaller or rural Vietnamese educational institutions for check-in/check-out tracking and incident reporting.

| Website / Link | N/A |
|----------------|-----|
| **Target Users** | Institutions with minimal IT infrastructure |
| **Core Features** | Paper forms, manual logs, physical signature verification |
| **Strengths** | No technology dependency; works during power or internet outages; no training required |
| **Weaknesses** | No centralized data; impossible to query historical data; high risk of lost or damaged records; no real-time visibility for stakeholders; requires physical presence for every transaction; prone to human error (illegible handwriting, missing entries); no automated policy enforcement |
| **GAP for this project** | A paper-based system cannot provide real-time room occupancy visibility, cannot automatically calculate student reputation scores based on no-show penalties, cannot enforce weekly booking limits, and offers no protection against data loss. Modern educational institutions require digital records, audit trails, and remote access—all impossible with paper logs. |

#### 3.4 Section Summary

Enterprise housing management systems like StarRez address the right problem domain but at a cost and complexity level inappropriate for a mid-sized Vietnamese dormitory with 200-300 students and limited IT resources. Free tools like Google Sheets provide flexibility but no data integrity, no access control, no automated business logic, and no scalability. Paper-based systems offer no real-time visibility and no audit capabilities. None of these solutions provide a lightweight, self-hosted, role-based accommodation booking system with integrated reputation scoring, real-time occupancy tracking, and Vietnamese language support at zero recurring vendor cost—which is the specific gap SABS is designed to fill.

---

### 4. Proposed Solution

**Student Accommodation Booking System (SABS)** is a lightweight, self-hosted web application designed for mid-sized Vietnamese educational institutions (200-500 students) that need structured room booking management, role-based access control, and automated policy enforcement without the cost or complexity of enterprise housing platforms.

The system provides four distinct user experiences:

- **Students** view available rooms, check real-time availability for specific dates, submit booking requests with time-slot selection, view their booking history, and track their monthly reputation score (which affects booking eligibility).
- **Administrators** manage room inventory (create, update, deactivate rooms), review usage statistics, approve or reject asset incident reports, and view real-time room occupancy via SignalR-powered dashboard.
- **Security Guards** record student check-ins and check-outs, log asset inspections, report missing or damaged items, and view attendance history.
- **Facilities Staff** manage room assets, update maintenance status, and track asset lifecycle.

SABS closes critical gaps left by existing solutions:

1. **Automated double-booking prevention**: Database-level constraints and stored procedures ensure a room cannot be booked for overlapping time slots, eliminating manual conflict checking.
2. **Reputation-based booking eligibility**: Students earn/lose reputation points based on no-shows, cancellations, and asset incidents. Booking requests are automatically blocked when reputation falls below a configurable threshold—impossible in spreadsheets.
3. **Real-time occupancy visibility**: SignalR pushes live updates to admin dashboards showing which rooms are currently occupied, by whom, and for how long—critical for security and emergency response.
4. **Role-based data isolation**: Students see only their own bookings; guards see only check-in logs; admins see aggregated statistics. This privacy protection is impossible in shared spreadsheets.
5. **Audit trail and accountability**: Every booking, check-in, asset incident, and policy change is logged with timestamp and user ID, enabling compliance audits and dispute resolution.

For accommodation officers, the immediate benefit is elimination of manual availability checking and spreadsheet reconciliation. All bookings, check-ins, and asset records exist in one normalized database—accessible by authorized staff, protected from accidental modification, and automatically validated against business rules (room capacity, student eligibility, booking limits).

For students, the system replaces opaque, weeks-long waiting periods with self-service booking and real-time status visibility. Instead of submitting a paper form and waiting for a phone call, students select their preferred room and time slot online and receive instant confirmation if they meet eligibility criteria.

A self-hosted ASP.NET Core web application is the right architecture for this tool because it eliminates recurring SaaS subscription costs, keeps student data within the institution's own infrastructure (important for GDPR/privacy compliance in Vietnam), and can be extended with institution-specific workflows (integration with student information systems, custom reputation policies, localized reports) without requiring vendor approval or additional fees.

The system is built on proven, stable technologies (ASP.NET Core 8.0, SQL Server, SignalR) with strong security defaults (cookie + JWT authentication, anti-forgery tokens, parameterized queries) and requires only standard Windows Server hosting—infrastructure most Vietnamese universities already possess.

---

### 5. Project Scope & Limitations

#### 5.1 Major Features

| ID | Feature Name | Description | Addressed Gaps |
|----|--------------|-------------|----------------|
| **FE-01** | **Role-Based Authentication & Authorization** | Four user roles (STUDENT, ADMIN, SECURITY_GUARD, FACILITIES_STAFF) with cookie-based session authentication and JWT API tokens. Each role has a dedicated dashboard with role-specific navigation and data access. | GAP-01 (Access control), GAP-02 (Data privacy) |
| **FE-02** | **Student Room Booking Workflow** | Students browse available rooms, filter by building/floor/capacity, select a date to view real-time availability slots (via AJAX API), book a room for a specific time range within one day, and view their booking history. Booking requests are validated against active policies (reputation threshold, weekly limit). | GAP-01 (Double-booking prevention), GAP-03 (Real-time availability) |
| **FE-03** | **Reputation Scoring System** | Students start each month with an initial reputation score (default: 10 points). Points are deducted for no-shows (automatic penalty via background job), asset incidents (admin-approved), or cancellations. Bookings are blocked if monthly score falls below a configurable threshold. Students view their score and event history at `/student/reputation`. | GAP-04 (Automated policy enforcement), GAP-05 (Accountability) |
| **FE-04** | **Admin Room Management (CRUD)** | Administrators create, update, and delete rooms; set room status (AVAILABLE, MAINTENANCE, INACTIVE); track which admin last modified each room. All changes are logged to `AUDIT_LOG`. Admin dashboard includes room list with filtering by status and search by room code/building. | GAP-01 (Centralized data), GAP-05 (Audit trail) |
| **FE-05** | **Real-Time Occupancy Dashboard (SignalR)** | Admin dashboard at `/admin/occupied-rooms` displays all rooms currently occupied (student checked in but not yet checked out). SignalR hub pushes updates every 30 seconds and on demand. Shows room code, student name, check-in time, expected checkout time, and status (IN_USE, OVERDUE). | GAP-03 (Real-time visibility), GAP-06 (Emergency response capability) |
| **FE-06** | **Asset Incident Reporting & Review** | Security guards report asset incidents (MISSING, DAMAGED, NORMAL) during room inspections. Incidents are recorded with incident type, asset name, and optional notes. Admins review pending incidents at `/admin/incidents`, approve or reject them with admin notes. Approved MISSING incidents trigger automatic 10-point reputation deduction. | GAP-04 (Automated penalties), GAP-05 (Accountability), GAP-06 (Workflow structure) |
| **FE-07** | **Attendance Check-In/Check-Out** | Security guards record student check-ins and check-outs at `/guard/attendance`. System validates that a booking exists before allowing check-in. Guards can mark NO_SHOW for students who fail to appear. Background job automatically detects no-shows based on policy cutoff time and applies penalties. | GAP-04 (Automated no-show detection), GAP-05 (Audit trail) |
| **FE-08** | **Usage Statistics & Reporting** | Admin statistics page (`/admin/statistics`) shows booking volume, completion rate, cancellation rate, and occupancy percentage per room. Filterable by date range and building. Calculations performed via LINQ GroupBy on SQL Server. | GAP-06 (Data-driven decision making), GAP-07 (Performance metrics) |
| **FE-09** | **Policy Management** | Admins configure booking policies with effective date ranges: monthly initial reputation score, reputation threshold for booking eligibility, weekly booking limit per student, no-show penalty points, and no-show cutoff minutes. Multiple policies can coexist; the system uses the policy active at the time of booking. | GAP-04 (Flexible policy enforcement), GAP-08 (Multi-semester support) |
| **FE-10** | **JWT API Authentication** | In addition to cookie-based web authentication, the system exposes JWT-secured REST API endpoints at `/api/auth/login`, `/api/auth/me`, and role-specific test endpoints. Students, admins, guards, and staff can authenticate via API for mobile app integration or third-party system access. | GAP-09 (Mobile/API access), GAP-10 (Future extensibility) |

---

#### 5.2 Limitations & Exclusions

| ID | Excluded Item | Reason |
|----|---------------|--------|
| **LI-01** | **Email and SMS Notifications** | v1 communication is in-system only (TempData messages, dashboard alerts). Automated email/SMS notifications require SMTP/SMS gateway integration and are deferred to v2 after core workflow validation. Students must log in to check booking status. |
| **LI-02** | **Payment Integration (VNPay, PayOS, etc.)** | v1 assumes free dormitory bookings or payment handled offline. Online payment gateway integration (VNPay, PayOS) is out of scope. Bookings are approved immediately upon submission if eligibility criteria are met. Payment features are deferred to v2 based on institutional requirements. |
| **LI-03** | **Mobile Native Application** | v1 is a responsive web application accessible via mobile browsers. A native iOS/Android app is deferred until post-launch usage patterns confirm the need. JWT API endpoints (FE-10) support future mobile app development. |
| **LI-04** | **Multi-Language Support (English/Vietnamese Toggle)** | v1 UI is in Vietnamese only, matching the primary user base. English localization is deferred to v2 if international students enroll. All code comments and technical documentation are in English. |
| **LI-05** | **Advanced Analytics & Data Export** | v1 provides basic usage statistics (FE-08) in web UI only. Export to Excel/CSV, interactive charts (Chart.js, D3.js), and advanced BI dashboards are deferred to v2. Admins can query SQL Server directly if raw data export is needed. |
| **LI-06** | **Public Self-Registration** | v1 has no public student registration form. Accounts are pre-created by admins or imported from the university's student information system (SIS). Password reset functionality is manual (admin resets in database). Self-service registration is deferred pending security review. |
| **LI-07** | **Integration with University SIS (Student Information System)** | v1 operates as a standalone system. Automatic synchronization of student enrollment data, student ID, major, and academic status from university ERP/SIS is out of scope. Admins manually create student accounts. SIS integration is a v2 priority. |
| **LI-08** | **Roommate Matching & Preferences** | v1 supports individual room bookings only. Roommate pairing, preference surveys (quiet vs. social, sleep schedule), and group booking requests are deferred. Admins assign students to rooms manually if needed. |
| **LI-09** | **Maintenance Work Order Management** | Facilities staff can mark rooms as MAINTENANCE status, but v1 does not include a full work order system (assign technicians, track repair progress, log costs, vendor management). Work orders are managed externally. Full maintenance module deferred to v2. |
| **LI-10** | **Offline Mode / Progressive Web App (PWA)** | v1 requires an internet connection. Offline check-in capability (e.g., guards recording attendance in areas with poor connectivity) and PWA features (service workers, offline caching) are deferred. Guards must have stable network access. |

---

## End of Report 1

---

**Next Reports:**
- Report 2: Requirements Specification (Functional & Non-Functional Requirements)
- Report 3: System Design (Architecture, Database Schema, API Specification)
- Report 4: Implementation & Testing
- Report 5: Deployment & User Manual
