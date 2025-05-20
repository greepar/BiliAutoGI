using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BiliAutoGI;

public static class Program
{
    public static async Task Main()
    {
        Console.WriteLine("使用说明:1.放入直播播放的视频stream.mp4在同目录下(确保视频大于70min，否则直播不能够完成)\n2.确保ffmpeg在同目录下或者在系统环境里\n3.如已明白按任意键继续");
        Console.ReadKey();
        
        string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        
        string ffmpegFile = Path.Combine(currentDirectory, "ffmpeg.exe");
        string streamFile = Path.Combine(currentDirectory, "stream.mp4");
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ffmpegFile = "ffmpeg";
        }
        
        if (!File.Exists(Path.Combine(currentDirectory, "stream.mp4")))
        {
            Console.WriteLine("直播视频stream.mp4不存在，请放入同目录下");
            Console.ReadKey();
            Environment.Exit(0);
        }
        else
        {
            //check
            Console.WriteLine("stream.mp4存在");
            //check
            if (!File.Exists(ffmpegFile))
            {
                Console.WriteLine("ffmpeg.exe不存在，请检查");
                Console.ReadKey();
                Environment.Exit(0);
            }
            else
            {
                //check
                Console.WriteLine("ffmpeg存在");
                //check
                
                //依赖检查完毕，开始正式程序
                if (await NeedStreamNowAsync())
                {
                    await StartLiveAsync(ffmpegFile,streamFile);
                }
                else
                {
                    int randomDelay = new Random().Next(-70, 70);
                    Console.WriteLine("将等待到23:55开始直播任务");
                    await Task.Delay(DateTime.Now - DateTime.Today.AddHours(23).AddMinutes(55).AddSeconds(randomDelay));
                    await StartLiveAsync(ffmpegFile,streamFile);
                    //等待到23:55附近开始直播
                }
                Console.ReadKey();
            }
        }
    }

    private static async Task<bool> NeedStreamNowAsync()
    { 
        bool needStream = await BiliApi.NeedStreamAsync();
        if (DateTime.Now >= DateTime.Today.AddHours(23).AddMinutes(55))
        {
            return true;
        }

        if (!needStream)
        {
            return false;
        }

        if (DateTime.Now > DateTime.Today.AddHours(22).AddMinutes(58))
        {
            return false;
        }

        return true;
    }
    

    private static async Task StartLiveAsync(string ffmpegFile,string streamFile)
    {
        Console.WriteLine("开始直播模块\n开发中...");
        ProcessStartInfo ffmpegStartInfo = new ProcessStartInfo
        {
            FileName = ffmpegFile,
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        Process ffmpegProcess = Process.Start(ffmpegStartInfo);
    }
}