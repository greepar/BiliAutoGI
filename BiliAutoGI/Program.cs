using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BiliAutoGI;

public class Program
{
    private static BiliApi _api = new BiliApi();
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
                string biliCookie = 
                    "CURRENT_QUALITY=116;b_lsid=DF41101D3_19711F3CB63;enable_feed_channel=ENABLE;home_feed_column=5;buvid4=B0CD35D1-A6A9-C086-064E-F429E2B1291057244-025041014-LdretZUHbLpv82orc1W8Uw%3D%3D;CURRENT_FNVAL=4048;__at_once=12660277589927498497;header_theme_version=CLOSE;buvid3=D7F321FD-1591-5196-6385-C014D2818C1F55088infoc;bp_t_offset_196431435=1071656544363347968;sid=pqlywu4w;SESSDATA=03e1c6b2%2C1763904881%2C0a992%2A51CjAUAdWOISgeXUWi84LjSCSPIFlZrBrGSY327K9kB3VuJYd3giFgg5gyrYRFSLkBULwSVkJMdW9lTjFPazdfY1BDSDFLZV8wNVhlMlQ4RFo3djVWMWZEeVEtdjJrRlFvOUwtZmMzRFVKWVBOSzQzNTd0S29leDZKRnA1Y1NBeFN2NGFpTHR6SVRBIIEC;b_nut=1744296855;_uuid=EBB48E21-3FEB-3BAB-CFF3-CE5CF5E101013F71144infoc;bili_jct=9c0ac050451288fa4172f63c63c6abbc;bili_ticket=eyJhbGciOiJIUzI1NiIsImtpZCI6InMwMyIsInR5cCI6IkpXVCJ9.eyJleHAiOjE3NDg2MTIwODAsImlhdCI6MTc0ODM1MjgyMCwicGx0IjotMX0.hI_q3rnwYp_fo2hhxWimquaDHuUFH3PzYPK6LyBZOWM;bili_ticket_expires=1748612020;bmg_af_switch=1;bmg_src_def_domain=i2.hdslb.com;browser_resolution=1439-713;buvid_fp=a030ea98f640371c8f5ee9e0ac95bb05;DedeUserID=196431435;DedeUserID__ckMd5=0d64f23f097dd937;enable_web_push=DISABLE;hit-dyn-v2=1;PVID=1;rpdid=|(J|)|lYu~R)0J'u~RRkmY)m)";
                await _api.BiliLoginAsync(biliCookie);
                Console.WriteLine(await _api.GetRoomIdAsync());
                await File.WriteAllTextAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bili_cookie.txt"), biliCookie);
                Console.WriteLine("文件bili_cookie.txt已生成，请检查");
                Console.ReadKey();
                var loginSuccess = await _api.BiliLoginAsync(biliCookie);
                if(loginSuccess)
                {
                    Console.WriteLine("B站登录成功");
                    bool needStream = !await _api.NeedStreamAsync();
                    Console.WriteLine("今日直播60min任务状态: " + (needStream ? "已完成" : "未完成"));
                        //依赖检查完毕，开始正式程序
                     if (await IfNeedStreamNowAsync())
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
        // bool needStream = !await _api.If60MinTaskFinishedAsync();
        var needStream = true; // 假设需要直播
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