using SteamDatabase.ValvePak;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;

public class Program
{
    static bool ignorePanorama = false;
    static bool debug = false;
    public static void Main(string[] args)
    {
        ignorePanorama = GetYesNoInput("Игнорировать файлы panorama?");

        if (args.Length == 0 )
        {
            CheckVPKFiles("csgo/pak01_dir.vpk");
            return;
        }

        foreach (string file in args)
        {
            if (file == "-debug")
                debug = true;
            CheckVPKFiles(file + "/pak01_dir.vpk");
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
        string fileTypes = "";
        foreach (var entry in package.Entries)
        {
            filesCount += entry.Value.Count;
            fileTypes += entry.Key + " ";
        }

        Console.WriteLine($"Количество файлов: {filesCount}\nТипы файлов: {fileTypes}");

        DateTime unpackStartTime = DateTime.Now;
        foreach (var entry in package.Entries)
        {
            foreach (var item in entry.Value.ToArray())
            {
                string file = vpkfolder + item.GetFullPath();
                string? directory = Path.GetDirectoryName(file);

                if (file.Contains("panorama") && ignorePanorama)
                    continue;

                if (!File.Exists(file))
                {
                    Console.WriteLine($"{file} не найден");

                    if(directory != null)
                        Directory.CreateDirectory(directory);

                    package.ReadEntry(item, out byte[] fileContents);
                    File.WriteAllBytes(file, fileContents);
                }
                else if (CalculateCrc32FromFile(file) != item.CRC32)
                {
                    Console.WriteLine($"{file} изменен");

                    if (directory != null)
                        Directory.CreateDirectory(directory);

                    package.ReadEntry(item, out byte[] fileContents);
                    File.WriteAllBytes(file, fileContents);
                }
                else if (debug)
                {
                    Console.WriteLine($"{file} прошел проверку");
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