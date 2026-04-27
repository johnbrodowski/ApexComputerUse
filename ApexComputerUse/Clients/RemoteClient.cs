using System.Text.Json.Serialization;

namespace ApexComputerUse
{
    public sealed class RemoteClient
    {
        [JsonPropertyName("id")]          public string Id          { get; set; } = ClientIds.New();
        [JsonPropertyName("name")]        public string Name        { get; set; } = "";
        [JsonPropertyName("host")]        public string Host        { get; set; } = "";
        [JsonPropertyName("port")]        public int    Port        { get; set; } = 8081;
        [JsonPropertyName("api_key")]     public string ApiKey      { get; set; } = "";
        [JsonPropertyName("os_version")]  public string OsVersion   { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("created_at")]  public string CreatedAt   { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    }

    internal static class ClientIds
    {
        public static string New() => Guid.NewGuid().ToString("N")[..8];
    }
}
