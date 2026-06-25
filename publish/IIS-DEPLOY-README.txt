TalaPress — IIS Deploy (talapress.online)
=========================================

Publish output folder: publish\talapress.online\

1) SERVER REQUIREMENTS
   - Windows Server with IIS
   - .NET 8.0 ASP.NET Core Hosting Bundle (InProcess)
     https://dotnet.microsoft.com/download/dotnet/8.0
   - SQL Server reachable from the server

2) UPLOAD
   - Copy ALL files from publish\talapress.online\ to the IIS site physical path
     (e.g. C:\inetpub\talapress.online\)
   - Do NOT upload appsettings.Development.json

3) IIS SITE
   - Site name: talapress.online (or your choice)
   - Binding: https talapress.online (+ www if needed)
   - Application pool:
       .NET CLR version: No Managed Code
       Identity: ApplicationPoolIdentity (or dedicated account with uploads write access)
   - Physical path: folder containing TalaPress.dll + web.config

4) CONNECTION STRING (required before first run)
   Option A — edit on server (after upload):
     appsettings.Production.json → ConnectionStrings:DefaultConnection

   Option B — IIS Environment Variable (recommended):
     Name:  ConnectionStrings__DefaultConnection
     Value: Server=...;Database=TalaPress;User Id=...;Password=...;TrustServerCertificate=True;MultipleActiveResultSets=True;

5) UPLOADS FOLDER
   - Ensure write permission on wwwroot\uploads for the app pool identity

6) FIRST RUN
   - Browse https://talapress.online
   - DatabaseInitializer runs migrations on startup
   - Default admin (admin/admin) is NOT seeded in Production — create users in SQL or temporarily use Development (not recommended)

7) SSL
   - Install certificate for talapress.online in IIS bindings

8) RE-PUBLISH (from dev machine)
   dotnet publish -c Release -r win-x64 --self-contained false -o publish\talapress.online /p:AspNetCoreHostingModel=InProcess /p:EnvironmentName=Production

   Or: dotnet publish /p:PublishProfile=IIS-talapress-online
