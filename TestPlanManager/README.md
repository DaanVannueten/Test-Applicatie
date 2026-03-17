# TestPlanManager

TestPlanManager is een ASP.NET Core MVC webapplicatie voor het beheren en opvolgen van testplannen per versie/build (sprint).

De applicatie laat je:
- versies (builds) aanmaken, kopieren, als standaard instellen en verwijderen;
- testcategorieen beheren per versie;
- testcases beheren per categorie;
- teststatussen updaten en dashboard-metrics in real time bekijken.

## Inhoud

1. Projectoverzicht
2. Technologieen
3. Architectuur
4. Mappenstructuur
5. Datamodel
6. Functionele flow
7. Belangrijkste routes en endpoints
8. Installatie en lokaal draaien
9. Database en migraties
10. Configuratie
11. Presentatiehulp (spreekpunten)
12. Bekende aandachtspunten

## 1. Projectoverzicht

Het project combineert:
- MVC pagina's voor dagelijks gebruik (dashboard, versiebeheer, detail/edit schermen);
- eenvoudige REST API-endpoints voor data-operaties;
- SQLite als lokale database via Entity Framework Core.

De kern van de app draait rond deze hiërarchie:
`Sprint (versie/build) -> TestCategory -> Test`.

## 2. Technologieen

- .NET: `net10.0`
- Framework: ASP.NET Core MVC
- ORM: Entity Framework Core `10.0.3`
- Database: SQLite (`App_Data/TestPlan.db`)
- Frontend: Razor views + Bootstrap + jQuery

Belangrijke dependencies staan in `TestPlanManager.csproj`.

## 3. Architectuur

### Startup en dependency injection

In `Program.cs`:
- `TestPlanContext` wordt geregistreerd met SQLite connectie (`DefaultConnection`);
- MVC services worden toegevoegd met `AddControllersWithViews()`;
- `IDefaultVersionStore` wordt als singleton geregistreerd via file-based implementatie (`FileDefaultVersionStore`).

### Runtime gedrag

Bij opstart:
- bestaande testtitels worden opgeschoond via `TestTitleSanitizer.Clean(...)`;
- in development probeert de app automatisch de browser te openen;
- optioneel kan host/poort via `APP_URL` of `PORT` environment variabelen gestuurd worden.

### Route setup

Naast de standaardroute zijn er nette, leesbare custom routes:
- `test-categories/{id}`
- `test-categories/{id}/edit`
- `test-categories/{testCategoryId}/tests/new`

## 4. Mappenstructuur

Kernmappen:

- `Controllers/`
  - MVC + API controllers
- `Data/`
  - EF DbContext en default-versie opslag
- `Models/`
  - entities, enums, DTO's, viewmodels, inputmodels
- `Migrations/`
  - EF Core migratiehistoriek
- `Views/`
  - Razor pagina's
- `wwwroot/`
  - statische assets (css/js/libs)
- `App_Data/`
  - runtime appdata bestanden zoals `default-version.json` (niet committen)

Belangrijke rootbestanden:
- `Program.cs`
- `appsettings.json`
- `TestPlanManager.csproj`

## 5. Datamodel

### Sprint

Bestand: `Models/Sprint.cs`
- `SprintId` (PK)
- `BuildNr`
- `TestCategories` (1:N)

### TestCategory

Bestand: `Models/TestCategory.cs`
- `TestCategoryId` (PK)
- `SprintId` (FK)
- `Name`, `Description`, `Department`, `Sequence`
- geaggregeerde velden: `TotalTest`, `Passed`, `Failed`, `Blocked`, `OutOfScope`, `PercentagePassed`
- `TestDate` (laatste uitvoerdatum in categorie)
- `Tests` (1:N)

`Recalculate()` berekent de aggregaten op basis van onderliggende tests.

### Test

Bestand: `Models/Test.cs`
- `TestId` (PK)
- `TestCategoryId` (FK)
- `Name`, `Description`
- `ScopeStatus` (`InScope`/`OutOfScope`)
- `ExecutionStatus` (`NotRun`/`Passed`/`Failed`/`Blocked`)
- `Production`, `Comments`, `MediaUrl`
- `ExecutedAt` (UTC timestamp bij uitvoering)
- `LastExecutedBy` (laatste gebruiker die de test uitvoerde)

### Enum mapping

In `Data/TestPlanContext.cs` worden enums als strings opgeslagen voor leesbaarheid in de database.

### Relaties en delete gedrag

In `Data/TestPlanContext.cs`:
- Sprint -> TestCategory: cascade delete
- TestCategory -> Test: cascade delete

## 6. Functionele flow

### Dashboard

Controller: `Controllers/HomeController.cs`, view: `Views/Home/Index.cshtml`

- Laadt versies met categorieen en tests.
- Kiest actieve versie op basis van:
  1. `sprintId` query parameter,
  2. opgeslagen default versie,
  3. anders nieuwste/eerste versie.
- Berekent metrics:
  - totaal aantal tests,
  - aantal uitgevoerde tests,
  - completed categories (`afgerond / totaal`),
  - open/failed teller,
  - quality percentages (Passed/Failed/Blocked/OOS).
- Ondersteunt globale reset: alle tests naar `NotRun` voor gekozen versie.
- Toont klikbare breadcrumbs voor snellere terugnavigatie tussen niveaus.

### Versiebeheer

Controller: `Controllers/TestPlanVersionController.cs`, view: `Views/TestPlanVersion/Index.cshtml`

Mogelijkheden:
- lege versie aanmaken (`Create`)
- versie kopieren (`Copy`) van bestaande sprint
- default versie zetten (`SetDefault`)
- versie verwijderen (`Delete`)
- ungrouped cycles als inklapbare sectie tonen
- archived builds openen via aparte lijst (`Archived`)

Default versie wordt bewaard in `App_Data/default-version.json`.

### Categoriebeheer (MVC)

Controller: `Controllers/TestCategoryMvcController.cs`

Mogelijkheden:
- categorie details (`Details`)
- categorie aanmaken (`CreateCategory` GET/POST)
- categorie wijzigen (`EditCategory` GET/POST)
- categorie verwijderen (`DeleteCategory`)
- breadcrumbs tonen op create/edit/detail voor stap-voor-stap navigatie

### Testbeheer (MVC)

Ook in `TestCategoryMvcController`:
- test aanmaken (`CreateTest` GET/POST)
- test wijzigen (`EditTest` GET/POST)
- test verwijderen (`DeleteTest`)
- breadcrumbs tonen op create/edit voor snelle terugnavigatie

Bij statuswijzigingen:
- `ExecutedAt` wordt gezet/gereset;
- `TestDate` van de categorie wordt opnieuw afgeleid uit de laatste uitgevoerde test.

## 7. Belangrijkste routes en endpoints

### MVC routes

- `GET /Home/Index?sprintId={id}`: dashboard
- `GET /Home/Index?sprintId={id}&includeTemplates=true`: template-dashboard view
- `GET /TestPlanVersion/Index`: versiebeheer
- `GET /TestPlanVersion/Archived`: archived builds overzicht
- `GET /test-categories/{id}`: categorie detail
- `GET /test-categories/{id}/edit`: test bewerken
- `GET /test-categories/{testCategoryId}/tests/new`: nieuwe test

### API endpoints

`SprintController` (`/api/sprint`):
- `GET /api/sprint`
- `GET /api/sprint/{id}`
- `POST /api/sprint`
- `PUT /api/sprint/{id}`
- `DELETE /api/sprint/{id}`

`TestCategoryController` (`/api/testcategory`):
- `GET /api/testcategory/{id}`
- `GET /api/testcategory/sprint/{sprintId}/summary`
- `POST /api/testcategory`

`TestController` (`/api/test`):
- `GET /api/test/{id}`
- `POST /api/test`
- `PUT /api/test/{id}/status`

## 8. Installatie en lokaal draaien

### Vereisten

- .NET SDK geschikt voor `net10.0`
- Windows/macOS/Linux met `dotnet` CLI

### Starten

```bash
dotnet restore
dotnet build
dotnet run
```

Standaard development URLs staan in `Properties/launchSettings.json`:
- `http://localhost:5007`
- `https://localhost:5008`

## 9. Database en migraties

### Huidige database

- SQLite bestand: lokaal gegenereerd `App_Data/TestPlan.db` (niet committen)
- EF migraties: map `Migrations/`

### Handige EF commando's

```bash
# nieuwe migratie maken
dotnet ef migrations add <NaamMigratie>

# migraties toepassen op database
dotnet ef database update
```

## 10. Configuratie

### appsettings

Bestand: `appsettings.json`
- `ConnectionStrings:DefaultConnection = Data Source=App_Data/TestPlan.db`

De applicatie resolve't dit pad expliciet vanaf de project-root, zodat lokaal altijd dezelfde database gebruikt wordt, ook wanneer je de app vanuit een andere werkmap of tool start.

### Development secrets

Gebruik voor lokale seed-credentials `SeedAdmin:Email` en `SeedAdmin:Password` via user-secrets of environment variables in plaats van ze in `appsettings.Development.json` te committen.

```bash
dotnet user-secrets init
dotnet user-secrets set "SeedAdmin:Email" "admin@testplan.local"
dotnet user-secrets set "SeedAdmin:Password" "KiesEenSterkWachtwoord123!"
```

Als deze waarden niet gezet zijn, worden alleen de rollen geseed en geen default admin-account aangemaakt.

### Omgevingsvariabelen

Ondersteund in `Program.cs`:
- `APP_URL` (volledige url, heeft voorrang)
- `PORT` (fallback, luistert op `http://0.0.0.0:{PORT}`)

### Development beheercommando's

De CLI-commando's voor gebruikersbeheer in `Program.cs` zijn alleen beschikbaar in de `Development` environment:

```bash
dotnet run --no-launch-profile -- --list-users
dotnet run --no-launch-profile -- --ensure-user <email> <password> <role>
dotnet run --no-launch-profile -- --reset-password <email> <newPassword>
```

Deze commando's zijn bedoeld voor lokale recovery en troubleshooting, niet voor productiegebruik.

## 11. Presentatiehulp (spreekpunten)

Gebruik onderstaande structuur om vlot te presenteren:

1. Probleem dat de app oplost
- "We centraliseren testopvolging per build en maken progressie direct zichtbaar op 1 dashboard."

2. Kernconcept
- "Onze data is hiërarchisch: Build -> Categorie -> Testcase."

3. Demo flow
- Open `Versions` en toon create/copy/default.
- Open een build op dashboard en toon KPI's + filters.
- Open een categorie, wijzig 1 teststatus, toon dat cijfers mee evolueren.

4. Technische keuzes
- "ASP.NET MVC voor UI + EF Core + SQLite voor eenvoudige lokale setup."
- "Enums slaan we als string op voor leesbare data."

5. Sterke punten
- "Snelle onboarding, duidelijke structuur, uitbreidbaar met extra statussen of rapportering."

## 12. Bekende aandachtspunten

- Er staat een `ProductionStatus` enum in `Models/Enums.cs` die momenteel niet actief gebruikt wordt in de logica.
- De app gebruikt lokale file-opslag (`App_Data/default-version.json`) voor default sprint keuze.
- `UseHttpsRedirection()` staat enkel aan buiten development, wat lokaal handig is maar in productie expliciet gecontroleerd moet blijven.

---

## Snelle projecttour (voor jezelf)

- Startpunt: `Program.cs`
- Datamodel: `Models/`
- Database mapping: `Data/TestPlanContext.cs`
- Dashboardlogica: `Controllers/HomeController.cs`
- Versiebeheer: `Controllers/TestPlanVersionController.cs`
- Categorie/Test UI flow: `Controllers/TestCategoryMvcController.cs`
- Hoofdschermen: `Views/Home/Index.cshtml`, `Views/TestPlanVersion/Index.cshtml`, `Views/TestCategoryMvc/Details.cshtml`

Als je dit document volgt tijdens je presentatie, kun je zowel functioneel als technisch sterk uitleggen hoe het project is opgebouwd.