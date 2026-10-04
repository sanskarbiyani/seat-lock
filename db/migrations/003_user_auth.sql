ALTER TABLE users
    ADD COLUMN email VARCHAR(320),
    ADD COLUMN password_hash TEXT,
    ADD COLUMN role VARCHAR(20) NOT NULL DEFAULT 'user';

ALTER TABLE users
    ADD CONSTRAINT users_role_check
    CHECK (role IN ('user', 'admin'));

CREATE UNIQUE INDEX ux_users_email
    ON users (LOWER(email));