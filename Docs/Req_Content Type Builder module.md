You are designing the Content Type Builder module for a modern Headless CMS called "TalaPress".

TalaPress is an API-first, multilingual CMS built using ASP.NET Core and SQL Server 2019. The goal is to provide a user-friendly experience similar to WordPress while maintaining the flexibility of modern Headless CMS platforms.

Your task is to design the database structure, backend logic, and user experience for the following entities:

* ContentTypes
* ContentTypeFields

## Core Philosophy

TalaPress uses a hybrid approach:

1. All content items share a set of predefined system fields stored in the main Content table.

2. Users can extend content types by creating custom fields.

3. The system must be simple enough for non-technical users.

## System Fields (Core Fields)

The following fields already exist in the Content table and MUST NOT be recreated by users:

* Title
* Slug
* Summary
* Content
* FeaturedImage
* Status
* PublishDate
* CategoryId
* SubCategoryId

## SEO Fields (also predefined)

The following fields also exist in the Content table and MUST NOT be recreated:

* SeoTitle
* SeoDescription
* SeoKeywords
* CanonicalUrl

## User Experience Requirements

When creating or editing a Content Type:

1. Display the predefined system fields at the top of the page.

2. System fields must appear visually different:

   * Greyed out.
   * Read-only.
   * Cannot be deleted.
   * Cannot be duplicated.
   * Cannot have their data type changed.

3. Users must understand that these fields already exist and will automatically appear in the content entry screen.

4. Users should only create additional fields specific to their business requirements.

## Custom Fields

Users must be able to add custom fields using a visual builder.

Each custom field should support:

* Field Name
* Field Name (English)
* Label
* Label (English)
* Field Type
* Placeholder
* Placeholder (English)
* Help Text
* Help Text (English)
* Is Required
* Is Translatable
* Default Value
* Options JSON (for dropdowns, radios, etc.)
* Sort Order
* Is Active

## Supported Field Types

The initial implementation should support:

* Text
* Textarea
* RichText
* Number
* Decimal
* Date
* DateTime
* Boolean
* Select
* MultiSelect
* Image
* Gallery
* File
* Url
* Email
* Phone

## User Interface Requirements

The field builder should use drag-and-drop ordering.

The final display order must include both:

* System fields.
* Custom fields.

Example:

## System Fields (Read Only)

Title
Summary
Content
Featured Image

## Custom Fields

Source
News Date
Gallery

## SEO Fields (Read Only)

SEO Title
SEO Description
SEO Keywords
Canonical URL

## Final Content Entry Form

When a user creates a content item using this Content Type, the form should automatically be generated in the configured order.

Example:

Title
Summary
Content
Featured Image
Source
News Date
Gallery
SEO Title
SEO Description

## Default Content Types

Seed the following system content types:

* News
* Event
* Page
* Gallery
* Team
* Case Study
* Testimonial
* Service
* Project

## System Content Types

System content types:

* Cannot be deleted.
* Can be extended with additional fields.
* Can be cloned.

## Custom Content Types

Administrators should be able to create new content types such as:

* Product
* Course
* Vehicle
* Real Estate
* Job Vacancy

without writing any code.

## Design Goals

The solution must prioritize:

* Simplicity.
* Excellent user experience.
* Flexibility.
* Maintainability.
* Minimal database complexity.

The system should feel familiar to WordPress users while providing the flexibility expected from a modern Headless CMS.

Provide recommendations for:

* Database design.
* Backend implementation.
* API behavior.
* Validation rules.
* UX/UI interactions.
* Performance considerations.

The result should represent a production-ready Content Type Builder suitable for TalaPress.
