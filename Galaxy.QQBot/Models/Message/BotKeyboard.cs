using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Message;

public class BotKeyboard
{
    [JsonPropertyName("content")]
    public BotKeyboardContent Content { get; set; } = new BotKeyboardContent();
}

public class BotKeyboardContent
{
    [JsonPropertyName("rows")]
    public List<BotKeyboardRow> Rows { get; set; } = [];
}
public class BotKeyboardRow
{
    [JsonPropertyName("buttons")]
    public List<BotKeyboardButton> Buttons { get; set; } = [];
}

public class BotKeyboardButton
{
    [JsonPropertyName("id")]
    public string? Id { get; set; } = null;

    [JsonPropertyName("render_data")]
    public BotKeyboardRenderData RenderData { get; set; } = new BotKeyboardRenderData();

    [JsonPropertyName("action")]
    public BotKeyboardAction Action { get; set; } = new BotKeyboardAction();

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
    public BotKeyboardPermission Permission { get; set; } = new BotKeyboardPermission();

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
