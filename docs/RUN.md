# Запуск проекта с нуля

Инструкция проверена на Windows 11, .NET SDK 10.0.302 и PostgreSQL 18.

## 1. Что нужно установить

| Компонент | Версия |
|---|---|
| .NET SDK | 10 (LTS) |
| PostgreSQL | 18 (сервер слушает `localhost:5432`, есть пользователь `postgres` с паролем) |
| Git | любая свежая |

Пакеты (зафиксированы в `src/StudentsTracker.Web/StudentsTracker.Web.csproj`): Microsoft.EntityFrameworkCore 10.0.15, Microsoft.EntityFrameworkCore.Design 10.0.15, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.4. Они скачиваются автоматически при первой сборке, нужен интернет.

## 2. Получить код

```powershell
git clone https://github.com/globalsurveygpt-beep/students-tracker.git
cd students-tracker
git checkout develop
```

## 3. Настроить секреты

Пароль от PostgreSQL и пароль первого администратора хранятся не в git, а в пользовательских секретах .NET. Команды выполняются в папке проекта:

```powershell
cd src\StudentsTracker.Web
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=students_tracker;Username=postgres;Password=ПАРОЛЬ_POSTGRES"
dotnet user-secrets set "Admin:Login" "admin"
dotnet user-secrets set "Admin:Password" "ПАРОЛЬ_АДМИНА_ОТ_8_СИМВОЛОВ"
```

Логин администратора: латиница в нижнем регистре, цифры, точка, дефис, подчёркивание, от 3 до 50 символов.

## 4. Запустить

```powershell
dotnet run --urls http://localhost:5290
```

При первом запуске приложение само создаёт базу `students_tracker`, применяет миграции и создаёт первого администратора из `Admin:Login` и `Admin:Password` (только если в системе ещё нет ни одного администратора). Откройте http://localhost:5290 и войдите.

Проверка, что сервер жив: http://localhost:5290/api/health возвращает `{"status":"ok"}`.

## 5. Демо-данные (по желанию)

Скрипт `db/seed.sql` добавляет 3 группы, 24 студента, 5 предметов, 3 преподавателя, назначения, 28 занятий и отметки. Запускать после того, как приложение создало схему (шаг 4), либо после `db/schema.sql` (шаг 6).

```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U postgres -d students_tracker -v ON_ERROR_STOP=1 -f db\seed.sql
```

Повторный запуск отказывает с сообщением «Демо-данные уже загружены».

Все демо-учётные записи имеют пароль `Demo#2026ab`:

| Роль | Логин |
|---|---|
| Администратор | `demo.admin` |
| Преподаватель | `demo.nazarov`, `demo.yusupova`, `demo.sharipov` |
| Студент | `demo.azizov`, `demo.karimova` |

Даты занятий считаются от текущей даты, поэтому отчёты показывают последние две недели. У преподавателя `demo.nazarov` три последних занятия оставлены без отметки: на них удобно показать экран отметки.

## 6. Восстановление базы с нуля

Если нужно развернуть чистую базу без запуска приложения:

```powershell
$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"
& $psql -h localhost -U postgres -d postgres -c "DROP DATABASE IF EXISTS students_tracker"
& $psql -h localhost -U postgres -d postgres -c "CREATE DATABASE students_tracker ENCODING 'UTF8' TEMPLATE template0"
& $psql -h localhost -U postgres -d students_tracker -v ON_ERROR_STOP=1 -f db\schema.sql
& $psql -h localhost -U postgres -d students_tracker -v ON_ERROR_STOP=1 -f db\seed.sql
```

`db/schema.sql` сгенерирован из миграций EF (единственный источник схемы) и содержит таблицу `__EFMigrationsHistory`, поэтому приложение при старте не пытается применить миграции повторно. После изменения моделей и новой миграции файл нужно пересоздать:

```powershell
cd src\StudentsTracker.Web
dotnet ef migrations script -o ..\..\db\schema.sql
```

### Дамп перед защитой

Чтобы иметь точную копию рабочей базы на случай сбоя:

```powershell
& "C:\Program Files\PostgreSQL\18\bin\pg_dump.exe" -h localhost -U postgres -d students_tracker -F c -f db\students_tracker.dump
```

Восстановление из дампа в пустую базу:

```powershell
& "C:\Program Files\PostgreSQL\18\bin\pg_restore.exe" -h localhost -U postgres -d students_tracker --clean --if-exists db\students_tracker.dump
```

Файл дампа содержит данные пользователей и в git не добавляется.

## 7. Тесты

```powershell
cd src
dotnet test
```

Интеграционные тесты используют те же пользовательские секреты, но работают на отдельной базе `students_tracker_test`: она создаётся миграциями перед прогоном и удаляется после. Рабочая база `students_tracker` не затрагивается. Прогон проверяет вход и сессии, права трёх ролей, правила занятий и отметок, формулу посещаемости и ответы 409 при связанных данных.

## 8. Частые проблемы

| Симптом | Причина и решение |
|---|---|
| `No password has been provided but the backend requires one` | Не задан или неверен пароль в `ConnectionStrings:Default` (шаг 3) |
| Вход невозможен, в логе «Администратора нет, а Admin:Login / Admin:Password не заданы» | Не заданы секреты `Admin:*` (шаг 3) |
| Порт занят | Запустить с другим `--urls`, например `http://localhost:5291` |
| Тест падает с ошибкой строки подключения | Секреты заданы не в `src\StudentsTracker.Web` (шаг 3) |
| Не отображается шрифт | Файлы `wwwroot/fonts/*.woff2` не скачаны; интерфейс работает на системном шрифте |

## 9. Структура

```
docs/    PLAN.md, SPEC.md, RUN.md, DB_STRUCTURE.xlsx, диаграммы, печатный документ
mockups/ макеты HTML и !DESIGN_TOKENS.html
src/     StudentsTracker.Web (API и wwwroot), StudentsTracker.Tests
db/      schema.sql, seed.sql
```
