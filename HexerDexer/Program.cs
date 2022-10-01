using System;
using System.Drawing;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using NAudio;
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

public class AudioEngine
{
    private static WaveOutEvent? MusicOutputDevice;
    public static void PlayMusic(string audioLocation)
    {
        WaveFileReader reader = new WaveFileReader(audioLocation);
        LoopStream looper = new LoopStream(reader);
        MusicOutputDevice = new WaveOutEvent();
        MusicOutputDevice.Init(looper);
        MusicOutputDevice.Play();
    }
    public static void StopMusic()
    {
        MusicOutputDevice?.Stop();
        MusicOutputDevice?.Dispose();
        MusicOutputDevice = null;
    }


    public static WaveOutEvent PlaySound(string audioLocation)
    {
        WaveFileReader audioFile = new WaveFileReader(audioLocation);
        WaveOutEvent outputDevice = new WaveOutEvent();
        outputDevice.Init(audioFile);
        outputDevice.Play();
        return outputDevice;
    }
}


class Program
{
    public class ConsoleMessage
    {
        public string Contents { get; set; } = string.Empty;
        public ConsoleColor Color { get; set; } = ConsoleColor.White;
        public int NewLines { get; set; } = 1;
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
                Console.SetCursorPosition(entry.XVal, entry.YVal);                  // Set x & y cursor co-ordinates
                Console.Write(entry.Contents);                                      // Write message contents
                for (int i = 0; i < entry.NewLines; i++) { Console.WriteLine(""); } // Set-up any new lines required
            }
        }
        public static void Write(string contents, int newLines = 1, ConsoleColor color = ConsoleColor.White, int x = -1, int y = -1)
        {
            // -1 x & y is default code for current cursor position.
            if (x == -1) { x = Console.CursorLeft; }
            if (y == -1) { y = Console.CursorTop; }

            // Log the chat message so it can be re-written if the chat is updated or reset
            Log(new ConsoleMessage() { Contents = contents, NewLines = newLines, Color = color, XVal = x, YVal = y });

            Console.ForegroundColor = color;
            Console.SetCursorPosition(x, y);
            Console.Write(contents);
            for (int i = 0; i < newLines; i++) { Console.WriteLine(""); }
        }
    }

    // Print with colors  §(15) = White [default] §(0) = Black  || See Colors.md for codes ||
    public static void Print(string contents, int newLines = 1, int x = -1, int y = -1)
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
                if (i == texts.Length - 1) { MainConsole.Write(texts[i], newLines, color, x, y); }
                else { MainConsole.Write(texts[i], 0, color, x, y); }
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
        WaveOutEvent? music = null;
        if (audioLocation != "")
        {
            music = AudioEngine.PlaySound(audioLocation);
        }

        if (time.HasValue) { System.Threading.Thread.Sleep(time.Value); }
        else
        {
            while (true)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter)
                {
                    if (music != null) { music.Stop(); music.Dispose(); }
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



    


    static void HexToDec()
    {
        string nums = "0123456789ABCDEF";
        Random rand = new Random();
        AudioEngine.PlayMusic("bg-music.wav");

        bool? correct = null;
        int den;     char hex;
        int lastDen = 0; char lastHex = '0';
        int score = 0; int totalQuestions = 0; int percent = 0;
        int attempt = 0;
        bool GameLoop = true;
        WaveOutEvent? sfx = null;
        Console.Clear();


        while (GameLoop)
        {
            MainConsole.Clear();

            if (rand.Next(0,10) < 9) { den = rand.Next(10, 16); } // 90% chance of hard hex digit
            else                     { den = rand.Next(0,  10); } // 10% chance of easy hex digit
            hex = nums[den];

            Print($@"§(7)What is §(9){hex}§(7) in decimal?", 4);

            if (correct.HasValue) // it mustn't give a score until first question has been answered.
            {
                if (correct.Value)
                {
                    score++;
                    sfx = AudioEngine.PlaySound("correct.wav");
                    Print($@"§(10)Correct! §(9){lastHex} §(10)= §(9){lastDen}§(10).", 2);
                }
                else
                {
                    sfx = AudioEngine.PlaySound("incorrect.wav"); 
                    Print($@"§(12)Incorrect. §(9){lastHex} §(12)= §(9){lastDen}§(12). (You entered {attempt})", 2);
                }

                percent = score * 100 / totalQuestions;
                Print($@"§(7)Score: ", 0);

                if (percent > 74)
                {
                    Print($@"§(10){score} / {totalQuestions} ({percent}%)§(7).", 2);
                }
                else if (percent > 49)
                {
                    Print($@"§(6){score} / {totalQuestions} ({percent}%)§(7).", 2);
                }
                else
                {
                    Print($@"§(12){score} / {totalQuestions} ({percent}%)§(7).", 2);
                }
            }


            try { attempt = ReadInt(0, 2); }
            catch (EscapeException) { AudioEngine.StopMusic(); GameLoop = false; }
            
            if (attempt == den) { correct = true; }
            else { correct = false; }

            totalQuestions++;
            lastDen = den;
            lastHex = hex;
            if (sfx != null) { sfx.Stop(); sfx.Dispose(); }
        }
        return;
    }

    static void MainMenu()
    {
        MainConsole.Clear();

        Print("HexerDexer", 2);
        Print("1 §(9)Hex-Denary");
        Print("2 §(10)Denary-Hex", 2);

        int choice = 0;
        try { choice = ReadInt(); }
        catch (EscapeException) { System.Environment.Exit(1); }

        switch (choice)
        {
            case 1: HexToDec(); break;
            case 2: break;
        }
    }
    
    static void Main(string[] args)
    {
        CenterScreen("HexerDexer", "Press ENTER to start", audioLocation: "intro.wav");
        while (true) { MainMenu(); }
    }
}