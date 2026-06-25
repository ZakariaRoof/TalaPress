# تصدير تقارير PDF ومعالجة اللغة العربية

تشرح هذه الوثيقة كيفية التعامل مع اللغة العربية عند تصدير التقارير والجداول إلى ملفات PDF باستخدام مكتبة `jsPDF` و `jspdf-autotable`.

## المشكلة
مكتبة `jsPDF` لا تدعم بشكل افتراضي اللغة العربية من حيث:
1. **تشكيل الحروف (Shaping):** ربط الحروف ببعضها بالشكل الصحيح بناءً على موقعها في الكلمة.
2. **اتجاه النص (RTL):** كتابة النص من اليمين إلى اليسار.

## الحل المتبع
تم حل هذه المشكلة بالاعتماد على الخطوات التالية:

### 1. إضافة الخطوط والمكتبات المطلوبة
يتم استدعاء مكتبة `arabic-reshaper.js` المسؤولة عن معالجة الحروف، بالإضافة إلى ملف الخط العربي `Amiri-normal.js` (والذي تم توليده وإضافته لـ jsPDF) في الصفحة:

```html
<script src="~/js/arabic-reshaper.js"></script>
<script src="~/js/Amiri-normal.js"></script>
```

### 2. دالة معالجة النصوص `fixArabicPdfText`
تم إنشاء دالة مساعدة تقوم بفحص النص، وإذا كان يحتوي على حروف عربية، تقوم بمعالجته باستخدام `ArabicReshaper` ثم عكس ترتيب الحروف لتظهر بشكل صحيح من اليمين لليسار.

```javascript
function isArabicChar(char) {
    var code = typeof char === 'string' ? char.charCodeAt(0) : char;
    // استثناء الأرقام المشرقية لعدم عكسها (مثل ١٢٣)
    if (code >= 0x0660 && code <= 0x0669) return false;
    if (code >= 0x06F0 && code <= 0x06F9) return false;
    
    return (code >= 0x0600 && code <= 0x06FF) || (code >= 0x0750 && code <= 0x077F) || 
           (code >= 0x08A0 && code <= 0x08FF) || (code >= 0xFB50 && code <= 0xFDFF) || 
           (code >= 0xFE70 && code <= 0xFEFF);
}

function fixArabicPdfText(text) {
    if (!text) return text;
    text = String(text);
    
    // إضافة علامة RTL Mark (\u200F) حول علامات الترقيم لمنع تشويه الترتيب (Bidi Shifting) في قارئ الـ PDF
    text = text.replace(/:/g, '\u200F:\u200F')
               .replace(/\|/g, '\u200F|\u200F')
               .replace(/-/g, '\u200F-\u200F');
    
    // تقسيم النص إلى كلمات للحفاظ على ترتيب الأرقام والتواريخ
    var words = text.split(' ');
    var processedWords = [];
    
    for (var i = 0; i < words.length; i++) {
        var word = words[i];
        var hasArabic = false;
        
        for (var j = 0; j < word.length; j++) {
            if (isArabicChar(word[j])) {
                hasArabic = true;
                break;
            }
        }
        
        if (hasArabic && typeof ArabicReshaper !== 'undefined') {
            var reshaped = ArabicReshaper.convertArabic(word);
            processedWords.push(reshaped.split('').reverse().join(''));
        } else {
            processedWords.push(word);
        }
    }
    
    // عكس مصفوفة الكلمات لضمان القراءة من اليمين لليسار
    return processedWords.reverse().join(' ');
}
```

### 3. استخدام الدالة عند بناء ملف الـ PDF (jsPDF & autoTable)
أثناء استخدام إضافة `autoTable`، يتم الاستفادة من الحدث `didParseCell` لتمرير نصوص الخلايا والعناوين عبر دالة المعالجة، مع التأكد من إعداد الخط المخصص:

```javascript
const { jsPDF } = window.jspdf;
const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' });

// إعداد الخط العربي
doc.setFont("Amiri", "normal");
doc.setFontSize(14);

// طباعة عنوان التقرير بعد المعالجة
var reportTitle = fixArabicPdfText("عنوان التقرير");
doc.text(reportTitle, 280, 10, { align: 'left' }); 
// ملاحظة: يتم ضبط المحاذاة حسب الاتجاه وتنسيق الصفحة.

// إضافة معلومات الفلاتر (السطر أسفل العنوان وفوق الجدول)
// تنبيه هام: يجب تمرير أي نص يحتوي على عربي إلى دالة fixArabicPdfText وإلا سيظهر مشفراً أو غير متصل
var filters = [];
filters.push("الشركة: تالا بريس");
filters.push("السنة: 2026");

if (filters.length > 0) {
    var filtersText = filters.join(" | ");
    doc.setFontSize(12);
    // تمرير النص المجمع إلى الدالة لمعالجته
    doc.text(fixArabicPdfText(filtersText), 280, 20, { align: 'left' });
}

// معالجة الجدول
doc.autoTable({
    // ...
    styles: { font: "Amiri", halign: 'right' },
    headStyles: { font: "Amiri", halign: 'center' },
    didParseCell: function (data) {
        // معالجة النصوص داخل الجدول
        if (data.cell && data.cell.text && data.cell.text.length > 0) {
            for (var i = 0; i < data.cell.text.length; i++) {
                data.cell.text[i] = fixArabicPdfText(data.cell.text[i]);
            }
        }
    }
});
```

بهذه الطريقة يتم تصدير الكلمات العربية داخل ملف الـ PDF متصلة بشكل صحيح ومقروءة من اليمين إلى اليسار.
