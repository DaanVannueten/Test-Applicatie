# TestPlanManager - Technische Uitleg En Werkmethode

## 1. Doel van de applicatie
TestPlanManager is een ASP.NET Core MVC applicatie om testcycli (builds/templates), testcategorieen en individuele testcases te beheren.

Belangrijkste doelen:
- centraal overzicht van teststatus per build
- opvolging van testuitvoering (NotRun, Passed, Failed, Blocked)
- onderscheid tussen InScope en OutOfScope tests
- rollen en rechten voor veilig beheer

## 2. Architectuur in het kort
De applicatie volgt een klassieke MVC-opbouw:
- Models: domeinobjecten zoals Sprint, TestCategory, Test
- Views: Razor paginas voor UI
- Controllers: business flow en request handling
- Data: Entity Framework Core DbContext en migrations

Belangrijke bestanden:
- Program startup en configuratie: Program.cs
- DbContext: Data/TestPlanContext.cs
- Dashboard flow: Controllers/HomeController.cs
- Testbeheer UI flow: Controllers/TestCategoryMvcController.cs

## 3. Dataopslag
De applicatie gebruikt SQLite.
- hoofd databasebestand: App_Data/TestPlan.db
- provider: Microsoft.EntityFrameworkCore.Sqlite
- ORM: Entity Framework Core

### 3.1 Mediaopslag
Media (beeld/video) wordt opgeslagen op disk onder:
- wwwroot/uploads/test-media

In de database wordt enkel het pad opgeslagen (MediaUrl), bijvoorbeeld:
- /uploads/test-media/20260320153000123_xxx.jpg

Waarom deze aanpak:
- statische bestanden blijven toegankelijk via UseStaticFiles
- data in DB blijft licht (geen grote blobs)

## 4. Rollen en rechten
Rollen:
- Administrator
- Test Manager
- Tester

Rechten die nu expliciet afgedwongen zijn:
- tester mag geen testcases aanmaken
- create test is enkel voor managerrollen (Administrator/Test Manager)

Dit is afgedwongen in:
- UI (knop alleen zichtbaar voor managers)
- backend (Authorize op CreateTest acties)

## 5. Belangrijkste functionele flow
### 5.1 Dashboard
Bestand: Controllers/HomeController.cs

Wat gebeurt er:
- gekozen sprint wordt geladen
- categorieen en tests worden ingeladen
- aggregaties worden herberekend
- dashboard metrics worden samengesteld

Belangrijke regel:
- Passed percentage gebruikt nu in-scope noemer
- formule: passedInScope / inScopeTotal * 100

### 5.2 Testcategorie details
Bestanden:
- Controllers/TestCategoryMvcController.cs
- Views/TestCategoryMvc/Details.cshtml

Wat gebeurt er:
- overzicht van tests per categorie
- visualisatie van voortgang
- acties op basis van rol

### 5.3 Test bewerken + media upload
Bestanden:
- Controllers/TestCategoryMvcController.cs
- Views/TestCategoryMvc/EditTest.cshtml

Wat gebeurt er bij opslaan:
1. validatie van basisvelden
2. optioneel: removeMedia -> bestaand lokaal bestand verwijderen
3. optioneel: nieuw bestand uploaden
4. validatie upload (extensie + max 50MB)
5. opslaan in wwwroot/uploads/test-media met unieke bestandsnaam
6. MediaUrl op testrecord bijwerken

## 6. Waarom media vroeger verdween en hoe dat is opgelost
### Probleem
- uploadveld bestond in UI, maar backend verwerkte bestand niet
- MediaUrl kon overschreven worden naar leeg bij edit

### Oplossing
- IFormFile mediaFile toegevoegd in CreateTest en EditTest
- helpermethodes toegevoegd:
  - TryValidateMediaFile
  - SaveMediaFileAsync
  - DeleteMediaFileIfLocal
- bestaande media blijft behouden als er geen nieuwe upload is

## 7. Propere werkwijze (wat en waarom)
Ik werk volgens volgende stappen:
1. eerst probleem afbakenen met gerichte code-inspectie
2. oorzaak valideren in controller + view
3. fix op backend en UI toepassen (defense in depth)
4. compilecheck uitvoeren met dotnet build
5. repository opruimen

Concreet opgeschoond:
- tijdelijke hulpscripts onder .tmp verwijderd
- .gitignore uitgebreid met .tmp/ om herhaling te voorkomen

## 8. Kwaliteitskeuzes
Toegepaste kwaliteitsprincipes:
- single source of truth voor rechten in backend
- expliciete validatie van uploads
- unieke bestandsnamen om collisions te vermijden
- delete van vervangen media om orphan files te beperken
- build-validatie na wijzigingen

## 9. Hoe je dit kan aantonen aan docent
### 9.1 Technische demonstratie
Toon deze scenario's:
1. Login als Tester:
- Add Test knop is niet zichtbaar
- directe create test URL geeft geen toegang

2. Login als Test Manager:
- Add Test knop zichtbaar
- testcase aanmaken lukt

3. Media demo:
- upload image/video op test
- bestand verschijnt in wwwroot/uploads/test-media
- test heropenen: media blijft zichtbaar
- applicatie herstarten: media blijft zichtbaar

4. Dashboard demo:
- zet een deel OutOfScope
- Passed % reageert op in-scope noemer

### 9.2 Build bewijs
Gebruik:

```powershell
dotnet build
```

Verwacht resultaat:
- Build succeeded

## 10. Volgende verbeteringen (optioneel)
- server-side mime type inspectie naast extensiecheck
- periodic cleanup job voor echt ongebruikte uploads
- audit logging per statuswijziging
- unit/integration tests rond media upload flow

---
Dit document beschrijft de werking, de keuzes en de concrete manier van werken zodat de code-review en mondelinge verdediging duidelijk en controleerbaar zijn.
