# Students Tracker

Веб-приложение учёта посещаемости студентов. Курсовая работа, ХПИ ТТУ, группа 2_40.03.01ра.

Три роли: администратор (справочники, учётные записи, исправление отметок), преподаватель (занятия и отметка посещаемости, отчёты по своим группам), студент (своя посещаемость).

## Стек

ASP.NET Core 10 (контроллеры), EF Core 10 + Npgsql, PostgreSQL 18, HTML/CSS/JavaScript без фреймворков (`wwwroot`).

## Быстрый старт

```powershell
cd src\StudentsTracker.Web
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=students_tracker;Username=postgres;Password=ПАРОЛЬ"
dotnet user-secrets set "Admin:Login" "admin"
dotnet user-secrets set "Admin:Password" "ПАРОЛЬ_ОТ_8_СИМВОЛОВ"
dotnet run --urls http://localhost:5290
```

Затем откройте http://localhost:5290. Подробности, демо-данные и восстановление базы: [docs/RUN.md](docs/RUN.md). Тесты: `cd src` и `dotnet test`.

## Структура

| Путь | Содержимое |
|---|---|
| `src/` | Решение .NET: `StudentsTracker.Web` (API и интерфейс), `StudentsTracker.Tests` |
| `db/` | `schema.sql`, `seed.sql` |
| `docs/` | `SPEC.md` (спецификация), `PLAN.md` (план и факт), `RUN.md` (запуск), `DEFENSE.md` (план показа), печатный документ, диаграммы, скриншоты |
| `mockups/` | Макеты и `!DESIGN_TOKENS.html` |
