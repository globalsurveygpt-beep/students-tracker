CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE groups (
    id integer GENERATED ALWAYS AS IDENTITY,
    name character varying(50) NOT NULL,
    start_year smallint NOT NULL,
    CONSTRAINT pk_groups PRIMARY KEY (id),
    CONSTRAINT ck_groups_start_year CHECK (start_year between 2000 and 2100)
);

CREATE TABLE subjects (
    id integer GENERATED ALWAYS AS IDENTITY,
    name character varying(200) NOT NULL,
    CONSTRAINT pk_subjects PRIMARY KEY (id)
);

CREATE TABLE users (
    id integer GENERATED ALWAYS AS IDENTITY,
    login character varying(50) NOT NULL,
    password_hash text NOT NULL,
    role character varying(16) NOT NULL,
    full_name character varying(200) NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamptz NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (id),
    CONSTRAINT ck_users_role CHECK (role in ('admin','teacher','student'))
);

CREATE TABLE lessons (
    id integer GENERATED ALWAYS AS IDENTITY,
    lesson_date date NOT NULL,
    pair_number smallint NOT NULL,
    group_id integer NOT NULL,
    subject_id integer NOT NULL,
    teacher_id integer NOT NULL,
    topic character varying(500),
    CONSTRAINT pk_lessons PRIMARY KEY (id),
    CONSTRAINT ck_lessons_pair_number CHECK (pair_number between 1 and 8),
    CONSTRAINT fk_lessons_groups_group_id FOREIGN KEY (group_id) REFERENCES groups (id) ON DELETE RESTRICT,
    CONSTRAINT fk_lessons_subjects_subject_id FOREIGN KEY (subject_id) REFERENCES subjects (id) ON DELETE RESTRICT,
    CONSTRAINT fk_lessons_users_teacher_id FOREIGN KEY (teacher_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE students (
    id integer GENERATED ALWAYS AS IDENTITY,
    full_name character varying(200) NOT NULL,
    group_id integer NOT NULL,
    user_id integer,
    CONSTRAINT pk_students PRIMARY KEY (id),
    CONSTRAINT fk_students_groups_group_id FOREIGN KEY (group_id) REFERENCES groups (id) ON DELETE RESTRICT,
    CONSTRAINT fk_students_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE teaching_assignments (
    id integer GENERATED ALWAYS AS IDENTITY,
    teacher_id integer NOT NULL,
    group_id integer NOT NULL,
    subject_id integer NOT NULL,
    CONSTRAINT pk_teaching_assignments PRIMARY KEY (id),
    CONSTRAINT fk_teaching_assignments_groups_group_id FOREIGN KEY (group_id) REFERENCES groups (id) ON DELETE RESTRICT,
    CONSTRAINT fk_teaching_assignments_subjects_subject_id FOREIGN KEY (subject_id) REFERENCES subjects (id) ON DELETE RESTRICT,
    CONSTRAINT fk_teaching_assignments_users_teacher_id FOREIGN KEY (teacher_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE attendance (
    id integer GENERATED ALWAYS AS IDENTITY,
    lesson_id integer NOT NULL,
    student_id integer NOT NULL,
    status character varying(16) NOT NULL,
    note character varying(500),
    CONSTRAINT pk_attendance PRIMARY KEY (id),
    CONSTRAINT ck_attendance_status CHECK (status in ('present','absent','excused','late')),
    CONSTRAINT fk_attendance_lessons_lesson_id FOREIGN KEY (lesson_id) REFERENCES lessons (id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendance_students_student_id FOREIGN KEY (student_id) REFERENCES students (id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX ix_attendance_lesson_id_student_id ON attendance (lesson_id, student_id);

CREATE INDEX ix_attendance_student_id_lesson_id ON attendance (student_id, lesson_id);

CREATE UNIQUE INDEX ix_groups_name ON groups (name);

CREATE UNIQUE INDEX ix_lessons_group_id_lesson_date_pair_number ON lessons (group_id, lesson_date, pair_number);

CREATE INDEX ix_lessons_subject_id ON lessons (subject_id);

CREATE INDEX ix_lessons_teacher_id ON lessons (teacher_id);

CREATE INDEX ix_students_group_id ON students (group_id);

CREATE UNIQUE INDEX ix_students_user_id ON students (user_id);

CREATE UNIQUE INDEX ix_subjects_name ON subjects (name);

CREATE INDEX ix_teaching_assignments_group_id ON teaching_assignments (group_id);

CREATE INDEX ix_teaching_assignments_subject_id ON teaching_assignments (subject_id);

CREATE UNIQUE INDEX ix_teaching_assignments_teacher_id_group_id_subject_id ON teaching_assignments (teacher_id, group_id, subject_id);

CREATE UNIQUE INDEX ix_users_login ON users (login);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261125135018_InitialCreate', '10.0.15');

COMMIT;

