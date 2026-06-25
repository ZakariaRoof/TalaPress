Review the existing implementation of the TalaPress Content Type Builder and improve it according to the following recommendations.

The current implementation is already functional and includes:

* Content Types management.
* System content types.
* Custom fields builder.
* SEO fields section.
* Drag-and-drop ordering.
* Clone functionality.
* Claims-based permissions.

Your task is NOT to rebuild the module from scratch.

Instead, refine the user experience and architecture according to the following requirements.

=============================

1. SINGLE SORTABLE EXPERIENCE
   =============================

Currently, the screen separates:

1. Core System Fields.
2. Custom Fields.
3. SEO Fields.

Improve this experience.

Requirements:

* Keep Core Fields and SEO Fields visually read-only.
* Keep them protected from modification.
* However, allow administrators to control their visibility and display order.

The final ordering experience should behave like a single form builder.

Example:

Title 🔒
Featured Image 🔒
Source
News Date
Gallery
SEO Title 🔒
SEO Description 🔒

Locked fields:

* Cannot be edited.
* Cannot be deleted.
* Cannot change field type.

But:

* Can be reordered.
* Can be hidden from the final entry screen.

===================================
2. SYSTEM FIELD CONFIGURATION
=============================

Introduce support for configuring system fields.

For each predefined field, allow:

* IsVisible
* SortOrder

Examples:

News:

* Title = visible
* Summary = visible
* FeaturedImage = visible

Testimonial:

* Summary = hidden
* Content = hidden

Partner:

* Summary = hidden
* Content = hidden

These configurations must be stored per Content Type.

===================================
3. PREVIEW ENTRY FORM
=====================

Add a new button:

"Preview Entry Form"

When clicked:

* Render the final content entry form exactly as editors will see it.
* Include:

  * Visible Core Fields.
  * Custom Fields.
  * Visible SEO Fields.
* Respect configured ordering.

This preview must require no database save.

===================================
4. RESERVED FIELD VALIDATION
============================

Prevent administrators from creating fields using reserved names.

Reserved names include:

Id
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

Validation must exist:

* Client-side.
* Server-side.

===================================
5. IMPROVE FIELD BUILDER UX
===========================

The current field creation modal should be simplified.

Organize field types into categories.

Content Fields:

* Text
* Textarea
* RichText

Selection Fields:

* Select
* MultiSelect

Media Fields:

* Image
* Gallery
* File

Date Fields:

* Date
* DateTime

Other Fields:

* Number
* Decimal
* Boolean
* Url
* Email
* Phone

Improve discoverability and reduce cognitive load.

===================================
6. IMPROVE CUSTOM FIELD CARDS
=============================

Each custom field card should display:

* Arabic Label.
* English Label.
* Field Type Badge.
* Required Badge.
* Translatable Badge.

Provide quick actions:

* Edit.
* Duplicate.
* Delete.

===================================
7. DUPLICATE FIELD FEATURE
==========================

Allow administrators to duplicate custom fields.

Example:

Gallery

↓

Gallery Copy

Administrators can then rename and adjust it.

System fields cannot be duplicated.

===================================
8. CLONE CONTENT TYPE IMPROVEMENTS
==================================

The existing clone functionality should:

* Clone all custom fields.
* Clone system field visibility settings.
* Clone system field ordering.
* Clone SEO visibility settings.

The new content type name should default to:

"{Original Name} Copy"

===================================
9. SYSTEM CONTENT TYPE RULES
============================

System Content Types:

* Cannot be deleted.
* Can be extended.
* Can be cloned.
* Can have field visibility customized.
* Can have field ordering customized.

===================================
10. OVERALL DESIGN GOAL
=======================

The Content Type Builder should feel:

* Simple like WordPress.
* Flexible like Strapi.
* Easy for non-technical users.
* Suitable for enterprise environments.

Avoid exposing technical database concepts.

Administrators should feel that they are designing:

"The Content Entry Experience"

rather than:

"Database Structures".

Provide implementation recommendations only for improving the existing module without breaking current functionality.
