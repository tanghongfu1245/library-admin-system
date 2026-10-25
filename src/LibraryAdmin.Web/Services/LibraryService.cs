using LibraryAdmin.Web.Data;
using LibraryAdmin.Web.Models;
using MySqlConnector;

namespace LibraryAdmin.Web.Services;

public sealed class LibraryService
{
    private readonly MySqlConnectionFactory _connectionFactory;

    public LibraryService(MySqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM books WHERE is_archived = FALSE) AS book_titles,
                (SELECT COUNT(*) FROM book_copies c JOIN books b ON b.id = c.book_id
                 WHERE b.is_archived = FALSE) AS total_copies,
                (SELECT COUNT(*) FROM book_copies c JOIN books b ON b.id = c.book_id
                 WHERE b.is_archived = FALSE AND c.is_disabled = FALSE
                   AND NOT EXISTS (SELECT 1 FROM loans l
                                   WHERE l.book_copy_id = c.id AND l.returned_at IS NULL)) AS available_copies,
                (SELECT COUNT(*) FROM loans WHERE returned_at IS NULL) AS active_loans,
                (SELECT COUNT(*) FROM loans WHERE returned_at IS NULL AND due_at < @today) AS overdue_loans,
                (SELECT COUNT(*) FROM user_accounts WHERE role = 'Reader' AND is_disabled = FALSE) AS readers;
            """;
        command.Parameters.AddWithValue("@today", DateOnly.FromDateTime(DateTime.Today));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new DashboardStats(
            Convert.ToInt32(reader["book_titles"]),
            Convert.ToInt32(reader["total_copies"]),
            Convert.ToInt32(reader["available_copies"]),
            Convert.ToInt32(reader["active_loans"]),
            Convert.ToInt32(reader["overdue_loans"]),
            Convert.ToInt32(reader["readers"]));
    }

    public async Task<PagedResult<Book>> SearchBooksAsync(
        string? query,
        bool includeArchived,
        int page,
        int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        var normalized = query?.Trim() ?? "";
        const string where = """
            WHERE (@includeArchived = TRUE OR b.is_archived = FALSE)
              AND (@query = '' OR b.title LIKE CONCAT('%', @query, '%')
                   OR b.author LIKE CONCAT('%', @query, '%')
                   OR b.isbn LIKE CONCAT('%', @query, '%')
                   OR b.category LIKE CONCAT('%', @query, '%'))
            """;

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var count = connection.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM books b {where}";
        count.Parameters.AddWithValue("@includeArchived", includeArchived);
        count.Parameters.AddWithValue("@query", normalized);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken));

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT b.*,
                   (SELECT COUNT(*) FROM book_copies c WHERE c.book_id = b.id) AS copy_count,
                   (SELECT COUNT(*) FROM book_copies c
                    WHERE c.book_id = b.id AND c.is_disabled = FALSE
                      AND NOT EXISTS (SELECT 1 FROM loans l
                                      WHERE l.book_copy_id = c.id AND l.returned_at IS NULL)) AS available_count,
                   (SELECT COUNT(*) FROM loans l
                    JOIN book_copies c ON c.id = l.book_copy_id
                    WHERE c.book_id = b.id AND l.returned_at IS NULL) AS active_loan_count
            FROM books b
            {where}
            ORDER BY b.is_archived, b.updated_at DESC, b.title
            LIMIT @pageSize OFFSET @offset;
            """;
        command.Parameters.AddWithValue("@includeArchived", includeArchived);
        command.Parameters.AddWithValue("@query", normalized);
        command.Parameters.AddWithValue("@pageSize", pageSize);
        command.Parameters.AddWithValue("@offset", (page - 1) * pageSize);

        var books = new List<Book>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            books.Add(MapBook(reader));
        }

        return new PagedResult<Book>(books, page, pageSize, total);
    }

    public async Task<Book?> GetBookAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT b.*,
                   (SELECT COUNT(*) FROM book_copies c WHERE c.book_id = b.id) AS copy_count,
                   (SELECT COUNT(*) FROM book_copies c
                    WHERE c.book_id = b.id AND c.is_disabled = FALSE
                      AND NOT EXISTS (SELECT 1 FROM loans l
                                      WHERE l.book_copy_id = c.id AND l.returned_at IS NULL)) AS available_count,
                   (SELECT COUNT(*) FROM loans l
                    JOIN book_copies c ON c.id = l.book_copy_id
                    WHERE c.book_id = b.id AND l.returned_at IS NULL) AS active_loan_count
            FROM books b
            WHERE b.id = @id;
            """;
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapBook(reader) : null;
    }

    public async Task<(OperationResult Result, long BookId)> SaveBookAsync(
        long? id,
        BookInput input,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            if (id is null)
            {
                command.CommandText = """
                    INSERT INTO books
                        (isbn, title, author, category, publisher, publication_year, description)
                    VALUES
                        (@isbn, @title, @author, NULLIF(@category, ''), NULLIF(@publisher, ''),
                         @publicationYear, NULLIF(@description, ''));
                    """;
            }
            else
            {
                command.CommandText = """
                    UPDATE books
                    SET isbn = @isbn, title = @title, author = @author,
                        category = NULLIF(@category, ''), publisher = NULLIF(@publisher, ''),
                        publication_year = @publicationYear, description = NULLIF(@description, '')
                    WHERE id = @id;
                    """;
                command.Parameters.AddWithValue("@id", id.Value);
            }

            command.Parameters.AddWithValue("@isbn", input.Isbn.Trim());
            command.Parameters.AddWithValue("@title", input.Title.Trim());
            command.Parameters.AddWithValue("@author", input.Author.Trim());
            command.Parameters.AddWithValue("@category", input.Category?.Trim() ?? "");
            command.Parameters.AddWithValue("@publisher", input.Publisher?.Trim() ?? "");
            command.Parameters.AddWithValue("@publicationYear", input.PublicationYear);
            command.Parameters.AddWithValue("@description", input.Description?.Trim() ?? "");
            await command.ExecuteNonQueryAsync(cancellationToken);
            long bookId;
            if (id is null)
            {
                await using var idCommand = connection.CreateCommand();
                idCommand.CommandText = "SELECT LAST_INSERT_ID();";
                bookId = Convert.ToInt64(await idCommand.ExecuteScalarAsync(cancellationToken));
            }
            else
            {
                bookId = id.Value;
            }

            return (OperationResult.Ok(id is null ? "图书已新增。" : "图书已更新。"), bookId);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return (OperationResult.Fail("该 ISBN 已被其他图书使用。"), 0);
        }
    }

    public async Task<OperationResult> SetBookArchivedAsync(
        long id,
        bool archived,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE books SET is_archived = @archived WHERE id = @id;";
        command.Parameters.AddWithValue("@archived", archived);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? OperationResult.Ok(archived ? "图书已归档。" : "图书已恢复。")
            : OperationResult.Fail("图书不存在。");
    }

    public async Task<OperationResult> DeleteBookAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var lockedCopies = connection.CreateCommand();
        lockedCopies.Transaction = transaction;
        lockedCopies.CommandText = "SELECT id FROM book_copies WHERE book_id = @id FOR UPDATE;";
        lockedCopies.Parameters.AddWithValue("@id", id);
        await using (var copyReader = await lockedCopies.ExecuteReaderAsync(cancellationToken))
        {
            while (await copyReader.ReadAsync(cancellationToken))
            {
            }
        }

        await using var history = connection.CreateCommand();
        history.Transaction = transaction;
        history.CommandText = """
            SELECT COUNT(*)
            FROM loans l
            JOIN book_copies c ON c.id = l.book_copy_id
            WHERE c.book_id = @id;
            """;
        history.Parameters.AddWithValue("@id", id);
        if (Convert.ToInt32(await history.ExecuteScalarAsync(cancellationToken)) > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail("该图书的副本已有借阅历史，只能归档，不能删除。");
        }

        try
        {
            await using var deleteCopies = connection.CreateCommand();
            deleteCopies.Transaction = transaction;
            deleteCopies.CommandText = "DELETE FROM book_copies WHERE book_id = @id;";
            deleteCopies.Parameters.AddWithValue("@id", id);
            await deleteCopies.ExecuteNonQueryAsync(cancellationToken);

            await using var deleteBook = connection.CreateCommand();
            deleteBook.Transaction = transaction;
            deleteBook.CommandText = "DELETE FROM books WHERE id = @id;";
            deleteBook.Parameters.AddWithValue("@id", id);
            var affected = await deleteBook.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return affected == 1
                ? OperationResult.Ok("图书及其无借阅历史的副本已永久删除。")
                : OperationResult.Fail("图书不存在。");
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail("图书仍有借阅记录引用，只能归档，不能删除。");
        }
    }

    public async Task<IReadOnlyList<BookCopy>> GetCopiesAsync(
        long bookId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.*,
                   l.id AS active_loan_id,
                   l.borrowed_at,
                   l.due_at,
                   u.display_name AS borrowed_by,
                   EXISTS (SELECT 1 FROM loans history WHERE history.book_copy_id = c.id) AS has_history
            FROM book_copies c
            LEFT JOIN loans l ON l.book_copy_id = c.id AND l.returned_at IS NULL
            LEFT JOIN user_accounts u ON u.id = l.user_id
            WHERE c.book_id = @bookId
            ORDER BY c.is_disabled, c.copy_code;
            """;
        command.Parameters.AddWithValue("@bookId", bookId);
        var copies = new List<BookCopy>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            copies.Add(new BookCopy
            {
                Id = reader.GetInt64("id"),
                BookId = reader.GetInt64("book_id"),
                CopyCode = reader.GetString("copy_code"),
                ShelfLocation = reader.GetNullableString("shelf_location"),
                IsDisabled = reader.GetBoolean("is_disabled"),
                ActiveLoanId = reader.GetNullableInt64("active_loan_id"),
                BorrowedBy = reader.GetNullableString("borrowed_by"),
                BorrowedAt = reader.GetNullableDateTime("borrowed_at"),
                DueAt = reader.GetNullableDateOnly("due_at"),
                HasHistory = reader.GetBoolean("has_history"),
                CreatedAt = reader.GetDateTime("created_at")
            });
        }

        return copies;
    }

    public async Task<(OperationResult Result, long CopyId)> SaveCopyAsync(
        long? copyId,
        long bookId,
        CopyInput input,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            if (copyId is null)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO book_copies (book_id, copy_code, shelf_location)
                    VALUES (@bookId, @copyCode, NULLIF(@shelfLocation, ''));
                    """;
                command.Parameters.AddWithValue("@bookId", bookId);
                command.Parameters.AddWithValue("@copyCode", input.CopyCode.Trim());
                command.Parameters.AddWithValue("@shelfLocation", input.ShelfLocation?.Trim() ?? "");
                await command.ExecuteNonQueryAsync(cancellationToken);
                await using var idCommand = connection.CreateCommand();
                idCommand.Transaction = transaction;
                idCommand.CommandText = "SELECT LAST_INSERT_ID();";
                var insertedId = Convert.ToInt64(await idCommand.ExecuteScalarAsync(cancellationToken));
                await transaction.CommitAsync(cancellationToken);
                return (OperationResult.Ok("馆藏副本已新增。"), insertedId);
            }

            await using var lockCopy = connection.CreateCommand();
            lockCopy.Transaction = transaction;
            lockCopy.CommandText = """
                SELECT id
                FROM book_copies
                WHERE id = @copyId AND book_id = @bookId
                FOR UPDATE;
                """;
            lockCopy.Parameters.AddWithValue("@copyId", copyId.Value);
            lockCopy.Parameters.AddWithValue("@bookId", bookId);
            if (await lockCopy.ExecuteScalarAsync(cancellationToken) is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (OperationResult.Fail("副本不存在或不属于当前图书。"), 0);
            }

            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE book_copies
                SET copy_code = @copyCode, shelf_location = NULLIF(@shelfLocation, '')
                WHERE id = @copyId AND book_id = @bookId;
                """;
            update.Parameters.AddWithValue("@copyCode", input.CopyCode.Trim());
            update.Parameters.AddWithValue("@shelfLocation", input.ShelfLocation?.Trim() ?? "");
            update.Parameters.AddWithValue("@copyId", copyId.Value);
            update.Parameters.AddWithValue("@bookId", bookId);
            await update.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (OperationResult.Ok("馆藏副本已更新。"), copyId.Value);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (OperationResult.Fail("该馆藏编号已被使用。"), 0);
        }
    }

    public async Task<OperationResult> SetCopyDisabledAsync(
        long copyId,
        bool disabled,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE book_copies SET is_disabled = @disabled WHERE id = @id;";
        command.Parameters.AddWithValue("@disabled", disabled);
        command.Parameters.AddWithValue("@id", copyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? OperationResult.Ok(disabled ? "副本已停用，历史借阅仍会保留。" : "副本已启用。")
            : OperationResult.Fail("副本不存在。");
    }

    public async Task<OperationResult> DeleteCopyAsync(
        long copyId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var lockCopy = connection.CreateCommand();
        lockCopy.Transaction = transaction;
        lockCopy.CommandText = "SELECT id FROM book_copies WHERE id = @id FOR UPDATE;";
        lockCopy.Parameters.AddWithValue("@id", copyId);
        if (await lockCopy.ExecuteScalarAsync(cancellationToken) is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail("副本不存在。");
        }

        await using var history = connection.CreateCommand();
        history.Transaction = transaction;
        history.CommandText = "SELECT COUNT(*) FROM loans WHERE book_copy_id = @id;";
        history.Parameters.AddWithValue("@id", copyId);
        if (Convert.ToInt32(await history.ExecuteScalarAsync(cancellationToken)) > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail("该副本已有借阅历史，只能停用，不能删除。");
        }

        try
        {
            await using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM book_copies WHERE id = @id;";
            delete.Parameters.AddWithValue("@id", copyId);
            var affected = await delete.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return affected == 1 ? OperationResult.Ok("副本已永久删除。") : OperationResult.Fail("副本不存在。");
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail("副本仍被借阅记录引用，只能停用，不能删除。");
        }
    }

    public async Task<(OperationResult Result, long LoanId)> BorrowAsync(
        long userId,
        long copyId,
        int dueDays,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (dueDays is < 1 or > LoanPolicy.MaximumDays)
        {
            return (OperationResult.Fail("借阅期限必须为 1 至 30 天。"), 0);
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        await using var copyCommand = connection.CreateCommand();
        copyCommand.Transaction = transaction;
        copyCommand.CommandText = """
            SELECT c.is_disabled, b.is_archived
            FROM book_copies c
            JOIN books b ON b.id = c.book_id
            WHERE c.id = @copyId
            FOR UPDATE;
            """;
        copyCommand.Parameters.AddWithValue("@copyId", copyId);
        bool copyDisabled;
        bool bookArchived;
        await using (var reader = await copyCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return (OperationResult.Fail("馆藏副本不存在。"), 0);
            }

            copyDisabled = reader.GetBoolean("is_disabled");
            bookArchived = reader.GetBoolean("is_archived");
        }

        if (copyDisabled || bookArchived)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (OperationResult.Fail("该副本当前不可借阅。"), 0);
        }

        await using var userCommand = connection.CreateCommand();
        userCommand.Transaction = transaction;
        userCommand.CommandText = """
            SELECT is_disabled, role
            FROM user_accounts
            WHERE id = @userId
            FOR UPDATE;
            """;
        userCommand.Parameters.AddWithValue("@userId", userId);
        await using (var reader = await userCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken) || reader.GetBoolean("is_disabled"))
            {
                await transaction.RollbackAsync(cancellationToken);
                return (OperationResult.Fail("借阅账号不存在或已停用。"), 0);
            }

            if (reader.GetString("role") != "Reader")
            {
                await transaction.RollbackAsync(cancellationToken);
                return (OperationResult.Fail("只有普通读者账号可以借阅。"), 0);
            }
        }

        var borrowedAt = DateTime.UtcNow;
        var dueAt = LoanPolicy.CalculateDueDate(DateOnly.FromDateTime(DateTime.Today), dueDays);
        try
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO loans (user_id, book_copy_id, borrowed_at, due_at, notes)
                VALUES (@userId, @copyId, @borrowedAt, @dueAt, NULLIF(@notes, ''));
                """;
            insert.Parameters.AddWithValue("@userId", userId);
            insert.Parameters.AddWithValue("@copyId", copyId);
            insert.Parameters.AddWithValue("@borrowedAt", borrowedAt);
            insert.Parameters.AddWithValue("@dueAt", dueAt);
            insert.Parameters.AddWithValue("@notes", notes?.Trim() ?? "");
            await insert.ExecuteNonQueryAsync(cancellationToken);
            await using var idCommand = connection.CreateCommand();
            idCommand.Transaction = transaction;
            idCommand.CommandText = "SELECT LAST_INSERT_ID();";
            var loanId = Convert.ToInt64(await idCommand.ExecuteScalarAsync(cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return (OperationResult.Ok("借阅成功。"), loanId);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (OperationResult.Fail("该副本刚刚被借出，请刷新后重试。"), 0);
        }
    }

    public async Task<OperationResult> ReturnAsync(
        long loanId,
        long requesterId,
        bool requesterIsAdmin,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var loanCommand = connection.CreateCommand();
        loanCommand.Transaction = transaction;
        loanCommand.CommandText = """
            SELECT user_id, returned_at
            FROM loans
            WHERE id = @loanId
            FOR UPDATE;
            """;
        loanCommand.Parameters.AddWithValue("@loanId", loanId);
        long ownerId;
        bool returned;
        await using (var reader = await loanCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult.Fail("借阅记录不存在。");
            }

            ownerId = reader.GetInt64("user_id");
            returned = !reader.IsDBNull(reader.GetOrdinal("returned_at"));
        }

        if (!requesterIsAdmin && ownerId != requesterId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail("只能归还自己的借阅。");
        }

        if (returned)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Ok("该借阅已归还。");
        }

        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE loans SET returned_at = UTC_TIMESTAMP(6) WHERE id = @loanId;";
        update.Parameters.AddWithValue("@loanId", loanId);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok("归还成功。");
    }

    public async Task<OperationResult> VoidActiveLoanAsync(
        long loanId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM loans WHERE id = @loanId AND returned_at IS NULL;";
        command.Parameters.AddWithValue("@loanId", loanId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1
            ? OperationResult.Ok("未归还借阅已撤销，副本恢复可借。")
            : OperationResult.Fail("仅可撤销尚未归还的借阅记录。");
    }

    public async Task<PagedResult<Loan>> SearchLoansAsync(
        long? userId,
        string? query,
        string status,
        int page,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        var normalized = query?.Trim() ?? "";
        status = status is "active" or "overdue" or "returned" ? status : "all";
        const string where = """
            WHERE (@userId IS NULL OR l.user_id = @userId)
              AND (@query = '' OR b.title LIKE CONCAT('%', @query, '%')
                   OR c.copy_code LIKE CONCAT('%', @query, '%')
                   OR u.display_name LIKE CONCAT('%', @query, '%'))
              AND (@status = 'all'
                   OR (@status = 'active' AND l.returned_at IS NULL)
                   OR (@status = 'overdue' AND l.returned_at IS NULL AND l.due_at < @today)
                   OR (@status = 'returned' AND l.returned_at IS NOT NULL))
            """;

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var count = connection.CreateCommand();
        count.CommandText = $"""
            SELECT COUNT(*)
            FROM loans l
            JOIN user_accounts u ON u.id = l.user_id
            JOIN book_copies c ON c.id = l.book_copy_id
            JOIN books b ON b.id = c.book_id
            {where};
            """;
        AddLoanFilterParameters(count, userId, normalized, status);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken));

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT l.*, u.username, u.display_name,
                   c.copy_code, b.id AS book_id, b.title AS book_title
            FROM loans l
            JOIN user_accounts u ON u.id = l.user_id
            JOIN book_copies c ON c.id = l.book_copy_id
            JOIN books b ON b.id = c.book_id
            {where}
            ORDER BY (l.returned_at IS NULL) DESC, l.due_at, l.borrowed_at DESC
            LIMIT @pageSize OFFSET @offset;
            """;
        AddLoanFilterParameters(command, userId, normalized, status);
        command.Parameters.AddWithValue("@pageSize", pageSize);
        command.Parameters.AddWithValue("@offset", (page - 1) * pageSize);

        var loans = new List<Loan>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            loans.Add(new Loan
            {
                Id = reader.GetInt64("id"),
                UserId = reader.GetInt64("user_id"),
                Username = reader.GetString("username"),
                UserDisplayName = reader.GetString("display_name"),
                BookCopyId = reader.GetInt64("book_copy_id"),
                CopyCode = reader.GetString("copy_code"),
                BookId = reader.GetInt64("book_id"),
                BookTitle = reader.GetString("book_title"),
                BorrowedAt = reader.GetDateTime("borrowed_at"),
                DueAt = DateOnly.FromDateTime(reader.GetDateTime("due_at")),
                ReturnedAt = reader.GetNullableDateTime("returned_at"),
                Notes = reader.GetNullableString("notes")
            });
        }

        return new PagedResult<Loan>(loans, page, pageSize, total);
    }

    public async Task<IReadOnlyList<BorrowerOption>> GetBorrowersAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, display_name, username
            FROM user_accounts
            WHERE role = 'Reader' AND is_disabled = FALSE
            ORDER BY display_name, username;
            """;
        var borrowers = new List<BorrowerOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            borrowers.Add(new BorrowerOption(
                reader.GetInt64("id"),
                reader.GetString("display_name"),
                reader.GetString("username")));
        }

        return borrowers;
    }

    public async Task<IReadOnlyList<AvailableCopyOption>> GetAvailableCopiesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.id, c.copy_code, b.title
            FROM book_copies c
            JOIN books b ON b.id = c.book_id
            WHERE c.is_disabled = FALSE AND b.is_archived = FALSE
              AND NOT EXISTS (
                  SELECT 1 FROM loans l
                  WHERE l.book_copy_id = c.id AND l.returned_at IS NULL
              )
            ORDER BY b.title, c.copy_code;
            """;
        var copies = new List<AvailableCopyOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            copies.Add(new AvailableCopyOption(
                reader.GetInt64("id"),
                reader.GetString("copy_code"),
                reader.GetString("title")));
        }

        return copies;
    }

    private static void AddLoanFilterParameters(
        MySqlCommand command,
        long? userId,
        string query,
        string status)
    {
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@query", query);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@today", DateOnly.FromDateTime(DateTime.Today));
    }

    private static Book MapBook(MySqlDataReader reader) => new()
    {
        Id = reader.GetInt64("id"),
        Isbn = reader.GetString("isbn"),
        Title = reader.GetString("title"),
        Author = reader.GetString("author"),
        Category = reader.GetNullableString("category"),
        Publisher = reader.GetNullableString("publisher"),
        PublicationYear = reader.IsDBNull(reader.GetOrdinal("publication_year"))
            ? null
            : reader.GetInt16("publication_year"),
        Description = reader.GetNullableString("description"),
        IsArchived = reader.GetBoolean("is_archived"),
        CopyCount = Convert.ToInt32(reader["copy_count"]),
        AvailableCount = Convert.ToInt32(reader["available_count"]),
        ActiveLoanCount = Convert.ToInt32(reader["active_loan_count"]),
        CreatedAt = reader.GetDateTime("created_at"),
        UpdatedAt = reader.GetDateTime("updated_at")
    };
}
