using System.Net;
using System.Text.Json;
using QRCoder;
namespace BiliAutoGI;


public class BiliApi
{
    private static string _biliCookie =null!;
    private static bool _needStream = false;
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;
    private const string UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:138.0) Gecko/20100101 Firefox/138.0";
    public class LiveInfo
    {
        public required string RtmpAddr { get; init; }
        public required string RtmpKey { get; init; }
    }
    public class LoginInfo
    {
        public required string QrCodeKey { get; init; }
        public required string LoginUrl { get; init; }
    }
    public BiliApi()
    {
        _cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = _cookieContainer,
        };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    private async Task<LoginInfo?> GetLoginQrCode()
    {
        var loginApi = "https://passport.bilibili.com/x/passport-login/web/qrcode/generate";
        var response = await _httpClient.GetAsync(loginApi);
        if (response.IsSuccessStatusCode)
        {
            var stringResponse = await response.Content.ReadAsStringAsync(); 
            try
            {
                var jsonDoc = JsonDocument.Parse(stringResponse);
                var jsonRoot = jsonDoc.RootElement;
                var apiResultCode = jsonRoot.GetProperty("code").GetInt32();
                if (apiResultCode == 0)
                {
                    var loginInfo = new LoginInfo
                    {
                        QrCodeKey = jsonRoot.GetProperty("data").GetProperty("qrcode_key").GetString()!,
                        LoginUrl = jsonRoot.GetProperty("data").GetProperty("url").GetString()!
                    };
                    return loginInfo;
                }
                else
                {
                    Console.WriteLine($"API Error: {jsonRoot.GetProperty("message").GetString()}");
                    return null;
                }
            }
            catch (JsonException e)
            {
                Console.WriteLine("JSON 转换出错: " + e.Message);
                throw;
            }
        }
        else
        {
            Console.WriteLine("API 请求失败，状态码: " + response.StatusCode);
            return null;
        }
    }
    public async Task<bool> BiliLoginAsync()
    {
        await Task.Delay(1);
        //未登录，开始登入程序
        var loginInfo = await GetLoginQrCode();
        if (loginInfo != null)
        {
            //获取到登录二维码后，开始轮询获取是否登录成功
            var qrcodeKey = loginInfo.QrCodeKey;
            var loginUrl = loginInfo.LoginUrl;
            await GenerateQrCodeAsync(loginUrl);
            Console.WriteLine($"请扫描二维码登录，登录后请按任意键继续...");
            var loginCheckApi = $"https://passport.bilibili.com/x/passport-login/web/qrcode/poll?qrcode_key={qrcodeKey}";
            for (int i = 0,apiResultCode = 123; i < 30 || apiResultCode == 0 ; i++)
            {
                using var response = await _httpClient.GetAsync(loginCheckApi);
                var stringResponse = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(stringResponse);
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        apiResultCode = jsonDoc.RootElement.GetProperty("data").GetProperty("code").GetInt32();
                        Console.WriteLine("登录api返回code: " + apiResultCode + "api返回信息: " + jsonDoc.RootElement.GetProperty("data").GetProperty("message").GetString());
                        if (apiResultCode == 0)
                        {
                            Console.WriteLine("登录成功，正在载入Cookie..."); 
                            var uri = new Uri("https://space.bilibili.com");
                            var cookie = _cookieContainer.GetCookies(uri);
                            _biliCookie = string.Join(";", cookie.Select(c => $"{c.Name}={c.Value}"));
                            Console.WriteLine("Cookie载入成功: " + _biliCookie);
                            //登录成功，载入Cookie
                            return true;
                        }
                        if (apiResultCode == 86101)
                        {
                            //未扫码，继续等待
                        }
                        else if (apiResultCode == 86090)
                        {
                            //已扫码，等待手机确认登录
                        }
                        else if (apiResultCode == 86038) 
                        {
                            Console.WriteLine("登录二维码已失效，请重新获取二维码。");
                            return false;
                        }
                        else
                        {
                            Console.WriteLine($"未知返回代码: {apiResultCode}");
                            return false;
                        }
                    }
                    catch (JsonException e)
                    {
                        Console.WriteLine("JSON 转换出错: " + e.Message);
                        throw;
                    }
                }
                else
                {
                    Console.WriteLine("登录检查API请求失败，状态码: " + response.StatusCode);
                    return false;
                }
                Console.WriteLine($"正在检查登录状态... {i + 1}/30");
                await Task.Delay(1000); // 每秒检查一次
            }
        }

        Console.WriteLine("获取登录二维码失败，可能是Cookie无效或网络问题。");
        return false;
        //已存在Cookie，直接检查Cookie是否有效
        var checkLoginApi = "https://api.bilibili.com/x/web-interface/nav";
        
        //已登录，直接载入Cookie
        Console.WriteLine("BiliLoginAsync开发中，直接return true了");
        return true; 
    }
    public async Task<bool?> Check60MinStatusAsync()
    {
        _needStream = false;
        //B站检查今日直播状态API
        string task60MinApi = "https://api.bilibili.com/x/activity_components/mission/info?task_id=6ERA4wloghvk5600&web_location=888.81821&w_rid=da939b1fbbfb9bff264b61baaec618c9&wts=1748099371";
        try
        {
            Console.WriteLine($"正在检查今日是否已经完成60min直播任务 {task60MinApi}");
            var response = await _httpClient.GetAsync(task60MinApi);
            if (response.IsSuccessStatusCode)
            {
                string stringResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"API Response JSON: {stringResponse}");
                try
                { 
                    var jsonDoc = JsonDocument.Parse(stringResponse);
                    var jsonRoot = jsonDoc.RootElement;
                    // var apiResultCode = jsonRoot.GetProperty("code").GetInt32();
                    Console.WriteLine("api请求成功，开始解析数据");
                    var jsonData = jsonRoot.GetProperty("data");
                    var rewardMessageFromJsonData = jsonData.GetProperty("message").GetString();
                    Console.WriteLine("从 API 获取的消息: " + rewardMessageFromJsonData);
                    if (rewardMessageFromJsonData == "获取奖励")
                    {
                        return false;
                    }
                    else
                    {
                        Console.WriteLine("今天似乎还未完成直播任务，或任务状态不符合预期,将会开始今日直播。");
                        return true;
                    }
                }
                catch (JsonException e)
                {
                    Console.WriteLine("JSON 转换出错: " + e.Message);
                    throw;
                }
            }
            else
            {
                return null;
                Console.WriteLine("API 请求失败，状态码: " + response.StatusCode);
            }
        }
        catch(Exception e)
        {
            //虽然我不知道这能出什么异常xD
            Console.WriteLine("CheckStatusAsync 外部出现异常: " + e.Message);
            throw;
        }
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
    private async Task<long?> GetRoomIdAsync()
    {
        if (string.IsNullOrEmpty(_biliCookie))
        {
            Console.WriteLine("Cookie string is null or empty.");
            return null;
        }
        //通过API获取房间ID
        try
        {
            var response = await _httpClient.GetAsync("https://api.live.bilibili.com/xlive/app-blink/v1/highlight/getRoomHighlightState");
            string stringResponse = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(stringResponse);
            if (response.IsSuccessStatusCode)
            {
                var roomId = jsonDoc.RootElement.GetProperty("data").GetProperty("room_id").GetInt64();
                return roomId;
            }
            else
            {
                Console.WriteLine($"请求失败，状态码: {jsonDoc.RootElement.GetProperty("data").GetProperty("code").GetInt32()}, 错误信息: {jsonDoc.RootElement.GetProperty("data").GetProperty("message").GetString()}");
                return null;
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"HttpRequestException in GetRoomHighlightStateAsync: {e.Message}");
            return null;
        }
        catch (JsonException e)
        {
            Console.WriteLine($"JsonException in GetRoomHighlightStateAsync: {e.Message}");
            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Generic Exception in GetRoomHighlightStateAsync: {e.Message}");
            return null;
        }
    }
     public async Task<LiveInfo?> StartLiveAsync()
    {
        string csrf = GetCsrfFromCookie(_biliCookie);
        if (string.IsNullOrEmpty(csrf))
        {
            Console.WriteLine("csrf获取失败");
            return null;
        }
        var apiUrl = "https://api.live.bilibili.com/room/v1/Room/startLive";
        //get room_id
        var roomId = (await GetRoomIdAsync()).ToString();
        if (string.IsNullOrEmpty(roomId))
        {
            Console.WriteLine("获取房间ID失败，无法开始直播");
            return null;
        }
        Console.WriteLine($"room_id: {roomId}");
        var formData = new Dictionary<string, string>
        {
            { "platform", "ios_link" },
            { "room_id" , roomId}, // 你的房间ID
            { "area_v2", "321" }, // 原神分区的area_id
            { "csrf", csrf }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
        {
            Content = new FormUrlEncodedContent(formData)
        };
        try
        {
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine("输出JSON"+jsonResponse);
                using var jsonDoc = JsonDocument.Parse(jsonResponse);
                var rtmpAddr = jsonDoc.RootElement.GetProperty("data").GetProperty("rtmp").GetProperty("addr").GetString()!;
                var rtmpKey = jsonDoc.RootElement.GetProperty("data").GetProperty("rtmp").GetProperty("code").GetString()!;
                Console.WriteLine($"biliapi:测试获取直播信息，RTMP地址: {rtmpAddr}, RTMP密钥: {rtmpKey}");
                var liveInfo = new LiveInfo()
                {
                    RtmpAddr = rtmpAddr,
                    RtmpKey = rtmpKey
                };
                return liveInfo;
            }
            Console.WriteLine($"登录失败，状态码: {response.StatusCode}");
            return null;
        }
        catch (JsonException e)
        {
            Console.WriteLine($"JsonException in StartLiveAsync: {e.Message}");
            throw;
        }
        catch (HttpRequestException)
        {
            Console.WriteLine("HttpRequestException in StartLiveAsync");
            throw;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Generic Exception in StartLiveAsync: {e.Message}");
            throw;
        }
    }
    private async Task GenerateQrCodeAsync(string url)
    {
        Console.WriteLine($"生成登录二维码: {url}");
        using QRCodeGenerator qrGenerator = new QRCodeGenerator();
        using QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeImage = qrCode.GetGraphic(20);
        await File.WriteAllBytesAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qrcode.png"), qrCodeImage);
    }
}