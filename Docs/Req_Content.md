You are implementing the **Content Management Module** for **TalaPress**, a multilingual Headless CMS built using:

* ASP.NET Core Razor Pages
* SQL Server 2019
* Bootstrap 5
* jQuery
* DataTables
* SweetAlert2

The philosophy of TalaPress is:

> **WordPress Simplicity + Headless CMS Flexibility**

The system already contains and MUST be respected:

* Categories
* ContentTypes
* ContentTypeFields
* Users / Roles / Permissions
* Settings

The Content Type Builder module has already been implemented successfully.

---

# CRITICAL ARCHITECTURAL RULE

The Content Type Builder is the **single source of truth** for generating Content Entry screens.

The Content module MUST NEVER reconstruct forms by independently separating:

* Core Fields,
* Custom Fields,
* SEO Fields.

The administrator has already configured:

* Field ordering,
* Field visibility,
* Custom fields.

Therefore, Content screens MUST render exactly what administrators configured inside the Content Type Builder.

---

# CONTENT DATABASE DESIGN

Generate SQL Server 2019 scripts for the following table:

```text
Content
```

Columns:

```text
Id BIGINT IDENTITY PRIMARY KEY

ContentTypeId BIGINT NOT NULL

Title NVARCHAR(500) NULL
Title_En NVARCHAR(500) NULL

Slug NVARCHAR(500) NULL

Summary NVARCHAR(MAX) NULL
Summary_En NVARCHAR(MAX) NULL

Content NVARCHAR(MAX) NULL
Content_En NVARCHAR(MAX) NULL

FeaturedImage NVARCHAR(500) NULL

Status NVARCHAR(50) NOT NULL

PublishDate DATETIME2 NULL

CategoryId BIGINT NULL
SubCategoryId BIGINT NULL

SeoTitle NVARCHAR(500) NULL
SeoTitle_En NVARCHAR(500) NULL

SeoDescription NVARCHAR(1000) NULL
SeoDescription_En NVARCHAR(1000) NULL

SeoKeywords NVARCHAR(1000) NULL
SeoKeywords_En NVARCHAR(1000) NULL

CanonicalUrl NVARCHAR(500) NULL

CustomFieldsJson NVARCHAR(MAX) NULL

CreatedBy BIGINT NOT NULL
CreatedAt DATETIME2 NOT NULL

UpdatedBy BIGINT NULL
UpdatedAt DATETIME2 NULL

IsDeleted BIT NOT NULL DEFAULT 0
DeletedAt DATETIME2 NULL
DeletedBy BIGINT NULL
```

Requirements:

* Use foreign keys where appropriate.
* Use soft delete.
* Categories are shared system fields and MUST exist in Content.
* ContentTypeId must reference ContentTypes.

---

# DYNAMIC CONTENT ENTRY FORM

When creating or editing content:

The system MUST:

1. Load the selected Content Type.
2. Load its configured field definitions.
3. Respect administrator-defined visibility.
4. Respect administrator-defined ordering.
5. Generate the final form dynamically.

The Content module MUST NOT redefine layouts independently.

---

# ADD CONTENT EXPERIENCE

When the administrator clicks:

```text
Add Content
```

DO NOT open an empty form immediately.

Instead:

Display a modal window containing cards for all active Content Types.

Cards must be loaded from the database.

Each card displays:

```text
Icon
Arabic Name
English Name
Description
```

Examples:

```text
News
Page
Gallery
Event
Team
Service
Project
```

---

# CONTENT TYPE SELECTION BEHAVIOR

After selecting a Content Type:

* Open the Content Entry screen.
* Set the selected type as the default.

Inside the form:

Display a Content Type dropdown.

If the administrator changes the Content Type:

* Reload the form dynamically.
* Rebuild it according to the selected type configuration.
* Use the latest selected type as the default during the user's session.

---

# CONTENT ENTRY SCREEN

The screen should resemble WordPress.

Use a responsive two-column layout.

However:

DO NOT independently regroup fields.

Instead:

Respect the administrator-defined sequence.

Example:

If administrators configured:

```text
Title
FeaturedImage
Summary
Gallery
Content
SeoTitle
SeoDescription
```

The Content Entry screen MUST render them exactly in that order.

---

# FIELD RENDERING RULES

System fields:

```text
Title
Slug
Summary
Content
FeaturedImage
Status
PublishDate
CategoryId
SubCategoryId

SeoTitle
SeoDescription
SeoKeywords
CanonicalUrl
```

Rules:

```text
Cannot be deleted.
Cannot change type.
Cannot change FieldName.
Can be hidden.
Can be reordered.
```

Custom fields:

```text
Can be created.
Can be edited.
Can be deleted.
Can be reordered.
Can be duplicated.
```

---

# SAVING CONTENT

Save shared fields into:

```text
Content table columns
```

Save dynamic fields into:

```text
Content.CustomFieldsJson
```

JSON property names MUST match:

```text
FieldName
```

Example:

```json
{
  "Source": "Reuters",
  "Gallery": [
    "/uploads/1.jpg",
    "/uploads/2.jpg"
  ],
  "Location": "Doha",
  "EventDate": "2026-07-15"
}
```

Requirements:

* Save only active custom fields.
* Validate required fields.
* Ignore hidden fields.
* Preserve field ordering metadata.

---

# SUCCESS EXPERIENCE

After successful save:

Display SweetAlert2.

Arabic:

```text
تم حفظ المحتوى بنجاح.
```

English:

```text
Content has been saved successfully.
```

After confirmation:

```text
Clear the form.

Prepare for adding new content.

Retain the latest selected Content Type.
```

The administrator should be able to continue publishing efficiently.

---

# EDIT EXPERIENCE

When editing content:

The system MUST:

```text
Load Content.

Load configured field definitions.

Deserialize CustomFieldsJson.

Populate all dynamic fields.

Respect visibility settings.

Respect field ordering.

Load media values.
```

The administrator should never realize that dynamic fields are stored as JSON.

---

# CONTENT LIST PAGE

Create a unified Content Management page.

Display all content regardless of Content Type.

Use Bootstrap 5 and DataTables.

Columns:

```text
Featured Image
Title
Content Type
Category
SubCategory
Status
Publish Date
Author
Created Date
Actions
```

Actions:

```text
Edit
Duplicate
Preview
Delete
```

---

# DATATABLES REQUIREMENTS

Implement Server-Side Processing.

Support:

```text
Pagination

Sorting

Search

Responsive Layout

Page Size Selection

Information Summary
```

Filtering MUST NEVER rely on client-side records.

All filtering MUST execute SQL queries.

---

# ADVANCED FILTERS

Provide an advanced filter panel.

Supported filters:

```text
Content Type

Category

SubCategory

Status

Author

Publish Date From

Publish Date To

Free Text Search
```

Requirements:

```text
AJAX filtering.

Dynamic SQL generation.

Only selected filters should apply.

No page reloads.
```

---

# DUPLICATE CONTENT

Provide Duplicate functionality.

Requirements:

Copy:

```text
Shared fields.

CustomFieldsJson.

Featured image references.

SEO values.
```

The duplicated record MUST default to:

```text
Draft
```

---

# DELETE EXPERIENCE

Implement Soft Delete.

Requirements:

```text
IsDeleted = 1

DeletedAt = GETDATE()

DeletedBy = CurrentUser
```

Deleted records MUST NOT appear in standard listings.

---

# PREVIEW EXPERIENCE

Provide Preview functionality.

Preview MUST render content exactly as the Frontend API would expose it.

Include:

```text
Shared fields.

Dynamic fields.

SEO fields.

Media values.
```

Preview MUST work without publishing.

---

# HEADLESS API READINESS

The architecture must support future Headless APIs.

The expected output format is:

```json
{
    "id": 15,

    "contentType": "News",

    "title": "System Launch",

    "summary": "...",

    "category": "News",

    "publishDate": "2026-06-15",

    "fields": {
        "Source": "Reuters",
        "Gallery": [
            "/uploads/1.jpg",
            "/uploads/2.jpg"
        ]
    }
}
```

---

# USER EXPERIENCE PHILOSOPHY

Administrators should feel that they are:

```text
Writing articles.

Publishing pages.

Managing websites.

Maintaining content.
```

NOT:

```text
Managing SQL structures.

Editing JSON.

Designing databases.
```

---

# TALAPRESS DESIGN GOAL

The final experience should feel like:

```text
WordPress

+

Modern Headless CMS

+

Bootstrap 5

+

Enterprise-ready simplicity
```

Provide:

```text
SQL Server scripts.

Razor Pages implementation.

DataTables server-side strategy.

Validation recommendations.

SweetAlert integration points.

Performance considerations.

UX recommendations.
```

The implementation must remain:

```text
Simple.

Fast.

Scalable.

Maintainable.

Consistent with TalaPress philosophy.
```
