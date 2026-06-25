USE [TalaPress]
GO
/****** Object:  Table [dbo].[Categories]    Script Date: 6/13/2026 2:43:52 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Categories](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](250) NOT NULL,
	[Name_En] [nvarchar](250) NULL,
	[Slug] [nvarchar](250) NULL,
	[ParentId] [bigint] NULL,
	[IconValue] [nvarchar](500) NULL,
	[Image] [nvarchar](500) NULL,
	[SortOrder] [int] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ContentTypeFields]    Script Date: 6/13/2026 2:43:52 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ContentTypeFields](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[ContentTypeId] [bigint] NOT NULL,
	[FieldName] [nvarchar](100) NOT NULL,
	[Label] [nvarchar](250) NOT NULL,
	[FieldType] [nvarchar](50) NOT NULL,
	[Placeholder] [nvarchar](500) NULL,
	[HelpText] [nvarchar](1000) NULL,
	[IsRequired] [bit] NOT NULL,
	[IsTranslatable] [bit] NOT NULL,
	[DefaultValue] [nvarchar](max) NULL,
	[OptionsJson] [nvarchar](max) NULL,
	[SortOrder] [int] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[Label_En] [nvarchar](250) NULL,
	[Placeholder_En] [nvarchar](500) NULL,
	[HelpText_En] [nvarchar](1000) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ContentTypes]    Script Date: 6/13/2026 2:43:52 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ContentTypes](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[Description] [nvarchar](1000) NULL,
	[IconValue] [nvarchar](500) NULL,
	[IsSystem] [bit] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[Name_En] [nvarchar](100) NULL,
	[Description_En] [nvarchar](1000) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Permissions]    Script Date: 6/13/2026 2:43:52 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Permissions](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[Code] [nvarchar](100) NOT NULL,
	[Name] [nvarchar](150) NOT NULL,
	[Name_En] [nvarchar](150) NOT NULL,
	[Description] [nvarchar](500) NULL,
	[Description_En] [nvarchar](500) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Permissions] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[RolePermissions]    Script Date: 6/13/2026 2:43:52 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RolePermissions](
	[RoleId] [bigint] NOT NULL,
	[PermissionId] [bigint] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_RolePermissions] PRIMARY KEY CLUSTERED 
(
	[RoleId] ASC,
	[PermissionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Roles]    Script Date: 6/13/2026 2:43:52 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Roles](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](150) NOT NULL,
	[Name_En] [nvarchar](150) NOT NULL,
	[Description] [nvarchar](500) NULL,
	[Description_En] [nvarchar](500) NULL,
	[IsSystem] [bit] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Settings]    Script Date: 6/13/2026 2:43:52 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Settings](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[SiteName] [nvarchar](150) NOT NULL,
	[SiteName_En] [nvarchar](150) NULL,
	[SiteDescription] [nvarchar](500) NULL,
	[SiteDescription_En] [nvarchar](500) NULL,
	[Logo] [nvarchar](500) NULL,
	[Favicon] [nvarchar](500) NULL,
	[DefaultLanguage] [nvarchar](10) NOT NULL,
	[SupportedLanguages] [nvarchar](50) NOT NULL,
	[Theme] [nvarchar](20) NOT NULL,
	[AccentColor] [nvarchar](20) NULL,
	[EnableDarkMode] [bit] NOT NULL,
	[EnableRTL] [bit] NOT NULL,
	[DateFormat] [nvarchar](20) NOT NULL,
	[TimeFormat] [nvarchar](20) NOT NULL,
	[DateTimeFormat] [nvarchar](30) NOT NULL,
	[TimeZone] [nvarchar](50) NOT NULL,
	[DefaultPageSize] [int] NOT NULL,
	[MaxUploadSizeMB] [int] NOT NULL,
	[AllowedFileExtensions] [nvarchar](500) NULL,
	[EnableSeo] [bit] NOT NULL,
	[EnableCategories] [bit] NOT NULL,
	[EnableMediaLibrary] [bit] NOT NULL,
	[EnableContentVersioning] [bit] NOT NULL,
	[AutoGenerateSlug] [bit] NOT NULL,
	[DefaultContentStatus] [nvarchar](20) NOT NULL,
	[ApiEnabled] [bit] NOT NULL,
	[GoogleAnalyticsCode] [nvarchar](100) NULL,
	[GoogleTagManagerCode] [nvarchar](100) NULL,
	[CompanyName] [nvarchar](150) NULL,
	[CompanyName_En] [nvarchar](150) NULL,
	[CompanyEmail] [nvarchar](150) NULL,
	[CompanyPhone] [nvarchar](50) NULL,
	[CompanyAddress] [nvarchar](500) NULL,
	[CompanyAddress_En] [nvarchar](500) NULL,
	[FacebookUrl] [nvarchar](250) NULL,
	[TwitterUrl] [nvarchar](250) NULL,
	[InstagramUrl] [nvarchar](250) NULL,
	[LinkedInUrl] [nvarchar](250) NULL,
	[YouTubeUrl] [nvarchar](250) NULL,
	[FooterCopyright] [nvarchar](150) NULL,
	[FooterCopyright_En] [nvarchar](150) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UserRoles]    Script Date: 6/13/2026 2:43:52 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UserRoles](
	[UserId] [bigint] NOT NULL,
	[RoleId] [bigint] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC,
	[RoleId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Users]    Script Date: 6/13/2026 2:43:52 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Users](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[Username] [nvarchar](150) NOT NULL,
	[Email] [nvarchar](256) NOT NULL,
	[PasswordHash] [nvarchar](500) NOT NULL,
	[FullName] [nvarchar](250) NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET IDENTITY_INSERT [dbo].[Categories] ON 
GO
INSERT [dbo].[Categories] ([Id], [Name], [Name_En], [Slug], [ParentId], [IconValue], [Image], [SortOrder], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (1, N'الصفحات', N'Pages', N'pages', NULL, N'bi bi-file-earmark-text', NULL, 1, 1, CAST(N'2026-06-13T00:27:01.7400000' AS DateTime2), NULL)
GO
INSERT [dbo].[Categories] ([Id], [Name], [Name_En], [Slug], [ParentId], [IconValue], [Image], [SortOrder], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (2, N'الأخبار', N'News', N'news', NULL, N'bi bi-newspaper', NULL, 2, 1, CAST(N'2026-06-13T00:27:01.7400000' AS DateTime2), NULL)
GO
INSERT [dbo].[Categories] ([Id], [Name], [Name_En], [Slug], [ParentId], [IconValue], [Image], [SortOrder], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (3, N'الخدمات', N'Services', N'services', NULL, N'bi bi-briefcase', NULL, 3, 1, CAST(N'2026-06-13T00:27:01.7400000' AS DateTime2), NULL)
GO
INSERT [dbo].[Categories] ([Id], [Name], [Name_En], [Slug], [ParentId], [IconValue], [Image], [SortOrder], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (4, N'المشاريع', N'Projects', N'projects', NULL, N'bi bi-kanban', NULL, 4, 1, CAST(N'2026-06-13T00:27:01.7400000' AS DateTime2), NULL)
GO
SET IDENTITY_INSERT [dbo].[Categories] OFF
GO
SET IDENTITY_INSERT [dbo].[ContentTypes] ON 
GO
INSERT [dbo].[ContentTypes] ([Id], [Name], [Description], [IconValue], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt], [Name_En], [Description_En]) VALUES (1, N'خبر', N'قالب لإدارة الأخبار والمقالات الإخبارية', N'bi bi-newspaper', 1, 1, CAST(N'2026-06-13T00:24:32.1566667' AS DateTime2), NULL, N'News', N'Template for managing news and news articles')
GO
INSERT [dbo].[ContentTypes] ([Id], [Name], [Description], [IconValue], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt], [Name_En], [Description_En]) VALUES (2, N'حدث', N'قالب لإدارة الفعاليات والأحداث', N'bi bi-calendar-event', 1, 1, CAST(N'2026-06-13T00:24:32.1566667' AS DateTime2), NULL, N'Event', N'Template for managing events and activities')
GO
INSERT [dbo].[ContentTypes] ([Id], [Name], [Description], [IconValue], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt], [Name_En], [Description_En]) VALUES (3, N'صفحة', N'قالب لإنشاء الصفحات الثابتة', N'bi bi-file-earmark-text', 1, 1, CAST(N'2026-06-13T00:24:32.1566667' AS DateTime2), NULL, N'Page', N'Template for creating static pages')
GO
INSERT [dbo].[ContentTypes] ([Id], [Name], [Description], [IconValue], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt], [Name_En], [Description_En]) VALUES (4, N'معرض صور', N'قالب لإدارة معارض الصور', N'bi bi-images', 1, 1, CAST(N'2026-06-13T00:24:32.1566667' AS DateTime2), NULL, N'Gallery', N'Template for managing image galleries')
GO
INSERT [dbo].[ContentTypes] ([Id], [Name], [Description], [IconValue], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt], [Name_En], [Description_En]) VALUES (5, N'فريق العمل', N'قالب لإدارة أعضاء فريق العمل', N'bi bi-people', 1, 1, CAST(N'2026-06-13T00:24:32.1566667' AS DateTime2), NULL, N'Team', N'Template for managing team members')
GO
INSERT [dbo].[ContentTypes] ([Id], [Name], [Description], [IconValue], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt], [Name_En], [Description_En]) VALUES (6, N'دراسة حالة', N'قالب لإدارة دراسات الحالة وقصص النجاح', N'bi bi-journal-richtext', 1, 1, CAST(N'2026-06-13T00:24:32.1566667' AS DateTime2), NULL, N'Case Study', N'Template for managing case studies and success stories')
GO
INSERT [dbo].[ContentTypes] ([Id], [Name], [Description], [IconValue], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt], [Name_En], [Description_En]) VALUES (7, N'شهادة عميل', N'قالب لإدارة آراء وتقييمات العملاء', N'bi bi-chat-quote', 1, 1, CAST(N'2026-06-13T00:24:32.1566667' AS DateTime2), NULL, N'Testimonial', N'Template for managing customer testimonials')
GO
INSERT [dbo].[ContentTypes] ([Id], [Name], [Description], [IconValue], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt], [Name_En], [Description_En]) VALUES (8, N'خدمة', N'قالب لإدارة الخدمات المقدمة', N'bi bi-briefcase', 1, 1, CAST(N'2026-06-13T00:24:32.1566667' AS DateTime2), NULL, N'Service', N'Template for managing services')
GO
INSERT [dbo].[ContentTypes] ([Id], [Name], [Description], [IconValue], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt], [Name_En], [Description_En]) VALUES (9, N'مشروع', N'قالب لإدارة المشاريع والأعمال المنجزة', N'bi bi-kanban', 1, 1, CAST(N'2026-06-13T00:24:32.1566667' AS DateTime2), NULL, N'Project', N'Template for managing projects and portfolios')
GO
SET IDENTITY_INSERT [dbo].[ContentTypes] OFF
GO
SET IDENTITY_INSERT [dbo].[Permissions] ON 
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (1, N'Content.View', N'عرض المحتوى', N'View Content', N'عرض وقراءة مقالات النظام والمسودات', N'Allows viewing and reading article lists and drafts.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (2, N'Content.Create', N'إنشاء محتوى جديد', N'Create Content', N'إضافة وكتابة مسودات مقالات جديدة', N'Allows creating new content drafts.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (3, N'Content.Edit', N'تعديل المحتوى', N'Edit Content', N'تعديل المقالات القائمة والمسودات', N'Allows editing existing articles and drafts.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (4, N'Content.Delete', N'حذف المحتوى', N'Delete Content', N'حذف المقالات والمسودات من النظام نهائياً', N'Allows deleting articles and drafts permanently.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (5, N'Content.Publish', N'نشر المحتوى', N'Publish Content', N'تغيير حالة المقال إلى منشور لجعله متاحاً للعامة', N'Allows publishing content to make it publicly visible.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (6, N'Category.View', N'عرض التصنيفات', N'View Categories', N'عرض أقسام وتصنيفات المحتوى', N'Allows viewing article categories.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (7, N'Category.Create', N'إنشاء تصنيف جديد', N'Create Category', N'إضافة قسم رئيسي أو فرعي جديد للمقالات', N'Allows creating new categories.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (8, N'Category.Edit', N'تعديل التصنيف', N'Edit Category', N'تعديل تفاصيل وأسماء الأقسام والتصنيفات', N'Allows editing existing categories.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (9, N'Category.Delete', N'حذف التصنيف', N'Delete Category', N'إزالة تصنيف من النظام', N'Allows deleting categories.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (10, N'ContentType.View', N'عرض أنواع المحتوى', N'View Content Types', N'عرض أنواع المحتوى المخصصة في النظام', N'Allows viewing custom content types.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (11, N'ContentType.Create', N'إنشاء نوع محتوى', N'Create Content Type', N'إضافة نوع محتوى مخصص جديد (مقال، خبر، بودكاست...)', N'Allows creating new content types.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (12, N'ContentType.Edit', N'تعديل نوع محتوى', N'Edit Content Type', N'تعديل الحقول والمواصفات لنوع محتوى مخصص', N'Allows editing content type schemas.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (13, N'ContentType.Delete', N'حذف نوع محتوى', N'Delete Content Type', N'حذف نوع محتوى مخصص من النظام', N'Allows deleting content types.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (14, N'Media.View', N'عرض مكتبة الوسائط', N'View Media', N'تصفح وعرض مكتبة الصور والملفات', N'Allows viewing the media library.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (15, N'Media.Upload', N'رفع ملفات وسائط', N'Upload Media', N'رفع صور أو مستندات أو ملفات وسائط جديدة للموقع', N'Allows uploading files to the media library.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (16, N'Media.Delete', N'حذف ملفات وسائط', N'Delete Media', N'حذف ملفات أو صور نهائياً من الخادم', N'Allows deleting files from the media library.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (17, N'Settings.View', N'عرض الإعدادات', N'View Settings', N'عرض إعدادات النظام وتخصيص الموقع', N'Allows viewing global system configurations.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (18, N'Settings.Edit', N'تعديل الإعدادات', N'Edit Settings', N'تحديث وتعديل خيارات تهيئة النظام والموقع', N'Allows updating global system configurations.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (19, N'Users.View', N'عرض المستخدمين', N'View Users', N'عرض حسابات مستخدمي لوحة التحكم والأعضاء', N'Allows viewing dashboard user accounts.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (20, N'Users.Create', N'إنشاء مستخدم جديد', N'Create User', N'إضافة حساب مستخدم جديد للوحة التحكم', N'Allows creating new dashboard user accounts.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (21, N'Users.Edit', N'تعديل بيانات مستخدم', N'Edit User', N'تعديل وتحديث معلومات حساب مستخدم أو كلمة المرور', N'Allows editing existing user accounts.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (22, N'Users.Delete', N'حذف حساب مستخدم', N'Delete User', N'حذف حساب مستخدم نهائياً من لوحة التحكم', N'Allows deleting user accounts.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (23, N'Roles.View', N'عرض الأدوار والصلاحيات', N'View Roles', N'عرض الأدوار الوظيفية المتاحة بالنظام', N'Allows viewing user security roles.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (24, N'Roles.Create', N'إنشاء دور جديد', N'Create Role', N'إضافة دور أمني مخصص جديد لتوزيع الصلاحيات', N'Allows creating new roles.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (25, N'Roles.Edit', N'تعديل الدور', N'Edit Role', N'تعديل المسمى والخيارات المرتبطة بالدور الوظيفي', N'Allows editing existing roles.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (26, N'Roles.Delete', N'حذف دور', N'Delete Role', N'إزالة دور أمني مخصص من النظام', N'Allows deleting non-system roles.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (27, N'Permissions.View', N'عرض الصلاحيات التفصيلية', N'View Permissions', N'عرض قائمة كافة الصلاحيات المتاحة في النظام', N'Allows viewing the master list of all permissions.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
INSERT [dbo].[Permissions] ([Id], [Code], [Name], [Name_En], [Description], [Description_En], [CreatedAt]) VALUES (28, N'Dashboard.View', N'دخول لوحة الإحصائيات', N'View Dashboard', N'عرض إحصائيات لوحة التحكم الرئيسية', N'Allows viewing the administrator dashboard hub.', CAST(N'2026-06-12T23:39:22.9900000' AS DateTime2))
GO
SET IDENTITY_INSERT [dbo].[Permissions] OFF
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 1, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 2, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 3, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 4, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 5, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 6, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 7, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 8, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 9, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 10, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 11, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 12, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 13, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 14, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 15, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 16, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 17, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 18, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 19, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 20, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 21, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 22, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 23, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 24, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 25, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 26, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 27, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
INSERT [dbo].[RolePermissions] ([RoleId], [PermissionId], [CreatedAt]) VALUES (1, 28, CAST(N'2026-06-12T23:39:23.0066667' AS DateTime2))
GO
SET IDENTITY_INSERT [dbo].[Roles] ON 
GO
INSERT [dbo].[Roles] ([Id], [Name], [Name_En], [Description], [Description_En], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (1, N'مدير النظام العام', N'Super Administrator', N'يمتلك كافة الصلاحيات على النظام ولا يمكن تعديله أو حذفه', N'Full system access. Cannot be modified or deleted.', 1, 1, CAST(N'2026-06-12T23:39:22.9833333' AS DateTime2), NULL)
GO
INSERT [dbo].[Roles] ([Id], [Name], [Name_En], [Description], [Description_En], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (2, N'مدير النظام', N'Administrator', N'يمتلك صلاحيات إدارة المحتوى والمستخدمين وإعدادات الموقع الأساسية', N'Administrative privileges for content, user, and settings management.', 1, 1, CAST(N'2026-06-12T23:39:22.9833333' AS DateTime2), NULL)
GO
INSERT [dbo].[Roles] ([Id], [Name], [Name_En], [Description], [Description_En], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (3, N'مدير المحتوى', N'Content Manager', N'مسؤول عن مراجعة وجدولة وإدارة الأقسام والوسائط', N'Responsible for content strategy, category setups, and media assets.', 1, 1, CAST(N'2026-06-12T23:39:22.9833333' AS DateTime2), NULL)
GO
INSERT [dbo].[Roles] ([Id], [Name], [Name_En], [Description], [Description_En], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (4, N'المحرر', N'Editor', N'مسؤول عن مراجعة وتعديل ونشر مقالات الكتاب والناشرين', N'Can review, edit, approve, and publish content written by authors.', 1, 1, CAST(N'2026-06-12T23:39:22.9833333' AS DateTime2), NULL)
GO
INSERT [dbo].[Roles] ([Id], [Name], [Name_En], [Description], [Description_En], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (5, N'الكاتب', N'Author', N'يمتلك صلاحية كتابة وتعديل مقالاته الخاصة وحفظها كمسودات', N'Can write, edit, and save their own content as drafts.', 1, 1, CAST(N'2026-06-12T23:39:22.9833333' AS DateTime2), NULL)
GO
INSERT [dbo].[Roles] ([Id], [Name], [Name_En], [Description], [Description_En], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (6, N'المترجم', N'Translator', N'مسؤول عن ترجمة المقالات والوسوم بين اللغات المختلفة للموقع', N'Responsible for translating articles and taxonomies across languages.', 1, 1, CAST(N'2026-06-12T23:39:22.9833333' AS DateTime2), NULL)
GO
INSERT [dbo].[Roles] ([Id], [Name], [Name_En], [Description], [Description_En], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (7, N'مدير الوسائط', N'Media Manager', N'مسؤول عن رفع وإدارة ملفات الوسائط المتعددة والصور', N'Manages multimedia library uploads and directory configurations.', 1, 1, CAST(N'2026-06-12T23:39:22.9833333' AS DateTime2), NULL)
GO
INSERT [dbo].[Roles] ([Id], [Name], [Name_En], [Description], [Description_En], [IsSystem], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (8, N'أخصائي سيو SEO', N'SEO Specialist', N'مسؤول عن إدارة الكلمات المفتاحية وأوصاف الميتا لرفع كفاءة الأرشفة', N'Manages keywords, meta tags, and content optimization definitions.', 1, 1, CAST(N'2026-06-12T23:39:22.9833333' AS DateTime2), NULL)
GO
SET IDENTITY_INSERT [dbo].[Roles] OFF
GO
SET IDENTITY_INSERT [dbo].[Settings] ON 
GO
INSERT [dbo].[Settings] ([Id], [SiteName], [SiteName_En], [SiteDescription], [SiteDescription_En], [Logo], [Favicon], [DefaultLanguage], [SupportedLanguages], [Theme], [AccentColor], [EnableDarkMode], [EnableRTL], [DateFormat], [TimeFormat], [DateTimeFormat], [TimeZone], [DefaultPageSize], [MaxUploadSizeMB], [AllowedFileExtensions], [EnableSeo], [EnableCategories], [EnableMediaLibrary], [EnableContentVersioning], [AutoGenerateSlug], [DefaultContentStatus], [ApiEnabled], [GoogleAnalyticsCode], [GoogleTagManagerCode], [CompanyName], [CompanyName_En], [CompanyEmail], [CompanyPhone], [CompanyAddress], [CompanyAddress_En], [FacebookUrl], [TwitterUrl], [InstagramUrl], [LinkedInUrl], [YouTubeUrl], [FooterCopyright], [FooterCopyright_En], [CreatedAt], [UpdatedAt]) VALUES (1, N'TalaPress', N'TalaPress', N'نظام إدارة محتوى عصري ومتعدد اللغات', N'Modern multilingual content management system', NULL, NULL, N'ar', N'ar,en', N'Light', N'#0D6EFD', 1, 1, N'dd/MM/yyyy', N'HH:mm', N'dd/MM/yyyy HH:mm', N'Arab Standard Time', 10, 20, N'jpg,jpeg,png,gif,webp,pdf,doc,docx,xls,xlsx,zip', 1, 1, 1, 1, 1, N'Draft', 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'© جميع الحقوق محفوظة', N'© All rights reserved', CAST(N'2026-06-13T00:46:41.7233333' AS DateTime2), NULL)
GO
SET IDENTITY_INSERT [dbo].[Settings] OFF
GO
INSERT [dbo].[UserRoles] ([UserId], [RoleId], [CreatedAt]) VALUES (1, 1, CAST(N'2026-06-12T23:41:12.0266667' AS DateTime2))
GO
SET IDENTITY_INSERT [dbo].[Users] ON 
GO
INSERT [dbo].[Users] ([Id], [Username], [Email], [PasswordHash], [FullName], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (1, N'admin', N'admin@talapress.com', N'AQAAAAIAAYagAAAAENUe6/Eo90nNxesQ2JRIcjz+D4TsQh6IVkaJZl0Ir4qYPrUG0Zn91naYLi/bVyc7rg==', N'Super Admin', 1, CAST(N'2026-06-12T23:41:12.0100000' AS DateTime2), NULL)
GO
SET IDENTITY_INSERT [dbo].[Users] OFF
GO
ALTER TABLE [dbo].[Categories] ADD  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[Categories] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Categories] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ContentTypeFields] ADD  DEFAULT ((0)) FOR [IsRequired]
GO
ALTER TABLE [dbo].[ContentTypeFields] ADD  DEFAULT ((0)) FOR [IsTranslatable]
GO
ALTER TABLE [dbo].[ContentTypeFields] ADD  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[ContentTypeFields] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[ContentTypeFields] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ContentTypes] ADD  DEFAULT ((0)) FOR [IsSystem]
GO
ALTER TABLE [dbo].[ContentTypes] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[ContentTypes] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Permissions] ADD  DEFAULT (getutcdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[RolePermissions] ADD  DEFAULT (getutcdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Roles] ADD  DEFAULT ((0)) FOR [IsSystem]
GO
ALTER TABLE [dbo].[Roles] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Roles] ADD  DEFAULT (getutcdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT (N'ar') FOR [DefaultLanguage]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT (N'ar,en') FOR [SupportedLanguages]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT (N'Light') FOR [Theme]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT ((1)) FOR [EnableDarkMode]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT ((1)) FOR [EnableRTL]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT (N'dd/MM/yyyy') FOR [DateFormat]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT (N'HH:mm') FOR [TimeFormat]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT (N'dd/MM/yyyy HH:mm') FOR [DateTimeFormat]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT (N'Arab Standard Time') FOR [TimeZone]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT ((10)) FOR [DefaultPageSize]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT ((20)) FOR [MaxUploadSizeMB]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT ((1)) FOR [EnableSeo]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT ((1)) FOR [EnableCategories]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT ((1)) FOR [EnableMediaLibrary]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT ((1)) FOR [EnableContentVersioning]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT ((1)) FOR [AutoGenerateSlug]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT (N'Draft') FOR [DefaultContentStatus]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT ((1)) FOR [ApiEnabled]
GO
ALTER TABLE [dbo].[Settings] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[UserRoles] ADD  DEFAULT (getutcdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Users] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Users] ADD  DEFAULT (getutcdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Categories]  WITH CHECK ADD  CONSTRAINT [FK_Categories_Parent] FOREIGN KEY([ParentId])
REFERENCES [dbo].[Categories] ([Id])
GO
ALTER TABLE [dbo].[Categories] CHECK CONSTRAINT [FK_Categories_Parent]
GO
ALTER TABLE [dbo].[ContentTypeFields]  WITH CHECK ADD  CONSTRAINT [FK_ContentTypeFields_ContentTypes] FOREIGN KEY([ContentTypeId])
REFERENCES [dbo].[ContentTypes] ([Id])
GO
ALTER TABLE [dbo].[ContentTypeFields] CHECK CONSTRAINT [FK_ContentTypeFields_ContentTypes]
GO
ALTER TABLE [dbo].[RolePermissions]  WITH CHECK ADD  CONSTRAINT [FK_RolePermissions_Permissions] FOREIGN KEY([PermissionId])
REFERENCES [dbo].[Permissions] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[RolePermissions] CHECK CONSTRAINT [FK_RolePermissions_Permissions]
GO
ALTER TABLE [dbo].[RolePermissions]  WITH CHECK ADD  CONSTRAINT [FK_RolePermissions_Roles] FOREIGN KEY([RoleId])
REFERENCES [dbo].[Roles] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[RolePermissions] CHECK CONSTRAINT [FK_RolePermissions_Roles]
GO
ALTER TABLE [dbo].[UserRoles]  WITH CHECK ADD  CONSTRAINT [FK_UserRoles_Roles] FOREIGN KEY([RoleId])
REFERENCES [dbo].[Roles] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UserRoles] CHECK CONSTRAINT [FK_UserRoles_Roles]
GO
ALTER TABLE [dbo].[UserRoles]  WITH CHECK ADD  CONSTRAINT [FK_UserRoles_Users] FOREIGN KEY([UserId])
REFERENCES [dbo].[Users] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UserRoles] CHECK CONSTRAINT [FK_UserRoles_Users]
GO
