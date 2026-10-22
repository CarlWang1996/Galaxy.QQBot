using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Message;

public class BotKeyboard
{
    [JsonPropertyName("content")]
    public BotKeyboardContent? Content { get; set; } = null;
}

public class BotKeyboardContent
{
    [JsonPropertyName("rows")]
    public BotKeyboardRow[]? Rows { get; set; } = null;
}
public class BotKeyboardRow
{
    [JsonPropertyName("buttons")]
    public BotKeyboardButton[]? Buttons { get; set; } = null;
}

public class BotKeyboardButton
{
    [JsonPropertyName("id")]
    public string? Id { get; set; } = null;

    [JsonPropertyName("render_data")]
    public BotKeyboardRenderData? RenderData { get; set; } = null;

    [JsonPropertyName("action")]
    public BotKeyboardAction? Action { get; set; } = null;

    [JsonPropertyName("group_id")]
    public string? GroupId { get; set; } = null;
}

public class BotKeyboardRenderData
{
    [JsonPropertyName("label")]
    public string? Label { get; set; } = null;

    [JsonPropertyName("visited_label")]
    public string? VisitedLabel { get; set; } = null;

    [JsonPropertyName("style")]
    public int? Style { get; set; } = null;
}

public class BotKeyboardAction
{
    [JsonPropertyName("type")]
    public int? Type { get; set; } = null;

    [JsonPropertyName("permission")]
    public BotKeyboardPermission? Permission { get; set; } = null;

    [JsonPropertyName("data")]
    public string? Data { get; set; } = null;

    [JsonPropertyName("unsupport_tips")]
    public string? UnsupportTips { get; set; } = null;

    [JsonPropertyName("enter")]
    public bool? Enter { get; set; } = null;

    [JsonPropertyName("reply")]
    public bool? Reply { get; set; } = null;

    [JsonPropertyName("anchor")]
    public int? Anchor { get; set; } = null;

    [JsonPropertyName("modal")]
    public BotKeyboardModal? Modal { get; set; } = null;
}

public class BotKeyboardPermission
{
    [JsonPropertyName("type")]
    public int? Type { get; set; } = null;

    [JsonPropertyName("specify_user_ids")]
    public string[]? SpecifyUserIds { get; set; } = null;
}

public class BotKeyboardModal
{
    [JsonPropertyName("content")]
    public string? Content { get; set; } = null;

    [JsonPropertyName("confirm_text")]
    public string? ConfirmText { get; set; } = null;

    [JsonPropertyName("cancel_text")]
    public string? CancelText { get; set; } = null;
}
