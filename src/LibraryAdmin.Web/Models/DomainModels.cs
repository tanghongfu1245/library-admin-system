using System.ComponentModel.DataAnnotations;
using LibraryAdmin.Web.Validation;

namespace LibraryAdmin.Web.Models;

public enum AccountRole
{
    Reader,
    Admin
}

public enum ApplicationStatus
{
    Pending,
    Approved,
    Rejected
}

public sealed class UserAccount
{
    public long Id { get; init; }
    public string Username { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string PasswordHash { get; init; } = "";
    public AccountRole Role { get; init; }
    public bool IsDisabled { get; init; }
    public bool MustChangePassword { get; init; }
    public bool IsBootstrapAdmin { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class Book
{
    public long Id { get; init; }
    public string Isbn { get; init; } = "";
    public string Title { get; init; } = "";
    public string Author { get; init; } = "";
    public string? Category { get; init; }
    public string? Publisher { get; init; }
    public short? PublicationYear { get; init; }
    public string? Description { get; init; }
    public bool IsArchived { get; init; }
    public int CopyCount { get; init; }
    public int AvailableCount { get; init; }
    public int ActiveLoanCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class BookCopy
{
    public long Id { get; init; }
    public long BookId { get; init; }
    public string CopyCode { get; init; } = "";
    public string? ShelfLocation { get; init; }
    public bool IsDisabled { get; init; }
    public long? ActiveLoanId { get; init; }
    public string? BorrowedBy { get; init; }
    public DateTime? BorrowedAt { get; init; }
    public DateOnly? DueAt { get; init; }
    public bool HasHistory { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class Loan
{
    public long Id { get; init; }
    public long UserId { get; init; }
    public string Username { get; init; } = "";
    public string UserDisplayName { get; init; } = "";
    public long BookCopyId { get; init; }
    public string CopyCode { get; init; } = "";
    public long BookId { get; init; }
    public string BookTitle { get; init; } = "";
    public DateTime BorrowedAt { get; init; }
    public DateOnly DueAt { get; init; }
    public DateTime? ReturnedAt { get; init; }
    public string? Notes { get; init; }

    public string StatusText => ReturnedAt is not null
        ? "已归还"
        : DueAt < DateOnly.FromDateTime(DateTime.Today)
            ? "已逾期"
            : "借阅中";

    public string StatusCss => ReturnedAt is not null
        ? "status-returned"
        : DueAt < DateOnly.FromDateTime(DateTime.Today)
            ? "status-overdue"
            : "status-active";
}

public sealed class AdminApplication
{
    public long Id { get; init; }
    public long UserId { get; init; }
    public string Username { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public ApplicationStatus Status { get; init; }
    public DateTime SubmittedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ReviewedBy { get; init; }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public sealed record DashboardStats(
    int BookTitles,
    int TotalCopies,
    int AvailableCopies,
    int ActiveLoans,
    int OverdueLoans,
    int Readers);

public sealed class LoginInput
{
    [Required(ErrorMessage = "请输入用户名")]
    [StringLength(50, ErrorMessage = "用户名不能超过 50 个字符")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "请输入密码")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public sealed class RegistrationInput
{
    [Required(ErrorMessage = "请输入用户名")]
    [AllowedUsername(ErrorMessage = UsernameRules.Message)]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "请输入姓名")]
    [StringLength(100, ErrorMessage = "姓名不能超过 100 个字符")]
    public string DisplayName { get; set; } = "";

    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    [StringLength(254)]
    public string? Email { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "请输入密码")]
    [MinLength(8, ErrorMessage = "密码至少 8 个字符")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "请再次输入密码")]
    [Compare(nameof(Password), ErrorMessage = "两次密码不一致")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";

    [Required]
    public string RegistrationType { get; set; } = "Reader";
}

public sealed class ProfileInput
{
    [Required(ErrorMessage = "请输入姓名")]
    [StringLength(100, ErrorMessage = "姓名不能超过 100 个字符")]
    public string DisplayName { get; set; } = "";

    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    [StringLength(254)]
    public string? Email { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }
}

public sealed class ChangePasswordInput
{
    [Required(ErrorMessage = "请输入当前密码")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "请输入新密码")]
    [MinLength(8, ErrorMessage = "新密码至少 8 个字符")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "请再次输入新密码")]
    [Compare(nameof(NewPassword), ErrorMessage = "两次新密码不一致")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class BookInput
{
    [Required(ErrorMessage = "请输入 ISBN")]
    [StringLength(20, ErrorMessage = "ISBN 不能超过 20 个字符")]
    public string Isbn { get; set; } = "";

    [Required(ErrorMessage = "请输入书名")]
    [StringLength(200, ErrorMessage = "书名不能超过 200 个字符")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "请输入作者")]
    [StringLength(150, ErrorMessage = "作者不能超过 150 个字符")]
    public string Author { get; set; } = "";

    [StringLength(80)]
    public string? Category { get; set; }

    [StringLength(150)]
    public string? Publisher { get; set; }

    [Range(1000, 2100, ErrorMessage = "出版年应在 1000 至 2100 之间")]
    public short? PublicationYear { get; set; }

    [StringLength(2000, ErrorMessage = "简介不能超过 2000 个字符")]
    public string? Description { get; set; }
}

public sealed class CopyInput
{
    [Required(ErrorMessage = "请输入馆藏编号")]
    [StringLength(40, ErrorMessage = "馆藏编号不能超过 40 个字符")]
    public string CopyCode { get; set; } = "";

    [StringLength(80, ErrorMessage = "书架位置不能超过 80 个字符")]
    public string? ShelfLocation { get; set; }
}

public sealed class BorrowInput
{
    [Range(1, long.MaxValue, ErrorMessage = "请选择馆藏副本")]
    public long CopyId { get; set; }

    [Range(1, 30, ErrorMessage = "借阅期限必须为 1 至 30 天")]
    public int DueDays { get; set; } = 30;

    [StringLength(500, ErrorMessage = "备注不能超过 500 个字符")]
    public string? Notes { get; set; }
}

public sealed record OperationResult(bool Succeeded, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}

public sealed record BorrowerOption(long Id, string DisplayName, string Username);

public sealed record AvailableCopyOption(long Id, string CopyCode, string BookTitle);

public sealed record PaginationView(
    string PageName,
    int Page,
    int TotalPages,
    string? Query = null,
    string? Status = null,
    bool IncludeArchived = false);
