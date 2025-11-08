using Antlr4.Runtime.Tree.Xpath;
using AtlusScriptLibrary.Common.Collections;
using AtlusScriptLibrary.Common.Libraries;
using AtlusScriptLibrary.Common.Logging;
using AtlusScriptLibrary.Common.Text.Encodings;
using AtlusScriptLibrary.FlowScriptLanguage;
using AtlusScriptLibrary.FlowScriptLanguage.Compiler;
using AtlusScriptLibrary.FlowScriptLanguage.Syntax;
using AtlusScriptLibrary.MessageScriptLanguage;
using AtlusScriptLibrary.MessageScriptLanguage.Decompiler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FlowFormatVersion = AtlusScriptLibrary.FlowScriptLanguage.FormatVersion;
using MsgFormatVersion = AtlusScriptLibrary.MessageScriptLanguage.FormatVersion;

namespace AemulusModManager.Utilities.FileMerging
{
    // the very very vast majority of this class's original functionality has been refactored and moved to AtlusScriptMerger.cs, currently im just using this for any misc file operations that dont fit better in some other class

    public class Utils
    {
        public enum Game
        {
            P1,
            P3FES,
            P3P_PSP,
            P4G_PC32,
            P4G_Vita,
            P5,
            P5R_PS4,
            P5R_Switch,
            P5S,
            PQ,
            PQ2
        }

        public static string ToString(Game game)
        {
            switch (game)
            {
                case Game.P1:
                    return "Persona 1 (PSP)";
                case Game.P3FES:
                    return "Persona 3 FES";
                case Game.P3P_PSP:
                    return "Persona 3 Portable";
                case Game.P4G_PC32:
                    return "Persona 4 Golden (PC 32-Bit)";
                case Game.P4G_Vita:
                    return "Persona 4 Golden (Vita)";
                case Game.P5:
                    return "Persona 5";
                case Game.P5R_PS4:
                    return "Persona 5 Royal (PS4)";
                case Game.P5R_Switch:
                    return "Persona 5 Royal (Switch)";
                case Game.P5S:
                    return "Persona 5 Strikers";
                case Game.PQ:
                    return "Persona Q";
                case Game.PQ2:
                    return "Persona Q2";
                default:
                    throw new ArgumentException("Unrecognized game");
            }
        }

        public static Game FromString(string game)
        {
            switch (game)
            {
                case "Persona 1 (PSP)":
                    return Game.P1;
                case "Persona 3 FES":
                    return Game.P3FES;
                case "Persona 3 Portable":
                case "Persona 3 Portable (PSP)":
                    return Game.P3P_PSP;
                case "Persona 4 Golden":
                case "Persona 4 Golden (PC 32-Bit)":
                    return Game.P4G_PC32;
                case "Persona 4 Golden (Vita)":
                    return Game.P4G_Vita;
                case "Persona 5":
                    return Game.P5;
                case "Persona 5 Royal":
                case "Persona 5 Royal (PS4)":
                    return Game.P5R_PS4;
                case "Persona 5 Royal (Switch)":
                    return Game.P5R_Switch;
                case "Persona 5 Strikers":
                    return Game.P5S;
                case "Persona Q":
                    return Game.PQ;
                case "Persona Q2":
                    return Game.PQ2;
                default:
                    throw new ArgumentException("Unrecognized game");
            }
        }

        private Game curGame;

        public static string aemDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public static string packagesDir = Path.Combine(aemDir, "Packages");
        public static string originalDir = Path.Combine(aemDir, "Original");
        public static string configDir = Path.Combine(aemDir, "Config");

        public string curGamePackageFolder;
        public string curGameOriginalFileFolder;
        public string curGameConfigFolder;

        public static string dependenciesDir = Path.Combine(aemDir, "Dependencies");
        public static string pakPackExe = Path.Combine(dependenciesDir, "PAKPack", "PAKPack.exe");
        public static string _7zExe = Path.Combine(dependenciesDir, "7z", "7z.exe");
        public static string filteredCpkCsv = Path.Combine(dependenciesDir, "FilteredCpkCsv");
        public static string decEbootExe = Path.Combine(dependenciesDir, "DecEboot", "deceboot.exe");
        public static string preappfile = Path.Combine(dependenciesDir, "Preappfile", "preappfile.exe");

        public Utils(Game _curGame) => ChangeGame(_curGame);

        public void ChangeGame(Game game)
        {
            curGame = game;
            curGamePackageFolder = Path.Combine(packagesDir, ToString(curGame));
            curGameOriginalFileFolder = Path.Combine(originalDir, ToString(curGame));
            curGameConfigFolder = Path.Combine(configDir, ToString(curGame));
        }

        public void ChangeGame(string game)
        {
            curGame = FromString(game);
            curGamePackageFolder = Path.Combine(packagesDir, game);
            curGameOriginalFileFolder = Path.Combine(originalDir, game);
            curGameConfigFolder = Path.Combine(configDir, game);
        }

        public static bool IsFileLocked(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }

            //file is not locked
            return false;
        }

        /// <summary>
        /// Checks if two files are the same by comparing their last write time
        /// This is not perfect as file contents are not actually compared however, it is significantly faster than comparing file contents/hashes
        /// and will almost always be right (you'd have to actually try to create two different files with the exact same last write time)
        /// </summary>
        /// <param name="file1">The full path to the first file to check</param>
        /// <param name="file2">The full path to the second file to check</param>
        /// <returns>True if the two files have the same last write time, 
        /// false if they do not or if an error occurs checking the files (such as one not existing)</returns>
        public static bool SameFiles(string file1, string file2)
        {
            try
            {
                FileInfo file1Info = new FileInfo(file1);
                FileInfo file2Info = new FileInfo(file2);
                return file1Info.LastWriteTimeUtc.Equals(file2Info.LastWriteTimeUtc);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if two files are the same by comparing their SHA512 hashes
        /// <param name="=file1">The full path to the first file to check</param>
        /// <param name="=file2">The full path to the second file to check</param>
        /// </summary>
        /// <returns>True if the two files have the same hash (they're the same file), 
        /// false if they are different or if an error occurs checking the two files (such as one not existing)</returns>
        public static bool SameFilesByHash(string file1, string file2)
        {
            var timer = new Stopwatch();
            timer.Start();
            try
            {
                byte[] file1Bytes = File.ReadAllBytes(file1);
                byte[] file2Bytes = File.ReadAllBytes(file2);
                var sha512 = new SHA512CryptoServiceProvider();
                var hash1 = sha512.ComputeHash(file1Bytes);
                var hash2 = sha512.ComputeHash(file2Bytes);
                for (int i = 0; i < hash1.Length; i++)
                {
                    if (hash1[i] == hash2[i])
                        return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                timer.Stop();
                Utilities.ParallelLogger.Log($@"[INFO] Compared file hashes in {timer.ElapsedMilliseconds}ms");
            }
        }

        public static void UpperAll(string modDir)
        {
            Utilities.ParallelLogger.Log($"[INFO] Attempting to rename all in {modDir} to uppercase for platform support.");
            Stack<string> directoryStack = new Stack<string>();
            directoryStack.Push(modDir);
            while (directoryStack.Count > 0)
            {
                string currentDir = directoryStack.Pop();
                if (Directory.Exists(currentDir))
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(currentDir);
                    foreach (DirectoryInfo subdir in directoryInfo.GetDirectories())
                    {
                        string newName = subdir.FullName.ToUpper();
                        if (newName != subdir.FullName)
                        {
                            Directory.Move(subdir.FullName, $@"{subdir.FullName}temp");
                            Directory.Move($@"{subdir.FullName}temp", newName);
                        }
                        directoryStack.Push(newName);
                    }
                    foreach (FileInfo file in directoryInfo.GetFiles())
                    {
                        string name = Path.GetFileNameWithoutExtension(file.Name);
                        string extension = Path.GetExtension(file.Name);

                        string newName = Path.Combine(directoryInfo.FullName, name.ToUpper() + extension.ToUpper());
                        if (newName != file.FullName)
                        {
                            File.Move(file.FullName, $@"{file.FullName}temp");
                            File.Move($@"{file.FullName}temp", newName);
                        }
                    }
                }
                else
                {
                    throw new DirectoryNotFoundException("Directory not found: " + currentDir);
                }
            }
        }

        public static byte[] SliceArray(byte[] source, int start, int end)
        {
            int length = end - start;
            byte[] dest = new byte[length];
            Array.Copy(source, start, dest, 0, length);
            return dest;
        }

        public static void PAKPackCMD(string args)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = $"\"{pakPackExe}\"";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = args;
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                // Add this: wait until process does its work
                process.WaitForExit();
            }
        }
    }
}
