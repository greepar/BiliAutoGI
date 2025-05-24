using System.Text.Json;
using System.Text.Json.Serialization;

namespace BiliAutoGI;

public class BiliApi
{
    // 对应 "data" 对象，我们只关心 "message" (可能还有 "status" 或 "task_finished" 来辅助判断)
    private class MinimalTaskData
    {
        // 你最关心的字段
        public string Message { get; set; }

        // 如果需要根据状态或完成情况来决定 Message 的含义，也可以包含它们
        public int Status { get; set; }
        [JsonPropertyName("task_finished")] // JSON 中是 task_finished
        public bool TaskFinished { get; set; }
    }
    private class MinimalApiResponse
    {
        // Code 通常是必须的，用来判断请求是否成功
        public int Code { get; set; }
        // 顶层的 Message 可能对调试有用，但如果只关心 data.message，可以省略
        // public string Message { get; set; }
        public MinimalTaskData Data { get; set; }
    }
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
    public static async Task<bool> NeedStreamAsync()
    {
        await BiliLoginAsync();
        Task.Run(CheckStatusAsync);
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
    // 用于解析 getRoomHighlightState API 的响应
    private class RoomHighlightStateResponseData
    {
        public int room_id { get; set; }
    }
    private class RoomHighlightStateResponse
    {
        public int code { get; set; }
        public string message { get; set; }
        public RoomHighlightStateResponseData data { get; set; }
    }
    public async Task<long?> GetRoomHighlightStateAsync(string cookieString)
    {
        if (string.IsNullOrEmpty(cookieString))
        {
            Console.WriteLine("Cookie string is null or empty.");
            return null;
        }

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.live.bilibili.com/xlive/app-blink/v1/highlight/getRoomHighlightState");
        request.Headers.Add("Cookie", cookieString);

        try
        {
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode(); // 抛出异常如果状态码不是 2xx

            string jsonResponse = await response.Content.ReadAsStringAsync();
            // Console.WriteLine($"GetRoomHighlightState Response: {jsonResponse}"); // 调试用

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<RoomHighlightStateResponse>(jsonResponse, options);

            if (result != null && result.code == 0 && result.data != null)
            {
                return result.data.room_id;
            }
            else
            {
                Console.WriteLine($"Failed to get room_id. Code: {result?.code}, Message: {result?.message}");
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
     public async Task<string> StartLiveAsync(string cookieString, int areaId)
    {
        string csrf = GetCsrfFromCookie(cookieString);
        if (string.IsNullOrEmpty(csrf))
        {
            return "{\"error\":\"Failed to get CSRF token from cookie.\"}";
        }

        long? roomId = await GetRoomHighlightStateAsync(cookieString);
        if (!roomId.HasValue)
        {
            return "{\"error\":\"Failed to get room_id.\"}";
        }

        var apiUrl = "https://api.live.bilibili.com/room/v1/Room/startLive";

        var formData = new Dictionary<string, string>
        {
            { "platform", "web_link" },
            { "room_id", roomId.Value.ToString() },
            { "area_v2", areaId.ToString() },
            { "backup_stream", "0" },
            { "csrf", csrf },
            { "csrf_token", csrf }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
        {
            Content = new FormUrlEncodedContent(formData)
        };
        request.Headers.Add("Cookie", cookieString);
        // User-Agent 已经通过 _httpClient.DefaultRequestHeaders 设置了

        try
        {
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            // B站这个接口即使失败也可能返回200，需要检查返回的JSON内容
            // response.EnsureSuccessStatusCode(); // 这里可能不适用，看具体API行为

            string jsonResponse = await response.Content.ReadAsStringAsync();
            // 你可能需要解析这个jsonResponse来确认是否成功
            // 例如:
            // var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            // var result = JsonSerializer.Deserialize<StartLiveApiResponse>(jsonResponse, options); // 假设你有这样一个类
            // if (result != null && result.code == 0) { // 假设成功返回 code 0
            //     Console.WriteLine("Successfully started live!");
            // } else {
            //     Console.WriteLine($"Failed to start live: {result?.message}");
            // }
            return jsonResponse;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"HttpRequestException in StartLiveAsync: {e.Message}");
            return $"{{\"error\":\"HttpRequestException: {e.Message}\"}}";
        }
        catch (Exception e)
        {
            Console.WriteLine($"Generic Exception in StartLiveAsync: {e.Message}");
            return $"{{\"error\":\"Generic Exception: {e.Message}\"}}";
        }
    }
    public static async Task BiliLoginAsync()
    {
        //检查登录
        //已登录，直接载入Cookie
        //未登录，开始登入程序
    }
    private async Task CheckStatusAsync()
    {
        //检查是否登录
        Console.WriteLine("检查今日是否完成直播60min任务，开发中...");
        _needStream = false;
        //B站检查今日直播状态API
        string fullApiUrl = "https://api.bilibili.com/x/activity_components/mission/info?task_id=6ERA4wloghvk5600&web_location=888.81821&w_rid=da939b1fbbfb9bff264b61baaec618c9&wts=1748099371";
        try
        {
            Console.WriteLine($"Fetching award status from: {fullApiUrl}");
            HttpResponseMessage response = await _httpClient.GetAsync(fullApiUrl);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"API Response JSON: {jsonResponse}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // 推荐加上，方便匹配
                };
                // 使用最小化的类来反序列化
                MinimalApiResponse apiResult = JsonSerializer.Deserialize<MinimalApiResponse>(jsonResponse, options);

                if (apiResult != null && apiResult.Code == 0 && apiResult.Data != null)
                {
                    string dataMessageText = apiResult.Data.Message;
                    Console.WriteLine(
                        $"API returned data.message: '{dataMessageText}' (Status: {apiResult.Data.Status}, TaskFinished: {apiResult.Data.TaskFinished})");

                    bool isClaimable = (dataMessageText == "获取奖励");
                    // return (isClaimable, dataMessageText);
                }
                else
                {
                    Console.WriteLine(
                        $"API call successful but data indicates an issue or unexpected structure: Code={apiResult?.Code}");
                    // return (false, $"Error: API Code {apiResult?.Code}");
                }
            }
            // ... (其他 catch 和错误处理) ...
            // catch (JsonException jsonEx)
            // {
            //     Console.WriteLine(
            //         $"JSON Deserialization error: {jsonEx.Message}. JSON: {await response.Content.ReadAsStringAsync()}");
            //     return (false, "Error: JSON parsing failed.");
            // }
            // catch (Exception ex)
            // {
            //     Console.WriteLine($"An error occurred: {ex.Message}");
            //     return (false, $"Error: {ex.Message}");
            // }
            // return (false, "Unknown error or request failed before parsing."); // 确保所有路径都有返回值
        }
        catch(Exception)
        {
            Console.WriteLine("test");
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