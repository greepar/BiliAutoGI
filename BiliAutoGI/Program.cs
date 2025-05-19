using System.Diagnostics;

namespace BiliAutoGI;

public static class Program
{
    public static async Task Main()
    {
        Console.WriteLine("使用说明:1.放入直播播放的视频stream.mp4在同目录下(确保视频大于70min，否则直播不能够完成)\n2.确保ffmpeg在同目录下或者在系统环境里\n3.如已明白按任意键继续");
        Console.ReadKey();
        if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "stream.mp4")))
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
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg")))
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
                Process.Start(Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg"));
                //依赖检查完毕，开始正式程序
                Console.ReadKey();
            }
        }
    }

    public static async Task TimeBasedTrigger()
    {
        int randomDelay = new Random().Next(0, 360);
        DateTime streamTime = DateTime.Today.AddHours(23).AddMinutes(55).AddSeconds(randomDelay);
        
           //获取当日直播状态   
           if (await BiliApi.NeedStream())
           {
               await BiliApi.BiliStreamEnabler();
           } else
           {
               Console.WriteLine("今天已直播，无需再次直播");
               Environment.Exit(0);
           }
        await StartLive();

        if (DateTime.Now >= DateTime.Today.AddHours(23).AddMinutes(55))
        {
            Console.WriteLine("立即直播 70 分钟");
            await StartLive();
        }
        else
        {
            // 今天已直播：在23:55之前不再直播，等待至23:55
            if (await BiliApi.NeedStream())
            {
                await Task.Delay(streamTime - DateTime.Now);
                Console.WriteLine($"等待{streamTime}再开始直播");
                await StartLive();
            }
            else
            {
                // 今天未直播：在22:58之前立即直播，否则等待至23:55
                if (DateTime.Now < DateTime.Today.AddHours(22).AddMinutes(58))
                {
                    Console.WriteLine("立即直播 70 分钟");
                    await StartLive();
                }
                else
                {
                    Console.WriteLine("今天直播任务已无法完成，等待 23:55 再开始直播");
                    await Task.Delay(DateTime.Today.AddHours(23).AddMinutes(55) - DateTime.Now);
                    await StartLive();
                }
            }
        }
    }

    

    public static async Task StartLive()
    {
        Console.WriteLine("开始直播模块\n开发中...");
    }
}