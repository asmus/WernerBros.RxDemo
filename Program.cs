using System.Collections.Immutable;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace RxDemo;

public static class RxDemo
{
    private static readonly ImmutableDictionary<string, (ConsoleColor FGColor, ConsoleColor BGColor)> ColorDefinitions =
        ImmutableDictionary<string, (ConsoleColor FGColor, ConsoleColor BGColor)>.Empty
            .Add("error", (ConsoleColor.Black, ConsoleColor.Red))
            .Add("warning", (ConsoleColor.Black, ConsoleColor.Yellow))
            .Add("info", (ConsoleColor.White, ConsoleColor.Green));
    
    static void Main(string[] args)
    {
        var path = Directory.GetCurrentDirectory();
        using var watcher = new FileSystemWatcher(path);
        using (Watch(args[0]).Subscribe(ProcessFileEntry))
        {
            Console.ReadLine();
            Console.WriteLine($"ciao");
        }
    }
    
    /* Continuously watch for changes in the given file and return a stream of file changes */
    private static IObservable<string> Watch(string fileToWatch)
    {
        return Observable.Create<string>(observer =>
        {
            var watcher = new FileSystemWatcher();
            watcher.Path = Path.GetDirectoryName(fileToWatch);
            watcher.Filter =  Path.GetFileName(fileToWatch);
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
            watcher.EnableRaisingEvents = true;
            return Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                handler => watcher.Changed += handler,
                handler => watcher.Changed -= handler)
                // the following throttle is just there because im too lazy to figure out what is going actually wrong
                // it seems that the editor has still blocked the file when saving it while we are trying to read it
                .Throttle(TimeSpan.FromMilliseconds(50))
                .Select(_ => GetFileEnd(fileToWatch))
                .Scan((PrevLen: (long)0, NewLen: GetFileEnd(fileToWatch)), (previous, newLen)
                    => (PrevLen: previous.NewLen, NewLen: newLen))
                .Select(fileLen => fileLen.PrevLen)
                .Select(fileLen => GetText(fileToWatch, fileLen))
                .SelectMany(SplitLines)
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Subscribe(observer.OnNext);
        });
    }

    private static IObservable<string> SplitLines(string input)
    {
        return Observable.Create<string>(observer =>
        {
            foreach (var line in input.Split(Environment.NewLine))
            {
                observer.OnNext(line);
            }
            return Disposable.Empty;
        });
    }

    private static long GetFileEnd(string file)
    {
        using var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        return fs.Length;
    }

    private static string GetText(string file, long start)
    {
        using var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(fs);
        return reader.ReadToEnd();
    }

    private static void ProcessFileEntry(string fileEntry)
    {
        var colorDefinition = GetColorDefinition(fileEntry);
        Console.BackgroundColor = colorDefinition.BGColor;
        Console.ForegroundColor = colorDefinition.FGColor;
        Console.WriteLine(fileEntry);
    }

    private static (ConsoleColor FGColor, ConsoleColor BGColor) GetColorDefinition(string logEntry)
    {
        foreach (var colorDefinition in ColorDefinitions.Where(colorDefinition => logEntry.Contains($"[{colorDefinition.Key}]")))
        {
            return colorDefinition.Value;
        }

        return DefaultColor;
    }

    private static readonly (ConsoleColor FGColor, ConsoleColor BGColor) DefaultColor = (ConsoleColor.Black, ConsoleColor.White);
}