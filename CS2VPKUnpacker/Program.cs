using SteamDatabase.ValvePak;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;

public class Program
{
    static bool ignorePanorama = false;
    static bool debug = false;

    static List<string> ignoreList = new List<string>();
    public static void Main(string[] args)
    {
        if(args.Contains("-debug"))
            debug = true;
        if(args.Contains("-ignorePanoramaFiles"))
            ignorePanorama = true;

        int ignoreListIndex = Array.IndexOf(args, "-ignore");
        if (ignoreListIndex != -1 && ignoreListIndex + 1 < args.Length)
        {
            string filesString = args[ignoreListIndex + 1];
            string[] files = filesString.Split([' '], StringSplitOptions.RemoveEmptyEntries);

            foreach (string file in files)
            {
                ignoreList.Add(file);
            }
            Console.Write($"\nИгнор лист: {filesString}\n");
        }
        else
        {
            Console.Write("\nИгнор лист пуст\n");
        }

        int vpksIndex = Array.IndexOf(args, "-vpks");
        if (vpksIndex != -1 && vpksIndex + 1 < args.Length)
        {
            string foldersString = args[vpksIndex + 1];
            string[] folders = foldersString.Split([' '], StringSplitOptions.RemoveEmptyEntries);

            foreach (string file in folders)
            {
                CheckVPKFiles(file + "/pak01_dir.vpk");
            }
        }
        else
        {
            Console.Write("\nПапки не были указаны");
        }

        Console.Write("\n\nНажми любую клавишу для продолжения...");
        Console.ReadKey();
    }
    public static void CheckVPKFiles(string vpkFile)
    {
        Console.WriteLine($"\n-------------- {vpkFile} --------------\n");

        if (File.Exists(vpkFile + ".bak") && !File.Exists(vpkFile))
        {
            if (GetYesNoInput("Найден только бекап vpk, восстановить?"))
            {
                File.Copy(vpkFile + ".bak", vpkFile);
                File.Delete(vpkFile + ".bak");
                if (!GetYesNoInput("Распаковать VPK?"))
                    return;
            }
            else
            {
                return;
            }
        }
        else if (!File.Exists(vpkFile))
        {
            Console.WriteLine($"pak01_dir.vpk файл не найден в этой директории {Path.GetDirectoryName(vpkFile)}...");
            return;
        }

        string? vpkfolder = Path.GetDirectoryName(vpkFile)+"/";

        using var package = new Package();
        package.Read(vpkFile);

        int filesCount = 0;
        foreach (var entry in package.Entries)
        {
            filesCount += entry.Value.Count;
        }

        Console.WriteLine($"Количество файлов: {filesCount}");

        DateTime unpackStartTime = DateTime.Now;
        foreach (var entry in package.Entries)
        {
            foreach (var item in entry.Value.ToArray())
            {
                string file = vpkfolder + item.GetFullPath();
                string? directory = Path.GetDirectoryName(file);

                bool fileInIgnoreList = ignoreList.Contains(item.GetFileName()) ||
                        ((file.Contains("panorama/layout") || file.Contains("panorama/styles") || file.Contains("panorama/scripts")) && ignorePanorama);

                bool fileExists = File.Exists(file);

                if (!fileExists || CalculateCrc32FromFile(file) != item.CRC32)
                {
                    if (fileInIgnoreList && fileExists)
                    {
                        Console.WriteLine($"{file} ({item.GetFileName()}) проигнорирован");
                        continue;
                    }
                    else if (fileInIgnoreList && !fileExists)
                    {
                        Console.WriteLine($"{file} ({item.GetFileName()}) файл в игнор листе, но не существует, записываем");
                    }
                    else if (!fileExists)
                    {
                        Console.WriteLine($"{file} ({item.GetFileName()}) не найден");
                    }
                    else
                    {
                        Console.WriteLine($"{file} ({item.GetFileName()}) изменен");
                    }

                    if (directory != null)
                        Directory.CreateDirectory(directory);

                    package.ReadEntry(item, out byte[] fileContents);
                    File.WriteAllBytes(file, fileContents);

                }
                else if (debug)
                {
                    Console.WriteLine($"{file} ({item.GetFileName()}) прошел проверку");
                }
            }
        }
        package.Dispose();

        DateTime unpackEndTime = DateTime.Now;
        TimeSpan timeDifference = unpackEndTime - unpackStartTime;

        Console.WriteLine($"\nРаспаковка заняла {timeDifference.Minutes} минут, {timeDifference.Seconds} секунд\n");

        bool backupCreated = false;
        if(File.Exists(vpkFile + ".bak"))
        {
            if (GetYesNoInput("Найден бекап vpk, пересоздать?"))
            {
                File.Delete(vpkFile + ".bak");
                File.Copy(vpkFile, vpkFile + ".bak");
                File.Delete(vpkFile);
                backupCreated = true;
                return;
            }
            else
            {
                return;
            }
        }

        if(backupCreated)
            return;

        if (GetYesNoInput("Создать бекап vpk?"))
        {
            if (File.Exists(vpkFile + ".bak"))
                File.Delete(vpkFile + ".bak");

            File.Copy(vpkFile, vpkFile + ".bak");
            File.Delete(vpkFile);
        }
    }

    public static uint CalculateCrc32(byte[] data)
    {
        var crc32 = new System.IO.Hashing.Crc32();
        crc32.Append(data);
        var hashBytes = crc32.GetCurrentHash();

        return BitConverter.ToUInt32(hashBytes);
    }

    public static uint CalculateCrc32FromFile(string filePath)
    {
        if(!File.Exists(filePath))
            return 0;

        byte[] data = File.ReadAllBytes(filePath);
        return CalculateCrc32(data);
    }
    static bool GetYesNoInput(string text)
    {
        Console.WriteLine($"{text} (Y/N)");
        Console.Write("> ");

        string keyChar = Console.ReadKey().KeyChar.ToString().ToLower();
        Console.Write("\n");
        return keyChar switch
        {
            "y" => true,
            "n" => false,
            _ => GetYesNoInput(text),
        };
    }

}