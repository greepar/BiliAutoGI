using System.Text.Json;
using System.Text.Json.Serialization;

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
    public async Task<bool> BiliLoginAsync(string inputCookie)
    {
        await Task.Delay(1);
        _biliCookie = inputCookie;
        //已存在Cookie，直接检查Cookie是否有效
        var checkLoginApi = "https://api.bilibili.com/x/web-interface/nav";
        
        //已登录，直接载入Cookie
        return true; 
        //未登录，开始登入程序
    }
    private async Task Check60MinStatusAsync()
    {
        _needStream = false;
        //B站检查今日直播状态API
        string task60MinApi = "https://api.bilibili.com/x/activity_components/mission/info?task_id=6ERA4wloghvk5600&web_location=888.81821&w_rid=da939b1fbbfb9bff264b61baaec618c9&wts=1748099371";
        try
        {
            Console.WriteLine($"正在检查今日是否已经完成60min直播任务 {task60MinApi}");
            var request = new HttpRequestMessage(HttpMethod.Get, task60MinApi);
            request.Headers.Add("Cookie", _biliCookie);
            HttpResponseMessage response = await _httpClient.SendAsync(request);
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
                        _needStream = false;
                    }
                    else
                    {
                        Console.WriteLine("今天似乎还未完成直播任务，或任务状态不符合预期,将会开始今日直播。");
                        _needStream = true;
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
            }
        }
        catch(Exception e)
        {
            //虽然我不知道这能出什么异常xD
            Console.WriteLine("CheckStatusAsync 外部出现异常: " + e.Message);
            throw;
        }
    }
    public async Task<bool> NeedStreamAsync()
    {
        await Check60MinStatusAsync();
        Console.WriteLine(_needStream ? "今天直播任务还未完成" : "今日直播任务已完成");
        return _needStream;
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
    public async Task<long?> GetRoomIdAsync()
    {
        if (string.IsNullOrEmpty(_biliCookie))
        {
            Console.WriteLine("Cookie string is null or empty.");
            return null;
        }

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.live.bilibili.com/xlive/app-blink/v1/highlight/getRoomHighlightState");
        request.Headers.Add("Cookie", _biliCookie);
        try
        {
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            string stringResponse = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(stringResponse);
            var jsonRoot = jsonDoc.RootElement;
            if (response.IsSuccessStatusCode)
            {
                var roomId = jsonRoot.GetProperty("data").GetProperty("room_id").GetInt64();
                return roomId;
            }
            else
            {
                Console.WriteLine($"请求失败，状态码: {jsonRoot.GetProperty("data").GetProperty("code").GetInt32()}, 错误信息: {jsonRoot.GetProperty("data").GetProperty("message").GetString()}");
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
     public async Task<string> StartLiveAsync()
    {
        string csrf = GetCsrfFromCookie(_biliCookie);
        if (string.IsNullOrEmpty(csrf))
        {
            return "{\"error\":\"Failed to get CSRF token from cookie.\"}";
        }
        //get csrf
        Console.WriteLine("csrf字段: " + csrf);
        var apiUrl = "https://api.live.bilibili.com/room/v1/Room/startLive";
        //get room_id
        long? roomId = await GetRoomIdAsync();
        Console.WriteLine($"room_id: {roomId}");
        if (!roomId.HasValue)
        {
            return "{\"error\":\"Failed to get room_id.\"}";
        }
        var formData = new Dictionary<string, string>
        {
            { "platform", "web_link" },
            { "room_id", "10431980" }, // 你的房间ID
            { "area_v2", "321" }, // 原神分区的area_id
            { "backup_stream", "0" },
            { "csrf", csrf },
            { "csrf_token", csrf }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
        {
            Content = new FormUrlEncodedContent(formData)
        };
        request.Headers.Add("Cookie", _biliCookie);
        try
        {
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine("请求已发送，API响应: " + jsonResponse);
                _isLogin = true;
                return "success";
            }
            Console.WriteLine($"登录失败，状态码: {response.StatusCode}");
            _isLogin = false;
                return "error";
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

    public static async Task GetStreamKeyAsync()
    {
        
    }
    public static async Task BiliStreamEnabler()
    {
        Console.WriteLine("开始直播，开发中...");
    }
}