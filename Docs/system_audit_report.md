# تقرير فحص النظام — TalaPress

**تاريخ الفحص:** 23 يونيو 2026  
**النطاق:** البناء، الأمان، Pearl API، صفحات الإدارة، التوثيق، الجاهزية للإنتاج  
**المرجع التفاعلي للـ API:** `/api/docs` (محمي — يتطلب تسجيل دخول)

---

## 1. الملخص التنفيذي

| الجانب | الحالة |
|--------|--------|
| Build (`dotnet build`) | ✅ ناجح — بدون أخطاء |
| Pearl API (`/api/v1/*`) | ✅ يعمل — مصادقة Pearl مطلوبة |
| توثيق API (`/api/docs/*`) | ✅ يعمل — محمي للمستخدمين المسجّلين |
| تعارض المسارات | ✅ لا يوجد تعارض بين `/api/docs` و `/api/v1` |
| الجاهزية للإنتاج | ⚠️ **غير جاهز** — ثغرات حرجة يجب إصلاحها أولاً |

**الخلاصة:** TalaPress مناسب للتطوير والاختبار الداخلي. قبل النشر العام يُوصى بإغلاق الثغرات الحرجة (خصوصاً تسريب المحتوى، رفع الملفات، وأسرار الإعدادات).

---

## 2. حالة النظام (Smoke Test)

| الاختبار | النتيجة المتوقعة | ملاحظة |
|----------|------------------|--------|
| `GET /api/v1/content` بدون مفتاح | 401 | ✅ |
| `GET /api/docs` بدون تسجيل | 302 → Login | ✅ |
| `GET /ContentPreview?id=1&handler=JSON` بدون auth | 200 | ❌ **ثغرة** |
| Bootstrap CSS على `/api/docs/*` | 200 | ✅ (بعد إصلاح المسارات المطلقة) |

---

## 3. مشاكل حرجة (Critical)

### 3.1 تسريب محتوى JSON بدون مصادقة

**المسار:** `GET /ContentPreview?id={id}&handler=JSON`  
**الملف:** `Pages/ContentPreview.cshtml.cs` — `OnGetJSONAsync`

- `OnGetAsync` (صفحة HTML) تفحص تسجيل الدخول.
- `OnGetJSONAsync` **لا يفحص** auth ولا صلاحيات.
- يُرجع المحتوى الكامل بأي حالة (`Draft`, `Pending`, …).

**التوصية:**
- إما حذف هذا الـ handler واستخدام `/api/v1/content/{id}` فقط.
- أو حمايته بـ Pearl Key أو Cookie + `Content.View`.
- تقييد الحالات المنشورة فقط للاستخدام العام.

---

### 3.2 رفع ملفات بدون صلاحيات

**المسار:** `POST /ContentEdit?handler=UploadFile`  
**الملف:** `Pages/ContentEdit.cshtml.cs` — `OnPostUploadFileAsync`

- لا فحص `IsAuthenticated` ولا `Content.Edit` / `Content.Create`.
- لا allowlist للامتدادات ولا حد للحجم.
- يُحفظ في `wwwroot/uploads/{year}/` — خطر رفع `.html` / `.js`.

**التوصية:** نفس ضوابط `Pages/Media.cshtml.cs` (صلاحيات + امتدادات + حجم).

---

### 3.3 أسرار قاعدة البيانات في المستودع

**الملف:** `appsettings.json`

- كلمة مرور `sa` بصيغة plaintext.
- لا يوجد `.gitignore` في جذر المشروع لاستبعاد الأسرار وملفات البناء.

**التوصية:**
- User Secrets (تطوير) + متغيرات بيئة (إنتاج).
- إضافة `.gitignore`.
- تغيير كلمة مرور SQL فوراً إذا رُفع المستودع لـ Git.

---

### 3.4 `ApiEnabled` غير مُطبَّق

**الملفات:** `Pages/Settings.cshtml.cs`, `Api/Controllers/SettingsController.cs`

- الإعداد يُحفظ ويُعرض في API (`apiEnabled`).
- **لا middleware ولا filter** يمنع Pearl API عند التعطيل.

**التوصية:** Middleware قبل `MapControllers` يقرأ `ApiEnabled` ويُرجع 503.

---

### 3.5 مستخدم افتراضي `admin` / `admin`

**الملف:** `Pages/Login.cshtml.cs` — `EnsureDefaultUserAsync`

- عند قاعدة بيانات فارغة يُنشأ Super Admin تلقائياً.
- مناسب للتطوير؛ **خطر في الإنتاج**.

**التوصية:** تعطيل في Production أو فرض تغيير كلمة المرور عند أول دخول.

---

## 4. مشاكل متوسطة (Medium)

### 4.1 Pearl Authentication

**الملف:** `Api/Security/PearlAuthenticationHandler.cs`

| الموضوع | الوضع |
|---------|--------|
| `AllowedOrigins` على المفاتيح | محفوظ — **غير مُفحص** |
| تجزئة المفتاح | SHA-256 بدون salt — يُفضّل slow hash |
| Rate limiting | غير موجود |
| Scopes للمفاتيح | غير موجود — أي مفتاح صالح يصل لكل endpoints |

---

### 4.2 `POST /api/forms/submit`

**الملف:** `Controllers/FormController.cs`

- بدون Pearl auth.
- `[IgnoreAntiforgeryToken]`.
- بدون CAPTCHA أو rate limit.
- `SendEmailNotification` غير مُنفَّذ (تعليق placeholder فقط).

**التوصية:** rate limit + honeypot/CAPTCHA + تنفيذ الإيميل أو إزالة الخيار من UI.

---

### 4.3 عدم اتساق استجابات API

| Endpoint | شكل الاستجابة |
|----------|----------------|
| `GET /content` | `{ data, meta }` |
| `GET /content-types` | `{ items }` |
| `GET /categories` (scoped) | `{ contentType, scope, items, meta }` |
| `GET /settings` | كائن flat |
| `GET /menus/{code}` | `{ items, meta }` (شجرة) |

**التوصية:** توحيد envelope (مثلاً `{ data, meta }` دائماً) في إصدار API لاحق.

---

### 4.4 فجوات صلاحيات في صفحات الإدارة

| الموقع | المشكلة |
|--------|---------|
| `Pages/Forms.cshtml.cs` — `OnGetPreview` | بدون auth — ينفّذ SQL من إعدادات النموذج |
| `Pages/Menus.cshtml.cs` — `OnGetSearchContentAsync` | auth فقط — بدون `Menu.View` |
| `Pages/ApiDocs/ApiDocsPageModel.cs` | أي `Permission` claim — واسع جداً |
| `Pages/ApiKeys.cshtml.cs` | `Permissions.View` يكفي لإدارة المفاتيح |

**التوصية:** `[Authorize]` على مجلد Pages + سياسات صلاحيات؛ مراجعة handlers يدوياً.

---

### 4.5 SQL ديناميكي في النماذج

**الملف:** `ViewComponents/DynamicFormViewComponent.cs`

- تنفيذ `sqlQuery` من `FormFields.OptionsJson`.
- blocklist جزئي — خطر مع حساب admin مخترق.

**التوصية:** parameterized queries + whitelist لـ `logic` في Query Builder.

---

### 4.6 CORS

**الملف:** `Program.cs`

- لا `AddCors` / `UseCors`.
- Frontend من domain آخر يحتاج BFF أو Proxy — المفتاح لا يُوضَع في المتصفح.

---

### 4.7 توثيق قديم / غير متزامن

| المصدر | الحالة |
|--------|--------|
| `/api/docs` | ✅ محدّث (Pearl API Reference) |
| `Docs/pearl_api.md` | ⚠️ ترميز معطوب + ناقص (Menus, Settings, categories scoped…) |
| ApiDocs — تفاصيل | `sortBy=hits` موثّق لكن غير مدعوم في `ContentController`؛ Menus يذكر `label` والـ API يُرجع `title` |

**التوصية:** اعتماد `/api/docs` كمصدر رسمي؛ تحديث أو إيقاف `pearl_api.md`.

---

### 4.8 DDL وقت التشغيل في API

**الملف:** `Api/Helpers/CategoryApiHelper.cs` — `EnsureContentTypeIdColumnAsync`

- قد ينفّذ `ALTER TABLE` عند أول طلب categories.
- يُفضّل migrations/`DatabaseInitializer` فقط.

---

## 5. ملاحظات منخفضة الأولوية (Low)

- حقل `hitts` (typo) بجانب `hits` في استجابات المحتوى — للتوافق مع JS قديم.
- `fieldsDisplay` موجود في الكود — غير موثّق في `pearl_api.md`.
- لا OpenAPI/Swagger.
- لا API عام للوسائط أو Workflow (قد يكون مقصوداً).
- Logout عبر GET — عرضة لـ CSRF logout.
- جلسات Login دائماً `IsPersistent = true`.

---

## 6. نقاط إيجابية

- جميع Pearl controllers محمية بـ `[Authorize(Pearl)]`.
- مكتبة الوسائط: path traversal + allowlist امتدادات + حد حجم.
- `SanitizeFields` يزيل حقولاً حساسة من API.
- معظم صفحات الإدارة تفحص `Permission` في handlers.
- HSTS مفعّل خارج Development.
- مسارات التوثيق `/api/docs` منفصلة عن `/api/v1` — لا كسر للـ API.

---

## 7. مصفوفة Endpoints (كود vs توثيق)

| Endpoint | الكود | `/api/docs` | `pearl_api.md` |
|----------|-------|-------------|----------------|
| `GET /api/v1/content` | ✅ | ✅ | ✅ |
| `GET /api/v1/content/{id}` | ✅ | ✅ | ✅ |
| `GET /api/v1/content-types` | ✅ | ✅ | ✅ |
| `GET /api/v1/content-types/{id}/categories` | ✅ | ✅ | ❌ |
| `GET /api/v1/categories` | ✅ | ✅ | ✅ |
| `GET /api/v1/menus` | ✅ | ✅ | ❌ |
| `GET /api/v1/menus/{code}` | ✅ | ✅ | ❌ |
| `GET /api/v1/settings` | ✅ | ✅ | ❌ |
| `POST /api/forms/submit` | ✅ | ❌ | ❌ |
| `GET /ContentPreview?handler=JSON` | ✅ (shadow) | ❌ | ❌ |

---

## 8. خطة العمل المقترحة

### المرحلة 1 — فوراً (قبل Production)

1. حماية أو إزالة `ContentPreview?handler=JSON`.
2. حماية `ContentEdit` upload (auth + allowlist + size).
3. نقل الأسرار خارج `appsettings.json` + `.gitignore`.
4. تطبيق `ApiEnabled` في middleware.
5. تعطيل/تغيير seed `admin/admin` في Production.

### المرحلة 2 — قصير المدى

6. Pearl middleware: `AllowedOrigins` + rate limiting.
7. `[Authorize]` عام على Razor Pages.
8. حماية `/api/forms/submit` (rate limit + CAPTCHA).
9. توحيد شكل استجابات API.
10. مزامنة ApiDocs مع الكود (`hits`, `title` في Menus).

### المرحلة 3 — متوسط المدى

11. OpenAPI/Swagger (داخلي محمي).
12. Scopes لمفاتيح Pearl.
13. Slow hash للمفاتيح.
14. `GET /api/v1/forms/{code}` + إرسال إيميل النماذج.
15. CI + فحص أمني دوري.

---

## 9. ملفات مرجعية

| الموضوع | المسار |
|---------|--------|
| Pearl Auth | `Api/Security/PearlAuthenticationHandler.cs` |
| Pearl API | `Api/Controllers/*.cs` |
| توثيق تفاعلي | `Pages/ApiDocs/*`, `Pages/Shared/_ApiDocsLayout.cshtml` |
| إعدادات API | `Pages/Settings.cshtml.cs` |
| مفاتيح API | `Pages/ApiKeys.cshtml.cs` |
| Auth & RBAC | `Docs/auth_system.md` |
| Pearl API (قديم) | `Docs/pearl_api.md` |

---

*آخر تحديث: 23 يونيو 2026 — يُحدَّث هذا التقرير بعد كل جولة إصلاح أمني.*
