USE TalaPress;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Delete garbled records
DELETE FROM dbo.Content WHERE ContentTypeId IN (11, 12) OR CategoryId = 10008;
DELETE FROM dbo.Categories WHERE Id = 10008;
DELETE FROM dbo.ContentTypes WHERE Id IN (11, 12);
DELETE FROM dbo.Content WHERE Title LIKE N'%Ø%' OR Title LIKE N'%Ù%' OR Title LIKE N'%(تجريبي)%';

DECLARE @UserId BIGINT = (SELECT TOP 1 Id FROM dbo.Users ORDER BY Id);
IF @UserId IS NULL SET @UserId = 1;

DECLARE @ArticleTypeId BIGINT = (SELECT TOP 1 Id FROM dbo.ContentTypes WHERE Name_En = 'Article');
DECLARE @NewsTypeId BIGINT = (SELECT TOP 1 Id FROM dbo.ContentTypes WHERE Name_En = 'News');
DECLARE @VideoTypeId BIGINT = (SELECT TOP 1 Id FROM dbo.ContentTypes WHERE Name_En = 'Video');

-- If content types don't exist, insert defaults
IF @ArticleTypeId IS NULL 
BEGIN
    INSERT INTO dbo.ContentTypes (Name, Name_En, Description, Description_En, IconValue) VALUES (N'مقال', 'Article', N'للمقالات', 'For articles', 'bi-file-text');
    SET @ArticleTypeId = SCOPE_IDENTITY();
END

IF @NewsTypeId IS NULL 
BEGIN
    INSERT INTO dbo.ContentTypes (Name, Name_En, Description, Description_En, IconValue) VALUES (N'خبر', 'News', N'للأخبار', 'For news', 'bi-newspaper');
    SET @NewsTypeId = SCOPE_IDENTITY();
END

IF @VideoTypeId IS NULL 
BEGIN
    INSERT INTO dbo.ContentTypes (Name, Name_En, Description, Description_En, IconValue) VALUES (N'فيديو', 'Video', N'للفيديوهات', 'For videos', 'bi-play-circle');
    SET @VideoTypeId = SCOPE_IDENTITY();
END

DECLARE @CategoryId BIGINT = (SELECT TOP 1 Id FROM dbo.Categories WHERE Slug = 'general');
IF @CategoryId IS NULL
BEGIN
    INSERT INTO dbo.Categories (Name, Name_En, Slug, ContentTypeId) VALUES (N'عام', 'General', 'general', @ArticleTypeId);
    SET @CategoryId = SCOPE_IDENTITY();
END

-- Insert Dummy Data for different statuses and months
-- Month 1
INSERT INTO dbo.Content (ContentTypeId, Title, Summary, Content, Status, PublishDate, CategoryId, CreatedBy, CreatedAt, Hits)
VALUES 
(@ArticleTypeId, N'مقال تحليلي عن التكنولوجيا (تجريبي)', N'ملخص المقال الأول', N'<p>نص المقال الأول لعام 2026</p>', 'Published', '2026-01-15', @CategoryId, @UserId, '2026-01-10 10:00:00', 150),
(@NewsTypeId, N'أخبار الاقتصاد اليوم (تجريبي)', N'تحديثات اقتصادية', N'<p>نص الخبر الاقتصادي</p>', 'Published', '2026-02-20', @CategoryId, @UserId, '2026-02-18 11:00:00', 320),
(@VideoTypeId, N'فيديو تعليمي لبرمجة الذكاء الاصطناعي (تجريبي)', N'شرح أساسيات الذكاء', N'<p>فيديو توضيحي</p>', 'Published', '2026-03-05', @CategoryId, @UserId, '2026-03-02 09:30:00', 450),
(@ArticleTypeId, N'كيف تحسن إنتاجيتك (تجريبي)', N'ملخص الإنتاجية', N'<p>خطوات عملية لتحسين الإنتاجية</p>', 'Published', '2026-04-10', @CategoryId, @UserId, '2026-04-05 08:15:00', 210),
(@NewsTypeId, N'تطورات الذكاء الاصطناعي هذا الشهر (تجريبي)', N'أبرز أحداث الذكاء الاصطناعي', N'<p>تحديثات مهمة جداً</p>', 'Published', '2026-05-12', @CategoryId, @UserId, '2026-05-10 14:00:00', 600),
(@VideoTypeId, N'مراجعة لأحدث الأجهزة الذكية (تجريبي)', N'مراجعة تقنية', N'<p>مراجعة كاملة</p>', 'Published', '2026-06-01', @CategoryId, @UserId, '2026-06-01 16:45:00', 330),

-- Current Month / Recent
(@ArticleTypeId, N'مقال جديد لهذا الأسبوع (تجريبي)', N'مقال الأسبوع الحالي', N'<p>نص تجريبي لمقال جديد</p>', 'Published', GETUTCDATE(), @CategoryId, @UserId, GETUTCDATE(), 50),
(@NewsTypeId, N'خبر عاجل اليوم (تجريبي)', N'خبر من اليوم', N'<p>تفاصيل الخبر العاجل...</p>', 'Published', GETUTCDATE(), @CategoryId, @UserId, GETUTCDATE(), 120),

-- Pending Approval
(@ArticleTypeId, N'مقال بانتظار المراجعة والاعتماد (تجريبي)', N'مقال غير منشور', N'<p>نص المقال المعلق</p>', 'Pending', NULL, @CategoryId, @UserId, DATEADD(day, -2, GETUTCDATE()), 0),
(@NewsTypeId, N'خبر جديد يتطلب تدقيق (تجريبي)', N'خبر غير منشور', N'<p>تفاصيل الخبر قيد التدقيق</p>', 'Pending', NULL, @CategoryId, @UserId, DATEADD(day, -5, GETUTCDATE()), 0),
(@VideoTypeId, N'فيديو مرسل للمراجعة (تجريبي)', N'فيديو غير منشور', N'<p>تفاصيل الفيديو المعلق</p>', 'Pending', NULL, @CategoryId, @UserId, DATEADD(day, -1, GETUTCDATE()), 0),

-- Scheduled
(@ArticleTypeId, N'مقال مجدول للنشر غداً (تجريبي)', N'مقال مستقبلي', N'<p>مقال يتم نشره في المستقبل</p>', 'Scheduled', DATEADD(day, 1, GETUTCDATE()), @CategoryId, @UserId, GETUTCDATE(), 0),
(@NewsTypeId, N'أخبار الأسبوع القادم مجدولة (تجريبي)', N'خبر مستقبلي', N'<p>خبر سيتم نشره الأسبوع القادم</p>', 'Scheduled', DATEADD(day, 7, GETUTCDATE()), @CategoryId, @UserId, GETUTCDATE(), 0),
(@VideoTypeId, N'فيديو حصري الشهر القادم (تجريبي)', N'فيديو مستقبلي', N'<p>فيديو سيتم إصداره الشهر القادم</p>', 'Scheduled', DATEADD(day, 30, GETUTCDATE()), @CategoryId, @UserId, GETUTCDATE(), 0);
