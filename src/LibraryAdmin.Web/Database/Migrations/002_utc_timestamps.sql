UPDATE schema_versions
SET applied_at = DATE_SUB(applied_at, INTERVAL 8 HOUR)
WHERE version = 1;

UPDATE user_accounts
SET created_at = DATE_SUB(created_at, INTERVAL 8 HOUR),
    updated_at = DATE_SUB(updated_at, INTERVAL 8 HOUR);

UPDATE books
SET created_at = DATE_SUB(created_at, INTERVAL 8 HOUR),
    updated_at = DATE_SUB(updated_at, INTERVAL 8 HOUR);

UPDATE book_copies
SET created_at = DATE_SUB(created_at, INTERVAL 8 HOUR),
    updated_at = DATE_SUB(updated_at, INTERVAL 8 HOUR);

UPDATE admin_applications
SET submitted_at = DATE_SUB(submitted_at, INTERVAL 8 HOUR);
