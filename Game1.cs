using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        string alarmTime = "";
        string clockFaceColorFile = "./clockface.ini";
        string snoozeAlarmTime = null;
        string nowPlayingText = "";
        string[] musicToPlay = new string[0];

        Color clockFaceColor = Color.White;
        Color clockFaceDisabledColor = Color.Gray;
        Color clockButtonOutlineColor = Color.Gray;

        List<Color> clockFaceColorsList = new List<Color>();
        List<Color> clockFaceDisabledColorsList = new List<Color>();
        int clockFaceColorIndex = 0;

        Texture2D alarmEnabledIcon;
        Texture2D alarmDisabledIcon;
        Texture2D hamburgerMenuIcon;
        Texture2D primitiveTexture;
        Rectangle alarmMinusOutline;
        Rectangle alarmPlusOutline;
        Rectangle snoozeOutline;
        Rectangle alarmBellOutline;
        Rectangle hamburgerOutline;
        Rectangle alarmStopOutline;

        Vector2 screenSize = new Vector2();
        Vector2 screenCenterVector = new Vector2();
        int lowerRowTextAlign = 333;

        Task setAlarmTimeTask;
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken;

        Stopwatch clockRestartTimer = new Stopwatch();
        Stopwatch delayAlarmingOnSameTime = new Stopwatch();
        Stopwatch clockFaceSaveTimer = new Stopwatch();
        Stopwatch clockFlashTimer = new Stopwatch();

        MouseState currentMouseState = Mouse.GetState();
        MouseState lastMouseState = Mouse.GetState();

        ProcessStartInfo mpg123ProcessInfo = new ProcessStartInfo();
        Process mpg123Process;
        int totalSongs;
        int currentSong = 0;


        public Game1()
        {
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            graphics = new GraphicsDeviceManager(this)
            {
                IsFullScreen = false,
                PreferredBackBufferWidth = 800,
                PreferredBackBufferHeight = 400
            };
            graphics.ApplyChanges();

            // For some reason, graphics needs to be applied twice to take effect. Nonsense bug in monogame.
            graphics.PreferredBackBufferWidth = 800;
            graphics.PreferredBackBufferHeight = 400;
            graphics.ApplyChanges();
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();
        }

        protected override void LoadContent()
        {
            Console.WriteLine("Is Linux: " + isLinux.ToString());
            spriteBatch = new SpriteBatch(GraphicsDevice);
            cancellationToken = cancellationTokenSource.Token;

            clockNumbersFont = Content.Load<SpriteFont>("clockNumbers");
            nowPlayingFont = Content.Load<SpriteFont>("clockNowPlaying");
            snoozeRegularFont = Content.Load<SpriteFont>("clockAlarm");
            alarmSetButtonsFont = Content.Load<SpriteFont>("clockAlarm");
            //clockNumbersFont = Content.Load<SpriteFont>("commodoreAngled");
            //nowPlayingFont = Content.Load<SpriteFont>("commodoreAngledNowPlaying");
            //snoozeRegularFont = Content.Load<SpriteFont>("commodoreAngledNormal");
            //alarmSetButtonsFont = Content.Load<SpriteFont>("commodoreAngledNormal");
            alarmEnabledIcon = Content.Load<Texture2D>("lcdsegmentbell50x50");
            alarmDisabledIcon = Content.Load<Texture2D>("lcdsegmentbelloff50x50");
            hamburgerMenuIcon = Content.Load<Texture2D>("hamburger50x50");

            alarmMinusOutline = new Rectangle(5, 340, 50, 50);
            alarmPlusOutline = new Rectangle(56, 340, 50, 50);
            snoozeOutline = new Rectangle(120, 340, 600, 50);
            alarmBellOutline = new Rectangle(740, 340, 50, 50);
            hamburgerOutline = new Rectangle(740, 10, 50, 50);
            alarmStopOutline = new Rectangle(0, 60, graphics.GraphicsDevice.Viewport.Width, 280);

            primitiveTexture = createButtonOutline();

            screenSize = new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            screenCenterVector = new Vector2(screenSize.X / 2, screenSize.Y / 2);
            
            alarmTime = GetAlarmTimeFromFile();
            GetSongsInMusicFolder();
            SetupClockFaceColorsLists();
            clockFaceColorIndex = GetClockFaceColorFromFile();
            if (clockFaceColorIndex >= clockFaceColorsList.Count)
            {
                clockFaceColorIndex = 0;
            }
            clockFaceColor = clockFaceColorsList[clockFaceColorIndex];
            clockFaceDisabledColor = clockFaceDisabledColorsList[clockFaceColorIndex];
            clockFlashTimer.Start();

            mpg123ProcessInfo.FileName = @"mpg123";
            mpg123ProcessInfo.Arguments = "";
            mpg123ProcessInfo.CreateNoWindow = true;
#if DEBUG
            mpg123ProcessInfo.FileName = @"T:\mpg123\mpg123.exe";
            mpg123ProcessInfo.WindowStyle = ProcessWindowStyle.Hidden;
#endif

            Process mpg123Process = Process.Start(mpg123ProcessInfo);
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
                            //setAlarmTimeTask.Wait();
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
                            //setAlarmTimeTask.Wait();
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

                //toggling alarm
                if (MouseCursorInRectangle(currentMouseState.Position, alarmBellOutline))
                {
                    if (alarmEnabled == true)
                    {
                        alarmEnabled = false;
                        if (snoozing == true || isAlarming == true)
                        {
                            snoozeAlarmTime = null;
                            snoozing = false;
                            displaySnooze = false;
                            isAlarming = false;
                            displayNowPlaying = false;
                            alarmedToday = true;
                            delayAlarmingOnSameTime.Reset();
                            delayAlarmingOnSameTime.Start();
                        }
                    }
                    else
                    {
                        alarmEnabled = true;
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
                    snoozeAlarmTime = DateTime.Now.AddMinutes(7).ToShortTimeString();
                    StopMusicIfPlaying();
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
                    snoozeAlarmTime = null;
                    StopMusicIfPlaying();
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
                Console.WriteLine("Mouse button released");
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
                (snoozeAlarmTime == DateTime.Now.ToShortTimeString() && isAlarming == false && alarmedToday == false && alarmEnabled == true))
            {
                isAlarming = true;
                displayNowPlaying = true;
                displaySnooze = true;
                snoozing = false;
                GetSongsInMusicFolder();
                totalSongs = musicToPlay.Length;
            }

            //dont alarm a re-alarm if we cancel an alarm immediately
            if (delayAlarmingOnSameTime.IsRunning)
            {
                //2 minutes
                if (delayAlarmingOnSameTime.ElapsedMilliseconds > 120000)
                {
                    delayAlarmingOnSameTime.Stop();
                    delayAlarmingOnSameTime.Reset();
                    alarmedToday = false;
                }
            }

            //play music if we're alarming
            if (isAlarming)
            {
                if (mpg123Process == null || mpg123Process.HasExited)
                {
                    Console.WriteLine("playing music");
                    string currentSongPathArgument = "\"" + Path.GetFullPath(musicToPlay[currentSong]) + "\"";
                    nowPlayingText = "Now Playing: " + Path.GetFileName(musicToPlay[currentSong]);
                    mpg123ProcessInfo.Arguments = currentSongPathArgument;
                    mpg123Process = Process.Start(mpg123ProcessInfo);

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

        //cancels music if alarm is supposed to be stopping
        private void StopMusicIfPlaying()
        {
            if (mpg123Process.HasExited == false)
            {
                if (isLinux)
                {
                    Process mpg123kill = Process.Start("/bin/bash", " -c 'sudo pkill -f mpg123'");
                }
                else
                {
                    mpg123Process.Kill();
                    mpg123Process.Dispose();
                }                
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            string displayTime;
            GraphicsDevice.Clear(Color.Black);
                        
            spriteBatch.Begin();

            //uncomment to see where the button edges are
            //spriteBatch.Draw(primitiveTexture, alarmBellOutline, clockButtonOutlineColor);
            //spriteBatch.Draw(primitiveTexture, alarmMinusOutline, clockButtonOutlineColor);
            //spriteBatch.Draw(primitiveTexture, alarmPlusOutline, clockButtonOutlineColor);
            //spriteBatch.Draw(primitiveTexture, snoozeOutline, clockButtonOutlineColor);
            //spriteBatch.Draw(primitiveTexture, hamburgerOutline, clockButtonOutlineColor);
            //spriteBatch.Draw(primitiveTexture, alarmStopOutline, clockButtonOutlineColor);

            //draw clock numbers
            if (displayAlarmTime)
            {
                displayTime = alarmTime;
            }
            else
            {
                displayTime = DateTime.Now.ToShortTimeString();
                                
            }
            Vector2 clockTimeDisplayOffsetVector = GetTextOffsetVector(displayTime, clockNumbersFont);
            Vector2 clockTimeDisplayPosition = new Vector2(screenCenterVector.X - clockTimeDisplayOffsetVector.X, screenCenterVector.Y - clockTimeDisplayOffsetVector.Y - 20);
            spriteBatch.DrawString(clockNumbersFont, displayTime, clockTimeDisplayPosition, clockFaceColor);
            

            //draw alarm set numbers
            spriteBatch.DrawString(alarmSetButtonsFont, "-", new Vector2(18, lowerRowTextAlign), clockFaceColor);
            spriteBatch.DrawString(alarmSetButtonsFont, "+", new Vector2(70, lowerRowTextAlign), clockFaceColor);

            //draw snoozing button
            if (displaySnooze)
            {
                if (snoozing)
                {
                    if (clockFlashTimer.ElapsedMilliseconds < 1000)
                    {
                        Vector2 snoozeDisplayOffset = GetTextOffsetVector("SNOOZING", snoozeRegularFont);
                        Vector2 snoozeDisplayPosition = new Vector2(screenCenterVector.X - snoozeDisplayOffset.X, lowerRowTextAlign);
                        spriteBatch.DrawString(snoozeRegularFont, "SNOOZING", snoozeDisplayPosition, clockFaceDisabledColor);
                    }
                    else if (clockFlashTimer.ElapsedMilliseconds < 2000)
                    {

                    }
                    else
                    {
                        clockFlashTimer.Restart();
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

            //draw now playing text
            if (displayNowPlaying)
            {
                Vector2 nowPlayingDisplayOffset = GetTextOffsetVector(nowPlayingText, nowPlayingFont);
                Vector2 nowPlayingDisplayPosition = new Vector2(screenCenterVector.X - nowPlayingDisplayOffset.X, 24);
                spriteBatch.DrawString(nowPlayingFont, nowPlayingText, nowPlayingDisplayPosition, clockFaceColor);
            }

            spriteBatch.End();

            base.Draw(gameTime);
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
                Thread.Sleep(200);
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

        //find the songs we are supposed to play
        private void GetSongsInMusicFolder()
        {
            musicToPlay = Directory.GetFiles(@"./Content/music", "*.*", SearchOption.AllDirectories);
        }

        //list of clock face colors, manually make sure enabled/disabled colors are aligned between lists
        private void SetupClockFaceColorsLists()
        {
            clockFaceColorsList.Add(Color.White);
            clockFaceColorsList.Add(new Color(72, 159, 247));//light blue
            clockFaceColorsList.Add(new Color(0, 255, 4));//neon green
            clockFaceColorsList.Add(new Color(240, 130, 12));//orange
            clockFaceColorsList.Add(new Color(179, 14, 230));//purple
            clockFaceColorsList.Add(new Color(237, 7, 30));//red

            clockFaceDisabledColorsList.Add(Color.Gray);
            clockFaceDisabledColorsList.Add(new Color(36, 81, 125));//light blue disabled
            clockFaceDisabledColorsList.Add(new Color(16, 122, 0));//neon green disabled
            clockFaceDisabledColorsList.Add(new Color(125, 67, 5));//orange disabled
            clockFaceDisabledColorsList.Add(new Color(77, 6, 99));//purple disabled
            clockFaceDisabledColorsList.Add(new Color(128, 8, 20));//red disabled
        }
    }
}
