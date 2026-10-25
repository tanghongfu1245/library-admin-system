using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace LibraryAdmin.Web.Validation;

public static class UsernameRules
{
    public const string Pattern =
        @"^[A-Za-z0-9_\u00C0-\u02AF\u0370-\u052F\u3040-\u30FF\u3400-\u9FFF\uAC00-\uD7AF-]{3,30}$";

    public const string Message = "用户名为 3-30 位文字、字母、数字、下划线或短横线";

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        Regex.IsMatch(value.Trim(), Pattern, RegexOptions.CultureInvariant);
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class AllowedUsernameAttribute : ValidationAttribute, IClientModelValidator
{
    public override bool IsValid(object? value) =>
        value is string text && UsernameRules.IsValid(text);

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes["data-val"] = "true";
        context.Attributes["data-val-regex"] = ErrorMessage ?? UsernameRules.Message;
        context.Attributes["data-val-regex-pattern"] = UsernameRules.Pattern;
    }
}
