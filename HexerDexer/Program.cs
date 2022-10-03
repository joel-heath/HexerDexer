using System;
using System.Drawing;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using NAudio;
using NAudio.Codecs;
using NAudio.Dmo;
using NAudio.Wave;
using static System.Net.Mime.MediaTypeNames;
using static HexerDexer.Program;


namespace HexerDexer;
public class LoopStream : WaveStream
{
    WaveStream sourceStream;

    /// Creates a new Loop stream
    public LoopStream(WaveStream sourceStream)
    {
        this.sourceStream = sourceStream;
        this.EnableLooping = true;
    }

    /// Use this to turn looping on or off
    public bool EnableLooping { get; set; }

    /// Return source stream's wave format
    public override WaveFormat WaveFormat
    {
        get { return sourceStream.WaveFormat; }
    }

    /// LoopStream simply returns
    public override long Length
    {
        get { return sourceStream.Length; }
    }

    /// LoopStream simply passes on positioning to source stream
    public override long Position
    {
        get { return sourceStream.Position; }
        set { sourceStream.Position = value; }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int totalBytesRead = 0;

        while (totalBytesRead < count)
        {
            int bytesRead = sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
            if (bytesRead == 0)
            {
                if (sourceStream.Position == 0 || !EnableLooping)
                {
                    // something wrong with the source stream
                    break;
                }
                // loop
                sourceStream.Position = 0;
            }
            totalBytesRead += bytesRead;
        }
        return totalBytesRead;
    }
}

public class WavePlayer
{
    WaveFileReader Reader;
    public WaveChannel32 Channel { get; set; }
    string FileName { get; set; }
    public WavePlayer(string FileName)
    {
        this.FileName = FileName;
        Reader = new WaveFileReader(FileName);
        var loop = new LoopStream(Reader);
        Channel = new WaveChannel32(loop) { PadWithZeroes = false };
    }
    public void Dispose()
    {
        if (Channel != null)
        {
            Channel.Dispose();
            Reader.Dispose();
        }
    }
}

public class AudioEngine
{
    public static string[][] Tracks = new string[][] {
        new string[5] {"001-guitar.wav",        // Hex -> Dec
                       "002-bass.wav",
                       "003-tambourine.wav",
                       "004-atmpospherics.wav",
                       "005-drums.wav" },
        new string[7] {"song-001.wav",          // Dec -> Hex
                       "song-002.wav",
                       "song-003.wav",
                       "song-004.wav",
                       "song-005.wav",
                       "song-006.wav",
                       "song-007.wav" } };

    private static List<WavePlayer> Music = new List<WavePlayer>();
    private static DirectSoundOut MusicOutputDevice = new DirectSoundOut();
    public static void PlayMusic(string[] tracks)
    {
        if (Music.Count == 0)
        {
            foreach (string track in tracks)
            {
                Music.Add(new WavePlayer(track));
            }
            MixingWaveProvider32 mixer = new MixingWaveProvider32(Music.Select(c => c.Channel));
            MusicOutputDevice.Init(mixer);
            MusicOutputDevice.Play();
        }
        else
        {
            MusicOutputDevice.Play();
        }
    }
    public static void PlayMusic(string track) // overload for just one track
    {
        if (Music.Count == 0)
        {
            Music.Add(new WavePlayer(track));
            MixingWaveProvider32 mixer = new MixingWaveProvider32(Music.Select(c => c.Channel));
            MusicOutputDevice.Init(mixer);
            MusicOutputDevice.Play();
        }
        else
        {
            MusicOutputDevice.Play();
        }
    }
    public static void SetVolume(int trackID, float volume)
    {
        Music[trackID].Channel.Volume = volume;
    }
    public static void PauseMusic()
    {
        MusicOutputDevice.Stop();
    }
    public static void StopMusic()
    {
        MusicOutputDevice.Stop();
        foreach (WavePlayer track in Music)
        {
            track.Dispose();
        }
        Music.Clear();
        MusicOutputDevice.Dispose();

    }
    public static WaveOutEvent PlaySound(string audioLocation) // Sounds are one-off
    {
        WaveFileReader audioFile = new WaveFileReader(audioLocation);
        WaveOutEvent outputDevice = new WaveOutEvent();
        outputDevice.Init(audioFile);
        outputDevice.Play();
        return outputDevice; // And are manually stopped & disposed of after
    }
}

class Program
{
    public class ConsoleMessage
    {
        public string Contents { get; set; } = string.Empty;
        public int NewLines { get; set; } = 1;
        public ConsoleColor Color { get; set; } = ConsoleColor.White;
        public ConsoleColor Highlight { get; set; } = ConsoleColor.Black;
        public int XVal { get; set; } = 0;
        public int YVal { get; set; } = 0;
    }
    public class ConsoleLogs
    {
        private List<ConsoleMessage> ConsoleHistory = new List<ConsoleMessage>();
        public List<ConsoleMessage> History { get { return ConsoleHistory; } }
        public int Count { get { return ConsoleHistory.Count; } }
        public void Log(ConsoleMessage message) { ConsoleHistory.Add(message); }
        public void Clear() { ConsoleHistory.Clear(); }
        public void Remove(int x, int y)
        {
            ConsoleMessage? removeMe = new ConsoleMessage { XVal = -1 }; // fake item that can't exist, so nothing will be removed if item is not found
            foreach (ConsoleMessage log in ConsoleHistory)
            {
                if (log.YVal == y && log.XVal == x) { removeMe = log; } // if item is found remove it
            }
            ConsoleHistory.Remove(removeMe);
        }
        public void Shift(int x, int y, int distance)
        {
            foreach (ConsoleMessage log in ConsoleHistory)
            {
                if (log.YVal == y && log.XVal >= x)
                {
                    log.XVal += distance; // Move any character to the right of starting point by the distance
                }
            }
        }
    }
    static class MainConsole // special case of a ConsoleLogs(): this is the live one that is printed to the screen
    {
        private static readonly ConsoleLogs TheConsole = new ConsoleLogs();
        public static List<ConsoleMessage> History { get { return TheConsole.History; } }
        public static void Log(ConsoleMessage message)
        {
            TheConsole.Log(message);
        }
        public static void Remove(ConsoleMessage message)
        {
            TheConsole.Remove(message.XVal, message.YVal);
        }
        public static void Remove(int x, int y)
        {
            TheConsole.Remove(x, y);
        }
        public static void Clear()
        {
            TheConsole.Clear();
            Console.Clear();
        }
        public static void Refresh(ConsoleLogs? extraMessages = null)
        {
            Console.Clear();

            List<ConsoleMessage> messages = History.ToList();

            if (extraMessages != null)
            {
                foreach (ConsoleMessage message in extraMessages.History) { messages.Add(message); }
            }

            foreach (var entry in messages)
            {
                Console.ForegroundColor = entry.Color;                              // Set message color
                Console.BackgroundColor = entry.Highlight;                          // Highlight text
                Console.SetCursorPosition(entry.XVal, entry.YVal);                  // Set x & y cursor co-ordinates
                Console.Write(entry.Contents);                                      // Write message contents
                for (int i = 0; i < entry.NewLines; i++) { Console.WriteLine(""); } // Set-up any new lines required
            }
        }
        public static void Write(string contents, int newLines, ConsoleColor color, ConsoleColor highlight, int x, int y)
        {
            // -1 x & y is default code for current cursor position.
            if (x == -1) { x = Console.CursorLeft; }
            if (y == -1) { y = Console.CursorTop; }

            // Log the chat message so it can be re-written if the chat is updated or reset
            Log(new ConsoleMessage() { Contents = contents, NewLines = newLines, Color = color, Highlight = highlight, XVal = x, YVal = y });

            Console.ForegroundColor = color;
            Console.BackgroundColor = highlight;
            Console.SetCursorPosition(x, y);
            Console.Write(contents);
            for (int i = 0; i < newLines; i++) { Console.WriteLine(""); }
        }
    }

    // Print with multiple colors  §(15) = White [default] §(0) = Black  || See Colors.md for codes ||      [ONLY 1 HIGHLIGHT]
    public static void Print(string contents, int newLines = 1, int x = -1, int y = -1, ConsoleColor highlight = ConsoleColor.Black)
    {
        ConsoleColor color = ConsoleColor.White;
        Regex rx = new Regex(@"\§\((\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        string[] texts = rx.Split(contents);
        // texts is a string array where every even index is a string and odd index is a color code

        for (int i = 0; i < texts.Length; i++)
        {
            // If it's an even index then its text to be Written
            if (i % 2 == 0)
            {
                // If last character in string, print the new lines aswell
                if (i == texts.Length - 1) { MainConsole.Write(texts[i], newLines, color, highlight, x, y); }
                else { MainConsole.Write(texts[i], 0, color, highlight, x, y); }
            }
            else // otherwise it's a color code
            {
                color = (ConsoleColor)int.Parse(texts[i]);
            }
        }
    }
    static void CenterScreen(string title, string subtitle = "", int? time = null, string audioLocation = "")
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Clear();

        // Margin-top
        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine("");
        }

        // Title & subtitle
        Console.SetCursorPosition((Console.WindowWidth - title.Length) / 2, Console.CursorTop);
        Console.WriteLine(title);
        Console.SetCursorPosition((Console.WindowWidth - subtitle.Length) / 2, (Console.CursorTop + 1));
        Console.WriteLine(subtitle);

        // Spacing to cursor
        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine("");
        }
        Console.SetCursorPosition((Console.WindowWidth) / 2, (Console.CursorTop + 2));

        // Music & wait for keypress
        if (audioLocation != "")
        {
            AudioEngine.PlayMusic(audioLocation);
        }

        if (time.HasValue) { System.Threading.Thread.Sleep(time.Value); }
        else
        {
            while (true)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter)
                {
                    AudioEngine.StopMusic();
                    break;
                }
            }
        }
    }
    public class EscapeException : Exception { }
    public class EnterException : Exception { }
    static (int,int) HandleKeyPress(ConsoleLogs inputString, ConsoleKeyInfo keyPressed, int margin, int xPos, int yPos)
    {
        switch (keyPressed.Key)
        {
            case ConsoleKey.Escape: throw new EscapeException();
            case ConsoleKey.Enter: throw new EnterException();
            case ConsoleKey.Home: xPos = margin; break;
            case ConsoleKey.End: xPos = margin + inputString.Count; break;
            case ConsoleKey.LeftArrow: xPos = (xPos == margin) ? xPos : xPos - 1; break;                      // Don't move back if at start of string
            case ConsoleKey.RightArrow: xPos = (xPos == margin + inputString.Count) ? xPos : xPos + 1; break; // Don't move forward if at end of string

            case ConsoleKey.Backspace: // Backspace is just a delete with the cursor moved back one
                if (xPos != margin)   // If there is room to do so
                {
                    keyPressed = new ConsoleKeyInfo('\0', ConsoleKey.Delete, false, false, false); xPos--; // Creating a delete keypress
                    HandleKeyPress(inputString, keyPressed, margin, xPos, yPos);                           // Calling self to delete
                }
                break;

            case ConsoleKey.Delete:
                if (xPos != margin + inputString.Count)
                {
                    inputString.Remove(xPos, yPos);     // Remove character at cursor position
                    inputString.Shift(xPos, yPos, -1);  // Shift everything to the right of the cursor back by one
                    MainConsole.Refresh(inputString);   // Refresh screen
                }
                break;

            default:
                if (!char.IsControl(keyPressed.KeyChar)) // if key pressed isnt a control key (only visible characters)
                {
                    string letter = keyPressed.KeyChar.ToString();
                    inputString.Shift(xPos, yPos, 1);                                                                       // Move everything infront of cursor to the right
                    inputString.Log(new ConsoleMessage() { Contents = letter, XVal = xPos, YVal = yPos, NewLines = 0 });    // Log new character inputted
                    xPos++;                                                                                                 // Move cursor one step forward
                    MainConsole.Refresh(inputString);                                                                       // Refresh screen
                }
                break;
        }
        return (xPos, yPos); // return new x and y co-ords
    }
    static string ReadChars()
    {
        string output = string.Empty;
        bool complete = false;
        int startPoint = Console.CursorLeft; // so that cursor does not go beyond starting point of text
        int x = startPoint; int y = Console.CursorTop;

        ConsoleLogs input = new ConsoleLogs();

        while (!complete)
        {
            Console.SetCursorPosition(x, y);
            ConsoleKeyInfo keyPressed =  Console.ReadKey(true);
            try { (x, y) = HandleKeyPress(input, keyPressed, startPoint, x, y); }
            catch (EnterException) { complete = true; }
        }

        foreach (ConsoleMessage message in input.History)
        {
            output += message.Contents;
        }

        return output;
    }
    static int ReadInt(int xCoord = -1, int yCoord = -1)
    {
        while (true)
        {
            Print("> ", newLines: 0, x: xCoord, y: yCoord);
            string uInput = ReadChars();
            if (int.TryParse(uInput, out int result))
            {
                return result;
            }
            else
            {
                MainConsole.Refresh();
            }
        }
    }

    static string ReadStr(int xCoord = -1, int yCoord = -1, int maxLength = -1)
    {
        while (true)
        {
            Print("> ", newLines: 0, x: xCoord, y: yCoord);
            string uInput = ReadChars();
            int len = uInput.Length;
            if (0 < len && (len <= maxLength || maxLength == -1)) // insert more logical checks like is alphanumeric
            {
                return uInput;
            }
            else
            {
                MainConsole.Refresh();
            }
        }
    }
    enum Mode
    {
        HexToDec,
        DecToHex,
    }
    class Problem
    {
        private int Number { get; }
        public Mode Mode { get; }
        public string Question { get => Number.ToString(Mode == Mode.DecToHex ? "" : "X"); }
        public string Answer { get => Number.ToString(Mode == Mode.DecToHex ? "X" : ""); }
        public string TargetNumberSystem { get => Mode == Mode.DecToHex ? "hexadecimal" : "decimal"; }
        public Problem() : this(Random.Shared.Next(20), (Mode)Random.Shared.Next(2)) {} // completely random question with random mode
        public Problem(int digits, Mode mode)
        {
            this.Mode = mode;
            int _base = (mode == Mode.DecToHex) ? 10 : 16;
            if (digits == 1)
            {
                if (Random.Shared.Next(0, 10) < 9) { this.Number = Random.Shared.Next(10, 16); } // 90% chance of hard digit
                else { this.Number = Random.Shared.Next(0, 10); }                                // 10% chance of easy digit
            }
            else
            {
                this.Number = Random.Shared.Next((int)Math.Pow(_base, digits - 1), (int)Math.Pow(_base, digits));
            }
        }
    }

    static void HexDex(Mode gameMode)
    {
        // Custom Options
        string[] customOptions = GameOptions();
        int digits = int.Parse(customOptions[0]);

        // Initialising
        bool? correct = null;
        Problem problem; Problem? lastProblem = null;
        int score = 0; int totalQuestions = 0; int percent;
        string attempt = "";
        int musicCount = 1;
        WaveOutEvent? sfx = null;
        string[] tracks = AudioEngine.Tracks[(int)gameMode];

        AudioEngine.PlayMusic(tracks);

        bool GameLoop = true;
        while (GameLoop)
        {
            MainConsole.Clear();

            // Track volume based on score, +1 track when you're correct, back to 0 if you fail
            for (int i = 0; i < tracks.Length; i++)
            {
                AudioEngine.SetVolume(i, (i < musicCount) ? 1 : 0);
            }

            problem = new Problem(digits, gameMode);
            Print($@"§(7)What is §(9){problem.Question}§(7) in {problem.TargetNumberSystem}?", 4);

            // Feedback on user's answer
            if (correct.HasValue) // it mustn't give a score until first question has been answered.
            {
                // Marking their answer
                if (correct.Value)
                {
                    score++;
                    sfx = AudioEngine.PlaySound("correct.wav");
                    Print($@"§(10)Correct! §(9){lastProblem?.Question} §(10)= §(9){lastProblem?.Answer}§(10).", 2);
                }
                else
                {
                    sfx = AudioEngine.PlaySound("incorrect.wav");
                    Print($@"§(12)Incorrect. §(9){lastProblem?.Question} §(12)= §(9){lastProblem?.Answer}§(12). (You entered {attempt})", 2);
                }

                // Score roundup
                percent = score * 100 / totalQuestions;
                Print($@"§(7)Score: ", 0);
                string scoreRoundup = $"{score} / {totalQuestions} ({percent}%)";

                if (percent > 74)
                {
                    Print($@"§(10){scoreRoundup}§(7).", 2);
                }
                else if (percent > 49)
                {
                    Print($@"§(6){scoreRoundup}§(7).", 2);
                }
                else
                {
                    Print($@"§(12){scoreRoundup}§(7).", 2);
                }
            }

            // Read user's attempt
            try { attempt = ReadStr(0, 2).ToUpper(); }
            catch (EscapeException) { AudioEngine.StopMusic(); GameLoop = false; }

            // Determine if it's correct
            if (attempt == problem.Answer) { correct = true; musicCount = (musicCount <= tracks.Length) ? musicCount + 1 : musicCount; }
            else { correct = false; musicCount = 1; }


            totalQuestions++;
            lastProblem = problem;
            if (sfx != null) { sfx.Stop(); sfx.Dispose(); }
        }
        return;
    }

    static string[] GameOptions()
    {
        MainConsole.Clear();
        string[] options = new string[1];

        Print("Hex-Dex", 2);
        Print("How many digits do you want?", 2);
        Print(" > ", 0); Print("1", 0, highlight:ConsoleColor.DarkGray); Print(" < ");

        int num = 1;
        bool chosen = false;
        while (!chosen)
        {
            Console.CursorVisible = false;
            switch (Console.ReadKey(true).Key)
            {
                case ConsoleKey.UpArrow: if (num < 9) { num++; }; break;
                case ConsoleKey.DownArrow: if (num > 1) { num--; } break;
                case ConsoleKey.Enter: chosen = true; break;
            }
            MainConsole.Remove(3, 4);
            Print($"{num}", 0, 3, 4, ConsoleColor.DarkGray);
        }
        options[0] = num.ToString();
        Console.CursorVisible = true;
        Console.BackgroundColor = ConsoleColor.Black;

        return options;
    }

    static void MainMenu()
    {
        MainConsole.Clear();

        Print("HexerDexer", 2);
        Print("1 §(9)Hex -> Dex");
        Print("2 §(10)Dex -> Hex", 2);

        int choice = 0;
        try { choice = ReadInt(); }
        catch (EscapeException) { Environment.Exit(1); }

        switch (choice)
        {
            case 1: HexDex(Mode.HexToDec); break;
            case 2: HexDex(Mode.DecToHex); break;
        }
    }
    
    static void Intro()
    {
        WaveOutEvent music = AudioEngine.PlaySound("intro.wav");
        string msg1 = "Loading";
        string msg2 = "...";
        string msg3 = "Starting HexerDexer";
        Thread.Sleep(1000);
        foreach (char letter in msg1) { Console.Write(letter); Thread.Sleep(400); }
        Thread.Sleep(1000);
        foreach (char letter in msg2) { Console.Write(letter); Thread.Sleep(1000); }
        Thread.Sleep(3000); Console.WriteLine(""); Thread.Sleep(1000);
        foreach (char letter in msg3) { Console.Write(letter); Thread.Sleep(120); }

        while (music.PlaybackState == PlaybackState.Playing)
        {
            Thread.Sleep(1000);
        }

    }
    static void Main(string[] args)
    {
        Intro();
        CenterScreen("HexerDexer", "Press ENTER to start", audioLocation: "bg-music.wav");
        while (true) { MainMenu(); }
    }
}