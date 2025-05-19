namespace BiliAutoGI;

public class BiliApi
{
    private static bool _needStream = false;
    public static async Task<bool> NeedStream()
    {
        await CheckStatus();
        if (_needStream)
        {
            Console.WriteLine("今天已直播，无需再次直播");
            return true;
        }
        else
        {
            return false;
        }
    }

    private static async Task CheckStatus()
    {
        Console.WriteLine("检查状态，开发中...");
        _needStream = true;
    }
    
    public static async Task BiliStreamEnabler()
    {
        Console.WriteLine("开始直播，开发中...");
    }
}