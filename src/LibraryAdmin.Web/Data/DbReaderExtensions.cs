using MySqlConnector;

namespace LibraryAdmin.Web.Data;

internal static class DbReaderExtensions
{
    public static string? GetNullableString(this MySqlDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetString(name);

    public static DateTime? GetNullableDateTime(this MySqlDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetDateTime(name);

    public static DateOnly? GetNullableDateOnly(this MySqlDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name))
            ? null
            : DateOnly.FromDateTime(reader.GetDateTime(name));

    public static long? GetNullableInt64(this MySqlDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetInt64(name);
}
