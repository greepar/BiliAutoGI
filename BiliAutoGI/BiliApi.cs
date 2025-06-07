using System.Diagnostics;
using System.Net;
using System.Text.Json;
using QRCoder;
namespace BiliAutoGI;


public class BiliApi
{
    private static string _biliCookie =null!;
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
    public async Task<bool> BiliLoginAsync(string? inputCookie)
    {
        if (inputCookie != null)
        {
            //已存在Cookie，直接检查Cookie是否有效
            _biliCookie = inputCookie;
            _cookieContainer.SetCookies(new Uri("https://api.bilibili.com/"), _biliCookie);
            var cookiePairs = _biliCookie.Split(';');
            foreach (var pair in cookiePairs)
            {
                var cookieParts = pair.Split('=', 2);
                if (cookieParts.Length != 2) continue;
                var name = cookieParts[0].Trim();
                var value = cookieParts[1].Trim();
                // 添加Cookie到CookieContainer
                _cookieContainer.Add(new Uri("https://www.bilibili.com"), new Cookie(name, value) { Domain = ".bilibili.com" });
            }
            var checkLoginApi = "https://api.bilibili.com/x/web-interface/nav";
            var response = await _httpClient.GetAsync(checkLoginApi);
            using var jsonDoc = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
            if (response.IsSuccessStatusCode)
            {
                var isLogin = jsonDoc.RootElement.GetProperty("data").GetProperty("isLogin").GetBoolean();
                if (isLogin)
                {
                    //Cookie有效
                    Console.WriteLine("\nCookie有效，正在载入...");
                    return true;
                }
                //Cookie无效
                Console.WriteLine("response.message: " + jsonDoc.RootElement.GetProperty("message").GetString());
                Console.WriteLine("Cookie无效，请重新登录");
                await Program.SaveConfig(null);
                return false;
            }
            //连接失败
            Console.WriteLine("Cookie无效，请重新登录");
            return false;
        }
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
            for (int i = 0; i < 30 ; i++)
            {
                using var response = await _httpClient.GetAsync(loginCheckApi);
                var stringResponse = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(stringResponse);
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var apiResultCode = jsonDoc.RootElement.GetProperty("data").GetProperty("code").GetInt32();
                        if (apiResultCode == 0)
                        {
                            Console.WriteLine("登录成功，正在载入Cookie..."); 
                            var uri = new Uri("https://space.bilibili.com");
                            var cookie = _cookieContainer.GetCookies(uri);
                            _biliCookie = string.Join(";", cookie.Select(c => $"{c.Name}={c.Value}"));
                            Console.WriteLine("Cookie载入成功: " + _biliCookie);
                            await Program.SaveConfig(_biliCookie);
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
                await Task.Delay(2000); // 每2秒检查一次
            }
        }
        //30次轮询后仍未登录成功
        Console.WriteLine("获取登录二维码失败，可能是超时或网络问题。");
        return false;
    }
    public async Task<bool?> Check60MinStatusAsync()
    {
        //B站检查今日直播状态API
        try
        {
            var task60MinApiSource = "https://raw.githubusercontent.com/greepar/BiliAutoGI/refs/heads/master/api";
            using var ghResponse = await _httpClient.GetAsync(task60MinApiSource);
            if (!ghResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"获取API失败，状态码: {ghResponse.StatusCode}");
                return null;
            }
            var task60MinApi = await ghResponse.Content.ReadAsStringAsync();
            Console.WriteLine("\n获取最新API成功: " + task60MinApi);
            // string task60MinApi = "https://api.bilibili.com/x/activity_components/mission/info?task_id=6ERA4wloghvk5600&web_location=888.81821&w_rid=da939b1fbbfb9bff264b61baaec618c9&wts=1748099371";
            Console.WriteLine($"正在检查今日是否已经完成60min直播任务 {task60MinApi}");
            var response = await _httpClient.GetAsync(task60MinApi);
            if (response.IsSuccessStatusCode)
            {
                string stringResponse = await response.Content.ReadAsStringAsync();
                try
                { 
                    using var jsonDoc = JsonDocument.Parse(stringResponse);
                    // var apiResultCode = jsonRoot.GetProperty("code").GetInt32();
                    var rewardMessageFromJsonData = jsonDoc.RootElement.GetProperty("data").GetProperty("message").GetString();
                    var currentApiVersion = jsonDoc.RootElement.GetProperty("data").GetProperty("act_name").GetString();
                    Console.WriteLine($"当前API版本：{currentApiVersion}\n从API获取的消息:{rewardMessageFromJsonData}");
                    if (rewardMessageFromJsonData == "获取奖励")
                    {
                        return false;
                    }
                    Console.WriteLine("今天似乎还未完成直播任务，或任务状态不符合预期,将会开始今日直播。");
                    return true;
                }
                catch (JsonException e)
                {
                    Console.WriteLine("JSON 转换出错: " + e.Message);
                    throw;
                }
            }
            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    private string? GetCsrfFromCookie(string cookieString)
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
        string? csrf = GetCsrfFromCookie(_biliCookie);
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
                using var jsonDoc = JsonDocument.Parse(jsonResponse);
                var rtmpAddr = jsonDoc.RootElement.GetProperty("data").GetProperty("rtmp").GetProperty("addr").GetString()!;
                var rtmpKey = jsonDoc.RootElement.GetProperty("data").GetProperty("rtmp").GetProperty("code").GetString()!;
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
        using QRCodeGenerator qrGenerator = new QRCodeGenerator();
        using QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeImage = qrCode.GetGraphic(20);
        var qrcodePng = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qrcode.png");
        await File.WriteAllBytesAsync(qrcodePng, qrCodeImage);
        Console.WriteLine("二维码已生成在当前目录下: qrcode.png\n如未自动打开，请手动打开此文件。");
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo(qrcodePng)
                {
                    UseShellExecute = true 
                }
            };
            process.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"无法自动打开图片文件: {ex.Message}");
            Console.WriteLine($"请手动打开文件: {qrcodePng}");
        }
    }
}
