CREATE TABLE IF NOT EXISTS schema_versions (
    version INT NOT NULL PRIMARY KEY,
    applied_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS user_accounts (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) NOT NULL,
    display_name VARCHAR(100) NOT NULL,
    email VARCHAR(254) NULL,
    phone VARCHAR(30) NULL,
    password_hash VARCHAR(500) NOT NULL,
    role ENUM('Reader', 'Admin') NOT NULL DEFAULT 'Reader',
    is_disabled BOOLEAN NOT NULL DEFAULT FALSE,
    must_change_password BOOLEAN NOT NULL DEFAULT FALSE,
    is_bootstrap_admin BOOLEAN NOT NULL DEFAULT FALSE,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    UNIQUE KEY uq_user_username (username),
    UNIQUE KEY uq_user_email (email),
    KEY ix_user_role_enabled (role, is_disabled)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS books (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    isbn VARCHAR(20) NOT NULL,
    title VARCHAR(200) NOT NULL,
    author VARCHAR(150) NOT NULL,
    category VARCHAR(80) NULL,
    publisher VARCHAR(150) NULL,
    publication_year SMALLINT NULL,
    description TEXT NULL,
    is_archived BOOLEAN NOT NULL DEFAULT FALSE,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    UNIQUE KEY uq_book_isbn (isbn),
    KEY ix_book_title (title),
    KEY ix_book_author (author)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS book_copies (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    book_id BIGINT NOT NULL,
    copy_code VARCHAR(40) NOT NULL,
    shelf_location VARCHAR(80) NULL,
    is_disabled BOOLEAN NOT NULL DEFAULT FALSE,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    UNIQUE KEY uq_copy_code (copy_code),
    KEY ix_copy_book_id (book_id),
    CONSTRAINT fk_copy_book FOREIGN KEY (book_id) REFERENCES books(id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS loans (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    book_copy_id BIGINT NOT NULL,
    borrowed_at DATETIME(6) NOT NULL,
    due_at DATE NOT NULL,
    returned_at DATETIME(6) NULL,
    notes VARCHAR(500) NULL,
    active_copy_id BIGINT GENERATED ALWAYS AS (
        CASE WHEN returned_at IS NULL THEN book_copy_id ELSE NULL END
    ) STORED,
    UNIQUE KEY uq_active_copy_loan (active_copy_id),
    KEY ix_loan_user (user_id),
    KEY ix_loan_copy (book_copy_id),
    KEY ix_loan_due (due_at),
    CONSTRAINT fk_loan_user FOREIGN KEY (user_id) REFERENCES user_accounts(id) ON DELETE RESTRICT,
    CONSTRAINT fk_loan_copy FOREIGN KEY (book_copy_id) REFERENCES book_copies(id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS admin_applications (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    status ENUM('Pending', 'Approved', 'Rejected') NOT NULL DEFAULT 'Pending',
    submitted_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    reviewed_at DATETIME(6) NULL,
    reviewed_by_user_id BIGINT NULL,
    KEY ix_application_status (status),
    KEY ix_application_user (user_id),
    CONSTRAINT fk_application_user FOREIGN KEY (user_id) REFERENCES user_accounts(id) ON DELETE CASCADE,
    CONSTRAINT fk_application_reviewer FOREIGN KEY (reviewed_by_user_id) REFERENCES user_accounts(id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO schema_versions (version)
VALUES (1)
ON DUPLICATE KEY UPDATE version = VALUES(version);
