﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
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
        private SpriteFont snoozeItalicFont;
        private SpriteFont alarmSetButtonsFont;
        
        bool isAlarming = false;
        bool alarmEnabled = false;
        bool settingAlarm = false;
        bool displaySnooze = true;
        bool displayAlarmTime = false;

        string alarmFile = "./alarm.ini";
        string alarmTime = "";
        string snoozeAlarmTime = "";

        string nowPlayingText = "";

        Color clockFaceColor = Color.White;
        Color clockButtonOutlineColor = Color.Gray;

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

        Thread setAlarmTimeThread;

        Stopwatch clockRestartTimer = new Stopwatch();

        MouseState currentMouseState = Mouse.GetState();
        MouseState lastMouseState = Mouse.GetState();

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
            spriteBatch = new SpriteBatch(GraphicsDevice);

            clockNumbersFont = Content.Load<SpriteFont>("clockNumbers");
            nowPlayingFont = Content.Load<SpriteFont>("clockNowPlaying");
            snoozeRegularFont = Content.Load<SpriteFont>("clockAlarm");
            snoozeItalicFont = Content.Load<SpriteFont>("clockAlarmItalic");
            alarmSetButtonsFont = Content.Load<SpriteFont>("clockAlarm");
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

            alarmTime = GetAlarmTimeFromFile();

            //TEMP NOW PLAY
            nowPlayingText = "Now Playing: New Terror Class - Did you hear that we fucked";
        }



        protected override void Update(GameTime gameTime)
        {
            currentMouseState = Mouse.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            if (currentMouseState.LeftButton == ButtonState.Pressed && lastMouseState.LeftButton == ButtonState.Released)
            {
                if (MouseCursorInRectangle(currentMouseState.Position, alarmPlusOutline))
                {
                    if (clockRestartTimer.IsRunning)
                    {
                        clockRestartTimer.Stop();
                        clockRestartTimer.Reset();
                    }
                    settingAlarm = true;
                    displayAlarmTime = true;
                    if (setAlarmTimeThread != null)
                    {
                        if (setAlarmTimeThread.IsAlive == false)
                        {
                            setAlarmTimeThread = new Thread(IncreaseAlarmTime);
                            setAlarmTimeThread.Start();
                        }
                    }
                    else
                    {
                        setAlarmTimeThread = new Thread(IncreaseAlarmTime);
                        setAlarmTimeThread.Start();
                    }
                }

                if (MouseCursorInRectangle(currentMouseState.Position, alarmMinusOutline))
                {
                    if (clockRestartTimer.IsRunning)
                    {
                        clockRestartTimer.Stop();
                        clockRestartTimer.Reset();
                    }
                    settingAlarm = true;
                    displayAlarmTime = true;
                    if (setAlarmTimeThread != null)
                    {
                        if (setAlarmTimeThread.IsAlive == false)
                        {
                            setAlarmTimeThread = new Thread(DecreaseAlarmTime);
                            setAlarmTimeThread.Start();
                        }
                    }
                    else 
                    {
                        setAlarmTimeThread = new Thread(DecreaseAlarmTime);
                        setAlarmTimeThread.Start();
                    }
                }                

                if (MouseCursorInRectangle(currentMouseState.Position, alarmBellOutline))
                {
                    alarmEnabled = !alarmEnabled;
                }

                if (MouseCursorInRectangle(currentMouseState.Position, snoozeOutline) && isAlarming)
                {
                    isAlarming = false;
                    snoozeAlarmTime = DateTime.Now.AddMinutes(10).ToShortTimeString();
                }

                if (MouseCursorInRectangle(currentMouseState.Position, alarmStopOutline) && isAlarming)
                {
                    isAlarming = false;
                }

                if (MouseCursorInRectangle(currentMouseState.Position, hamburgerOutline))
                {
                    //change color
                }

            }

            if (currentMouseState.LeftButton == ButtonState.Released && lastMouseState.LeftButton == ButtonState.Pressed)
            {
                if (settingAlarm)
                {
                    settingAlarm = false;
                    clockRestartTimer.Start();
                }
            }

            if (clockRestartTimer.IsRunning && clockRestartTimer.ElapsedMilliseconds > 2000)
            {
                clockRestartTimer.Stop();
                clockRestartTimer.Reset();
                displayAlarmTime = false;
                WriteNewAlarmTimeToFile();
            }

            lastMouseState = currentMouseState;
            
            base.Update(gameTime);
        }

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

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            Vector2 screenSize = new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            Vector2 screenCenterVector = new Vector2(screenSize.X / 2, screenSize.Y / 2);
            string displayTime;
            int lowerRowTextAlign = 333;
            
            spriteBatch.Begin();


            spriteBatch.Draw(primitiveTexture, alarmBellOutline, clockButtonOutlineColor);
            spriteBatch.Draw(primitiveTexture, alarmMinusOutline, clockButtonOutlineColor);
            spriteBatch.Draw(primitiveTexture, alarmPlusOutline, clockButtonOutlineColor);
            spriteBatch.Draw(primitiveTexture, snoozeOutline, clockButtonOutlineColor);
            spriteBatch.Draw(primitiveTexture, hamburgerOutline, clockButtonOutlineColor);
            spriteBatch.Draw(primitiveTexture, alarmStopOutline, clockButtonOutlineColor);


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


            spriteBatch.DrawString(alarmSetButtonsFont, "-", new Vector2(18, lowerRowTextAlign), clockFaceColor);
            spriteBatch.DrawString(alarmSetButtonsFont, "+", new Vector2(70, lowerRowTextAlign), clockFaceColor);


            if (displaySnooze)
            {
                Vector2 snoozeDisplayOffset = GetTextOffsetVector("SNOOZE", snoozeRegularFont);
                Vector2 snoozeDisplayPosition = new Vector2(screenCenterVector.X - snoozeDisplayOffset.X, 335);
                spriteBatch.DrawString(snoozeRegularFont, "SNOOZE", new Vector2(300, lowerRowTextAlign), clockFaceColor);
            }

            if (alarmEnabled)
            {
                spriteBatch.Draw(alarmEnabledIcon, new Vector2(screenSize.X - 60, screenSize.Y - 60), clockFaceColor);
            }
            else
            {
                spriteBatch.Draw(alarmDisabledIcon, new Vector2(screenSize.X - 60, screenSize.Y - 60), clockFaceColor);
            }

            spriteBatch.Draw(hamburgerMenuIcon, new Vector2(screenSize.X - 60, 10), clockFaceColor);
            

            Vector2 nowPlayingDisplayOffset = GetTextOffsetVector(nowPlayingText, nowPlayingFont);
            Vector2 nowPlayingDisplayPosition = new Vector2(screenCenterVector.X - nowPlayingDisplayOffset.X, 24);
            spriteBatch.DrawString(nowPlayingFont, nowPlayingText, nowPlayingDisplayPosition, clockFaceColor);

            spriteBatch.End();

            base.Draw(gameTime);
        }

        private Vector2 GetTextOffsetVector(string text, SpriteFont font)
        {
            Vector2 textSize = font.MeasureString(text);
            return new Vector2((int)Math.Round(textSize.X, 0) / 2, (int)Math.Round(textSize.Y, 0) / 2);
        }

        public Texture2D createButtonOutline()
        {
            Texture2D texture;

            texture = new Texture2D(GraphicsDevice, 1, 1);
            texture.SetData(new Color[] { clockButtonOutlineColor });

            return texture;
        }

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

        private void IncreaseAlarmTime()
        {
            Stopwatch alarmSettingTimer = new Stopwatch();
            alarmSettingTimer.Start();
            while (settingAlarm)
            {
                if (alarmSettingTimer.ElapsedMilliseconds < 2000 || !(alarmTime.EndsWith("0 AM") || alarmTime.EndsWith("0 PM")))
                {
                    alarmTime = DateTime.Parse(alarmTime).AddMinutes(1).ToShortTimeString();
                }
                else
                {
                    alarmTime = DateTime.Parse(alarmTime).AddMinutes(10).ToShortTimeString();
                }    
                Thread.Sleep(150);
            }
            alarmSettingTimer.Stop();
            alarmSettingTimer.Reset();
        }
        private void DecreaseAlarmTime()
        {
            Stopwatch alarmSettingTimer = new Stopwatch();
            alarmSettingTimer.Start();
            while (settingAlarm)
            {
                if (alarmSettingTimer.ElapsedMilliseconds < 2000 || !(alarmTime.EndsWith("0 AM") || alarmTime.EndsWith("0 PM")))
                {
                    alarmTime = DateTime.Parse(alarmTime).AddMinutes(-1).ToShortTimeString();
                }
                else
                {
                    alarmTime = DateTime.Parse(alarmTime).AddMinutes(-10).ToShortTimeString();
                }
                Thread.Sleep(150);
            }
            alarmSettingTimer.Stop();
            alarmSettingTimer.Reset();
        }
        private void WriteNewAlarmTimeToFile()
        {
            File.WriteAllText(alarmFile, alarmTime);
        }
    }
}
