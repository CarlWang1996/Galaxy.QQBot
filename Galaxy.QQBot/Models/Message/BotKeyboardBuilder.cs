namespace Galaxy.QQBot.Models.Message;

public enum BotButtonStyle
{
    GrayBorder = 0,
    BlueBorder = 1,
    WhiteBgAndRedFont = 3,
    BlueBgAndWhiteFont = 4
}

public enum BotButtonType
{
    Jump = 0,
    CallBack = 1,
    Command = 2
}

public enum BotButtonPermission
{
    Specified = 0,
    Manager = 1,
    All = 2
}

public sealed class BotKeyboardBuilder
{
    private int _autoId = 0;
    private readonly BotKeyboard _keyboard;

    public BotKeyboardBuilder()
    {
        _keyboard = new BotKeyboard();
    }

    public BotKeyboardBuilder AddNewRow()
    {
        _keyboard.Content.Rows.Add(new BotKeyboardRow());
        return this;
    }

    public BotKeyboardBuilder AddButton(
        string? lable = null,string? data = null, string? visitedLabel = null,
        BotButtonType type = BotButtonType.CallBack,
        BotButtonStyle style = BotButtonStyle.BlueBorder,
        BotButtonPermission permission = BotButtonPermission.All,
        string? unsupportTips = null,bool enter = false,
        bool reply = false,int anchor = 0,bool confirm = false,string? confirmContent = null,string? confirmText = null,
        string? cancleText = null, string[]? specifyUserIds = null,string? groupId = null)
    {
        if(_keyboard.Content.Rows.Count == 0)
        {
            AddNewRow();
        }

        var currrentRow = _keyboard.Content.Rows[^1];
        if(currrentRow.Buttons.Count >= 5)
        {
            AddNewRow();
        }

        currrentRow = _keyboard.Content.Rows[^1];

        BotKeyboardButton button = new BotKeyboardButton();
        button.Id = _autoId.ToString();
        _autoId++;
        button.RenderData.Label = lable;
        button.RenderData.Style = (int)style;
        if(visitedLabel != null)
        {
            button.RenderData.VisitedLabel = visitedLabel;
        }
        button.Action.Type = (int)type;
        button.Action.Permission.Type = (int)permission;
        button.Action.Permission.SpecifyUserIds = specifyUserIds;
        button.Action.Data = data;
        button.Action.UnsupportTips = unsupportTips;
        button.Action.Enter = enter;
        button.Action.Reply = reply;
        button.Action.Anchor = anchor;
        if(confirm)
        {
            button.Action.Modal = new BotKeyboardModal
            {
                Content = confirmContent,
                ConfirmText = confirmText,
                CancelText = cancleText
            };
        }
        button.GroupId = groupId;
        currrentRow.Buttons.Add(button);
        return this;
    }

    public BotKeyboard Build()
    {
        return _keyboard;
    }
}
