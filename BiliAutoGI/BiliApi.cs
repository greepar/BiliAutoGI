namespace BiliAutoGI;

public class BiliApi
{
    private static bool _needStream = false;
    private static bool _isLogin = false;
    private static string _biliCookie =null!;
    
    private readonly HttpClient _httpClient;
    private const string UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:138.0) Gecko/20100101 Firefox/138.0";
    public BiliApi()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }
    private string GetCsrfFromCookie(string cookieString)
    {
        if (string.IsNullOrEmpty(cookieString))
        {
            return null;
        }

        var cookies = cookieString.Split(';');
        foreach (var cookie in cookies)
        {
            var parts = cookie.Trim().Split('=');
            if (parts.Length == 2 && parts[0].Trim() == "bili_jct")
            {
                return parts[1].Trim();
            }
        }
        return null;
    }
    
    public static async Task<bool> NeedStreamAsync()
    {
        await BiliLoginAsync();
        await CheckStatusAsync();
        return _needStream;
    }
    public static async Task BiliLoginAsync()
    {
        //检查登录
        //已登录，直接载入Cookie
        //未登录，开始登入程序
    }
    private static async Task CheckStatusAsync()
    {
        //检查是否登录
        Console.WriteLine("检查今日是否完成直播60min任务，开发中...");
        _needStream = true;
    }
    public static async Task GetStreamKeyAsync()
    {
        
    }
    public static async Task BiliStreamEnabler()
    {
        Console.WriteLine("开始直播，开发中...");
    }
}