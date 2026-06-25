/**
 * TalaPress Main JavaScript
 * Handles theme switching, language toggle (RTL/LTR), and responsive menu layouts.
 */

document.addEventListener('DOMContentLoaded', () => {
  initTheme();
  initLanguage();
  highlightActiveNav();
});

// Theme Toggle Functionality
function initTheme() {
  const themeToggleBtns = document.querySelectorAll('.tp-theme-toggle');
  const savedTheme = localStorage.getItem('tp-theme') || 'dark'; // Default to dark for high premium feel
  
  applyTheme(savedTheme);

  themeToggleBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      const currentTheme = document.documentElement.getAttribute('data-bs-theme');
      const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
      applyTheme(newTheme);
    });
  });
}

function applyTheme(theme) {
  document.documentElement.setAttribute('data-bs-theme', theme);
  localStorage.setItem('tp-theme', theme);

  // Update toggle button icons
  const icons = document.querySelectorAll('.tp-theme-toggle i');
  icons.forEach(icon => {
    if (theme === 'dark') {
      icon.className = 'bi bi-sun-fill';
    } else {
      icon.className = 'bi bi-moon-stars-fill';
    }
  });

  // Dispatch custom event for charts and other components
  document.dispatchEvent(new CustomEvent('tp-theme-changed', { detail: { theme: theme } }));
}

// Language / Direction Toggle Functionality
const translations = {
  ar: {
    title: "TalaPress لوحة التحكم",
    dashboard: "لوحة التحكم",
    schedule: "الجدول الزمني",
    mapIt: "الخريطة",
    widgets: "الأدوات",
    settings: "الإعدادات",
    forms: "النماذج",
    charts: "الرسوم البيانية",
    workflow: "مسارات العمل",
    welcome: "مرحباً بك في TalaPress",
    welcomeText: "هذا النظام مصمم لمساعدتك في إدارة ونشر المحتوى بشكل متكامل. يمكنك متابعة الإحصائيات، وجدولة المقالات، وتعديل مسارات العمل البرمجية بكل سهولة.",
    shortcuts: "روابط سريعة",
    lists: "القوائم والمقالات الأخيرة",
    recentDrafts: "المسودات الأخيرة",
    showMore: "عرض المزيد",
    home: "الرئيسية",
    maps: "الخرائط",
    admin: "الإدارة",
    submit: "إرسال",
    login: "تسجيل الدخول",
    email: "البريد الإلكتروني",
    password: "كلمة المرور",
    datePicker: "محدد التاريخ وصناديق الاختيار",
    chooseCountry: "اختر الدولة...",
    totalArticles: "إجمالي المقالات",
    views: "المشاهدات",
    pendingReviews: "بانتظار المراجعة",
    activeWorkflows: "مسارات العمل النشطة",
    workflowDesigner: "مصمم مسارات العمل BPMN",
    bpmnTitle: "مخطط سير العمل المعتمد لنشر المقالات",
    bpmnDesc: "قم بسحب وإفلات العناصر لتصميم وتعديل مسار النشر (محرر - مدقق لغوي - مسؤول النشر).",
    saveWorkflow: "حفظ المسار",
    zoomIn: "تكبير",
    zoomOut: "تصغير",
    resetZoom: "إعادة تعيين",
    apexTitle: "التحليلات والتقارير الرسومية",
    apexDesc: "رسوم بيانية تفاعلية متقدمة باستخدام مكتبة ApexCharts لعرض أداء TalaPress.",
    statArticlesText: "المقالات المنشورة هذا الشهر",
    statViewsText: "مجموع زيارات TalaPress الكلية",
    statPendingText: "مقالات تنتظر مراجعة المحررين",
    statWorkflowText: "عمليات نشر جارية حالياً",
    account: "الحساب",
    updates: "التحديثات",
    messages: "الرسائل",
    tasks: "المهام",
    comments: "التعليقات",
    navDashboard: "لوحة التحكم",
    navContent: "إدارة المحتوى",
    navCategories: "إدارة التصنيفات",
    navMenus: "إدارة القوائم",
    navContentTypes: "أنواع المحتوى",
    navForms: "إدارة النماذج",
    navFormSubmissions: "ردود النماذج",
    navTemplates: "إدارة القوالب",
    navWorkflow: "سير العمل",
    navPermissions: "الأذونات",
    navUsers: "المستخدمين",
    navMedia: "مكتبة الوسائط",
    menusTitle: "إدارة القوائم والملاحة",
    menusDesc: "أنشئ وأدر قوائم الموقع، ونظم عناصر الملاحة والروابط.",
    selectMenuLabel: "اختر القائمة المراد تعديلها:",
    selectMenuDefault: "-- اختر قائمة --",
    btnCreateNewMenu: "إنشاء قائمة جديدة",
    itemsTreeHeader: "عناصر القائمة",
    menuStructure: "بنية القائمة:",
    menuSettingsBtn: "إعدادات القائمة",
    searchItemPlaceholder: "ابحث عن عنصر...",
    addNewRootItem: "إضافة عنصر رئيسي جديد",
    addNewItem: "إضافة عنصر جديد",
    editItem: "تعديل العنصر",
    itemTitleAr: "الاسم بالعربية",
    itemTitleArPlaceholder: "مثال: من نحن",
    itemTitleEn: "الاسم بالإنجليزية",
    itemTitleEnPlaceholder: "مثال: About Us",
    linkType: "نوع الرابط الموجه",
    linkTypeCustom: "رابط مخصص",
    linkTypeContent: "ربط بمحتوى ديناميكي",
    customUrl: "الرابط المخصص",
    customUrlPlaceholder: "/about-us أو https://google.com",
    contentTypeLabel: "نوع المحتوى",
    contentTypeDefault: "-- اختر نوع محتوى --",
    categoryLabel: "التصنيف (اختياري)",
    categoryDefault: "-- كل التصنيفات --",
    contentSearchLabel: "البحث عن المقال واختياره",
    contentSearchPlaceholder: "اكتب اسم المحتوى للبحث...",
    selectedContentPrefix: "المحتوى المحدد حالياً:",
    parentItem: "العنصر الأب",
    rootItemOption: "-- عنصر رئيسي (بدون أب) --",
    itemIcon: "أيقونة العنصر (اختياري)",
    itemIconSearch: "ابحث عن أيقونة...",
    sortOrderLabel: "ترتيب العرض",
    statusLabel: "الحالة",
    statusActive: "نشط ومفعل",
    saveNewItemBtn: "إضافة العنصر الجديد",
    saveEditItemBtn: "حفظ العنصر",
    cancelBtn: "إلغاء",
    deleteItemBtn: "حذف العنصر",
    noMenusMessage: "الرجاء اختيار قائمة من الأعلى للبدء بتعديلها أو إدارة هيكلها.",
    emptyDatabaseTitle: "لا توجد أي قوائم حالياً في نظام TalaPress.",
    emptyDatabaseDesc: "ابدأ بإنشاء أول قائمة لموقعك (مثل القائمة الرئيسية) عبر تعبئة النموذج بالأعلى.",
    loginWelcome: "مرحباً بك مجدداً!",
    loginSubtitle: "سجل الدخول للمتابعة إلى TalaPress",
    usernameLabel: "اسم المستخدم أو البريد الإلكتروني",
    usernamePlaceholder: "أدخل اسم المستخدم أو البريد",
    passwordLabel: "كلمة المرور",
    passwordPlaceholder: "أدخل كلمة المرور",
    forgotPassword: "هل نسيت كلمة المرور؟",
    rememberMeLabel: "تذكرني على هذا الجهاز",
    signInBtn: "تسجيل الدخول",
    noAccountText: "ليس لديك حساب؟",
    signupText: "إنشاء حساب جديد",
    searchUserPlaceholder: "ابحث عن مستخدم...",
    menuNameAr: "اسم القائمة بالعربية",
    menuNameArPlaceholder: "مثال: القائمة الرئيسية",
    menuNameEn: "اسم القائمة بالإنجليزية",
    menuNameEnPlaceholder: "مثال: Main Menu",
    menuCode: "كود القائمة (فريد)",
    menuCodePlaceholder: "مثال: main-menu",
    menuCodeHelp: "كود برمجي فريد للاستدعاء (يتم توليده تلقائياً من الاسم إذا ترك فارغاً).",
    menuIsActive: "نشطة ومفعلة",
    menuSaveBtn: "حفظ الإعدادات",
    menuCreateBtn: "إنشاء القائمة",
    menuDeleteBtn: "حذف القائمة بالكامل",
    menuCloseBtn: "إغلاق الإعدادات",
    mediaTitle: "مكتبة الوسائط",
    mediaNewFolder: "مجلد جديد",
    mediaUploadFiles: "رفع ملفات",
    mediaSearchPlaceholder: "بحث في المجلد الحالي والفرعي...",
    mediaEmptyStateTitle: "لا توجد ملفات أو مجلدات في هذا المسار.",
    mediaEmptyStateDesc: "اسحب الملفات وأفلتها هنا للرفع الفوري أو اضغط على رفع ملفات.",
    mediaDetailsTitle: "تفاصيل الملف",
    mediaFolderDetailsTitle: "تفاصيل المجلد",
    mediaFileName: "اسم الملف",
    mediaFolderName: "اسم المجلد",
    mediaSize: "الحجم",
    mediaUploadDate: "تاريخ الرفع",
    mediaDimensions: "الأبعاد (عرض × ارتفاع)",
    mediaDirectUrl: "الرابط المباشر للويب",
    copy: "نسخ",
    mediaDownloadFile: "تحميل الملف",
    mediaDeletePermanent: "حذف نهائي",
    mediaSelectToPreview: "حدد ملفاً أو مجلداً لمعاينة التفاصيل هنا.",
    mediaToolbarOptions: "خيارات الإدارة:",
    mediaToolbarRename: "تغيير الاسم",
    mediaToolbarMove: "نقل",
    mediaToolbarDownload: "تحميل",
    mediaToolbarDelete: "حذف",
    mediaModalCreateFolder: "إنشاء مجلد جديد",
    mediaModalFolderName: "اسم المجلد",
    mediaModalFolderNamePlaceholder: "أدخل اسم المجلد بدون مسافات أو رموز خاصة...",
    mediaModalFolderNameHint: "المجلد سيتم إنشاؤه داخل المسار الحالي للمكتبة.",
    mediaDragOverlayTitle: "أفلت الملفات لرفعها فورياً",
    mediaDragOverlayDesc: "يمكنك إفلات صور، ملفات PDF، مستندات، أو ملفات مضغوطة.",
    cancel: "إلغاء",
    create: "إنشاء",
    search: "بحث",
    mediaBreadcrumbRoot: "الرئيسية (uploads)",
    mediaSearchResults: "نتائج البحث عن:",
    langName: "English",
    apiDocsNav: "Pearl API",
    apiDocsOverview: "نظرة عامة",
    apiDocsAuth: "المصادقة",
    apiDocsContent: "المحتوى",
    apiDocsContentTypes: "أنواع المحتوى",
    apiDocsCategories: "التصنيفات",
    apiDocsMenus: "القوائم",
    apiDocsSettings: "الإعدادات"
  },
  en: {
    title: "TalaPress Admin Dashboard",
    dashboard: "Dashboard",
    schedule: "Schedule",
    mapIt: "Map It",
    widgets: "Widgets",
    settings: "Settings",
    forms: "Forms",
    charts: "Charts",
    workflow: "Workflows",
    welcome: "Welcome to TalaPress",
    welcomeText: "This system is designed to help you manage and publish content dynamically. Track statistics, schedule posts, and configure workflow business processes with ease.",
    shortcuts: "Shortcuts",
    lists: "Lists & Recent Articles",
    recentDrafts: "Recent Drafts",
    showMore: "Show More",
    home: "Home",
    maps: "Maps",
    admin: "Admin",
    submit: "Submit",
    login: "Login",
    email: "Email address",
    password: "Password",
    datePicker: "Date Picker & Select Boxes",
    chooseCountry: "Choose a Country...",
    totalArticles: "Total Articles",
    views: "Views",
    pendingReviews: "Pending Reviews",
    activeWorkflows: "Active Workflows",
    workflowDesigner: "BPMN Workflow Designer",
    bpmnTitle: "Approved Article Publishing Workflow",
    bpmnDesc: "Drag, drop and connect elements to configure the business process (Author -> Editor -> Publisher).",
    saveWorkflow: "Save Workflow",
    zoomIn: "Zoom In",
    zoomOut: "Zoom Out",
    resetZoom: "Reset Zoom",
    apexTitle: "Graphical Reports & Analytics",
    apexDesc: "Advanced interactive charts using ApexCharts displaying TalaPress publishing performance.",
    statArticlesText: "Published articles this month",
    statViewsText: "Total cumulative views",
    statPendingText: "Articles waiting for editor approval",
    statWorkflowText: "Publishing jobs currently running",
    account: "Account",
    updates: "Updates",
    messages: "Messages",
    tasks: "Tasks",
    comments: "Comments",
    navDashboard: "Dashboard",
    navContent: "Content Management",
    navCategories: "Category Management",
    navMenus: "Menus",
    navContentTypes: "Content Types",
    navForms: "Forms",
    navFormSubmissions: "Form Submissions",
    navTemplates: "Template Management",
    navWorkflow: "Workflow",
    navPermissions: "Permissions",
    navUsers: "Users",
    navMedia: "Media Library",
    menusTitle: "Menus & Navigation Management",
    menusDesc: "Create and manage site menus, and organize navigation items.",
    selectMenuLabel: "Select the menu to edit:",
    selectMenuDefault: "-- Select a Menu --",
    btnCreateNewMenu: "Create New Menu",
    itemsTreeHeader: "Menu Items",
    menuStructure: "Menu Structure:",
    menuSettingsBtn: "Menu Settings",
    searchItemPlaceholder: "Search for an item...",
    addNewRootItem: "Add New Root Item",
    addNewItem: "Add New Item",
    editItem: "Edit Item",
    itemTitleAr: "Arabic Name",
    itemTitleArPlaceholder: "Example: من نحن",
    itemTitleEn: "English Name",
    itemTitleEnPlaceholder: "Example: About Us",
    linkType: "Link Type",
    linkTypeCustom: "Custom Link",
    linkTypeContent: "Linked Content",
    customUrl: "Custom URL",
    customUrlPlaceholder: "/about-us or https://google.com",
    contentTypeLabel: "Content Type",
    contentTypeDefault: "-- Select Content Type --",
    categoryLabel: "Category (Optional)",
    categoryDefault: "-- All Categories --",
    contentSearchLabel: "Search and select content",
    contentSearchPlaceholder: "Type content name to search...",
    selectedContentPrefix: "Currently selected content:",
    parentItem: "Parent Item",
    rootItemOption: "-- Root Item (No Parent) --",
    itemIcon: "Item Icon (Optional)",
    itemIconSearch: "Search icon...",
    sortOrderLabel: "Sort Order",
    statusLabel: "Status",
    statusActive: "Active",
    saveNewItemBtn: "Add New Item",
    saveEditItemBtn: "Save Item",
    cancelBtn: "Cancel",
    deleteItemBtn: "Delete Item",
    noMenusMessage: "Please select a menu from above to start editing or managing its structure.",
    emptyDatabaseTitle: "There are currently no menus in TalaPress.",
    emptyDatabaseDesc: "Start by creating your first site menu (e.g. Main Menu) using the form above.",
    loginWelcome: "Welcome Back!",
    loginSubtitle: "Sign in to continue to TalaPress",
    usernameLabel: "Username or Email Address",
    usernamePlaceholder: "Enter username or email",
    passwordLabel: "Password",
    passwordPlaceholder: "Enter password",
    forgotPassword: "Forgot password?",
    rememberMeLabel: "Remember me on this device",
    signInBtn: "Sign In",
    noAccountText: "Don't have an account?",
    signupText: "Create a new account",
    searchUserPlaceholder: "Search user...",
    menuNameAr: "Arabic Menu Name",
    menuNameArPlaceholder: "Example: القائمة الرئيسية",
    menuNameEn: "English Menu Name",
    menuNameEnPlaceholder: "Example: Main Menu",
    menuCode: "Menu Code (Unique)",
    menuCodePlaceholder: "Example: main-menu",
    menuCodeHelp: "Unique programmatic code (auto-generated from name if left empty).",
    menuIsActive: "Active",
    menuSaveBtn: "Save Settings",
    menuCreateBtn: "Create Menu",
    menuDeleteBtn: "Delete Entire Menu",
    menuCloseBtn: "Close Settings",
    mediaTitle: "Media Library",
    mediaNewFolder: "New Folder",
    mediaUploadFiles: "Upload Files",
    mediaSearchPlaceholder: "Search current folder & subfolders...",
    mediaEmptyStateTitle: "No files or folders found in this directory.",
    mediaEmptyStateDesc: "Drag & drop files here for instant upload or click Upload Files.",
    mediaDetailsTitle: "File Details",
    mediaFolderDetailsTitle: "Folder Details",
    mediaFileName: "File Name",
    mediaFolderName: "Folder Name",
    mediaSize: "Size",
    mediaUploadDate: "Uploaded At",
    mediaDimensions: "Dimensions (W × H)",
    mediaDirectUrl: "Direct Web Link",
    copy: "Copy",
    mediaDownloadFile: "Download File",
    mediaDeletePermanent: "Delete Permanently",
    mediaSelectToPreview: "Select a file or folder to preview details here.",
    mediaToolbarOptions: "Management Options:",
    mediaToolbarRename: "Rename",
    mediaToolbarMove: "Move",
    mediaToolbarDownload: "Download",
    mediaToolbarDelete: "Delete",
    mediaModalCreateFolder: "Create New Folder",
    mediaModalFolderName: "Folder Name",
    mediaModalFolderNamePlaceholder: "Enter folder name without spaces or special chars...",
    mediaModalFolderNameHint: "The folder will be created inside the current library path.",
    mediaDragOverlayTitle: "Drop files to upload instantly",
    mediaDragOverlayDesc: "You can drop images, PDFs, documents, or zip archives.",
    cancel: "Cancel",
    create: "Create",
    search: "Search",
    mediaBreadcrumbRoot: "Root (uploads)",
    mediaSearchResults: "Search results for:",
    langName: "العربية",
    apiDocsNav: "Pearl API Docs",
    apiDocsOverview: "Overview",
    apiDocsAuth: "Authentication",
    apiDocsContent: "Content",
    apiDocsContentTypes: "Content Types",
    apiDocsCategories: "Categories",
    apiDocsMenus: "Menus",
    apiDocsSettings: "Settings"
  }
};

function initLanguage() {
  const langToggleBtns = document.querySelectorAll('.tp-lang-toggle');
  const savedLang = localStorage.getItem('tp-lang') || 'ar'; // Default to Arabic for TalaPress

  applyLanguage(savedLang);

  langToggleBtns.forEach(btn => {
    btn.addEventListener('click', (e) => {
      e.preventDefault();
      const currentLang = document.documentElement.getAttribute('lang') || 'ar';
      const newLang = currentLang === 'ar' ? 'en' : 'ar';
      applyLanguage(newLang);
    });
  });
}

function applyLanguage(lang) {
  const html = document.documentElement;
  html.setAttribute('lang', lang);
  localStorage.setItem('tp-lang', lang);

  // Sync body classes for styling and translations
  document.body.classList.toggle('lang-ar', lang === 'ar');
  document.body.classList.toggle('lang-en', lang === 'en');

  if (lang === 'ar') {
    html.setAttribute('dir', 'rtl');
  } else {
    html.setAttribute('dir', 'ltr');
  }

  const bootstrapCssLink = document.getElementById('bootstrap-style');

  if (bootstrapCssLink) {
    const rtlHref = bootstrapCssLink.getAttribute('data-bootstrap-rtl') || '/assets/css/bootstrap.rtl.min.css';
    const ltrHref = bootstrapCssLink.getAttribute('data-bootstrap-ltr') || '/assets/css/bootstrap.min.css';
    bootstrapCssLink.href = lang === 'ar' ? rtlHref : ltrHref;
  }

  // Update language toggle text
  const langTexts = document.querySelectorAll('.tp-lang-toggle-text');
  langTexts.forEach(txt => {
    txt.textContent = translations[lang].langName;
  });

  // Apply translations to UI elements with data-tp-i18n attributes
  const elements = document.querySelectorAll('[data-tp-i18n]');
  elements.forEach(el => {
    const key = el.getAttribute('data-tp-i18n');
    if (translations[lang][key]) {
      if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
        el.setAttribute('placeholder', translations[lang][key]);
      } else {
        el.textContent = translations[lang][key];
      }
    }
  });
  
  // Custom event to notify charts/bpmn pages to adjust layouts if needed
  window.dispatchEvent(new CustomEvent('tp-lang-changed', { detail: { lang: lang, dir: lang === 'ar' ? 'rtl' : 'ltr' } }));
}

// Highlight the active menu link
function highlightActiveNav() {
  const currentPath = window.location.pathname.toLowerCase();
  
  const navItems = document.querySelectorAll('.tp-nav-item, .tp-mobile-nav-item');
  navItems.forEach(item => {
    const rawHref = item.getAttribute('href');
    if (!rawHref || rawHref === '#') return;
    
    const href = rawHref.toLowerCase();
    
    // Check if the current route matches the nav link
    let isActive = false;
    if (href === '/' || href === 'index.html' || href === 'index') {
      isActive = (currentPath === '/' || currentPath === '/index' || currentPath.endsWith('/index.html') || currentPath === '');
    } else {
      // Remove extensions and leading/trailing slashes for comparison
      const cleanHref = href.replace('.html', '').replace(/^\//, '').trim();
      if (cleanHref) {
        isActive = currentPath.includes(cleanHref);
      }
    }
    
    if (isActive) {
      item.classList.add('active');
    } else {
      item.classList.remove('active');
    }
  });
}
