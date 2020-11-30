# monoclock
![Image of monoclock](https://i.imgur.com/tJ1QryD.png)

monoclock is an alarm clock program that is capable of playing audio files, including MP3's, written in C# using the Monogame framework for display.

The intended target is a Raspberry Pi, but it can be run on any computer that can run the .NET Core runtime and has a display. It uses about 77Mb of ram, and 80Mb when playing an audio file.

You will need at least `.NET Core 3.1` and the `mono-complete` framework (`Monogame` on windows), and `mpg123` for playing back audio files. You can change the default player in the code. It is also highly recommended that you download `PulseAudio` on linux as well if you are using a raspberry pi, as you will be able to fix the poor sound playback it has by default (this is done automatically when the program starts). 

The default target has a display of 800x480 pixels, fullscreen on linux and windowed on Windows. If your display is not this size, or if you want to change the screensize, you will most likely need to modify the code to have the button targets line up with the actual buttons. In the `Draw()` method, there are some debug draw calls you can uncomment to help you position things.

To start the program on linux, call it with `mono monoclock.dll` in the installation directory. On windows, simply run the `monoclock.exe` binary. Precompiled `Release` and `Debug` binaries are available in the `bin` folder if you do not want to compile it yourself.

# how to use
Set the alarm time using the `+` and `-` buttons in the bottom left. If the alarm is enabled, the alarm will sound every day at the specified time.

Snooze the alarm by pressing the `SNOOZE` button while the alarm is sounding. The alarm will stop for 7 minutes and then resume.

Stop the alarm for the day by pressing anywhere in the middle of the screen.

Enable and disable the alarm using the button in the bottom right. 

Change the clock face color using the hamburger menu in the top right.
