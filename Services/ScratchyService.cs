using Android.App;
using Android.Content;
using Android.Media;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using System;
using System.Diagnostics;

namespace Scratchy.Services
{
    [Service]
    [IntentFilter(new[] { ActionStop, ActionLearn, ActionMatch })]
    public class ScratchyService : Service 
    {
       
        public const string ActionStop = "com.xamarin.action.STOP";
        public const string ActionLearn = "com.ynformatics.action.LEARN";
        public const string ActionMatch = "com.ynformatics.action.MATCH";

        private MediaPlayer player;
        private AudioManager audioManager;
        private WifiManager wifiManager;
        private WifiManager.WifiLock wifiLock;
        private AudioStream audioStream;
        private const int NotificationId = 1;

        uint time = 0;

        Stopwatch stopwatch = Stopwatch.StartNew();

        bool silent = false;
        short silenceThreshold = 1000;
        long silenceStarted;

        Album currentlyPlayingAlbum;
        uint learningAlbumId = 0;

        Database database = new Database();
        Spectrum spectrum = new Spectrum();

        static String audioStoragePath = Android.OS.Environment.GetExternalStoragePublicDirectory(
             "Scratchy/Music") + "/";

        enum State { Idle, WaitMatch, Matching, Playing, WaitLearn, Learning }
        State state = State.Idle;

        /// <summary>
        /// On create simply detect some of our managers
        /// </summary>
        public override void OnCreate()
        {
            base.OnCreate();
            //Find our audio and notificaton managers
            audioManager = (AudioManager)GetSystemService(AudioService);
            wifiManager = (WifiManager)GetSystemService(WifiService);

            database.Load();
            Console.WriteLine("Database loaded");

            audioStream = new AudioStream(11025, 2048);
            audioStream.OnBroadcast += AudioStream_OnBroadcast;
        }

        private void AudioStream_OnBroadcast(object sender, EventArgs<byte[]> e)
        {
            switch (state)
            {          
                case State.WaitLearn:
                    if (IsSilence(e.Value))
                        break;

                    Console.WriteLine("Learning!");

                    StopAudio(); PlayAudio("intro.wav");

                    time = 0;
                    spectrum.Clear();
                    spectrum.AppendSample(e.Value, time);
                   
                    state = State.Learning;
                    break;

                case State.Learning:
                    if(time++ >= 100)
                    {
                        Console.WriteLine("Done Learning!");

                        StopAudio();   PlayAudio("ready.mp3");

                        var fps = spectrum.GetFingerprints(learningAlbumId);
                        database.AddFingerprints(fps);
                        database.Save();                                           

                        state = State.Idle;
                        break;
                    }
                    else
                        spectrum.AppendSample(e.Value, time);
                    break;            

                case State.WaitMatch:
                    if (IsSilence(e.Value))
                        break;

                    Console.WriteLine("Matching!");
                    PlayAudio("intro.wav");

                    time = 0;
                    spectrum.Clear();
                    spectrum.AppendSample(e.Value, time);
                   
                    silent = false;                  

                    state = State.Matching;
                    break;

                case State.Matching:
                    if (time++ >= 70)
                    {
                        StopAudio(); // stop intro

                        var mfps = spectrum.GetFingerprints(learningAlbumId);
                        var albumId = database.GetMatch(mfps);

                        if (albumId != null)
                        {
                            Console.WriteLine("Match album " + albumId);
                            PlayAlbum(albumId + ".m3u");
                            state = State.Playing;
                        }
                        else
                        {
                            Console.WriteLine("No match");
                            state = State.WaitMatch;
                        }
                      
                        break;
                    }
                    spectrum.AppendSample(e.Value, time);
                    break;

                case State.Playing:
                    if (silent)
                    {
                        if (!IsSilence(e.Value)) // silent -> not silent
                        {
                            player.SetVolume(1, 1);
                            silent = false;
                        }
                        else // still silent
                        {
                            if (stopwatch.ElapsedMilliseconds - silenceStarted > 5000)
                            {
                                Console.WriteLine("Stopping after silence!");
                                StopAudio();
                                state = State.WaitMatch;
                            }
                        }
                    }
                    else
                    {
                        if (IsSilence(e.Value)) // going silent for the first time
                        {             
                            silent = true;
                            silenceStarted = stopwatch.ElapsedMilliseconds;
                        }
                    }

                    break;

                case State.Idle:
                    break;

            }         
        }

        void PlayAlbum( string m3ufile)
        {
            currentlyPlayingAlbum = new Album(audioStoragePath + m3ufile);

            player.Completion += Player_Completion;
            PlayAudio(currentlyPlayingAlbum.GetNextTrack());
        }

        private void Player_Completion(object sender, EventArgs e)
        {
            var nextTrack = currentlyPlayingAlbum.GetNextTrack();

            if (!string.IsNullOrEmpty(nextTrack))            
                PlayAudio(nextTrack);
        }

        bool IsSilence( byte[] data)
        {
            short threshold = Average(data);
            Console.WriteLine("S:" + threshold);
            return threshold < silenceThreshold;
        }

        short Average(byte[] data)
        {
            double total = 0;
            for (int i = 0; i < data.Length / 2; i += 2)
            {
                short val = (short)(data[i] | data[i + 1] << 8);
            
                total += Math.Abs(val);
            }
            return (short)(total/(data.Length / 2));
        }
   
        /// <summary>
        /// Don't do anything on bind
        /// </summary>
        /// <param name="intent"></param>
        /// <returns></returns>
        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {

            switch (intent.Action) {
                case ActionStop: StopAudio(); break;
                case ActionLearn: Learn(intent.GetStringExtra("data")); break;
                case ActionMatch: Match(); break;
            }

            //Set sticky as we are a long running operation
            return StartCommandResult.Sticky;
        }

        private void IntializePlayer()
        {
            player = new MediaPlayer();

            //Tell our player to sream music
            player.SetAudioStreamType(Android.Media.Stream.Music);

            //Wake mode will be partial to keep the CPU still running under lock screen
            player.SetWakeMode(ApplicationContext, WakeLockFlags.Partial);

            //When we have prepared the song start playback
            player.Prepared += (sender, args) => player.Start();

            //When we have reached the end of the song stop ourselves, however you could signal next track here.
            player.Completion += (sender, args) => StopAudio();

            player.Error += (sender, args) =>
            {
                //playback error
                Console.WriteLine("Error in playback resetting: " + args.What);
                StopAudio();//this will clean up and reset properly.
            };
        }

        private async void PlayAudio(string fileName)
        {
            Console.WriteLine("Play: " + fileName);          

            if (player == null) {
              IntializePlayer();
            }
            player.SetVolume(1, 1);
            if (player.IsPlaying)
                return;

            try
            {      
                Java.IO.FileInputStream fis = new Java.IO.FileInputStream(audioStoragePath + fileName);
                await player.SetDataSourceAsync(fis.FD);
                
                player.PrepareAsync();
                AquireWifiLock();
                StartForeground();
            }
            catch (Exception ex) {
                //unable to start playback log error
                Console.WriteLine("Unable to start playback: " + ex);
            }
        }


        private void StopAudio()
        {
            state = State.WaitMatch;
            if (player == null)
                return;

            if (player.IsPlaying)
                player.Stop();

            player.Reset();
            StopForeground(true);
            ReleaseWifiLock();
        }

        /// <summary>
        /// When we start on the foreground we will present a notification to the user
        /// When they press the notification it will take them to the main page so they can control the music
        /// </summary>
        private void StartForeground()
        {         
            var pendingIntent = PendingIntent.GetActivity(ApplicationContext, 0,
                            new Intent(ApplicationContext, typeof(MainActivity)),
                            PendingIntentFlags.UpdateCurrent);

            var notification = new Notification
            {
                TickerText = new Java.Lang.String("Song started!"),
                Icon = Resource.Drawable.ic_stat_av_play_over_video
            };
            notification.Flags |= NotificationFlags.OngoingEvent;
            notification.SetLatestEventInfo(ApplicationContext, "Xamarin Streaming",
                            "Playing music!", pendingIntent);
            StartForeground(NotificationId, notification);
        }

        private void Learn(string albumName)
        {
            state = State.WaitLearn;

            learningAlbumId = database.NameToId(albumName);
            audioStream.Start();
        }
        private void Match()
        {
            state = State.WaitMatch;
            audioStream.Start();
        }   


        /// <summary>
        /// Lock the wifi so we can still stream under lock screen
        /// </summary>
        private void AquireWifiLock()
        {
            if (wifiLock == null){
                wifiLock = wifiManager.CreateWifiLock(WifiMode.Full, "xamarin_wifi_lock");
            } 
            wifiLock.Acquire();
        }

        /// <summary>
        /// This will release the wifi lock if it is no longer needed
        /// </summary>
        private void ReleaseWifiLock()
        {
            if (wifiLock == null)
                return;

            wifiLock.Release();
            wifiLock = null;
        }

        /// <summary>
        /// Properly cleanup of your player by releasing resources
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();
            if (player != null)
            {
                player.Release();
                player = null;
            }
        }

    }
}
