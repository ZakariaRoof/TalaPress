# TalaPress Forms API — Frontend & AI Agent Integration Guide

This document is the **single reference** for connecting a frontend (React, Next.js, Vue, mobile, etc.) to TalaPress **dynamic forms**. It is written for human developers and **AI coding agents** building the frontend.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Prerequisites](#2-prerequisites)
3. [Base URL & Authentication](#3-base-url--authentication)
4. [Endpoints Summary](#4-endpoints-summary)
5. [Workflow: Build a Contact Form](#5-workflow-build-a-contact-form)
6. [GET /api/v1/forms — List Forms](#6-get-apiv1forms--list-forms)
7. [GET /api/v1/forms/{id} — Form Schema](#7-get-apiv1formsid--form-schema)
8. [POST /api/v1/forms/{id}/submit — Submit Response](#8-post-apiv1formsidsubmit--submit-response)
9. [Field Types & Rendering](#9-field-types--rendering)
10. [Response Storage Types (Backend Behavior)](#10-response-storage-types-backend-behavior)
11. [SMTP & Email Notifications](#11-smtp--email-notifications)
12. [Security Rules (Must Follow)](#12-security-rules-must-follow)
13. [Next.js BFF Pattern (Recommended)](#13-nextjs-bff-pattern-recommended)
14. [TypeScript Types](#14-typescript-types)
15. [Error Handling](#15-error-handling)
16. [AI Agent Checklist](#16-ai-agent-checklist)
17. [Admin Panel (Not for Frontend)](#17-admin-panel-not-for-frontend)

---

## 1. Architecture Overview

```
┌─────────────────┐     Pearl Key (server-side)      ┌──────────────────────┐
│  Frontend UI    │ ──► POST /api/contact            │  Next.js API Route   │
│  (browser)      │     (no secret in browser)       │  (BFF / proxy)       │
└─────────────────┘                                  └──────────┬───────────┘
                                                                │
                     Authorization: Pearl tp_pearl_...          │
                     Content-Type: application/json             ▼
                                                     ┌──────────────────────┐
                                                     │  TalaPress Backend   │
                                                     │  /api/v1/forms/...   │
                                                     └──────────┬───────────┘
                                                                │
                              ┌─────────────────────────────────┼─────────────────┐
                              ▼                                 ▼                 ▼
                     FormSubmissions DB                  SMTP Email      Validation
```

| Layer | URL | Auth |
|-------|-----|------|
| **Headless API (use this)** | `/api/v1/forms/*` | Pearl key in header |
| Admin HTML UI | `/Forms`, `/FormSubmissions` | Cookie login |
| **Deprecated** | `/api/forms/submit` | Returns `410 Gone` |

**Never** call `/Forms` or `/api/v1/forms` from the browser with the Pearl key exposed in client JavaScript.

---

## 2. Prerequisites

Before integrating:

1. **Pearl API key** — create in admin: **API Keys** (`/ApiKeys`). Copy the full key once; only a hash is stored.
2. **API enabled** — **Settings → Content/API** → `ApiEnabled = true`. Otherwise all `/api/v1/*` return `503`.
3. **Form exists & active** — built in admin **Forms** (`/Forms`), status **Active**.
4. **SMTP configured** (if form uses email) — **Settings → Email (SMTP)** tab.

---

## 3. Base URL & Authentication

### Production

```
https://talapress.online/api/v1
```

### Local

```
https://localhost:7298/api/v1
```

### Headers (every API request)

**Preferred:**

```http
Authorization: Pearl tp_pearl_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Content-Type: application/json
```

**Alternative:**

```http
X-Pearl-Key: tp_pearl_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Content-Type: application/json
```

Notes:

- Scheme is **`Pearl`**, not `Bearer`.
- Wrong domain typo: use `talapress.online` (not `tatapress`).

---

## 4. Endpoints Summary

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/forms` | List forms (metadata) |
| `GET` | `/forms/{id}` | Form schema + fields for UI |
| `POST` | `/forms/{id}/submit` | Submit user response |

Internal admin docs mirror: `/api/docs/forms` (requires admin login).

---

## 5. Workflow: Build a Contact Form

**Step 1 — Discover forms**

```bash
curl -s -H "Authorization: Pearl YOUR_KEY" \
  "https://talapress.online/api/v1/forms"
```

**Step 2 — Load schema for form `id = 1`**

```bash
curl -s -H "Authorization: Pearl YOUR_KEY" \
  "https://talapress.online/api/v1/forms/1"
```

**Step 3 — Render UI** from `fields[]` (see [Field Types](#9-field-types--rendering)).

**Step 4 — Submit via your BFF** (not directly from browser with key):

```bash
curl -s -X POST \
  -H "Authorization: Pearl YOUR_KEY" \
  -H "Content-Type: application/json" \
  "https://talapress.online/api/v1/forms/1/submit" \
  -d '{"fullName":"أحمد","email":"ahmed@example.com","message":"مرحباً"}'
```

**Step 5 — Show** `response.message` to the user on success.

---

## 6. GET /api/v1/forms — List Forms

### Request

```http
GET /api/v1/forms?activeOnly=true
Authorization: Pearl YOUR_KEY
```

| Query | Type | Default | Description |
|-------|------|---------|-------------|
| `activeOnly` | bool | `true` | Only `IsActive = 1` forms |

### Response `200`

```json
{
  "items": [
    {
      "id": 1,
      "name": "اتصل بنا",
      "nameEn": "Contact Us",
      "description": "نموذج التواصل",
      "descriptionEn": "Contact form",
      "submitButtonText": "إرسال",
      "submitButtonTextEn": "Submit",
      "successMessage": "تم إرسال رسالتك بنجاح.",
      "successMessageEn": "Your message was sent.",
      "responseStorageType": "Both",
      "isActive": true,
      "createdAt": "2026-06-27T10:00:00Z"
    }
  ]
}
```

---

## 7. GET /api/v1/forms/{id} — Form Schema

Returns form definition + fields. **Inactive forms → `404`.**

### Response `200`

```json
{
  "id": 1,
  "name": "اتصل بنا",
  "nameEn": "Contact Us",
  "successMessage": "تم إرسال رسالتك بنجاح.",
  "successMessageEn": "Your message was sent.",
  "responseStorageType": "Database",
  "fields": [
    {
      "fieldName": "fullName",
      "label": "الاسم الكامل",
      "labelEn": "Full Name",
      "fieldType": "Text",
      "placeholder": null,
      "placeholderEn": null,
      "helpText": null,
      "helpTextEn": null,
      "isRequired": true,
      "defaultValue": null,
      "optionsJson": null
    },
    {
      "fieldName": "department",
      "label": "القسم",
      "labelEn": "Department",
      "fieldType": "Select",
      "isRequired": false,
      "optionsJson": "{\"mode\":\"static\",\"staticOptions\":[{\"value\":\"sales\",\"label\":\"المبيعات\",\"labelEn\":\"Sales\"}]}"
    }
  ]
}
```

### `optionsJson` for Select / Radio / Checkbox

Parse as JSON. Common shape:

```json
{
  "mode": "static",
  "staticOptions": [
    { "value": "sales", "label": "المبيعات", "labelEn": "Sales" }
  ]
}
```

Use `label` for Arabic UI, `labelEn` for English (match site locale).

---

## 8. POST /api/v1/forms/{id}/submit — Submit Response

### Request body formats

**Flat (recommended):**

```json
{
  "fullName": "أحمد محمد",
  "email": "ahmed@example.com",
  "message": "أريد التواصل معكم"
}
```

**Wrapped:**

```json
{
  "locale": "ar",
  "fields": {
    "fullName": "أحمد محمد",
    "email": "ahmed@example.com"
  }
}
```

| Property | Description |
|----------|-------------|
| `locale` | Optional. `"ar"` or `"en"` — picks `successMessage` vs `successMessageEn` |
| Field keys | Must match `fieldName` from schema **exactly** (case-insensitive) |

### Success `200`

```json
{
  "success": true,
  "message": "تم إرسال رسالتك بنجاح.",
  "submissionId": 42
}
```

- `submissionId` is present when stored in database (`Database` or `Both` storage).
- Omitted when storage is `Email` only.

### Error responses

| Status | Meaning |
|--------|---------|
| `401` | Missing/invalid Pearl key |
| `404` | Form not found or inactive |
| `400` | Validation error (missing required field, invalid email, unknown field) |
| `502` | Email-only form but SMTP send failed |
| `503` | API disabled in settings |

Example validation error:

```json
{
  "success": false,
  "message": "الحقل 'البريد الإلكتروني' مطلوب."
}
```

### Server-side validation (automatic)

- Required fields enforced
- `Email` type → valid email format
- `Number` type → numeric
- Max **4000 chars** per field
- Max **50 fields** per submit
- **Unknown field names rejected** (whitelist from form schema)

---

## 9. Field Types & Rendering

| `fieldType` | HTML | Notes |
|-------------|------|-------|
| `Text` | `<input type="text">` | |
| `Email` | `<input type="email">` | Validated server-side |
| `Number` | `<input type="number">` | |
| `Phone` | `<input type="tel">` | Max 30 chars |
| `Textarea` | `<textarea>` | |
| `Select` | `<select>` | Parse `optionsJson` |
| `Radio` | `<input type="radio">` | Same options |
| `Checkbox` | `<input type="checkbox">` | Multiple values → comma-separated string |

**Field name rule:** `[A-Za-z][A-Za-z0-9_]*` — use exact `fieldName` as JSON key when submitting.

---

## 10. Response Storage Types (Backend Behavior)

Configured per form in admin **Forms → Settings → نوع معالجة الردود**:

| `responseStorageType` | Database save | Email notification |
|----------------------|---------------|-------------------|
| `Database` | Yes | No |
| `Email` | No | Yes (SMTP) |
| `Both` | Yes | Yes |

Email recipient: form `NotificationEmail`, or fallback **Settings → Company email**.

---

## 11. SMTP & Email Notifications

Configured in admin: **Settings → Email (SMTP)**.

Required for `Email` or `Both` storage types:

- SMTP enabled
- Host, Port, From Email
- Username/Password if provider requires auth
- Password stored **encrypted** (ASP.NET Data Protection)

Test via **Send test email** button after saving settings.

---

## 12. Security Rules (Must Follow)

1. **Pearl key only on server** — use API Route / BFF / serverless function.
2. **Never** embed `tp_pearl_...` in React client bundles or public env vars prefixed `NEXT_PUBLIC_`.
3. **Validate on client for UX**, but server always re-validates.
4. **HTTPS only** in production.
5. Rate-limit your BFF endpoint to reduce abuse.
6. Do not submit fields not defined in the form schema.

---

## 13. Next.js BFF Pattern (Recommended)

### Environment (server only)

```env
TALAPRESS_API_URL=https://talapress.online/api/v1
TALAPRESS_PEARL_KEY=tp_pearl_xxxxxxxx
```

### `app/api/contact/route.ts`

```typescript
import { NextRequest, NextResponse } from "next/server";

const BASE = process.env.TALAPRESS_API_URL!;
const KEY = process.env.TALAPRESS_PEARL_KEY!;
const FORM_ID = 1;

export async function POST(req: NextRequest) {
  const body = await req.json();

  const res = await fetch(`${BASE}/forms/${FORM_ID}/submit`, {
    method: "POST",
    headers: {
      Authorization: `Pearl ${KEY}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body),
  });

  const data = await res.json();
  return NextResponse.json(data, { status: res.status });
}
```

### Client component

```typescript
async function submitContact(formData: Record<string, string>) {
  const res = await fetch("/api/contact", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(formData),
  });
  return res.json();
}
```

---

## 14. TypeScript Types

```typescript
export type FormResponseStorageType = "Database" | "Email" | "Both";

export type FormFieldType =
  | "Text" | "Email" | "Number" | "Phone" | "Textarea"
  | "Select" | "Radio" | "Checkbox";

export interface FormFieldSchema {
  fieldName: string;
  label: string;
  labelEn?: string | null;
  fieldType: FormFieldType;
  placeholder?: string | null;
  placeholderEn?: string | null;
  helpText?: string | null;
  helpTextEn?: string | null;
  isRequired: boolean;
  defaultValue?: string | null;
  optionsJson?: string | null;
}

export interface FormSchema {
  id: number;
  name: string;
  nameEn?: string | null;
  successMessage?: string | null;
  successMessageEn?: string | null;
  responseStorageType: FormResponseStorageType;
  fields: FormFieldSchema[];
}

export interface FormSubmitResponse {
  success: boolean;
  message: string;
  submissionId?: number;
}

export interface FormsListResponse {
  items: Array<{
    id: number;
    name: string;
    nameEn?: string | null;
    responseStorageType: FormResponseStorageType;
    isActive: boolean;
  }>;
}
```

---

## 15. Error Handling

```typescript
async function fetchFormSchema(formId: number, pearlKey: string): Promise<FormSchema> {
  const res = await fetch(`https://talapress.online/api/v1/forms/${formId}`, {
    headers: { Authorization: `Pearl ${pearlKey}` },
  });
  if (res.status === 401) throw new Error("Invalid Pearl API key");
  if (res.status === 404) throw new Error("Form not found or inactive");
  if (res.status === 503) throw new Error("TalaPress API is disabled");
  if (!res.ok) throw new Error(`API error ${res.status}`);
  return res.json();
}
```

---

## 16. AI Agent Checklist

When implementing a form on the frontend, the agent MUST:

- [ ] Read form schema from `GET /api/v1/forms/{id}` before building UI
- [ ] Use `fieldName` as JSON keys on submit
- [ ] Respect `isRequired` and `fieldType`
- [ ] Parse `optionsJson` for Select/Radio/Checkbox
- [ ] Proxy submit through server route with Pearl key
- [ ] Display API `message` on success/error
- [ ] Use `name` / `nameEn` and `label` / `labelEn` based on locale
- [ ] NOT use deprecated `/api/forms/submit`
- [ ] NOT expose Pearl key in client code

---

## 17. Admin Panel (Not for Frontend)

| Page | Purpose |
|------|---------|
| `/Forms` | Build/edit forms & fields |
| `/FormSubmissions` | View stored responses |
| `/Settings` → SMTP | Email delivery config |
| `/ApiKeys` | Create Pearl keys |
| `/api/docs/forms` | Internal API reference |

---

## Quick Reference Card

```
Base:     https://talapress.online/api/v1
Auth:     Authorization: Pearl YOUR_KEY
List:     GET  /forms
Schema:   GET  /forms/{id}
Submit:   POST /forms/{id}/submit
Body:     { "fieldName": "value", ... }
Success:  { "success": true, "message": "...", "submissionId": 42 }
```

---

*Last updated: TalaPress Forms API v1 — matches `/api/v1/forms` implementation.*
