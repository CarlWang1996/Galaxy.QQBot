using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Event;

public record class BotInteractionData
{
    [JsonPropertyName("type")]
    public int Type { get; init; } = 0;

    [JsonPropertyName("resolved")]
    public BotInteractionResolved Resolved { get; init; } = null!;
}

public record class BotInteractionResolved
{
    [JsonPropertyName("button_data")]
    public string ButtonData { get; init; } = string.Empty;

    [JsonPropertyName("button_id")]
    public string ButtonId { get; init; } = string.Empty;

    [JsonPropertyName("feature_id")]
    public string FeatureId { get; init; } = string.Empty;

    [JsonPropertyName("message_id")]
    public string MessageId { get; init; } = string.Empty;

    [JsonPropertyName("feedback_opt")]
    public string FeedbackOpt { get; init; } = string.Empty;

    [JsonPropertyName("checked")]
    public int? Checked { get; init; } = null;

    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("message_scene")]
    public BotInteractionMessageScene? MessageScene { get; init; } = null;

    [JsonPropertyName("authorize_data")]
    public BotInteractionAuthorizeData? AuthorizeData { get; init; } = null;

}

public class BotInteractionMessageScene
{
    [JsonPropertyName("ext")]
    public string[] Ext { get; init; } = [];
}

public class BotInteractionAuthorizeData
{
    [JsonPropertyName("opt_scene")]
    public string OptScene { get; init; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = string.Empty;
}
