namespace Galaxy.QQBot.Models.Event;

public class BotInteractionData
{
    public int type { get; set; } = 0;
    public BotInteractionResolved resolved { get; set; } = null!;
}

public class BotInteractionResolved
{
    public string button_data { get; set; } = string.Empty;
    public string button_id { get; set; } = string.Empty;
    public string feature_id { get; set; } = string.Empty;
    public string message_id { get; set; } = string.Empty;
    public string feedback_opt { get; set; } = string.Empty;
    public int? @checked { get; set; } = null;
    public string action { get; set; } = string.Empty;
    public BotInteractionMessageScene? message_scene = null;
    public BotInteractionAuthorizeData? authorize_data = null;

}

public class BotInteractionMessageScene
{
    public string[] ext { get; set; } = [];
}

public class BotInteractionAuthorizeData
{
    public string opt_scene { get; set; } = string.Empty;
    public string scope { get; set; } = string.Empty;
}
