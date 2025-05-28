namespace BiliAutoGI;

public class FfmpegController
{
    public static async Task FfmpegLiveAsync(string ffmpegFile,string streamFile,string rtmpUrl,string rtmpKey)
    {
        Console.WriteLine($"ffmpeg传入开始直播请求\n数据:ffmpeg文件{ffmpegFile},\n直播文件{streamFile},\n直播地址{rtmpUrl}，\n串流密钥{rtmpKey}");
    }
    
}