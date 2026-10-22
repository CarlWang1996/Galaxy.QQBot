using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Event;

public class BotInteractionData
{
    [JsonPropertyName("type")]
    public int Type { get; set; } = 0;

    [JsonPropertyName("resolved")]
    public BotInteractionResolved Resolved { get; set; } = null!;
}

public class BotInteractionResolved
{
    [JsonPropertyName("button_data")]
    public string ButtonData { get; set; } = string.Empty;

    [JsonPropertyName("button_id")]
    public string ButtonId { get; set; } = string.Empty;

    [JsonPropertyName("feature_id")]
    public string FeatureId { get; set; } = string.Empty;

    [JsonPropertyName("message_id")]
    public string MessageId { get; set; } = string.Empty;

    [JsonPropertyName("feedback_opt")]
    public string FeedbackOpt { get; set; } = string.Empty;

    [JsonPropertyName("checked")]
    public int? Checked { get; set; } = null;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("message_scene")]
    public BotInteractionMessageScene? MessageScene = null;

    [JsonPropertyName("authorize_data")]
    public BotInteractionAuthorizeData? AuthorizeData = null;

}

public class BotInteractionMessageScene
{
    [JsonPropertyName("ext")]
    public string[] Ext { get; set; } = [];
}

public class BotInteractionAuthorizeData
{
    [JsonPropertyName("opt_scene")]
    public string OptScene { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}
