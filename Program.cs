using CommandLine;
using CommandLine.Text;
using Microsoft.Data.Sqlite;

public static class Program
{
    private class CommandLineOptions
    {
        public CommandLineOptions(string filename, string? prefixFrom, string? prefixTo, bool unixToWindows, bool windowsToUnix, bool verbose)
        {
            Filename = filename;
            Verbose = verbose;
            PrefixFrom = prefixFrom;
            PrefixTo = prefixTo;
            UnixToWindows = unixToWindows;
            WindowsToUnix = windowsToUnix;
        }

        [Value(0, Required = true, HelpText = "Name of the database file to migrate.")]
        public string Filename { get; }

        [Option('f', "from-prefix", Required = false, HelpText = "Directory prefix from which to migrate.")]
        public string? PrefixFrom { get; }

        [Option('t', "to-prefix", Required = false, HelpText = "Directory prefix to which to migrate.")]
        public string? PrefixTo { get; }

        [Option('w', "to-windows", SetName = "conversion", HelpText = "Convert path syntax from Unix to Windows.")]
        public bool UnixToWindows { get; }

        [Option('u', "to-unix", SetName = "conversion", HelpText = "Convert path syntax from Windows to Unix.")]
        public bool WindowsToUnix { get; }

        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
        public bool Verbose { get; }

        [Usage(ApplicationAlias = "DarktableMigrator")]
        public static IEnumerable<Example> Examples =>
            new List<Example>
            {
                new("Migrate photos from Unix to Windows", new CommandLineOptions("library.db", @"/home/joe/photos/", @"P:\", false, true, false)),
                new("Migrate photos from Windows to Unix", new CommandLineOptions("library.db", @"P:\", @"/home/joe/photos/", true, false, false)),
                new("Migrate photos to another directory", new CommandLineOptions("library.db", @"C:\Users\Joe\Pictures\", @"P:\", false, false, false)),
            };
    }

    private static void RunProgram(CommandLineOptions options)
    {
        if ((options.PrefixFrom != null) != (options.PrefixTo != null))
        {
            Console.Error.WriteLine("Cannot use just one of --from-prefix and --to-prefix");
            return;
        }
        if (options.PrefixFrom != null
            && (options.PrefixFrom.EndsWith('/') || options.PrefixFrom.EndsWith('\\')) != (options.PrefixTo!.EndsWith('/') || options.PrefixTo!.EndsWith('\\'))
           )
        {
            Console.Error.WriteLine("Trailing path separator mismatch between --from-prefix and --to-prefix");
            return;
        }
        if (options is { UnixToWindows: true, PrefixFrom: not null } && (!IsUnixPath(options.PrefixFrom) || !IsWindowsPath(options.PrefixTo!)))
        {
            Console.Error.WriteLine("Invalid prefix syntax when using --to-windows");
            return;
        }
        if (options is { WindowsToUnix: true, PrefixFrom: not null } && (!IsWindowsPath(options.PrefixFrom) || !IsUnixPath(options.PrefixTo!)))
        {
            Console.Error.WriteLine("Invalid prefix syntax when using --to-unix");
            return;
        }

        var connectionString = new SqliteConnectionStringBuilder()
        {
            Mode = SqliteOpenMode.ReadWrite,
            DataSource = options.Filename,
        }.ToString();

        using var dbConnection = new SqliteConnection(connectionString);
        dbConnection.Open();

        var rollsToConvert = new Dictionary<long, (string, string)>();

        var rowsTotal = 0L;
        using (var readCommand = dbConnection.CreateCommand())
        {
            readCommand.CommandText = @"SELECT id, folder FROM film_rolls";
            using (var reader = readCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    ++rowsTotal;
                    var id = reader.GetInt64(0);
                    var folder = reader.GetString(1);

                    var convertedFolder = Convert(folder, options);
                    if (folder != convertedFolder)
                    {
                        rollsToConvert.Add(id, (folder, convertedFolder));
                    }
                }
            }
        }

        if (options.Verbose) Console.WriteLine($"{rowsTotal} rows in table, {rollsToConvert.Count} will be converted");

        foreach (var rollToConvert in rollsToConvert)
        {
            using var updateCommand = dbConnection.CreateCommand();

            updateCommand.CommandText = "UPDATE film_rolls SET folder=$folder WHERE id=$id";
            var id = rollToConvert.Key;
            var origFolder = rollToConvert.Value.Item1;
            var convertedFolder = rollToConvert.Value.Item2;
            updateCommand.Parameters.AddWithValue("id", id);
            updateCommand.Parameters.AddWithValue("folder", convertedFolder);

            if (options.Verbose) Console.WriteLine($"Migrating #{id}: '{origFolder}' to '{convertedFolder}");

            updateCommand.ExecuteNonQuery();
        }

        Console.WriteLine($"{rollsToConvert.Count} of {rowsTotal} entries migrated");
    }

    private static bool IsUnixPath(string path)
    {
        if (!path.StartsWith('/')) return false;
        if (path.Contains('\\')) return false;
        return true;
    }

    private static bool IsWindowsPath(string path)
    {
        if (path.Length < 3) return false;
        if (path[1] != ':' || path[2] != '\\') return false;
        if (path.Contains('/')) return false;

        if (path[0] >= 'A' && path[0] <= 'Z') return true;
        if (path[0] >= 'a' && path[0] <= 'z') return true;
        return false;
    }

    private static string Convert(string orig, CommandLineOptions options)
    {
        var str = orig;
        if (options.PrefixFrom != null && str.StartsWith(options.PrefixFrom))
        {
            str = options.PrefixTo + str[options.PrefixFrom.Length..];
        }
        if (options.UnixToWindows)
        {
            str = str.Replace('/', '\\');
        }
        if (options.WindowsToUnix)
        {
            str = str.Replace('\\', '/');
        }
        return str;
    }

    private static void DisplayHelp(ParserResult<CommandLineOptions> result, IEnumerable<Error> obj)
    {
        var helpText = HelpText.AutoBuild(result, h =>
        {
            h.AdditionalNewLineAfterOption = false;
            h.Heading = "DarktableMigrator";
            h.Copyright = "Copyright (c) 2023 Petr Kadlec";
            return HelpText.DefaultParsingErrorsHandler(result, h);
        }, e => e);
        Console.WriteLine(helpText);
    }

    public static void Main(string[] args)
    {
        try
        {
            var parser = new Parser(with => with.HelpWriter = null);
            var parserResult = parser.ParseArguments<CommandLineOptions>(args);
            parserResult
                .WithParsed(RunProgram)
                .WithNotParsed(err => DisplayHelp(parserResult, err));
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
        }
    }
}