using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using SIPSorcery.Media;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows;

class Program
{
    static async Task Main(string[] args) // 1- where to call, 2- user, 3- password, 4- path to audiofile
    {
        Console.WriteLine("SIP Audio Playback Example");

        // SIP settings
        string destination = "";          // where to call(without sip: etc)
        string sipUsername = "";          // our user
        string sipPassword = "";          // our password
        string audioFilePath = "";        // audiofile (RAW, 8, 16 kHz PCM)

        if (args.Length == 0)
        {
            //тест
            destination = "1014@192.168.10.160";
            sipUsername = "1012";
            sipPassword = "Ad123456";
            audioFilePath = "C:\\Users\\user\\Documents\\Рабочая\\ОбработкаГолосовогоНаДинамик\\audio.raw";
        }
        else
        {
            destination = args[0];
            sipUsername = args[1];
            sipPassword = args[2];
            audioFilePath = args[3];
        }
        CancellationTokenSource exitCts = new CancellationTokenSource();

        // logger (can remove it, if you dont need)
        AddConsoleLogger();

        // SIP-transport
        var sipTransport = new SIPTransport();
        sipTransport.EnableTraceLogs();

        // init UserAgent
        var userAgent = new SIPUserAgent(sipTransport, null);

        userAgent.ClientCallFailed += (uac, error, sipResponse) =>
        {
            Console.WriteLine($"Call failed: {error}");
            exitCts.Cancel();
        };

        userAgent.OnCallHungup += (dialog) => exitCts.Cancel();

        sipTransport.SIPRequestOutTraceEvent += (local, remote, request) =>
        {
            Console.WriteLine($"REQUEST OUT: {request}");
        };
        sipTransport.SIPResponseInTraceEvent += (local, remote, response) =>
        {
            Console.WriteLine($"RESPONSE IN: {response}");
        };

        // init media-session
        var windowsAudio = new WindowsAudioEndPoint(new AudioEncoder());
        var voipMediaSession = new VoIPMediaSession(windowsAudio.ToMediaEndPoints())
        {
            AcceptRtpFromAny = true
        };

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            if (userAgent.IsCalling || userAgent.IsRinging)
            {
                Console.WriteLine("Canceling in-progress call...");
                userAgent.Cancel();
            }
            else if (userAgent.IsCallActive)
            {
                Console.WriteLine("Hanging up the call...");
                userAgent.Hangup();
            }
            exitCts.Cancel();
        };

        Console.WriteLine($"Calling {destination}...");
        //make call
        var callTask = userAgent.Call(destination, sipUsername, sipPassword, voipMediaSession);

        bool callResult = await callTask;

        if (callResult)
        {
            Console.WriteLine("Call established. Playing audio...");

            await windowsAudio.PauseAudio();

            try
            {
                // playing audio
                await voipMediaSession.AudioExtrasSource.StartAudio();

                await voipMediaSession.AudioExtrasSource.SendAudioFromStream(new FileStream(audioFilePath, FileMode.Open), AudioSamplingRatesEnum.Rate16KHz);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing audio: {ex.Message}");
            }

            Console.WriteLine("Audio finished. Press Ctrl+C to exit.");
            //waiting for ctrl + c 
            //exitCts.Token.WaitHandle.WaitOne(); 
        }
        else
        {
            Console.WriteLine($"Call to {destination} failed.");
        }

        userAgent.Hangup();
        sipTransport.Shutdown();
        userAgent.Close();
        Console.WriteLine("Exiting...");
    }

    private static Microsoft.Extensions.Logging.ILogger AddConsoleLogger()
    {
        var serilogLogger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
        var factory = new SerilogLoggerFactory(serilogLogger);
        SIPSorcery.LogFactory.Set(factory);
        return factory.CreateLogger<Program>();
    }
}
