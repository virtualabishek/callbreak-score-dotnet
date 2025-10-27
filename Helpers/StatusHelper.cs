using System;

namespace CallbreakApp.Helpers;

public static class StatusHelper
{
    public static (string Status, double Extra, bool IsReceived) GetRoundStatus(int bid, int tricks)
    {
        if (tricks >= bid)
        {
            var extra = 0.1 * (tricks - bid);
            return ("Received", extra, true);
        }
        else
        {
            return ("Failed", 0, false);
        }
    }
}
