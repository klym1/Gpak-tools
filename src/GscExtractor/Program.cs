using System;
using System.IO;
using System.Linq;
using GPExtractor;

namespace GscExtractor
{
    class Program
    {
        static int Main(string[] args)
        {
            var list = args.Length > 0 && args[0] == "--list";
            var rest = list ? args.Skip(1).ToArray() : args;

            if (list ? rest.Length < 1 : rest.Length < 2)
            {
                Console.Error.WriteLine("Usage: GscExtractor <archive.gsc> <output-directory> [name-filter]");
                Console.Error.WriteLine("       GscExtractor --list <archive.gsc> [name-filter]");
                Console.Error.WriteLine("  name-filter keeps only files whose archive path contains it (case-insensitive), e.g. .gp");
                return 1;
            }

            var archivePath = rest[0];
            var filterIndex = list ? 1 : 2;
            var filter = rest.Length > filterIndex ? rest[filterIndex] : null;

            if (!File.Exists(archivePath))
            {
                Console.Error.WriteLine("Archive not found: " + archivePath);
                return 1;
            }

            try
            {
                using var archive = GscArchive.Open(archivePath);

                var entries = archive.Entries
                    .Where(entry => filter == null || entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (list)
                {
                    foreach (var entry in entries)
                    {
                        Console.WriteLine("{0,10}  {1}", entry.Size, entry.Name);
                    }

                    Console.WriteLine("{0} of {1} files", entries.Count, archive.Entries.Count);
                    return 0;
                }

                var outputDirectory = rest[1];
                var failed = 0;

                foreach (var entry in entries)
                {
                    try
                    {
                        archive.ExtractTo(entry, outputDirectory);
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        failed++;
                        Console.Error.WriteLine("Error in: " + entry.Name + ": " + ex.Message);
                    }
                }

                Console.WriteLine("Extracted {0} of {1} files to {2}", entries.Count - failed, entries.Count, outputDirectory);
                return failed == 0 ? 0 : 2;
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}
