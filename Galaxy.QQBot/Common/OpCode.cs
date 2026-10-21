namespace Galaxy.QQBot.Common;

public enum OpCode
{
    Dispatch = 0,
    Heartbeat = 1,
    Identify = 2,
    Resume = 6,
    Reconnect = 7,
    Invalid_Session = 9,
    Hello = 10,
    HeartbeatACK = 11,
    HTTPCallbackACK = 12,
    HTTPCallVerification = 13
}
