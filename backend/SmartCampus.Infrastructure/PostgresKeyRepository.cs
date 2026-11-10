using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Npgsql;

namespace SmartCampus.Infrastructure;

/// <summary>
/// Persistent, encrypted ASP.NET Core Data Protection key ring for ephemeral hosts.
/// XmlKeyManager encrypts XML with the configured X.509 certificate before storing it.
/// This repository intentionally does not create tables or apply migrations at startup.
/// </summary>
public sealed class PostgresKeyRepository(string connectionString) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(
            "SELECT \"Xml\" FROM smart_campus.\"DataProtectionKeys\" ORDER BY \"FriendlyName\"", connection);
        using var reader = command.ExecuteReader();
        var keys = new List<XElement>();
        while (reader.Read())
            keys.Add(XElement.Parse(reader.GetString(0), LoadOptions.None));
        return keys;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(friendlyName))
            throw new ArgumentException("A Data Protection key name is required.", nameof(friendlyName));
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(
            "INSERT INTO smart_campus.\"DataProtectionKeys\" (\"FriendlyName\",\"Xml\") VALUES (@name,@xml) ON CONFLICT (\"FriendlyName\") DO NOTHING", connection);
        command.Parameters.AddWithValue("name", friendlyName);
        command.Parameters.AddWithValue("xml", element.ToString(SaveOptions.DisableFormatting));
        command.ExecuteNonQuery();
    }
}
