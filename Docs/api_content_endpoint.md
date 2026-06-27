# TalaPress Content API — Frontend Integration Guide

This document explains how to fetch content from the **Pearl Headless API** in TalaPress. It is written for frontend developers integrating a separate application (React, Next.js, Vue, mobile app, etc.).

---

## Table of Contents

1. [Important: API vs Admin Panel](#1-important-api-vs-admin-panel)
2. [Base URL & Authentication](#2-base-url--authentication)
3. [Endpoints Overview](#3-endpoints-overview)
4. [List Content — `GET /api/v1/content`](#4-list-content--get-apiv1content)
5. [Query Parameters Reference](#5-query-parameters-reference)
6. [Custom Field Filters](#6-custom-field-filters)
7. [Pagination](#7-pagination)
8. [Sorting](#8-sorting)
9. [Response Shape](#9-response-shape)
10. [Content Item Fields](#10-content-item-fields)
    - [CardList field type](#cardlist-field-type)
11. [Get Single Item — `GET /api/v1/content/{id}`](#11-get-single-item--get-apiv1contentid)
12. [Featured Images & Media URLs](#12-featured-images--media-urls)
13. [Frontend Examples](#13-frontend-examples)
14. [TypeScript Types](#14-typescript-types)
15. [Common Errors & Troubleshooting](#15-common-errors--troubleshooting)
16. [Real-World Recipes](#16-real-world-recipes)

---

## 1. Important: API vs Admin Panel

| URL | Purpose | Auth |
|-----|---------|------|
| `/Content?contentTypeId=2` | Admin Razor Page (HTML UI) | Cookie login (`admin` / password) |
| `/api/v1/content?contentTypeId=2` | **Headless JSON API** | Pearl key header |

**Always use `/api/v1/...` from your frontend.**  
Sending `Authorization: Pearl ...` to `/Content` will **not** work — you will get the login page HTML.

---

## 2. Base URL & Authentication

### Production

```
https://talapress.online/api/v1
```

### Local development

```
http://localhost:5297/api/v1
https://localhost:7298/api/v1
```

### Pearl authentication (required on every request)

**Preferred header:**

```http
Authorization: Pearl tp_pearl_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

**Alternative header:**

```http
X-Pearl-Key: tp_pearl_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

Notes:

- The scheme must be **`Pearl`**, not `Bearer`.
- Keys are created in the admin panel: **API Keys** (`/ApiKeys`).
- The full key is shown **once** at creation; only a SHA-256 hash is stored server-side.
- The API must be enabled in **Settings → ApiEnabled** (returns `503` if disabled).

### Prerequisites

1. Valid Pearl key (active, not revoked, not expired).
2. `ApiEnabled = true` in site settings.
3. Correct domain: `talapress.online` (not `tatapress.online`).

---

## 3. Endpoints Overview

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/content` | List/filter content (`top`, `orderBy`, or pagination) |
| `GET` | `/api/v1/content/{id}` | Single content item by ID |

Related endpoints (same auth):

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/content-types` | All content type templates |
| `GET` | `/api/v1/categories` | Category tree |
| `GET` | `/api/v1/menus` | Navigation menus |
| `GET` | `/api/v1/settings` | Public site settings |

Interactive docs (browser, no key): `/api/docs/content`

---

## 4. List Content — `GET /api/v1/content`

### Minimal request (all published content)

```bash
curl -H "Authorization: Pearl YOUR_KEY" \
  "https://talapress.online/api/v1/content"
```

When you send **no** `top` and **no** explicit `page` / `pageSize` query keys, the API returns **all** matching published rows in one response.

Defaults applied:

- `status=Published` (only published items)
- `orderBy` / `sortBy` = `publishDate`
- `orderDir` / `sortDir` = `desc`
- **No row limit** (unless you add `top` or `page`+`pageSize`)

### Top N items (recommended for homepages, widgets)

```bash
curl -H "Authorization: Pearl YOUR_KEY" \
  "https://talapress.online/api/v1/content?contentTypeId=2&top=5&orderBy=publishDate&orderDir=desc"
```

### Filter by content type ID — all events (type 2)

```bash
curl -H "Authorization: Pearl YOUR_KEY" \
  "https://talapress.online/api/v1/content?contentTypeId=2&orderBy=publishDate&orderDir=desc"
```

Returns **every** published event (no `top`, no `page`).

### Classic pagination (explicit page)

```bash
curl -H "Authorization: Pearl YOUR_KEY" \
  "https://talapress.online/api/v1/content?contentType=News&page=1&pageSize=20&orderBy=publishDate"
```

### Filter by content type name (Arabic or English)

```bash
# By English name
curl -H "Authorization: Pearl YOUR_KEY" \
  "https://talapress.online/api/v1/content?contentType=Event"

# By Arabic name
curl -H "Authorization: Pearl YOUR_KEY" \
  "https://talapress.online/api/v1/content?contentType=حدث"
```

---

## 5. Query Parameters Reference

All parameters are optional unless noted. Combine them with `&`.

### Pagination (`page` + `pageSize`)

Use only when you **explicitly** send `page` or `pageSize` in the query string (and `top` is not set).

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| `page` | `int` | `1` | ≥ 1 | Page number (1-based) |
| `pageSize` | `int` | `20` | 1–100 | Items per page |

### Full-text search

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `q` | `string` | — | Free-text search (primary) |
| `search` | `string` | — | Alias for `q` (if `q` is empty, `search` is used) |

Search matches (LIKE `%keyword%`):

- Title / Title (EN)
- Summary / Summary (EN)
- Body / Body (EN)
- SEO title, description, keywords (AR/EN)
- Custom fields JSON (`CustomFieldsJson`)
- Content type name (AR/EN)
- Category / sub-category name (AR/EN)

Example:

```
/api/v1/content?q=قطر&status=Published
```

### Content type filters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `contentTypeId` | `long` | — | Exact content type ID (e.g. `1` = News, `2` = Event, `3` = Page) |
| `contentType` | `string` | — | Content type **name** in Arabic **or** English (exact match) |

Use **either** `contentTypeId` **or** `contentType`, or both (AND logic).

### Category filters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `categoryId` | `long` | — | Main category ID |
| `subCategoryId` | `long` | — | Sub-category ID |

### Status & identity

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `status` | `string` | `Published` | Filter by workflow status. Use `All` to include every status. Common values: `Published`, `Draft`. |
| `slug` | `string` | — | Exact URL slug match |
| `authorId` | `long` | — | Filter by creator user ID (`CreatedBy`) |

### Date ranges

Dates are ISO 8601 or any format ASP.NET can parse.

| Parameter | Type | Description |
|-----------|------|-------------|
| `publishDateFrom` | `datetime` | Publish date ≥ this value |
| `publishDateTo` | `datetime` | Publish date ≤ end of this day |
| `createdFrom` | `datetime` | Created at ≥ this value |
| `createdTo` | `datetime` | Created at ≤ end of this day |

Example — events published in June 2026:

```
/api/v1/content?contentTypeId=2&publishDateFrom=2026-06-01&publishDateTo=2026-06-30
```

### Media filter

| Parameter | Type | Description |
|-----------|------|-------------|
| `hasFeaturedImage` | `bool` | `true` = only items with a featured image; `false` = only items without |

Example:

```
/api/v1/content?hasFeaturedImage=true&contentType=News
```

### Sorting

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `orderBy` | `string` | — | **Preferred** sort column (overrides `sortBy` when set) |
| `sortBy` | `string` | `publishDate` | Legacy alias for `orderBy` |
| `orderDir` | `string` | — | **Preferred** sort direction: `asc` or `desc` (overrides `sortDir`) |
| `sortDir` | `string` | `desc` | Legacy alias for `orderDir` |

### Result limit — `top`

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `top` | `int` | — | Return at most N rows (1–10000). When set, pagination is ignored. |
| *(no `top`)* | — | — | Return **all** matching rows (unless `page` or `pageSize` is explicitly sent) |

**Behavior summary:**

| Request | Result |
|---------|--------|
| `?top=10&orderBy=publishDate&orderDir=desc` | First 10 items, sorted |
| `?contentTypeId=2` (no `top`, no `page`) | **All** published events |
| `?contentTypeId=2&page=1&pageSize=20` | Classic pagination (20 per page) |

---

## 6. Custom Field Filters

Content types can define custom fields (stored in `CustomFieldsJson`). Filter by field value using dynamic query keys:

```
field.{FieldName}={value}
fields.{FieldName}={value}
```

Both prefixes work identically.

Rules:

- `{FieldName}` must match the field name in the content type builder (alphanumeric + underscore only).
- Matching is **partial** (SQL `LIKE %value%`) against the JSON value.
- Multiple `field.*` parameters are combined with **AND**.

Examples:

```
/api/v1/content?field.source=Reuters&status=Published
/api/v1/content?fields.location=Doha&contentType=Event
/api/v1/content?contentTypeId=2&field.StartDate=2026-06
```

For Event content type (ID 2), typical custom fields might include:

- `StartDate`
- `EventEndDate`

---

## 7. Pagination

> **Note:** If you omit both `top` and explicit `page`/`pageSize`, the API returns **all** matching records in one response. Use `top=N` to cap results, or `page` + `pageSize` for classic pages.

The API returns pagination metadata in `meta.pagination` when paginating or when using `top`.

| Field | Type | Description |
|-------|------|-------------|
| `page` | `int` | Current page |
| `pageSize` | `int` | Page size used |
| `total` | `int` | Total matching records |
| `totalPages` | `int` | `ceil(total / pageSize)` |
| `hasNext` | `bool` | More pages available |
| `hasPrevious` | `bool` | Previous page exists |

### Fetch all items in one call

When `top` is omitted and `page` / `pageSize` are not in the query string, a single request returns everything:

```typescript
const res = await fetch(
  "https://talapress.online/api/v1/content?contentTypeId=2&orderBy=publishDate&orderDir=desc",
  { headers: { Authorization: `Pearl ${API_KEY}` } }
);
const { data, meta } = await res.json();
// data = all published events; meta.filters.fetchAll === true
```

### Paginated loop (when using page/pageSize)

```typescript
async function fetchPagedContent(params: Record<string, string>, apiKey: string) {
  const base = "https://talapress.online/api/v1/content";
  const headers = { Authorization: `Pearl ${apiKey}` };
  const pageSize = 100;
  let page = 1;
  const all: ContentItem[] = [];

  while (true) {
    const qs = new URLSearchParams({ ...params, page: String(page), pageSize: String(pageSize) });
    const res = await fetch(`${base}?${qs}`, { headers });
    if (!res.ok) throw new Error(await res.text());
    const json = await res.json();
    all.push(...json.data);
    if (!json.meta.pagination.hasNext) break;
    page++;
  }
  return all;
}
```

---

## 8. Sorting

Use **`orderBy`** + **`orderDir`** (recommended) or **`sortBy`** + **`sortDir`** (legacy aliases).

### Allowed `orderBy` / `sortBy` values

| Value | Sorts by |
|-------|----------|
| `id` | Content ID |
| `title` | Title (Arabic column) |
| `slug` | Slug |
| `status` | Workflow status |
| `contentType` | Content type English name |
| `category` | Main category name |
| `subCategory` | Sub-category name |
| `createdAt` | Creation timestamp |
| `updatedAt` | Last update timestamp |
| `publishDate` | Publish date (default) |
| `hits` | View count |

Unknown `orderBy` / `sortBy` values fall back to `COALESCE(PublishDate, CreatedAt)`.

### `orderDir` / `sortDir`

- `asc` — ascending
- `desc` — descending (default)

Tie-breaker: always `Id DESC` as secondary sort.

Example — top 5 latest events:

```
/api/v1/content?contentTypeId=2&top=5&orderBy=publishDate&orderDir=desc
```

Example — all events (no limit):

```
/api/v1/content?contentTypeId=2&orderBy=publishDate&orderDir=desc
```

---

## 9. Response Shape

### Success — list (`200 OK`)

```json
{
  "data": [ /* array of content items */ ],
  "meta": {
    "pagination": {
      "page": 1,
      "pageSize": 5,
      "total": 1,
      "totalPages": 1,
      "hasNext": false,
      "hasPrevious": false
    },
    "filters": {
      "q": null,
      "contentTypeId": 2,
      "contentType": null,
      "categoryId": null,
      "subCategoryId": null,
      "status": "Published",
      "slug": null,
      "authorId": null,
      "publishDateFrom": null,
      "publishDateTo": null,
      "createdFrom": null,
      "createdTo": null,
      "hasFeaturedImage": null,
      "sortBy": "publishDate",
      "sortDir": "desc",
      "orderBy": null,
      "orderDir": null,
      "top": 5,
      "fetchAll": false
    }
  }
}
```

`meta.filters` echoes the query parameters applied — useful for debugging.

### Error responses

| HTTP | Meaning |
|------|---------|
| `401 Unauthorized` | Missing/invalid Pearl key, or wrong auth scheme (`Bearer` instead of `Pearl`) |
| `404 Not Found` | Single item endpoint: ID does not exist or is soft-deleted |
| `503 Service Unavailable` | Pearl API disabled in settings (`{ "message": "Pearl API is disabled in site settings." }`) |
| `500` | Server/database error |

---

## 10. Content Item Fields

Each object in `data[]` has the following structure.

### Core fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | `number` | Unique content ID |
| `contentTypeId` | `number` | Content type template ID |
| `title` | `string?` | Title (Arabic / primary) |
| `titleEn` | `string?` | Title (English) |
| `slug` | `string?` | URL slug (may be null) |
| `summary` | `string?` | Short summary (HTML allowed) |
| `summaryEn` | `string?` | Summary (English) |
| `content` | `string?` | Full body (HTML) |
| `contentEn` | `string?` | Full body (English) |
| `featuredImage` | `string?` | Relative path, e.g. `/uploads/2026/abc.png` |
| `status` | `string` | e.g. `Published`, `Draft` |
| `publishDate` | `string?` | ISO UTC, e.g. `2026-06-25T20:56:00Z` |
| `categoryId` | `number?` | Main category ID |
| `subCategoryId` | `number?` | Sub-category ID |
| `hits` | `number` | View count |
| `hitts` | `number` | Same as `hits` (legacy alias) |

### SEO object

```json
"seo": {
  "title": "SEO title AR",
  "titleEn": "SEO title EN",
  "description": "...",
  "descriptionEn": "...",
  "keywords": "...",
  "keywordsEn": "...",
  "canonical": "https://example.com/page"
}
```

### Nested relations

```json
"contentType": {
  "id": 2,
  "name": "حدث",
  "nameEn": "Event",
  "icon": "bi bi-calendar-event"
},
"category": {
  "id": 2,
  "name": "اخبار ومقالات",
  "nameEn": "News and Articles",
  "slug": "news-and-articles"
},
"subCategory": null,
"author": {
  "id": 1,
  "name": "Super Admin",
  "username": "admin"
}
```

`category` and `subCategory` are `null` when not assigned.

### Custom fields — `fields` vs `fieldsDisplay`

| Property | Purpose |
|----------|---------|
| `fields` | Raw values from `CustomFieldsJson` (strings, numbers, **CardList objects**, etc.) |
| `fieldsDisplay` | Human-readable values; Select/MultiSelect fields resolve labels; **CardList** is identical to `fields` |

Example for a News item:

```json
"fields": {
  "source": "Reuters",
  "Type": "breaking"
},
"fieldsDisplay": {
  "source": "Reuters",
  "Type": {
    "value": "breaking",
    "label": "عاجل",
    "labelEn": "Breaking"
  }
}
```

Example for an Event (content type 2):

```json
"fields": {
  "StartDate": "2026-06-26T20:56",
  "EventEndDate": "2026-06-30T20:56"
},
"fieldsDisplay": {
  "StartDate": "2026-06-26T20:56",
  "EventEndDate": "2026-06-30T20:56"
}
```

**Security:** Sensitive field names (`Password`, `Token`, `Secret`, etc.) are stripped from API responses automatically.

### CardList field type {#cardlist-field-type}

`CardList` is a custom field type for **repeating card sections** (e.g. sponsorship types, process steps, feature grids). Values are stored in `CustomFieldsJson` and returned in both `fields` and `fieldsDisplay` as a **JSON object** (not a string).

#### Schema

The field key in `fields` is the **programmatic field name** defined in the Content Type Builder (e.g. `sponsorship_types`, `xtest`). The value shape:

```json
{
  "sectionTitle": "أنواع الكفالات",
  "sectionTitleEn": "Sponsorship Types",
  "sectionSubtitle": "ما الذي يميزنا؟",
  "sectionSubtitleEn": "What makes us special?",
  "sectionSummary": "اختر نوع الكفالة المناسب",
  "sectionSummaryEn": "Choose the right sponsorship type",
  "items": [
    {
      "title": "كفالة يتيم",
      "titleEn": "Orphan Sponsorship",
      "subtitle": "أساس تعاملنا مع المجتمع",
      "subtitleEn": "How we engage society",
      "description": "دعم شامل لليتيم",
      "descriptionEn": "Full support for an orphan",
      "mediaType": "icon",
      "icon": "bi-heart-fill",
      "image": "",
      "link": "/sponsorship/orphan",
      "featured": false
    },
    {
      "title": "بطاقة مميزة",
      "titleEn": "Featured Card",
      "subtitle": "نص العنوان الفرعي",
      "subtitleEn": "Subtitle text",
      "description": "وصف البطاقة المميزة",
      "descriptionEn": "Featured card description",
      "mediaType": "image",
      "icon": "",
      "image": "/uploads/2026/featured-values.jpg",
      "link": "/values",
      "featured": true
    }
  ]
}
```

| Property | Type | Description |
|----------|------|-------------|
| `sectionTitle` | `string` | Section heading (Arabic) |
| `sectionTitleEn` | `string` | Section heading (English) |
| `sectionSubtitle` | `string?` | Section subtitle / eyebrow (Arabic) |
| `sectionSubtitleEn` | `string?` | Section subtitle (English) |
| `sectionSummary` | `string?` | Optional section intro (Arabic) |
| `sectionSummaryEn` | `string?` | Optional section intro (English) |
| `items` | `array` | Card rows (may be empty `[]`) |
| `items[].title` | `string` | Card title (Arabic) |
| `items[].titleEn` | `string` | Card title (English) |
| `items[].subtitle` | `string?` | Card subtitle (Arabic) — often used on featured hero card |
| `items[].subtitleEn` | `string?` | Card subtitle (English) |
| `items[].description` | `string?` | Card body text (Arabic) |
| `items[].descriptionEn` | `string?` | Card body text (English) |
| `items[].mediaType` | `"icon"` \| `"image"` | Which media field is active; CMS stores only one |
| `items[].icon` | `string?` | Font Awesome class when `mediaType=icon`, e.g. `fas fa-heart` (legacy `bi-*` still supported) |
| `items[].image` | `string?` | Relative upload path when `mediaType=image` |
| `items[].link` | `string?` | Internal path or external URL |
| `items[].featured` | `boolean` | **At most one** `true` per CardList — render as hero/large card in frontend |

**Notes for integrators:**

- Use **`fields[fieldName]`** for rendering (same object appears in `fieldsDisplay` for `CardList`; no label resolution like `Select`).
- Empty items (all properties blank) are **not saved** by the CMS; `items` may be `[]` if the editor never added cards.
- A content type may define **multiple** `CardList` fields; each appears under its own key in `fields`.
- Each item uses **`mediaType`**: `"icon"` **or** `"image"` — never both in stored JSON.
- Exactly **one** item may have `"featured": true` per CardList; use it for the large hero card in your layout.
- Resolve `image` paths with [§12](#12-featured-images--media-urls); render `icon` with Bootstrap Icons.
- Field names must match `^[A-Za-z][A-Za-z0-9_]*$` (English letter first).

#### Example API response

```bash
curl -H "Authorization: Pearl YOUR_KEY" \
  "https://talapress.online/api/v1/content/123?contentTypeId=3"
```

```json
{
  "id": 123,
  "contentTypeId": 3,
  "title": "الكفالة",
  "fields": {
    "xtest": {
      "sectionTitle": "أنواع الكفالات",
      "sectionTitleEn": "Sponsorship Types",
      "sectionSummary": "",
      "sectionSummaryEn": "",
      "items": [
        {
          "title": "كفالة يتيم",
          "titleEn": "Orphan",
          "subtitle": "",
          "subtitleEn": "",
          "description": "دعم شامل",
          "descriptionEn": "Full support",
          "mediaType": "icon",
          "icon": "bi-heart-fill",
          "image": "",
          "link": "/orphan",
          "featured": false
        }
      ]
    }
  },
  "fieldsDisplay": {
    "xtest": {
      "sectionTitle": "أنواع الكفالات",
      "sectionTitleEn": "Sponsorship Types",
      "sectionSummary": "",
      "sectionSummaryEn": "",
      "items": [ "... same as fields ..." ]
    }
  }
}
```

#### TypeScript helpers

```typescript
export interface CardListFieldValue {
  sectionTitle: string;
  sectionTitleEn: string;
  sectionSubtitle: string;
  sectionSubtitleEn: string;
  sectionSummary: string;
  sectionSummaryEn: string;
  items: CardListItemValue[];
}

export interface CardListItemValue {
  title: string;
  titleEn: string;
  subtitle: string;
  subtitleEn: string;
  description: string;
  descriptionEn: string;
  mediaType: "icon" | "image";
  icon: string;
  image: string;
  link: string;
  featured: boolean;
}

function getCardListField(
  item: ContentItem,
  fieldName: string
): CardListFieldValue | null {
  const raw = item.fields[fieldName];
  if (!raw || typeof raw !== "object" || !Array.isArray((raw as CardListFieldValue).items)) {
    return null;
  }
  return raw as CardListFieldValue;
}

function pickCardListText(
  ar: string | undefined,
  en: string | undefined,
  locale: "ar" | "en"
): string {
  return locale === "ar" ? (ar || en || "") : (en || ar || "");
}
```

#### React rendering sketch

```tsx
function CardListSection({
  data,
  locale,
  cmsOrigin,
}: {
  data: CardListFieldValue;
  locale: "ar" | "en";
  cmsOrigin: string;
}) {
  const title = pickCardListText(data.sectionTitle, data.sectionTitleEn, locale);
  const subtitle = pickCardListText(data.sectionSubtitle, data.sectionSubtitleEn, locale);
  const summary = pickCardListText(data.sectionSummary, data.sectionSummaryEn, locale);
  const featured = data.items.find((x) => x.featured);
  const regular = data.items.filter((x) => !x.featured);

  const renderCard = (item: CardListItemValue, hero = false) => {
    const cardTitle = pickCardListText(item.title, item.titleEn, locale);
    const subtitle = pickCardListText(item.subtitle, item.subtitleEn, locale);
    const desc = pickCardListText(item.description, item.descriptionEn, locale);
    const img =
      item.mediaType === "image" && item.image
        ? absoluteMediaUrl(item.image, cmsOrigin)
        : null;
    return (
      <a key={cardTitle} href={item.link || "#"} className={hero ? "card card--featured" : "card"}>
        {img ? (
          <img src={img} alt={cardTitle} />
        ) : item.mediaType === "icon" && item.icon ? (
          <i className={`bi ${item.icon}`} />
        ) : null}
        <h3>{cardTitle}</h3>
        {subtitle && <p className="subtitle">{subtitle}</p>}
        {desc && <p>{desc}</p>}
      </a>
    );
  };

  return (
    <section>
      {title && <h2>{title}</h2>}
      {subtitle && <p className="section-subtitle">{subtitle}</p>}
      {summary && <p>{summary}</p>}
      <div className="card-grid">
        {featured && renderCard(featured, true)}
        {regular.map((item) => renderCard(item))}
      </div>
    </section>
  );
}

// Usage after fetch:
const sponsorship = getCardListField(item, "xtest");
if (sponsorship) {
  return <CardListSection data={sponsorship} locale="ar" cmsOrigin="https://talapress.online" />;
}
```

#### Instructions for AI agents

1. Call `GET /api/v1/content` or `GET /api/v1/content/{id}` with Pearl auth.
2. Read `item.fields.<fieldName>` where `fieldName` is the builder key (camelCase-safe; e.g. `xtest`, `sponsorship_types`).
3. Expect an **object** with `items[]`, not a stringified JSON string.
4. Do **not** parse `CardList` through Select/MultiSelect label logic.
5. Resolve `image` paths with the CMS origin; render `icon` with Bootstrap Icons when `mediaType === "icon"`.
6. Split layout: item with `featured: true` → hero card; others → grid. Use `subtitle` on featured overlay.
7. If `items` is empty or the key is missing, omit the section in the UI.

### Audit fields (list endpoint only)

| Field | Type |
|-------|------|
| `createdBy` | `number` |
| `createdAt` | `string` (ISO UTC) |
| `updatedBy` | `number?` |
| `updatedAt` | `string?` (ISO UTC) |

---

## 11. Get Single Item — `GET /api/v1/content/{id}`

```bash
curl -H "Authorization: Pearl YOUR_KEY" \
  "https://talapress.online/api/v1/content/20074"
```

Returns a **single object** (not wrapped in `data[]`). Same field names as list items, with a slightly smaller shape (no `categoryId` at root — use nested `category`).

Response `404`:

```json
{ "message": "Content was not found." }
```

Use cases:

- Detail page by ID from CMS
- Preview after resolving slug via list filter: `?slug=my-page&top=1`

---

## 12. Featured Images & Media URLs

`featuredImage` and HTML body images use **relative paths**:

```
/uploads/2026/752f6e5b9eac410ab0e10d7bd91c7d64.jpg
```

Build absolute URLs in your frontend:

```typescript
const CMS_ORIGIN = "https://talapress.online";

function absoluteMediaUrl(path: string | null | undefined): string | null {
  if (!path) return null;
  if (path.startsWith("http://") || path.startsWith("https://")) return path;
  return `${CMS_ORIGIN}${path.startsWith("/") ? path : `/${path}`}`;
}

// usage
const imageUrl = absoluteMediaUrl(item.featuredImage);
// → https://talapress.online/uploads/2026/752f6e5b9eac410ab0e10d7bd91c7d64.jpg
```

For HTML `content` / `contentEn`, parse and rewrite `<img src="...">` if needed.

---

## 13. Frontend Examples

### JavaScript `fetch`

```javascript
const API_KEY = process.env.TALAPRESS_PEARL_KEY;
const BASE = "https://talapress.online/api/v1";

async function getLatestEvents(limit = 5) {
  const url = new URL(`${BASE}/content`);
  url.searchParams.set("contentTypeId", "2");
  url.searchParams.set("status", "Published");
  url.searchParams.set("top", String(limit));
  url.searchParams.set("orderBy", "publishDate");
  url.searchParams.set("orderDir", "desc");

  const response = await fetch(url, {
    headers: {
      Authorization: `Pearl ${API_KEY}`,
      Accept: "application/json",
    },
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`TalaPress API ${response.status}: ${text}`);
  }

  return response.json();
}

async function getAllEvents() {
  const url = new URL(`${BASE}/content`);
  url.searchParams.set("contentTypeId", "2");
  url.searchParams.set("status", "Published");
  url.searchParams.set("orderBy", "publishDate");
  url.searchParams.set("orderDir", "desc");
  // no top, no page → all matching rows

  const response = await fetch(url, {
    headers: { Authorization: `Pearl ${API_KEY}` },
  });
  return response.json();
}
```

### Axios

```javascript
import axios from "axios";

const client = axios.create({
  baseURL: "https://talapress.online/api/v1",
  headers: {
    Authorization: `Pearl ${process.env.TALAPRESS_PEARL_KEY}`,
  },
});

const { data } = await client.get("/content", {
  params: {
    contentTypeId: 2,
    status: "Published",
    top: 10,
    orderBy: "publishDate",
    orderDir: "desc",
  },
});

console.log(data.data);       // items
console.log(data.meta.pagination.total);
```

### Next.js Server Component (App Router)

```typescript
// lib/talapress.ts
const BASE = process.env.TALAPRESS_API_URL ?? "https://talapress.online/api/v1";
const KEY = process.env.TALAPRESS_PEARL_KEY!;

export async function listContent(params: Record<string, string | number | boolean | undefined>) {
  const qs = new URLSearchParams();
  Object.entries(params).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== "") qs.set(k, String(v));
  });

  const res = await fetch(`${BASE}/content?${qs}`, {
    headers: { Authorization: `Pearl ${KEY}` },
    next: { revalidate: 60 }, // ISR: refresh every 60s
  });

  if (!res.ok) throw new Error(`Content API failed: ${res.status}`);
  return res.json();
}

// app/events/page.tsx
export default async function EventsPage() {
  const { data: events } = await listContent({
    contentTypeId: 2,
    status: "Published",
    top: 100,
    orderBy: "publishDate",
    orderDir: "desc",
  });

  return (
    <ul>
      {events.map((e: { id: number; title: string; publishDate: string }) => (
        <li key={e.id}>{e.title} — {e.publishDate}</li>
      ))}
    </ul>
  );
}
```

### Postman

| Setting | Value |
|---------|-------|
| Method | `GET` |
| URL | `https://talapress.online/api/v1/content?contentTypeId=2&status=Published` |
| Header | `Authorization: Pearl tp_pearl_...` |

Disable **Follow redirects** when debugging auth issues.

---

## 14. TypeScript Types

```typescript
export interface ContentListResponse {
  data: ContentItem[];
  meta: {
    pagination: PaginationMeta;
    filters: ContentFilters;
  };
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  hasNext: boolean;
  hasPrevious: boolean;
}

export interface ContentFilters {
  q: string | null;
  contentTypeId: number | null;
  contentType: string | null;
  categoryId: number | null;
  subCategoryId: number | null;
  status: string;
  slug: string | null;
  authorId: number | null;
  publishDateFrom: string | null;
  publishDateTo: string | null;
  createdFrom: string | null;
  createdTo: string | null;
  hasFeaturedImage: boolean | null;
  sortBy: string;
  sortDir: string;
  orderBy: string | null;
  orderDir: string | null;
  top: number | null;
  fetchAll: boolean;
}

export interface ContentItem {
  id: number;
  contentTypeId: number;
  title: string | null;
  titleEn: string | null;
  slug: string | null;
  summary: string | null;
  summaryEn: string | null;
  content: string | null;
  contentEn: string | null;
  featuredImage: string | null;
  status: string;
  publishDate: string | null;
  categoryId: number | null;
  subCategoryId: number | null;
  hits: number;
  hitts: number;
  seo: SeoMeta;
  fields: Record<string, unknown>;
  fieldsDisplay: Record<string, unknown>;
  createdBy: number;
  createdAt: string;
  updatedBy: number | null;
  updatedAt: string | null;
  contentType: ContentTypeRef;
  category: CategoryRef | null;
  subCategory: CategoryRef | null;
  author: AuthorRef;
}

export interface SeoMeta {
  title: string | null;
  titleEn: string | null;
  description: string | null;
  descriptionEn: string | null;
  keywords: string | null;
  keywordsEn: string | null;
  canonical: string | null;
}

export interface ContentTypeRef {
  id: number;
  name: string;
  nameEn: string | null;
  icon: string | null;
}

export interface CategoryRef {
  id: number;
  name: string;
  nameEn: string | null;
  slug: string | null;
}

export interface AuthorRef {
  id: number;
  name: string;
  username: string | null;
}

export interface CardListFieldValue {
  sectionTitle: string;
  sectionTitleEn: string;
  sectionSubtitle: string;
  sectionSubtitleEn: string;
  sectionSummary: string;
  sectionSummaryEn: string;
  items: CardListItemValue[];
}

export interface CardListItemValue {
  title: string;
  titleEn: string;
  subtitle: string;
  subtitleEn: string;
  description: string;
  descriptionEn: string;
  mediaType: "icon" | "image";
  icon: string;
  image: string;
  link: string;
  featured: boolean;
}
```

---

## 15. Common Errors & Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| HTML login page in response | Called `/Content` instead of `/api/v1/content` | Use API path |
| `401 Unauthorized` | Missing header or `Bearer` instead of `Pearl` | `Authorization: Pearl YOUR_KEY` |
| `503` + disabled message | API turned off in Settings | Enable **ApiEnabled** in admin |
| Empty `data: []` | Default `status=Published` hides drafts | Add `status=All` or publish content |
| Empty `data: []` | Wrong `contentTypeId` | Call `GET /api/v1/content-types` first |
| DNS / connection error | Typo in domain | Use `talapress.online` |
| Blocked from corporate network | Firewall/proxy (e.g. gov entity) | Request URL whitelist exception |

---

## 16. Real-World Recipes

### Latest 5 published events (type 2)

```
GET /api/v1/content?contentTypeId=2&status=Published&top=5&orderBy=publishDate&orderDir=desc
```

### All published events (type 2)

```
GET /api/v1/content?contentTypeId=2&status=Published&orderBy=publishDate&orderDir=desc
```

### Single page by slug

```
GET /api/v1/content?slug=home&contentTypeId=3&top=1
```

### Latest news in a category (paginated)

```
GET /api/v1/content?contentType=News&categoryId=2&page=1&pageSize=10&orderBy=publishDate&orderDir=desc
```

### Search + type filter

```
GET /api/v1/content?q=قطر&contentTypeId=1&status=Published
```

### News with featured image only

```
GET /api/v1/content?contentType=News&hasFeaturedImage=true
```

### Custom field: events starting in June 2026

```
GET /api/v1/content?contentTypeId=2&field.StartDate=2026-06
```

### Admin preview (include drafts)

```
GET /api/v1/content?contentTypeId=2&status=All
```

---

## Quick Reference — All Query Parameters

```
GET /api/v1/content
  ?page=1
  &pageSize=20
  &q=
  &search=
  &contentTypeId=
  &contentType=
  &categoryId=
  &subCategoryId=
  &status=Published
  &slug=
  &authorId=
  &publishDateFrom=
  &publishDateTo=
  &createdFrom=
  &createdTo=
  &hasFeaturedImage=
  &sortBy=publishDate
  &sortDir=desc
  &orderBy=publishDate
  &orderDir=desc
  &top=
  &field.{CustomFieldName}=
  &fields.{CustomFieldName}=
```

**Header:** `Authorization: Pearl YOUR_KEY`

---

*Source: `Api/Controllers/ContentController.cs` — TalaPress Pearl Headless API v1*
