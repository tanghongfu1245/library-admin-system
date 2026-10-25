namespace LibraryAdmin.Web.Services;

public static class LoanPolicy
{
    public const int DefaultDays = 30;
    public const int MaximumDays = 30;

    public static DateOnly CalculateDueDate(DateOnly borrowedDate, int days)
    {
        if (days is < 1 or > MaximumDays)
        {
            throw new ArgumentOutOfRangeException(nameof(days), $"借阅期限必须为 1 至 {MaximumDays} 天。");
        }

        return borrowedDate.AddDays(days);
    }
}
