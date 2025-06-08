using System.Diagnostics;

namespace BiliAutoGI;

public static class FfmpegController
{
    public static void FfmpegLiveAsync(string ffmpegFile,string streamFile,string rtmpUrl,string rtmpKey)
    {
        if (rtmpKey == "" || rtmpUrl == "")
        {
            Console.WriteLine("\n传入数据为空，可能是开播失败了...");
            return;
        }
        Console.WriteLine($"\nffmpeg传入开始直播请求,当前时间{DateTime.Now}\n数据:\nffmpeg文件{ffmpegFile},\n直播文件{streamFile},\nrtmp直播地址{rtmpUrl}，\n串流密钥{rtmpKey}");
        var random = new Random();
        var randomMin = random.Next(10, 15);
        var randomSec = random.Next(10, 59);
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegFile,
            Arguments = $" -re -stream_loop -1 -i {streamFile} -vf \"scale=1280:720,fps=24,rotate=0.05*sin(2*PI*t)\" -c:v libx264 -preset veryfast -b:v 500k -maxrate 500k -bufsize 1000k -c:a aac -b:a 128k -ar 44100 -ac 2 -f flv -t 01:{randomMin}:{randomSec} \"{rtmpUrl}{rtmpKey}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process.Start(startInfo);
    }
    
}