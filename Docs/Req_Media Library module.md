You are designing the Media Library module for a modern Headless CMS called "TalaPress".

TalaPress is an API-first, multilingual CMS built using ASP.NET Core and SQL Server 2019. The goal is to provide a user-friendly experience similar to WordPress while maintaining the flexibility of modern Headless CMS platforms.

Your task is to document and design the requirements for the Media Library, which allows administrators to manage the `wwwroot/uploads` directory.

## Core Philosophy

1. Provide a fast, interactive, and highly secure WordPress-like media manager.
2. Ensure strict security measures are in place to prevent Directory Traversal and malicious file execution.
3. Deliver a premium user experience with features like Drag & Drop, quick preview, and easy link copying.

## Supported Operations

The Media Library must support the following core operations:
1. **Browse:** List subfolders and files in the current directory, showing file size, extension, and upload date.
2. **Search:** Recursive search functionality across the media library.
3. **Upload:** Upload new files into the active folder.
4. **Create Folder:** Create new subdirectories.
5. **Delete:** Permanently delete files and empty folders.
6. **File Details:** View file properties and copy the direct URL.

## Security Requirements

Security is paramount for this module. The following rules must be strictly enforced on the backend:

1. **Directory Traversal Prevention:** 
   - The root path must be hardcoded to `wwwroot/uploads`.
   - All requested paths must be validated using `Path.GetFullPath`.
   - The system must reject any path that does not start with the root `uploads` path, and reject any requests containing `..`.
2. **File Extension Restriction:**
   - Executable and server-side script files must be strictly blocked (e.g., `.exe`, `.dll`, `.php`, `.asp`, `.aspx`, `.cshtml`, `.js`, `.css`).
   - Extensions must be checked in lowercase against an `AllowedFileExtensions` list.
3. **File Size Limits:**
   - Enforce a maximum upload size based on the system configuration (`MaxUploadSizeMB` in the `Settings` table, defaulting to 20MB).
4. **Sanitization:**
   - Uploaded file names and new folder names must be sanitized to remove special characters and prevent injection attacks.

## User Interface Requirements

The Frontend UI should be modern, interactive, and utilize AJAX for seamless operations:

1. **Toolbar:**
   - Interactive Breadcrumbs for navigation.
   - Action buttons: "New Folder", "Upload File".
   - Quick Search bar.
2. **Drag & Drop Zone:**
   - The main area should accept drag-and-drop file uploads natively.
3. **Grid Layout:**
   - Folders should be displayed first with a distinct folder icon colored with the theme's accent color.
   - Images should be displayed as thumbnails for quick visual scanning.
   - Other files (PDF, Word, Zip) should have representative icons.
   - Hovering over an item should reveal quick actions (View, Delete, Copy Link).
4. **Details Drawer (WordPress-style):**
   - Clicking a file should slide out a side panel or open a well-designed modal.
   - The panel must show:
     - Enlarged preview/thumbnail.
     - File metadata (Name, Size, Extension, Upload Date, Image Dimensions).
     - A read-only text field with the direct URL.
     - A "Copy Link" button that copies the URL to the clipboard.
     - A red "Permanently Delete" button with a confirmation prompt.

## Permissions

Operations must be protected by the following claims:
- `Media.View`: Required to access the page and browse files.
- `Media.Upload`: Required to upload files and create folders.
- `Media.Delete`: Required to delete files and folders.

## Localization

All UI text elements must be translatable via the existing `data-tp-i18n` attribute system, supporting both Arabic (`ar`) and English (`en`) keys.
