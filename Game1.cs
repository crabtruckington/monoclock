using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace monoclock
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private SpriteFont clockNumbersFont;
        private SpriteFont nowPlayingFont;
        private SpriteFont snoozeRegularFont;
        private SpriteFont alarmSetButtonsFont;

        bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        bool alarmEnabled = true;
        bool isAlarming = false;
        bool settingAlarm = false;
        bool displaySnooze = false;
        bool snoozing = false;
        bool displayNowPlaying = false;
        bool displayAlarmTime = false;
        bool alarmedToday = false;

        string alarmFile = "./alarm.ini";
        string clockFaceColorFile = "./clockface.ini";
        string alarmStatusFile = "./alarmstatus.ini";
        string alarmTime = "";
        string nowPlayingText = "";
        string[] musicToPlay = new string[0];
        static readonly string[] fileExtensions = new string [] { "aac", "ac3", "aiff", "aix", "asf", "asf_o", "asf_stream", 
                                                                  "avi", "avs", "bink", "bit", "cdg", "cdxl", "flac", "flic", 
                                                                  "flv", "h261", "h263", "h264", "hevc", "m4v", "matroska", 
                                                                  "matroska, webm", "mov", "mp4", "m4a", "3gp", "3g2", 
                                                                  "mp2", "mp3", "mp4", "mpeg", "mpeg1video", "mpeg2video", 
                                                                  "mpegvideo", "oga", "ogg", "ogv", "wav", "webm" };

        DateTime snoozeAlarmTime = DateTime.MaxValue;

        Color clockFaceColor = Color.White;
        Color clockFaceDisabledColor = Color.Gray;
        Color clockButtonOutlineColor = Color.Gray;

        List<Color> clockFaceColorsList = new List<Color>();
        List<Color> clockFaceDisabledColorsList = new List<Color>();
        int clockFaceColorIndex = 0;

        Texture2D alarmEnabledIcon;
        Texture2D alarmDisabledIcon;
        Texture2D hamburgerMenuIcon;
        Texture2D musicNoteIcon;
        Texture2D primitiveTexture;
        Rectangle alarmMinusOutline;
        Rectangle alarmPlusOutline;
        Rectangle snoozeOutline;
        Rectangle alarmBellOutline;
        Rectangle hamburgerOutline;
        Rectangle alarmTestOutline;
        Rectangle alarmStopOutline;

        Vector2 screenSize = new Vector2();
        Vector2 screenCenterVector = new Vector2();
        const int lowerRowTextAlign = 413;

        Task setAlarmTimeTask;
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken;

        Stopwatch clockRestartTimer = new Stopwatch();
        Stopwatch delayAlarmingOnSameTime = new Stopwatch();
        Stopwatch clockFaceSaveTimer = new Stopwatch();
        Stopwatch nowPlayingTextScrollTimer = new Stopwatch();
        
        MouseState currentMouseState = Mouse.GetState();
        MouseState lastMouseState = Mouse.GetState();

        ProcessStartInfo musicPlayerProcessInfo = new ProcessStartInfo();
        Process musicPlayerProcess;
        int totalSongs;
        int currentSong = 0;
        string nowPlayingDisplayText;
        int NowPlayingTextOffsetCounter = 0;
        int nowPlayingScrollDelayInMS = 500;
        const int nowPlayingMaximumTextWidth = 52;
        

        public Game1()
        {
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            graphics = new GraphicsDeviceManager(this)
            {                
                IsFullScreen = false,
                PreferredBackBufferWidth = 800,
                PreferredBackBufferHeight = 480
            };
            if (isLinux)
            {
                graphics.IsFullScreen = true;
            }
            graphics.ApplyChanges();

            // For some reason, graphics needs to be applied twice to take effect. Nonsense bug in monogame.
            graphics.PreferredBackBufferWidth = 800;
            graphics.PreferredBackBufferHeight = 480;
            if (isLinux)
            {
                graphics.IsFullScreen = true;
            }
            graphics.ApplyChanges();
        }

        protected override void Initialize()
        {
            //limit to 20fps
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromSeconds(1 / 10.0f);
            
            if (isLinux)
            {
                IsMouseVisible = false;
                Process fixPA = Process.Start("pactl", "load-module module-alsa-sink device='hw:0,0'");
                fixPA.WaitForExit();
                fixPA = Process.Start("pactl", "set-default-sink alsa_output.hw_0_0");
                fixPA.WaitForExit();
            }
            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            cancellationToken = cancellationTokenSource.Token;

            clockNumbersFont = Content.Load<SpriteFont>("clockNumbersFixedWidth");
            nowPlayingFont = Content.Load<SpriteFont>("clockNowPlaying");
            snoozeRegularFont = Content.Load<SpriteFont>("clockAlarm");
            alarmSetButtonsFont = Content.Load<SpriteFont>("clockAlarm");
            alarmEnabledIcon = Content.Load<Texture2D>("lcdsegmentbell50x50");
            alarmDisabledIcon = Content.Load<Texture2D>("lcdsegmentbelloff50x50");
            hamburgerMenuIcon = Content.Load<Texture2D>("hamburger50x50");
            musicNoteIcon = Content.Load<Texture2D>("lcdsegmentmusicnote50x50");

            int bottomScreenOffset = GraphicsDevice.Viewport.Height - 60;
            alarmMinusOutline = new Rectangle(5, bottomScreenOffset, 50, 50);
            alarmPlusOutline = new Rectangle(56, bottomScreenOffset, 50, 50);
            snoozeOutline = new Rectangle(120, bottomScreenOffset, 600, 50);
            alarmBellOutline = new Rectangle(740, bottomScreenOffset, 50, 50);
            hamburgerOutline = new Rectangle(740, 10, 50, 50);
            alarmTestOutline = new Rectangle(10, 10, 50, 50);
            alarmStopOutline = new Rectangle(0, 60, graphics.GraphicsDevice.Viewport.Width, bottomScreenOffset - 60);

            primitiveTexture = createButtonOutline();

            screenSize = new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            screenCenterVector = new Vector2(screenSize.X / 2, screenSize.Y / 2);
            
            alarmTime = GetAlarmTimeFromFile();
            GetSongsInMusicFolder();
            SetupClockFaceColorsLists();
            clockFaceColorIndex = GetClockFaceColorFromFile();
            alarmEnabled = GetAlarmStatusFromFile();
            if (clockFaceColorIndex >= clockFaceColorsList.Count)
            {
                clockFaceColorIndex = 0;
            }
            clockFaceColor = clockFaceColorsList[clockFaceColorIndex];
            clockFaceDisabledColor = clockFaceDisabledColorsList[clockFaceColorIndex];

            if (isLinux)
            {
                musicPlayerProcessInfo.FileName = "mpv";
            }
            else
            {
                musicPlayerProcessInfo.FileName = @"mpg123";
            }
            musicPlayerProcessInfo.Arguments = "";
            musicPlayerProcessInfo.CreateNoWindow = true;
#if DEBUG
            musicPlayerProcessInfo.FileName = @"T:\mpg123\mpg123.exe";
            musicPlayerProcessInfo.WindowStyle = ProcessWindowStyle.Hidden;
#endif

            Process mpg123Process = Process.Start(musicPlayerProcessInfo);
        }



        protected override void Update(GameTime gameTime)
        {
            currentMouseState = Mouse.GetState();

            //catch interface clicks
            if (currentMouseState.LeftButton == ButtonState.Pressed && lastMouseState.LeftButton == ButtonState.Released)
            {
                //adding alarm time
                if (MouseCursorInRectangle(currentMouseState.Position, alarmPlusOutline))
                {
                    if (clockRestartTimer.IsRunning)
                    {
                        clockRestartTimer.Stop();
                        clockRestartTimer.Reset();
                    }
                    settingAlarm = true;
                    displayAlarmTime = true;
                    if (setAlarmTimeTask != null)
                    {
                        if (setAlarmTimeTask.IsCompleted == false)
                        {
                            cancellationTokenSource.Cancel();
                            setAlarmTimeTask = Task.Run(() => { ChangeAlarmTime(1); }, cancellationToken);
                        }
                        else
                        {
                            setAlarmTimeTask = Task.Run(() => { ChangeAlarmTime(1); }, cancellationToken);
                        }
                    }
                    else
                    {
                        setAlarmTimeTask = Task.Run(() => { ChangeAlarmTime(1); }, cancellationToken );
                    }
                }

                //removing alarm time
                if (MouseCursorInRectangle(currentMouseState.Position, alarmMinusOutline))
                {
                    if (clockRestartTimer.IsRunning)
                    {
                        clockRestartTimer.Stop();
                        clockRestartTimer.Reset();
                    }
                    settingAlarm = true;
                    displayAlarmTime = true;
                    if (setAlarmTimeTask != null)
                    { 
                        if (setAlarmTimeTask.IsCompleted == false)
                        {
                            cancellationTokenSource.Cancel();
                            setAlarmTimeTask = Task.Run(() => { ChangeAlarmTime(-1); }, cancellationToken);
                        }
                        else
                        {
                            setAlarmTimeTask = Task.Run(() => { ChangeAlarmTime(-1); }, cancellationToken);
                        }
                    }
                    else
                    {
                        setAlarmTimeTask = Task.Run(() => { ChangeAlarmTime(-1); }, cancellationToken);
                    }
                }

                //display the alarm time without changing it
                if (MouseCursorInRectangle(currentMouseState.Position, alarmStopOutline) && (!isAlarming & !snoozing))
                {
                    if (clockRestartTimer.IsRunning)
                    {
                        clockRestartTimer.Stop();
                        clockRestartTimer.Reset();
                    }
                    displayAlarmTime = true;
                    clockRestartTimer.Start();
                }

                //alarm test button
                if (MouseCursorInRectangle(currentMouseState.Position, alarmTestOutline))
                {
                    if (isAlarming == false)
                    {
                        isAlarming = true;
                        displayNowPlaying = true;
                        displaySnooze = true;
                        if (!snoozing)
                        {
                            currentSong = 0;
                        }
                        snoozing = false;
                        GetSongsInMusicFolder();
                        totalSongs = musicToPlay.Length;
                    }
                }

                //toggling alarm
                if (MouseCursorInRectangle(currentMouseState.Position, alarmBellOutline))
                {
                    if (alarmEnabled == true)
                    {
                        alarmEnabled = false;
                        if (snoozing == true || isAlarming == true)
                        {
                            snoozeAlarmTime = DateTime.MaxValue;
                            snoozing = false;
                            displaySnooze = false;
                            isAlarming = false;
                            displayNowPlaying = false;
                            alarmedToday = true;
                            delayAlarmingOnSameTime.Reset();
                            delayAlarmingOnSameTime.Start();
                            if (isLinux)
                            {
                                StopMusicIfPlayingIfLinux();
                            }
                            else
                            {
                                StopMusicIfPlayingIfWindows();
                            }
                        }
                        WriteAlarmStatusToFile();
                    }
                    else
                    {
                        alarmEnabled = true;
                        WriteAlarmStatusToFile();
                    }
                }

                //enabling snooze
                if (MouseCursorInRectangle(currentMouseState.Position, snoozeOutline) && isAlarming)
                {
                    isAlarming = false;
                    alarmedToday = true;
                    snoozing = true;
                    displayNowPlaying = false;
                    delayAlarmingOnSameTime.Start();
                    snoozeAlarmTime = DateTime.Now.AddMinutes(7); //7 default
                    snoozeAlarmTime = snoozeAlarmTime.AddMilliseconds(1000 - snoozeAlarmTime.Millisecond);
                    if (nowPlayingTextScrollTimer.IsRunning)
                    {
                        nowPlayingTextScrollTimer.Stop();
                        nowPlayingTextScrollTimer.Reset();
                    }
                    if (isLinux)
                    {
                        StopMusicIfPlayingIfLinux();
                    }
                    else
                    {
                        StopMusicIfPlayingIfWindows();
                    }
                }

                //stopping alarm, cancelling snooze
                if (MouseCursorInRectangle(currentMouseState.Position, alarmStopOutline) && (isAlarming || snoozing))
                {
                    isAlarming = false;
                    alarmedToday = true;
                    displayNowPlaying = false;
                    snoozing = false;
                    displaySnooze = false;
                    snoozing = false;
                    delayAlarmingOnSameTime.Start();
                    snoozeAlarmTime = DateTime.MaxValue;
                    if (nowPlayingTextScrollTimer.IsRunning)
                    {
                        nowPlayingTextScrollTimer.Stop();
                        nowPlayingTextScrollTimer.Reset();
                    }
                    if (isLinux)
                    {
                        StopMusicIfPlayingIfLinux();
                    }
                    else
                    {
                        StopMusicIfPlayingIfWindows();
                    }
                }

                //changing clockface colors
                if (MouseCursorInRectangle(currentMouseState.Position, hamburgerOutline))
                {
                    clockFaceColorIndex += 1;
                    if (clockFaceColorIndex >= clockFaceColorsList.Count)
                    {
                        clockFaceColorIndex = 0;
                    }
                    clockFaceColor = clockFaceColorsList[clockFaceColorIndex];
                    clockFaceDisabledColor = clockFaceDisabledColorsList[clockFaceColorIndex];
                    clockFaceSaveTimer.Reset();
                    clockFaceSaveTimer.Start();
                }
            }

            //releasing mouse button
            if (currentMouseState.LeftButton == ButtonState.Released && lastMouseState.LeftButton == ButtonState.Pressed)
            {
                if (settingAlarm)
                {
                    settingAlarm = false;
                    clockRestartTimer.Start();
                    cancellationTokenSource = new CancellationTokenSource();
                    cancellationToken = cancellationTokenSource.Token;
                }
            }

            //saves the clock face color after 1 minute
            if (clockFaceSaveTimer.IsRunning && clockFaceSaveTimer.ElapsedMilliseconds > 60000)
            {
                clockFaceSaveTimer.Stop();
                clockFaceSaveTimer.Reset();
                WriteClockFaceColorToFile();
            }

            //show time display after setting alarm display
            if (clockRestartTimer.IsRunning && clockRestartTimer.ElapsedMilliseconds > 3000)
            {
                clockRestartTimer.Stop();
                clockRestartTimer.Reset();
                displayAlarmTime = false;
                WriteNewAlarmTimeToFile();
            }

            //start alarming
            if ((alarmTime == DateTime.Now.ToShortTimeString() && isAlarming == false && alarmedToday == false && alarmEnabled == true) ||
                (snoozeAlarmTime <= DateTime.Now && isAlarming == false && alarmedToday == false && alarmEnabled == true))
            {
                isAlarming = true;
                displayNowPlaying = true;
                displaySnooze = true;
                if (!snoozing)
                {
                    currentSong = 0;
                }
                snoozing = false;                
                GetSongsInMusicFolder();
                totalSongs = musicToPlay.Length;
            }

            //dont alarm a re-alarm if we cancel an alarm immediately
            if (delayAlarmingOnSameTime.IsRunning)
            {
                //2 minutes
                if (delayAlarmingOnSameTime.ElapsedMilliseconds > 120000) //120000 default
                {
                    delayAlarmingOnSameTime.Stop();
                    delayAlarmingOnSameTime.Reset();
                    alarmedToday = false;
                }
            }

            //play music if we're alarming
            if (isAlarming)
            {
                if (musicPlayerProcess == null || musicPlayerProcess.HasExited)
                {
                    string currentSongPathArgument;
                    if (isLinux)
                    {
                        currentSongPathArgument = "--no-video --really-quiet --audio-device=pulse/alsa_output.hw_0_0 \"" + Path.GetFullPath(musicToPlay[currentSong]) + "\"";
                    }
                    else
                    {
                        currentSongPathArgument = "\"" + Path.GetFullPath(musicToPlay[currentSong]) + "\"";
                    }
                    nowPlayingText = "Now Playing: " + Path.GetFileName(musicToPlay[currentSong]);
                    musicPlayerProcessInfo.Arguments = currentSongPathArgument;
                    musicPlayerProcess = Process.Start(musicPlayerProcessInfo);
                    NowPlayingTextOffsetCounter = 0;
                    if (nowPlayingTextScrollTimer.IsRunning)
                    {
                        nowPlayingTextScrollTimer.Stop();
                        nowPlayingTextScrollTimer.Reset();
                    }

                    if (currentSong < totalSongs - 1)
                    {
                        currentSong += 1;
                    }
                    else
                    {
                        currentSong = 0;
                    }
                }
            }

            lastMouseState = currentMouseState;
            
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            string displayTime;
            string displayDate = "";
            string displayAlarmText = "";
            Vector2 displayAlarmTextOffsetVector = new Vector2(0, 0);
            Vector2 displayAlarmTextPosition = new Vector2(0, 0);
            GraphicsDevice.Clear(Color.Black);
                        
            spriteBatch.Begin();

            //uncomment to see where the button edges are
            //spriteBatch.Draw(primitiveTexture, alarmBellOutline, clockButtonOutlineColor);
            //spriteBatch.Draw(primitiveTexture, alarmMinusOutline, clockButtonOutlineColor);
            //spriteBatch.Draw(primitiveTexture, alarmPlusOutline, clockButtonOutlineColor);
            //spriteBatch.Draw(primitiveTexture, snoozeOutline, clockButtonOutlineColor);
            //spriteBatch.Draw(primitiveTexture, hamburgerOutline, clockButtonOutlineColor);
            //spriteBatch.Draw(primitiveTexture, alarmStopOutline, clockButtonOutlineColor);
            //spriteBatch.Draw(primitiveTexture, alarmTestOutline, clockButtonOutlineColor);

            //draw clock numbers
            if (displayAlarmTime)
            {
                displayTime = alarmTime;
                displayAlarmText = "Alarm";
                displayAlarmTextOffsetVector = GetTextOffsetVector(displayAlarmText, nowPlayingFont);
                displayAlarmTextPosition = new Vector2(screenCenterVector.X - displayAlarmTextOffsetVector.X, 117);
            }
            else
            {
                displayTime = DateTime.Now.ToShortTimeString();
                displayDate = DateTime.Now.ToString("dddd, MMMM dd yyyy");
            }
            Vector2 clockTimeDisplayOffsetVector = GetTextOffsetVector(displayTime, clockNumbersFont);
            Vector2 clockTimeDisplayPosition = new Vector2(screenCenterVector.X - clockTimeDisplayOffsetVector.X, screenCenterVector.Y - clockTimeDisplayOffsetVector.Y - 20);
            spriteBatch.DrawString(clockNumbersFont, displayTime, clockTimeDisplayPosition, clockFaceColor);
            Vector2 clockDateDisplayOffsetVector = GetTextOffsetVector(displayDate, nowPlayingFont);
            Vector2 clockDateDisplayPosition = new Vector2(screenCenterVector.X - clockDateDisplayOffsetVector.X, 305);
            spriteBatch.DrawString(nowPlayingFont, displayDate, clockDateDisplayPosition, clockFaceColor);
            spriteBatch.DrawString(nowPlayingFont, displayAlarmText, displayAlarmTextPosition, clockFaceColor);
            

            //draw alarm set numbers
            spriteBatch.DrawString(alarmSetButtonsFont, "-", new Vector2(18, lowerRowTextAlign), clockFaceColor);
            spriteBatch.DrawString(alarmSetButtonsFont, "+", new Vector2(70, lowerRowTextAlign), clockFaceColor);

            //draw snoozing button
            if (displaySnooze)
            {
                if (snoozing)
                {
                    DateTime now = DateTime.Now;
                    TimeSpan snoozeTimeLeft = snoozeAlarmTime - now;
                    DateTime snoozeTimeLeftmmss = DateTime.MinValue.AddMinutes(snoozeTimeLeft.Minutes).AddSeconds(snoozeTimeLeft.Seconds);
                    string snoozeTimeLeftDisplay = snoozeTimeLeftmmss.ToString("mm:ss");
                    //flash snoozing every 1 second
                    if (now.Second % 2 == 0)
                    {
                        Vector2 snoozeDisplayOffset = GetTextOffsetVector("SNOOZING " + snoozeTimeLeftDisplay, snoozeRegularFont);
                        Vector2 snoozeDisplayPosition = new Vector2(screenCenterVector.X - snoozeDisplayOffset.X, lowerRowTextAlign);
                        spriteBatch.DrawString(snoozeRegularFont, "SNOOZING " + snoozeTimeLeftDisplay, snoozeDisplayPosition, clockFaceDisabledColor);
                    }
                }
                else
                {
                    Vector2 snoozeDisplayOffset = GetTextOffsetVector("SNOOZE", snoozeRegularFont);
                    Vector2 snoozeDisplayPosition = new Vector2(screenCenterVector.X - snoozeDisplayOffset.X, lowerRowTextAlign);
                    spriteBatch.DrawString(snoozeRegularFont, "SNOOZE", snoozeDisplayPosition, clockFaceColor);
                }
            }

            //drawn alarm toggle button
            if (alarmEnabled)
            {
                spriteBatch.Draw(alarmEnabledIcon, new Vector2(screenSize.X - 60, screenSize.Y - 60), clockFaceColor);
            }
            else
            {
                spriteBatch.Draw(alarmDisabledIcon, new Vector2(screenSize.X - 60, screenSize.Y - 60), Color.Red);
            }

            //drawn hamburger menu
            spriteBatch.Draw(hamburgerMenuIcon, new Vector2(screenSize.X - 60, 10), clockFaceColor);

            //draw alarmTest icon
            spriteBatch.Draw(musicNoteIcon, new Vector2(10, 10), clockFaceColor);

            //draw now playing text
            if (displayNowPlaying)
            {
                //624 pixels to use
                if (nowPlayingText.Length > nowPlayingMaximumTextWidth)
                {
                    if (nowPlayingTextScrollTimer.IsRunning == false)
                    {
                        nowPlayingTextScrollTimer.Start();
                    }
                    else if (nowPlayingTextScrollTimer.ElapsedMilliseconds > nowPlayingScrollDelayInMS)
                    {
                        NowPlayingTextOffsetCounter += 1;
                        if (nowPlayingMaximumTextWidth + NowPlayingTextOffsetCounter > nowPlayingText.Length)
                        {
                            NowPlayingTextOffsetCounter = 0;
                        }
                        nowPlayingTextScrollTimer.Restart();
                    }
                    nowPlayingDisplayText = nowPlayingText.Substring(NowPlayingTextOffsetCounter, nowPlayingMaximumTextWidth);
                }
                else
                {
                    nowPlayingDisplayText = nowPlayingText;
                }
                if (NowPlayingTextOffsetCounter == 0 || (nowPlayingMaximumTextWidth + NowPlayingTextOffsetCounter == nowPlayingText.Length))
                {
                    nowPlayingScrollDelayInMS = 2500;
                }
                else
                {
                    nowPlayingScrollDelayInMS = 500;
                }
                Vector2 nowPlayingDisplayOffset = GetTextOffsetVector(nowPlayingDisplayText, nowPlayingFont);
                Vector2 nowPlayingDisplayPosition = new Vector2(screenCenterVector.X - nowPlayingDisplayOffset.X, 24);
                spriteBatch.DrawString(nowPlayingFont, nowPlayingDisplayText, nowPlayingDisplayPosition, clockFaceColor);
            }

            spriteBatch.End();

            base.Draw(gameTime);
        }

        //cancels music if alarm is supposed to be stopping
        private void StopMusicIfPlayingIfLinux()
        {
            if (musicPlayerProcess.HasExited == false)
            {
                Process mpg123kill = Process.Start("/bin/bash", " -c 'pkill -f mpv'");
            }
        }

        private void StopMusicIfPlayingIfWindows()
        {
            if (musicPlayerProcess.HasExited == false)
            {
                musicPlayerProcess.Kill();
            }
        }

        //determines if the mouse cursor is over a button
        private bool MouseCursorInRectangle(Point mousePosition, Rectangle targetRectangle)
        {
            bool inRectangle = false;

            if (mousePosition.X >= targetRectangle.X && mousePosition.X <= targetRectangle.X + targetRectangle.Width &&
                mousePosition.Y >= targetRectangle.Y && mousePosition.Y <= targetRectangle.Y + targetRectangle.Height)
            {
                inRectangle = true;
            }

            return inRectangle;
        }

        //determines where to position centered text
        private Vector2 GetTextOffsetVector(string text, SpriteFont font)
        {
            Vector2 textSize = font.MeasureString(text);
            return new Vector2((int)Math.Round(textSize.X, 0) / 2, (int)Math.Round(textSize.Y, 0) / 2);
        }

        //creates a blank texture for primitive drawing 
        public Texture2D createButtonOutline()
        {
            Texture2D texture;

            texture = new Texture2D(GraphicsDevice, 1, 1);
            texture.SetData(new Color[] { clockButtonOutlineColor });

            return texture;
        }
                
        //changes the alarm time, takes an integer (negative for decrease)
        private void ChangeAlarmTime(int timeModifier)
        {
            Stopwatch alarmSettingTimer = new Stopwatch();
            alarmSettingTimer.Start();
            while (settingAlarm)
            {
                if (alarmSettingTimer.ElapsedMilliseconds < 2000 || !(alarmTime.EndsWith("0 AM") || alarmTime.EndsWith("0 PM")))
                {
                    alarmTime = DateTime.Parse(alarmTime).AddMinutes(1 * timeModifier).ToShortTimeString();
                }
                else
                {
                    alarmTime = DateTime.Parse(alarmTime).AddMinutes(10 * timeModifier).ToShortTimeString();
                }    
                Thread.Sleep(250);
            }
            alarmSettingTimer.Stop();
            alarmSettingTimer.Reset();
        }

        //reads the alarm time the ini file
        private string GetAlarmTimeFromFile()
        {
            string alarmTime = "";
            if (File.Exists(alarmFile))
            {
                alarmTime = File.ReadAllText(alarmFile);
            }
            else
            {
                string defaultAlarmTime = "12:00 AM";
                File.WriteAllText(alarmFile, defaultAlarmTime);
                alarmTime = defaultAlarmTime;
            }
            return alarmTime;
        }

        //write alarm time to alarm file
        private void WriteNewAlarmTimeToFile()
        {
            File.WriteAllText(alarmFile, alarmTime);
        }

        //reads the clockface color from the color file
        private int GetClockFaceColorFromFile()
        {
            int clockFaceColorFromFile = 0;
            if (File.Exists(clockFaceColorFile))
            {
                if (int.TryParse(File.ReadAllText(clockFaceColorFile), out clockFaceColorFromFile));
            }
            else
            {
                File.WriteAllText(clockFaceColorFile, clockFaceColor.ToString());
            }
            return clockFaceColorFromFile;
        }

        //writes the clock face color to the color file
        private void WriteClockFaceColorToFile()
        {
            File.WriteAllText(clockFaceColorFile, clockFaceColorIndex.ToString());
        }

        //reads the current alarm enabled/disabled status from a file
        private bool GetAlarmStatusFromFile()
        {
            int alarmStatusFromFile = 1;
            if (File.Exists(alarmStatusFile))
            {
                if (int.TryParse(File.ReadAllText(alarmStatusFile), out alarmStatusFromFile));
            }
            else
            {
                File.WriteAllText(alarmStatusFile, alarmStatusFromFile.ToString());
            }
            return Convert.ToBoolean(alarmStatusFromFile);
        }

        //writes the current alarm enabled/disabled status to a file
        private void WriteAlarmStatusToFile()
        {
            File.WriteAllText(alarmStatusFile, (Convert.ToInt32(alarmEnabled)).ToString());
        }

        //find the songs we are supposed to play
        private void GetSongsInMusicFolder()
        {
            musicToPlay = Directory.GetFiles(@"./Content/music", "*.*", SearchOption.AllDirectories).Where(s => fileExtensions.Any(s.EndsWith)).ToArray();
        }

        //list of clock face colors, manually make sure enabled/disabled colors are aligned between lists
        private void SetupClockFaceColorsLists()
        {
            clockFaceColorsList.Add(Color.White);
            clockFaceColorsList.Add(new Color(72, 159, 247));//light blue
            clockFaceColorsList.Add(new Color(9, 40, 240));//dark blue
            clockFaceColorsList.Add(new Color(21, 232, 214));//teal
            clockFaceColorsList.Add(new Color(8, 247, 163));//blue green
            clockFaceColorsList.Add(new Color(36, 181, 16));//dark green
            clockFaceColorsList.Add(new Color(0, 255, 4));//neon green
            clockFaceColorsList.Add(new Color(240, 130, 12));//orange
            clockFaceColorsList.Add(new Color(237, 7, 30));//red
            clockFaceColorsList.Add(new Color(240, 22, 236));//neon pink
            clockFaceColorsList.Add(new Color(247, 151, 148));//soft pink
            clockFaceColorsList.Add(new Color(226, 242, 3));//yellow
            clockFaceColorsList.Add(new Color(162, 174, 224));//light purple
            clockFaceColorsList.Add(new Color(179, 14, 230));//purple
            clockFaceColorsList.Add(new Color(58, 28, 252));//dark purple

            clockFaceDisabledColorsList.Add(Color.Gray);
            clockFaceDisabledColorsList.Add(new Color(36, 81, 125));//light blue disabled
            clockFaceDisabledColorsList.Add(new Color(20, 36, 140));//dark blue disabled
            clockFaceDisabledColorsList.Add(new Color(13, 122, 113));//teal disabled
            clockFaceDisabledColorsList.Add(new Color(9, 150, 101));//blue green disabled
            clockFaceDisabledColorsList.Add(new Color(24, 120, 12));//dark green disabled
            clockFaceDisabledColorsList.Add(new Color(16, 122, 0));//neon green disabled
            clockFaceDisabledColorsList.Add(new Color(125, 67, 5));//orange disabled
            clockFaceDisabledColorsList.Add(new Color(128, 8, 20));//red disabled
            clockFaceDisabledColorsList.Add(new Color(130, 26, 128));//neon pink disabled
            clockFaceDisabledColorsList.Add(new Color(191, 103, 100));//soft pink disabled
            clockFaceDisabledColorsList.Add(new Color(167, 176, 42));//yellow disabled
            clockFaceDisabledColorsList.Add(new Color(116, 125, 161));//light purple disabled
            clockFaceDisabledColorsList.Add(new Color(77, 6, 99));//purple disabled
            clockFaceDisabledColorsList.Add(new Color(41, 21, 173));//dark purple disabled
        }
    }
}
