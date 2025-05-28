using System.Runtime.InteropServices;

namespace BiliAutoGI;

public static class Program
{
    private static readonly BiliApi Api = new BiliApi();
    public static async Task Main()
    {
        Console.WriteLine("使用说明:1.放入直播播放的视频stream.mp4在同目录下(确保视频大于70min，否则直播不能够完成)\n2.确保ffmpeg在同目录下或者在系统环境里\n3.如已明白按任意键继续");
        Console.ReadKey();
        //程序同目录
        string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        //ffmpeg目录
        string ffmpegFile = Path.Combine(currentDirectory, "ffmpeg.exe");
        string streamFile = Path.Combine(currentDirectory, "stream.mp4");
        //非Linux平台情况下的ffmpeg文件目录
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ffmpegFile = "ffmpeg";
        }
        //检查文件是否存在
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
                
                //临时赋值Cookie
                string biliCookie = "";
                var loginSuccess = await Api.BiliLoginAsync(biliCookie);
                if(loginSuccess)
                {
                    Console.WriteLine("B站登录成功");
                        //依赖检查完毕，开始正式程序
                     if (await IfNeedStreamNowAsync())
                     {
                         var liveInfo = await Api.StartLiveAsync();
                         if (liveInfo != null)
                         {
                             await FfmpegController.FfmpegLiveAsync(ffmpegFile,streamFile,liveInfo.RtmpAddr,liveInfo.RtmpKey);
                             Console.ReadKey();
                         }
                         else
                         {
                             Console.WriteLine("<UNK>Cookie<UNK>");
                         }
                         Console.WriteLine("开始直播任务");
                     }
                     else
                     {
                         int randomDelay = new Random().Next(-70, 70);
                         Console.WriteLine("将等待到23:55开始直播任务");
                         await Task.Delay(DateTime.Now - DateTime.Today.AddHours(23).AddMinutes(55).AddSeconds(randomDelay));
                         // await StartLiveAsync(ffmpegFile,streamFile);
                         //等待到23:55附近开始直播
                     }
                }
                else
                {
                    Console.WriteLine("B站登录失败，请检查Cookie");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
                Console.ReadKey();
            }
        }
    }

    private static async Task<bool> IfNeedStreamNowAsync()
    { 
        var needStream = await Api.Check60MinStatusAsync();
        if (DateTime.Now >= DateTime.Today.AddHours(23).AddMinutes(55))
        {
            return true;
        }

        if (needStream.HasValue)
        {
            if (!needStream.Value)
            {
                Console.WriteLine("今日已完成60min直播任务，直播模块将不会启动");
                return false;
            }
        }
        
        if (DateTime.Now > DateTime.Today.AddHours(22).AddMinutes(58))
        {
            return false;
        }
        return true;
    }
}