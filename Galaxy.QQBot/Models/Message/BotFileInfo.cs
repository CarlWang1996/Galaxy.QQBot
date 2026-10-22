using System;
using System.Collections.Generic;
using System.Text;

namespace Galaxy.QQBot.Models.Message;

public class BotFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public int FileType { get; set; } = 4;
    public bool SrvSendMessage { get; set; } = false;
}
