-- Демо-данные для показа системы. Запускать на базе со схемой (db/schema.sql или миграции приложения).
-- Все демо-учётные записи имеют один пароль: Demo#2026ab (только для демонстрации, на рабочей системе не использовать).
-- Даты занятий считаются от текущей даты, поэтому отчёты всегда за последние две недели.
SET client_encoding = 'UTF8';

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM users WHERE login = 'demo.admin') THEN
        RAISE EXCEPTION 'Демо-данные уже загружены (есть пользователь demo.admin)';
    END IF;
END $$;

BEGIN;

INSERT INTO groups (name, start_year) VALUES
    ('ИС-21', 2021), ('ИС-22', 2022), ('ПЭ-23', 2023);

INSERT INTO subjects (name) VALUES
    ('Базы данных'), ('Веб-программирование'), ('Экономика организации'), ('Математика'), ('Английский язык');

INSERT INTO users (login, password_hash, role, full_name, is_active, created_at) VALUES
    ('demo.admin',    'AQAAAAIAAYagAAAAEMjzpYVoITXA4CJEzzkI2yR7ihJt6lAuWKFuLbN1t6BRdDHYIfcxKbgbMUOc5QpQDQ==', 'admin',   'Хасанова Дилноза Рустамовна', true, now()),
    ('demo.nazarov',  'AQAAAAIAAYagAAAAEMjzpYVoITXA4CJEzzkI2yR7ihJt6lAuWKFuLbN1t6BRdDHYIfcxKbgbMUOc5QpQDQ==', 'teacher', 'Назаров Бахтиёр Каримович',   true, now()),
    ('demo.yusupova', 'AQAAAAIAAYagAAAAEMjzpYVoITXA4CJEzzkI2yR7ihJt6lAuWKFuLbN1t6BRdDHYIfcxKbgbMUOc5QpQDQ==', 'teacher', 'Юсупова Малика Олимовна',     true, now()),
    ('demo.sharipov', 'AQAAAAIAAYagAAAAEMjzpYVoITXA4CJEzzkI2yR7ihJt6lAuWKFuLbN1t6BRdDHYIfcxKbgbMUOc5QpQDQ==', 'teacher', 'Шарипов Рустам Ахмадович',    true, now()),
    ('demo.azizov',   'AQAAAAIAAYagAAAAEMjzpYVoITXA4CJEzzkI2yR7ihJt6lAuWKFuLbN1t6BRdDHYIfcxKbgbMUOc5QpQDQ==', 'student', 'Азизов Фарход Шерович',       true, now()),
    ('demo.karimova', 'AQAAAAIAAYagAAAAEMjzpYVoITXA4CJEzzkI2yR7ihJt6lAuWKFuLbN1t6BRdDHYIfcxKbgbMUOc5QpQDQ==', 'student', 'Каримова Нодира Алиевна',     true, now());

INSERT INTO students (full_name, group_id, user_id)
SELECT v.full_name, g.id, u.id
FROM (VALUES
    ('Азизов Фарход Шерович',        'ИС-21', 'demo.azizov'),
    ('Каримова Нодира Алиевна',      'ИС-21', 'demo.karimova'),
    ('Рахимов Далер Тоирович',       'ИС-21', NULL),
    ('Саидова Мехрубон Исмоиловна',  'ИС-21', NULL),
    ('Холиқов Умар Ильхомович',      'ИС-21', NULL),
    ('Мирзоев Сухроб Анварович',     'ИС-21', NULL),
    ('Иванова Анна Сергеевна',       'ИС-21', NULL),
    ('Давлатов Шахзод Бахромович',   'ИС-21', NULL),
    ('Назарова Зарина Фаридуновна',  'ИС-21', NULL),
    ('Олимов Фирдавс Рустамович',    'ИС-22', NULL),
    ('Петров Дмитрий Андреевич',     'ИС-22', NULL),
    ('Шарипова Мадина Комиловна',    'ИС-22', NULL),
    ('Хайдаров Исмоил Наимович',     'ИС-22', NULL),
    ('Юнусова Гулноза Шавкатовна',   'ИС-22', NULL),
    ('Раджабов Амир Джамшедович',    'ИС-22', NULL),
    ('Курбонова Ситора Олимовна',    'ИС-22', NULL),
    ('Ахмедов Рустам Бахтиёрович',   'ИС-22', NULL),
    ('Собиров Комрон Фарходович',    'ПЭ-23', NULL),
    ('Сидорова Елена Викторовна',    'ПЭ-23', NULL),
    ('Гафуров Бехруз Абдуллоевич',   'ПЭ-23', NULL),
    ('Ниёзова Парвина Хуршедовна',   'ПЭ-23', NULL),
    ('Тошматов Аброр Самадович',     'ПЭ-23', NULL),
    ('Латипова Фарзона Икромовна',   'ПЭ-23', NULL),
    ('Бобоев Мухаммад Зафарович',    'ПЭ-23', NULL)
) AS v(full_name, grp, login)
JOIN groups g ON g.name = v.grp
LEFT JOIN users u ON u.login = v.login;

INSERT INTO teaching_assignments (teacher_id, group_id, subject_id)
SELECT u.id, g.id, s.id
FROM (VALUES
    ('demo.nazarov',  'ИС-21', 'Базы данных'),
    ('demo.nazarov',  'ИС-21', 'Веб-программирование'),
    ('demo.nazarov',  'ИС-22', 'Базы данных'),
    ('demo.yusupova', 'ИС-21', 'Экономика организации'),
    ('demo.yusupova', 'ПЭ-23', 'Экономика организации'),
    ('demo.sharipov', 'ИС-22', 'Математика'),
    ('demo.sharipov', 'ПЭ-23', 'Английский язык')
) AS v(login, grp, subj)
JOIN users u ON u.login = v.login
JOIN groups g ON g.name = v.grp
JOIN subjects s ON s.name = v.subj;

-- Четыре занятия по каждому назначению: 3, 7, 10 и 14 дней назад. Час внутри дня различается по предметам группы.
INSERT INTO lessons (lesson_date, pair_number, group_id, subject_id, teacher_id, topic)
SELECT CURRENT_DATE - d.days,
       (row_number() OVER (PARTITION BY a.group_id, d.days ORDER BY a.subject_id))::smallint,
       a.group_id, a.subject_id, a.teacher_id,
       s.name || ': занятие ' || d.n
FROM teaching_assignments a
JOIN subjects s ON s.id = a.subject_id
CROSS JOIN (VALUES (3, 4), (7, 3), (10, 2), (14, 1)) AS d(days, n);

-- Отметки: примерно 80 % «был», 8 % «не был», 7 % «опоздал», 5 % «уважительная».
-- Последнее занятие преподавателя Назарова оставлено без отметок, чтобы показать экран отметки.
INSERT INTO attendance (lesson_id, student_id, status)
SELECT l.id, s.id,
       CASE WHEN x.r < 80 THEN 'present' WHEN x.r < 88 THEN 'absent' WHEN x.r < 95 THEN 'late' ELSE 'excused' END
FROM lessons l
JOIN students s ON s.group_id = l.group_id
CROSS JOIN LATERAL (SELECT abs(hashtext(l.id::text || ':' || s.id::text)::bigint) % 100 AS r) x
WHERE NOT (l.lesson_date = CURRENT_DATE - 3
           AND l.teacher_id = (SELECT id FROM users WHERE login = 'demo.nazarov'));

COMMIT;
