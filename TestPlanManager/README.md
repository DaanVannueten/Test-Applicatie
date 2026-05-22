# TestPlanManager

TestPlanManager is een ASP.NET Core MVC webapplicatie voor het beheren en opvolgen van testplannen per versie/build (sprint).

De applicatie laat je:
- versies (builds) aanmaken, kopieren, als standaard instellen en verwijderen;
- testcategorieen beheren per versie;
- testcases beheren per categorie;
- teststatussen updaten en dashboard-metrics in real time bekijken.

What's New (recent)
- Consistente en duidelijkere validatie voor "build" / template / cycle namen: invoer accepteert alleen letters, cijfers, punten (.), underscores (_) en hyphens (-). Spaties en andere karakters worden geblokkeerd en tonen een begrijpelijke foutmelding.
- Client-side HTML5 pattern + title toegevoegd op relevante invoervelden zodat gebruikers direct een hint krijgen bij ongeldige invoer.
- Server-side validatie meldt nu concrete foutteksten terug (ModelState-meldingen) in plaats van alleen een generieke foutboodschap; modals blijven open zodat de gebruiker kan corrigeren zonder gegevens te verliezen.
- Template -> Create Cycle flow: formulier en controller (`TemplateController.CreateCycle`) trimmen invoer en tonen valide foutmeldingen wanneer nodig.
- TestPlanVersion modal voor "Create template from cycle" toont serverfouten in de modal en behoudt eerder ingevoerde waarden.

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
12. Unit tests
13. Bekende aandachtspunten

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
# TestPlanManager

TestPlanManager is een ASP.NET Core MVC webapplicatie om testplannen en testuitvoeringen per versie/build (sprint) te beheren. De applicatie is gericht op snelle lokale opzet, eenvoudige rollen/autoriteit en herbruikbare test-templates.

Doelgroep: testmanagers, testers en ontwikkelaars die testcases en testcycli per release willen organiseren en rapporteren.

Belangrijkste functionaliteit
- Versies (builds) aanmaken, kopiëren (van templates) en archiveren
- Templates maken van bestaande cycles en templates gebruiken om nieuwe testcycli te starten
- Testcategorieën en testcases beheren (CRUD)
- Testuitvoering bijhouden: status (NotRun / Passed / Failed / Blocked), uitvoerder en timestamp
- Dashboard en KPI's per versie: totaal tests, uitgevoerd, pass%, failed, blocked
- Rollen: Administrator, TestManager, Tester met verschillende rechten in de UI
- Eenvoudige REST API endpoints voor integratie of automatisering

What's New / Recent wijzigingen
- Consistente validatie voor build / template / cycle namen: alleen A–Z, a–z, 0–9, `.`, `_`, `-` zijn toegestaan; spaties en speciale tekens zijn niet toegestaan.
- Client-side HTML5 pattern + title toegevoegd op de relevante invoervelden zodat gebruikers direct feedback krijgen.
- Server-side validatie retourneert nu concrete ModelState-meldingen zodat de UI duidelijke foutteksten toont (bijv. in modals).

Quick start (lokaal)
1. Zorg dat `.NET SDK` (compatibel met `net10.0`) geïnstalleerd is.
2. Clone de repo en navigeer naar projectmap:

```bash
git clone <repo-url>
cd TestPlanManager
```

3. Restore, build en run:

```bash
dotnet restore
dotnet build
dotnet run
```

4. Open de browser op `http://localhost:5007` of `https://localhost:5008` (zie `Properties/launchSettings.json`).

Configuratie
- Connection string staat in `appsettings.json` als `ConnectionStrings:DefaultConnection`. Standaard wijst deze naar `App_Data/TestPlan.db`.
- Seed admin credentials (optioneel, development): gebruik `dotnet user-secrets` of environment variables met keys `SeedAdmin:Email` en `SeedAdmin:Password`.
- Omgevingsvariabelen ondersteund van `Program.cs`: `APP_URL`, `PORT`.

Databases en migraties
- SQLite-bestand: `App_Data/TestPlan.db` (lokale data; niet committen).
- EF Core migraties staan in de map `Migrations/`.
- Gebruik `dotnet ef migrations add <Name>` en `dotnet ef database update` voor schemawijzigingen.

Validatie en UI gedrag
- Validatieregels voor build/template namen zijn gedefinieerd in viewmodels (`Models/TestPlanVersionViewModels.cs` en `Models/TemplateViewModels.cs`) met een `RegularExpression` attribuut en duidelijke fouttekst.
- Views gebruiken HTML5 `pattern` en `title` attributes in de invoervelden (`Views/TestPlanVersion/Index.cshtml`, `Views/Template/Details.cshtml`) om snelle client-side hints te geven.
- POST-acties (bijv. `TestPlanVersionController`, `TemplateController`) trimmen invoer en retourneren specifieke ModelState-fouten zodat modals kunnen blijven openstaan en gebruikers hun invoer kunnen corrigeren.

Development workflow
- Build: `dotnet build`
- Run: `dotnet run`
- Tests: `dotnet test TestPlanManager.sln` of `dotnet test TestPlanManager.Tests/TestPlanManager.Tests.csproj`

Developer notes
- Belangrijke locaties:
  - `Program.cs` — app startup en DI
  - `Controllers/` — MVC en API controllers
  - `Models/` — domeinmodellen, enums, viewmodels
  - `Data/TestPlanContext.cs` — EF Core mapping, cascade rules
  - `Views/` — Razor pages en client scripts
- Hou validatieboodschappen consistent: verander zowel de `RegularExpression` ErrorMessage als de `title` in de view bij updates.

Contributie en style
- Fork & PR workflow. Run unit tests lokaal voordat je een PR opent.
- Code stijl volgt standaard C# conventies; gebruik `dotnet-format` of je IDE formatter voor consistente stijl.

Contact
- Voor vragen of hulp met local setup: overleg met het team of open een issue in de repository.

Licentie
- (Voeg hier de licentie toe indien van toepassing, of verwijder dit gedeelte.)

----

Bestanden genoemd in deze README:
- `Program.cs`, `appsettings.json`, `Models/`, `Controllers/`, `Views/`, `Data/TestPlanContext.cs`, `Migrations/`, `App_Data/`.
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

## 12. Unit tests

De unit tests staan in het aparte testproject:
- `TestPlanManager.Tests/`

### Wat wordt getest

- Domeinlogica:
  - `TestCategory.Recalculate()` (aggregaties, pass-percentage, status)
  - `TestTitleSanitizer.Clean(...)`
- Data utility:
  - `FileDefaultVersionStore` (missend bestand, corrupte JSON, null/waarde opslaan)
- Controller foutpaden:
  - API: `SprintController`, `TestController`, `TestCategoryController`
  - MVC: `HomeController`, `TestPlanVersionController`

De controller-tests gebruiken:
- EF Core InMemory database per test (`TestContextFactory`)
- Fake `HttpContext` user + rollen en `TempData` (`ControllerTestHelpers`)

### Tests runnen

Run alle tests in de solution:

```bash
dotnet test TestPlanManager.sln
```

Run alleen het testproject:

```bash
dotnet test TestPlanManager.Tests/TestPlanManager.Tests.csproj
```

Run met gedetailleerde output:

```bash
dotnet test TestPlanManager.sln -v normal
```

Run een specifiek testbestand (filter op class naam):

```bash
dotnet test TestPlanManager.sln --filter "FullyQualifiedName~TestControllerErrorTests"
```

Run 1 specifieke testmethode:

```bash
dotnet test TestPlanManager.sln --filter "FullyQualifiedName~UpdateStatus_ReturnsNotFound_WhenTestDoesNotExist"
```

### Coverage (optioneel)

Omdat `coverlet.collector` geinstalleerd is, kan je coverage verzamelen met:

```bash
dotnet test TestPlanManager.sln --collect:"XPlat Code Coverage"
```

Na de run vind je per testproject een `coverage.cobertura.xml` onder `TestResults/`.

## 13. Bekende aandachtspunten

## 12. Bekende aandachtspunten

- Er staat een `ProductionStatus` enum in `Models/Enums.cs` die momenteel niet actief gebruikt wordt in de logica.
- De app gebruikt lokale file-opslag (`App_Data/default-version.json`) voor default sprint keuze.
- `UseHttpsRedirection()` staat enkel aan buiten development, wat lokaal handig is maar in productie expliciet gecontroleerd moet blijven.

Developer notes
- Validatie voor build/template names staat in:
  - `Models/TestPlanVersionViewModels.cs` (`CreateVersionInputModel`, `CopyVersionInputModel`, `CreateTemplateFromCycleInputModel`)
  - `Models/TemplateViewModels.cs` (`CreateCycleFromTemplateInputModel`)
  - client-side patterns in `Views/TestPlanVersion/Index.cshtml` en `Views/Template/Details.cshtml`.
- Wanneer je validatieboodschappen aanpast, zorg dat zowel de `RegularExpression`-attribuuttekst als de `title` van het input-veld consistent blijven om verwarring te voorkomen.

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