# نشر TalaPress على QC Beta

هذا الإجراء مخصص لنشر TalaPress على:

- الرابط: `https://beta.qcharity.net/cms`
- المسار الفيزيائي: `C:\Website\Talapress`
- Checkout في موقع قطر الخيرية: `https://beta.qcharity.org`
- البيئة: `Production`
- إطار التشغيل: .NET 8 ASP.NET Core Hosting Bundle، In-Process

## إعداد IIS

داخل موقع `beta.qcharity.net` يجب أن يكون المجلد `/cms` معرفًا كـIIS Application وليس Virtual Directory فقط:

1. Alias: `cms`.
2. Physical path: `C:\Website\Talapress`.
3. Application Pool مستقل أو معروف.
4. Application Pool مضبوط على `No Managed Code`.
5. شهادة HTTPS الخاصة بـ`beta.qcharity.net` مرتبطة بالموقع الأب.

IIS/ASP.NET Core Module يمرر `/cms` إلى التطبيق كـ`PathBase`، لذلك لا يتم تثبيت `/cms` داخل `Program.cs`.

## الحزمة

الحزمة الجاهزة موجودة في:

`publish/TalaPress-beta-cms-20260805`

وتحتوي:

- إصدار Release من TalaPress.
- `web.config` ببيئة Production واستضافة In-Process.
- رابط Checkout مضبوطًا على `https://beta.qcharity.org`.
- Browser Session Checkout Sync مفعّلًا.
- مفتاح التوقيع مضبوطًا داخل `web.config` من دون طباعته.
- سكربت `_deploy-to-server.ps1` للنشر والرجوع التلقائي عند الفشل.

الحزمة لا تحتوي `appsettings.Development.json` ولا `wwwroot/uploads`.

## ما يتم الحفاظ عليه من الخادم

سكربت النشر يحتفظ تلقائيًا بما يلي من النسخة الحالية:

- `appsettings.json`.
- `appsettings.Production.json`.
- `wwwroot/uploads`.
- `logs`.

هذا مهم لأن ملفات appsettings على الخادم تحتوي اتصالات قواعد البيانات، ولأن uploads بيانات تشغيلية وليست جزءًا من الحزمة.

## تنفيذ النشر

1. انسخ الحزمة إلى مجلد مؤقت على الخادم، مثل:

   `C:\Deploy\TalaPress-beta-cms-20260805`

2. افتح Windows PowerShell كمسؤول.
3. انتقل إلى مجلد الحزمة.
4. نفذ:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\_deploy-to-server.ps1
```

يحاول السكربت اكتشاف Application Pool من المسار الفيزيائي. إذا لم يجده، مرر الاسم صراحة:

```powershell
.\_deploy-to-server.ps1 -AppPoolName "TalaPress-Beta"
```

يمكن تغيير مسار الوجهة عند الحاجة:

```powershell
.\_deploy-to-server.ps1 -Destination "C:\Website\Talapress" -AppPoolName "TalaPress-Beta"
```

السكربت يقوم بالآتي:

1. يوقف Application Pool بالكامل.
2. ينقل النسخة الحالية إلى مجلد backup مؤرخ.
3. ينسخ الإصدار الجديد.
4. يعيد appsettings وuploads وlogs من النسخة الحالية.
5. يشغّل Application Pool.
6. يعيد النسخة السابقة تلقائيًا إذا فشل النسخ.

## التحقق بعد النشر

1. افتح `https://beta.qcharity.net/cms`.
2. تأكد أن الصفحة والملفات الثابتة تعمل تحت `/cms`.
3. سجل الدخول إلى لوحة TalaPress.
4. تحقق أن الصور القديمة ما زالت موجودة.
5. نفذ طلب API باستخدام Pearl key صالح.
6. نفذ Checkout من الأقربون وتأكد أن التحويل يذهب إلى `https://beta.qcharity.org`.
7. تأكد من عدم ظهور المفتاح أو payload في query string؛ النقل يستخدم URL fragment ثم POST.

## ملاحظات مهمة

- لا تنسخ الحزمة يدويًا فوق الموقع وهو يعمل؛ استخدم السكربت أو أوقف Application Pool أولًا.
- لا تستبدل appsettings الخاصة بالخادم بملفات الحزمة الفارغة.
- لا تحذف backup قبل اكتمال اختبار تسجيل الدخول وAPI وCheckout.
- بعد نجاح الاختبار يمكن حذف backup القديم يدويًا.
- لا تسجل أو تطبع `AlakraboonCartSyncSigningKey`.
