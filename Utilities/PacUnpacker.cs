using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using AemulusModManager.Utilities;
using CriFsV2Lib;
using CriFsV2Lib.Definitions;
using System.Windows.Controls;
using CriFsV2Lib.Definitions.Structs;
using CriFsV2Lib.Definitions.Interfaces;
using CriFsV2Lib.Definitions.Utilities;
using CriFsV2Lib.Encryption.Game;

namespace AemulusModManager
{
    public static class PacUnpacker
    {
        private static string[] pakExtensions = { ".pak", ".pac", ".pack", ".bin", ".abin", ".tpc", ".fpc", ".gsd", ".arc" };
        private static string[] wantedFileExtensions = { ".bf", ".bmd", ".pm1", ".acb", ".awb", ".ctd", ".ftd", ".dat", ".spd", ".gtx" };

        private static string exe7zip = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\7z\7z.exe";

        internal class FileToExtract : IBatchFileExtractorItem
        {
            public string FullPath { get; set; }
            public CpkFile File { get; set; }
            public FileToExtract(string _fullPath, CpkFile _file)
            {
                FullPath = _fullPath;
                File = _file;
            }
        }
        //P1PSP
        public static async Task UnzipAndUnBin(string iso)
        {
            var tasks = new List<Task>();

            tasks.Add(Task.Run(() =>
            {
                if (!File.Exists(iso))
                {
                    Console.Write($"[ERROR] Couldn't find {iso}. Please correct the file path in config.");
                    throw new FileNotFoundException();
                }
            }));
            tasks.Add(Task.Run(() => { Directory.CreateDirectory($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 1 (PSP)"); }));

            var startInfo = Task<ProcessStartInfo>.Run(() =>
            {
                if (!File.Exists(exe7zip))
                {
                    Console.Write($"[ERROR] Couldn't find {exe7zip}. Please check if it was blocked by your anti-virus.");
                    throw new FileNotFoundException();
                }
                ProcessStartInfo startInfo = new ProcessStartInfo(exe7zip);
                startInfo.CreateNoWindow = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.UseShellExecute = false;
                startInfo.Arguments = $"x -y \"{iso}\" -o\"" + $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 1 (PSP)";
                return startInfo;
            });
            tasks.Add(startInfo);

            var ebootDecoder = Task<ProcessStartInfo>.Run(() =>
            {
                ProcessStartInfo ebootDecoder = new ProcessStartInfo();
                ebootDecoder.CreateNoWindow = true;
                ebootDecoder.UseShellExecute = false;
                ebootDecoder.FileName = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\\DecEboot\deceboot.exe";
                ebootDecoder.WindowStyle = ProcessWindowStyle.Hidden;
                ebootDecoder.Arguments = "\"" + $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 1 (PSP)\PSP_GAME\SYSDIR\EBOOT_ENC.BIN" + "\" \"" + $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 1 (PSP)\PSP_GAME\SYSDIR\EBOOT.BIN" + "\"";
                return ebootDecoder;
            });
            tasks.Add(ebootDecoder);
            var validation = Task.WhenAll(tasks);
            await validation;
            if (validation.IsFaulted) { return; }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });

            Utilities.ParallelLogger.Log($"[INFO] Extracting files from {iso}");
            using (Process process = new Process())
            {
                process.StartInfo = startInfo.Result;
                process.Start();
                process.WaitForExit();
            }
            
            File.Move($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 1 (PSP)\PSP_GAME\SYSDIR\EBOOT.BIN", $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 1 (PSP)\PSP_GAME\SYSDIR\EBOOT_ENC.BIN");
            Utilities.ParallelLogger.Log($"[INFO] Decrypting EBOOT.BIN");
            using (Process process = new Process())
            {
                process.StartInfo = ebootDecoder.Result;
                process.Start();

                // Add this: wait until process does its work
                process.WaitForExit();
            }
            File.Delete($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 1 (PSP)\PSP_GAME\SYSDIR\EBOOT_ENC.BIN");
            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }

        // P3F
        public static async Task Unzip(string iso)
        {
            var validationTasks = new List<Task>();

            validationTasks.Add(Task.Run(() =>
            {
                if (!File.Exists(iso))
                {
                    Console.Write($"[ERROR] Couldn't find {iso}. Please correct the file path in config.");
                    throw new FileNotFoundException();
                }
            }));

            var getStartInfo = Task<ProcessStartInfo>.Run(() =>
            {
                if (!File.Exists(exe7zip))
                {
                    Console.Write($"[ERROR] Couldn't find {exe7zip}. Please check if it was blocked by your anti-virus.");
                    throw new FileNotFoundException();
                }
                ProcessStartInfo startInfo = new ProcessStartInfo(exe7zip);
                startInfo.CreateNoWindow = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.UseShellExecute = false;
                startInfo.Arguments = $"x -y \"{iso}\" -o\"" + $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 FES" + "\" BTL.CVM DATA.CVM";
                return startInfo;
            });
            validationTasks.Add(getStartInfo);

            var validation = Task.WhenAll(validationTasks);
            await validation;
            if (validation.IsFaulted) { return; }
            var startInfo = getStartInfo.Result;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });

            var extractionTasks = new List<Task>();

            extractionTasks.Add(Task.Run(() =>
            {
                Utilities.ParallelLogger.Log($"[INFO] Extracting BTL.CVM and DATA.CVM from {iso}");
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();
                }
            }));

            var btlCvmStartInfo = Task<ProcessStartInfo>.Run(() =>
            {
                var _btlCvmInfo = new ProcessStartInfo();
                _btlCvmInfo = startInfo;
                _btlCvmInfo.Arguments = "x -y \"" + $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 FES\BTL.CVM" + "\" -o\"" + $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 FES\BTL" + "\" *.BIN *.PAK *.PAC *.TBL *.SPR *.BF *.BMD *.PM1 *.bf *.bmd *.pm1 *.FPC -r";

                return _btlCvmInfo;
            });
            extractionTasks.Add(btlCvmStartInfo);

            var dataCvmStartInfo = Task<ProcessStartInfo>.Run(() =>
            {
                var _dataCvmInfo = new ProcessStartInfo();
                _dataCvmInfo = startInfo;
                _dataCvmInfo.Arguments = "x -y \"" + $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 FES\DATA.CVM" + "\" -o\"" + $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 FES\DATA" + "\" *.BIN *.PAK *.PAC *.TBL *.SPR *.BF *.BMD *.PM1 *.bf *.bmd *.pm1 *.FPC -r";

                return _dataCvmInfo;
            });
            extractionTasks.Add(dataCvmStartInfo);
            await Task.WhenAll(extractionTasks);

            var extractionTasks2 = new List<Task>();
            extractionTasks2.Add(Task.Run(() =>
            {
                Utilities.ParallelLogger.Log($"[INFO] Extracting base files from BTL.CVM");
                using (Process process = new Process())
                {
                    process.StartInfo = btlCvmStartInfo.Result;
                    process.Start();
                    process.WaitForExit();
                }
            }));
            extractionTasks2.Add(Task.Run(() =>
            {
                Utilities.ParallelLogger.Log($"[INFO] Extracting base files from DATA.CVM");
                using (Process process = new Process())
                {
                    process.StartInfo = dataCvmStartInfo.Result;
                    process.Start();
                    process.WaitForExit();
                }
            }));
            await Task.WhenAll(extractionTasks2);
            await ExtractWantedFiles($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 FES");
            File.Delete($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 FES\BTL.CVM");
            File.Delete($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 FES\DATA.CVM");
            Utilities.ParallelLogger.Log($"[INFO] Finished unpacking base files!");
            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }

        // P3P
        public static async Task UnzipAndUnpackCPK(string iso)
        {
            var validationTasks = new List<Task>();

            var pathToExtract = Task<string>.Run(() =>
            {
                string pathToExtract = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 Portable";
                Directory.CreateDirectory(pathToExtract);
                return pathToExtract;
            });
            validationTasks.Add(pathToExtract);

            validationTasks.Add(Task.Run(() =>
            {
                if (!File.Exists(iso))
                {
                    Console.Write($"[ERROR] Couldn't find {iso}. Please correct the file path in config.");
                    throw new FileNotFoundException();
                }
            }));

            var startInfo = Task<ProcessStartInfo>.Run(() =>
            {
                if (!File.Exists(exe7zip))
                {
                    Console.Write($"[ERROR] Couldn't find {exe7zip}. Please check if it was blocked by your anti-virus.");
                    throw new FileNotFoundException();
                }
                ProcessStartInfo startInfo = new ProcessStartInfo(exe7zip);
                startInfo.CreateNoWindow = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.UseShellExecute = false;
                startInfo.Arguments = $"x -y \"{iso}\" -o\"" + $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 Portable" + "\" PSP_GAME\\USRDIR\\umd0.cpk";
                return startInfo;
            });
            validationTasks.Add(startInfo);

            var umd0Files = Task<string[]>.Run(() => { return File.ReadAllLines($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_umd0.csv"); });
            validationTasks.Add(umd0Files);

            var validation = Task.WhenAll(validationTasks);
            await validation;
            if (validation.IsFaulted) { return; }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });

            Utilities.ParallelLogger.Log($"[INFO] Extracting umd0.cpk from {iso}");
            using (Process process = new Process())
            {
                process.StartInfo = startInfo.Result;
                process.Start();
                process.WaitForExit();
            }

            var umd0Path = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 Portable\PSP_GAME\USRDIR\umd0.cpk";

            Utilities.ParallelLogger.Log($"[INFO] Extracting files from umd0.cpk");
            if (File.Exists(umd0Path))
                CriFsUnpack(umd0Path, pathToExtract.Result, umd0Files.Result);
            else
                Utilities.ParallelLogger.Log($@"[ERROR] Couldn't find {umd0Path}.");

            Utilities.ParallelLogger.Log("[INFO] Unpacking extracted files");
                await ExtractWantedFiles($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 Portable\data");
            if (Directory.Exists($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 Portable\PSP_GAME"))
                Directory.Delete($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 3 Portable\PSP_GAME", true);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }

        // P4G
        public static async Task Unpack(string directory, string cpk)
        {
            var setupTasks = new List<Task>();
            setupTasks.Add(Task.Run(() =>
            {
                if (!Directory.Exists(directory))
                {
                    Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {directory}. Please correct the file path in config.");
                    throw new DirectoryNotFoundException();
                }
            }));
            setupTasks.Add(Task.Run(() => { Directory.CreateDirectory($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 4 Golden"); }));

            var getPacs = Task<List<string>>.Run(() =>
            {
                List<string> pacs = new List<string>();
                switch (cpk)
                {
                    case "data_e.cpk":
                        pacs.Add("data00004.pac");
                        pacs.Add("data_e.cpk");
                        break;
                    case "data.cpk":
                        pacs.Add("data00000.pac");
                        pacs.Add("data00001.pac");
                        pacs.Add("data00003.pac");
                        pacs.Add("data.cpk");
                        break;
                    case "data_k.cpk":
                        pacs.Add("data00005.pac");
                        pacs.Add("data_k.cpk");
                        break;
                    case "data_c.cpk":
                        pacs.Add("data00006.pac");
                        pacs.Add("data_c.cpk");
                        break;
                }
                return pacs;
            });
            setupTasks.Add(getPacs);

            var getStartInfo = Task<ProcessStartInfo>.Run(() =>
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\Preappfile\preappfile.exe";
                if (!File.Exists(startInfo.FileName))
                {
                    Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {startInfo.FileName}. Please check if it was blocked by your anti-virus.");
                    throw new FileNotFoundException();
                }
                startInfo.CreateNoWindow = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;
                return startInfo;
            });
            setupTasks.Add(getStartInfo);
            var validation = Task.WhenAll(setupTasks);
            if(validation.IsFaulted) { return; }

            List<string> globs = new List<string> { "*[!0-9].bin", "*2[0-1][0-9].bin", "*.arc", "*.pac", "*.pack", "*.bf", "*.bmd", "*.pm1" };
            var pacs = getPacs.Result;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });

            var pacExtract = new List<Task>();
            foreach (var pac in pacs)
            {
                pacExtract.Add(Task.Run(async () =>
                {
                    Utilities.ParallelLogger.Log($"[INFO] Unpacking files for {pac}...");
                    var globExtract = new List<Task>();
                    foreach (var glob in globs)
                    {
                        globExtract.Add(Task.Run(() =>
                        {
                            var startInfo = new ProcessStartInfo();
                            startInfo = getStartInfo.Result;
                            startInfo.Arguments = $@"-i ""{directory}\{pac}"" -o ""{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 4 Golden\{Path.GetFileNameWithoutExtension(pac)}"" --unpack-filter {glob}";
                            using (Process process = new Process())
                            {
                                process.StartInfo = startInfo;
                                process.Start();
                                while (!process.HasExited)
                                {
                                    string text = process.StandardOutput.ReadLine();
                                    if (!String.IsNullOrEmpty(text))
                                        Utilities.ParallelLogger.Log($"[INFO] {text}");
                                }
                            }
                        }));
                    }
                    await Task.WhenAll(globExtract);
                    await ExtractWantedFiles($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 4 Golden\{Path.GetFileNameWithoutExtension(pac)}");
                }));
            }
            pacExtract.Add(Task.Run(() =>
            {
                if (File.Exists($@"{directory}\{cpk}") && !File.Exists($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 4 Golden\{cpk}"))
                {
                    Utilities.ParallelLogger.Log($@"[INFO] Backing up {cpk}");
                    File.Copy($@"{directory}\{cpk}", $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 4 Golden\{cpk}", true);
                }
            }));
            pacExtract.Add(Task.Run(() =>
            {
                if (File.Exists($@"{directory}\movie.cpk") && !File.Exists($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 4 Golden\movie.cpk"))
                {
                    Utilities.ParallelLogger.Log($@"[INFO] Backing up movie.cpk");
                    File.Copy($@"{directory}\movie.cpk", $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 4 Golden\movie.cpk", true);
                }
            }));
            await Task.WhenAll(pacExtract);

            Utilities.ParallelLogger.Log("[INFO] Finished unpacking base files!");
            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }


        public static async Task UnpackP5CPK(string directory)
        {
            var tasks = new List<Task>();

            if (!Directory.Exists(directory))
            {
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {directory}. Please correct the file path in config.");
                return;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });

            tasks.Add(Task.Run(() =>
            {
                if (File.Exists($@"{directory}\ps3.cpk.66600") && File.Exists($@"{directory}\ps3.cpk.66601") && File.Exists($@"{directory}\ps3.cpk.66602")
                   && !File.Exists($@"{directory}\ps3.cpk"))
                {
                    Console.Write("[INFO] Combining ps3.cpk parts");
                    ProcessStartInfo cmdInfo = new ProcessStartInfo();
                    cmdInfo.CreateNoWindow = true;
                    cmdInfo.FileName = @"CMD.exe";
                    cmdInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    cmdInfo.Arguments = $@"/C copy /b ""{directory}\ps3.cpk.66600"" + ""{directory}\ps3.cpk.66601"" + ""{directory}\ps3.cpk.66602"" ""{directory}\ps3.cpk""";

                    using (Process process = new Process())
                    {
                        process.StartInfo = cmdInfo;
                        process.Start();
                        process.WaitForExit();
                    }
                }
            }));

            var getPathToExtract = Task<string>.Run(() =>
            {
                string pathToExtract = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 5";
                Directory.CreateDirectory(pathToExtract);
                return pathToExtract;
            });
            tasks.Add(getPathToExtract);

            var dataFiles = Task<string[]>.Run(() =>
            {
                if(!File.Exists($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_data.csv"))
                {
                    Utilities.ParallelLogger.Log($@"[ERROR] Couldn't find CSV file used for unpacking in Dependencies\FilteredCpkCsv: filtered_data.csv");
                    throw new FileNotFoundException();
                }
                return File.ReadAllLines($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_data.csv");
            });
            tasks.Add(dataFiles);

            var ps3Files = Task<string[]>.Run(() =>
            {
                if (!File.Exists($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_ps3.csv"))
                {
                    Utilities.ParallelLogger.Log($@"[ERROR] Couldn't find CSV file used for unpacking in Dependencies\FilteredCpkCsv: filtered_ps3.csv");
                    throw new FileNotFoundException();
                }
                return File.ReadAllLines($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_ps3.csv");
            });
            tasks.Add(ps3Files);

            var validation = Task.WhenAll(tasks);
            await validation;
            if (validation.IsFaulted)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Mouse.OverrideCursor = null;
                });
                return;
            }

            var pathToExtract = getPathToExtract.Result;
            Utilities.ParallelLogger.Log($"[INFO] Extracting data.cpk");
            if (File.Exists($@"{directory}\data.cpk"))
                CriFsUnpack($@"{directory}\data.cpk", pathToExtract, dataFiles.Result);
            else
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find data.cpk in {directory}.");

            Utilities.ParallelLogger.Log($"[INFO] Extracting ps3.cpk");
            if (File.Exists($@"{directory}\ps3.cpk"))
                CriFsUnpack($@"{directory}\ps3.cpk", pathToExtract, ps3Files.Result);
            else
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find ps3.cpk in {directory}.");

            await ExtractWantedFiles(pathToExtract);
            Utilities.ParallelLogger.Log($"[INFO] Finished unpacking base files!");
            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }
        public static async Task UnpackP5RCPKs(string directory, string language, string version)
        {
            var tasks = new List<Task>();
            if (!Directory.Exists(directory))
            {
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {directory}. Please correct the file path.");
                return;
            }

            var getPathToExtract = Task<string>.Run(() =>
            {
                string pathToExtract = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 5 Royal (PS4)";
                Directory.CreateDirectory(pathToExtract);
                return pathToExtract;
            });
            tasks.Add(getPathToExtract);

            var dataRFiles = Task<string[]>.Run(() =>
            {
                if (!File.Exists($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_dataR.csv"))
                {
                    Utilities.ParallelLogger.Log($@"[ERROR] Couldn't find CSV file used for unpacking in Dependencies\FilteredCpkCsv: filtered_dataR.csv");
                    throw new FileNotFoundException();
                }
                return File.ReadAllLines($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_dataR.csv");
            });
            tasks.Add(dataRFiles);

            var ps4RFiles = Task<string[]>.Run(() =>
            {
                if (!File.Exists($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_ps4R.csv"))
                {
                    Utilities.ParallelLogger.Log($@"[ERROR] Couldn't find CSV file used for unpacking in Dependencies\FilteredCpkCsv: filtered_ps4R.csv");
                    throw new FileNotFoundException();
                }
                return File.ReadAllLines($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_ps4R.csv");
            });
            tasks.Add(ps4RFiles);

            var validation = Task.WhenAll(tasks);
            await validation;
            if (validation.IsFaulted) { return; }

            var pathToExtract = getPathToExtract.Result;
            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });

            Utilities.ParallelLogger.Log($"[INFO] Extracting dataR.cpk");
            if (File.Exists($@"{directory}\dataR.cpk"))
                CriFsUnpack($@"{directory}\dataR.cpk", pathToExtract, dataRFiles.Result);
            else
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find dataR.cpk in {directory}.");

            Utilities.ParallelLogger.Log($"[INFO] Extracting ps4R.cpk");
            if (File.Exists($@"{directory}\ps4R.cpk"))
                CriFsUnpack($@"{directory}\ps4R.cpk", pathToExtract, ps4RFiles.Result);
            else
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find ps4R.cpk in {directory}.");

            if (language != "English")
            {
                string[] dataRLocalizedFiles = File.ReadAllLines($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_dataR_Localized.csv");
                var localizedCpk = String.Empty;
                switch (language)
                {
                    case "French":
                        localizedCpk = "dataR_F.cpk";
                        break;
                    case "Italian":
                        localizedCpk = "dataR_I.cpk";
                        break;
                    case "German":
                        localizedCpk = "dataR_G.cpk";
                        break;
                    case "Spanish":
                        localizedCpk = "dataR_S.cpk";
                        break;
                }
                Utilities.ParallelLogger.Log($"[INFO] Extracting {localizedCpk}");
                if (File.Exists($@"{directory}\{localizedCpk}"))
                    CriFsUnpack($@"{directory}\{localizedCpk}", pathToExtract, dataRLocalizedFiles);
                else
                    Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {localizedCpk} in {directory}.");
            }

            // Extract patch2R.cpk files
            if (version == ">= 1.02")
            {
                string[] patch2RFiles = File.ReadAllLines($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_patch2R.csv");
                Utilities.ParallelLogger.Log($"[INFO] Extracting patch2R.cpk");
                if (File.Exists($@"{directory}\patch2R.cpk"))
                    CriFsUnpack($@"{directory}\patch2R.cpk", pathToExtract, patch2RFiles);
                else
                    Utilities.ParallelLogger.Log($"[ERROR] Couldn't find patch2R.cpk in {directory}.");
                if (language != "English")
                {
                    var patchSuffix = String.Empty;
                    switch (language)
                    {
                        case "French":
                            patchSuffix = "_F";
                            break;
                        case "Italian":
                            patchSuffix = "_I";
                            break;
                        case "German":
                            patchSuffix = "_G";
                            break;
                        case "Spanish":
                            patchSuffix = "_S";
                            break;
                    }
                    string[] patch2RLocalizedFiles = File.ReadAllLines($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_patch2R{patchSuffix}.csv");
                    Utilities.ParallelLogger.Log($"[INFO] Extracting patch2R{patchSuffix}.cpk");
                    if (File.Exists($@"{directory}\patch2R{patchSuffix}.cpk"))
                        CriFsUnpack($@"{directory}\patch2R{patchSuffix}.cpk", pathToExtract, patch2RLocalizedFiles);
                    else
                        Utilities.ParallelLogger.Log($"[ERROR] Couldn't find patch2R{patchSuffix}.cpk in {directory}.");
                }
            }

            await ExtractWantedFiles(pathToExtract);
            Utilities.ParallelLogger.Log($"[INFO] Finished unpacking base files!");
            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }
        public static async Task UnpackP5RSwitchCPKs(string directory, string language)
        {
            if (!Directory.Exists(directory))
            {
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {directory}. Please correct the file path.");
                return;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });

            string pathToExtract = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 5 Royal (Switch)";
            Directory.CreateDirectory(pathToExtract);

            Utilities.ParallelLogger.Log($"[INFO] Extracting PATCH1.CPK");
            if (File.Exists($@"{directory}\PATCH1.CPK"))
                CriFsUnpack($@"{directory}\PATCH1.CPK", pathToExtract);
            else
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find PATCH1.CPK in {directory}.");

            Utilities.ParallelLogger.Log($"[INFO] Extracting ALL_USEU.CPK (This will take awhile)");
            if (File.Exists($@"{directory}\ALL_USEU.CPK"))
                CriFsUnpack($@"{directory}\ALL_USEU.CPK", pathToExtract);
            else
                Utilities.ParallelLogger.Log($"[ERROR] Couldn't find ALL_USEU.CPK in {directory}.");

            await ExtractWantedFiles(pathToExtract);
            Utilities.ParallelLogger.Log($"[INFO] Finished unpacking base files!");
            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }

        public static async Task UnpackP4GCPK(string cpk)
        {
            var tasks = new List<Task>();

            tasks.Add(Task.Run(() =>
            {
                if (!File.Exists(cpk))
                {
                    Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {cpk}. Please correct the file path.");
                    throw new FileNotFoundException();
                }
            }));

            var pathToExtract = Task<string>.Run(() =>
            {
                string pathToExtract = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona 4 Golden (Vita)";
                Directory.CreateDirectory(pathToExtract);
                return pathToExtract;
            });
            tasks.Add(pathToExtract);

            var dataFiles = Task<string[]>.Run(() =>
            {
                if (!File.Exists($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_p4gdata.csv"))
                {
                    Utilities.ParallelLogger.Log($@"[ERROR] Couldn't find CSV file used for unpacking in Dependencies\FilteredCpkCsv");
                    throw new FileNotFoundException();
                }
                return File.ReadAllLines($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_p4gdata.csv");
            });
            tasks.Add(dataFiles);
            var validation = Task.WhenAll(tasks);
            await validation;
            if(validation.IsFaulted) { return; }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });

            Utilities.ParallelLogger.Log($"[INFO] Extracting data.cpk");
            CriFsUnpack(cpk, pathToExtract.Result, dataFiles.Result);

            Utilities.ParallelLogger.Log("[INFO] Unpacking extracted files");
            await ExtractWantedFiles(pathToExtract.Result);
            Utilities.ParallelLogger.Log($"[INFO] Finished unpacking base files!");
            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }
        public static async Task UnpackPQ2CPK(string cpk)
        {
            var validationTasks = new List<Task>();
            validationTasks.Add(Task.Run(() =>
            {
                if (!File.Exists(cpk))
                {
                    Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {cpk}. Please correct the file path.");
                    throw new FileNotFoundException();
                }
            }));

            var getPathToExtract = Task<string>.Run(() =>
            {
                var _pathToExtract = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona Q2";
                Directory.CreateDirectory(_pathToExtract);
                return _pathToExtract;
            });
            validationTasks.Add(getPathToExtract);

            var getDataFiles = Task<string[]>.Run(() =>
            {
                if (!File.Exists($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_data_pq2.csv"))
                {
                    Utilities.ParallelLogger.Log($@"[ERROR] Couldn't find CSV file used for unpacking in Dependencies\FilteredCpkCsv");
                    throw new FileNotFoundException();
                }
                return File.ReadAllLines($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_data_pq2.csv");
            });
            validationTasks.Add(getDataFiles);
            var validation = Task.WhenAll(validationTasks);
            await validation;
            if (validation.IsFaulted) { return; }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });

            string pathToExtract = getPathToExtract.Result;
            string[] dataFiles = getDataFiles.Result;
            
            Utilities.ParallelLogger.Log($"[INFO] Extracting data.cpk");
            CriFsUnpack(cpk, pathToExtract, dataFiles);
            Utilities.ParallelLogger.Log("[INFO] Unpacking extracted files");
            await ExtractWantedFiles(pathToExtract);
            Utilities.ParallelLogger.Log($"[INFO] Finished unpacking base files!");
            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }
        public static async Task UnpackPQCPK(string cpk)
        {
            var validationTasks = new List<Task>();
            validationTasks.Add(Task.Run(() =>
            {
                if (!File.Exists(cpk))
                {
                    Utilities.ParallelLogger.Log($"[ERROR] Couldn't find {cpk}. Please correct the file path.");
                    throw new FileNotFoundException();
                }
            }));

            var getPathToExtract = Task<string>.Run(() =>
            {
                var _pathToExtract = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Original\Persona Q";
                Directory.CreateDirectory(_pathToExtract);
                return _pathToExtract;
            });
            validationTasks.Add(getPathToExtract);

            var getDataFiles = Task<string[]>.Run(() =>
            {
                if (!File.Exists($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_data_pq.csv"))
                {
                    Utilities.ParallelLogger.Log($@"[ERROR] Couldn't find CSV file used for unpacking in Dependencies\FilteredCpkCsv");
                    throw new FileNotFoundException();
                }
                return File.ReadAllLines($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Dependencies\FilteredCpkCsv\filtered_data_pq.csv");
            });
            validationTasks.Add(getDataFiles);
            var validation = Task.WhenAll(validationTasks);
            validation.Wait();
            if (validation.IsFaulted) { return; }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });

            string pathToExtract = getPathToExtract.Result;
            string[] dataFiles = getDataFiles.Result;
            Utilities.ParallelLogger.Log($"[INFO] Extracting data.cpk");
            CriFsUnpack(cpk, pathToExtract, dataFiles);
            Utilities.ParallelLogger.Log("[INFO] Unpacking extracted files");
            await ExtractWantedFiles(pathToExtract);
            Utilities.ParallelLogger.Log($"[INFO] Finished unpacking base files!");
            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }
        private static void CriFsUnpack(string cpk, string dir, string[] fileList = null)
        {
            using var fileStream = new FileStream(cpk, FileMode.Open);
            using var reader = CriFsLib.Instance.CreateCpkReader(fileStream, true);
            var files = reader.GetFiles();
            fileStream.Close();

            bool extractAll = fileList == null;
            using var extractor = CriFsLib.Instance.CreateBatchExtractor<FileToExtract>(cpk, P5RCrypto.DecryptionFunction);
            for (int x = 0; x < files.Length; x++)
            {
                string filePath = string.IsNullOrEmpty(files[x].Directory) ? files[x].FileName : $@"{files[x].Directory}/{files[x].FileName}";
                if (extractAll || fileList.Contains(filePath))
                {
                    extractor.QueueItem(new FileToExtract(Path.Combine(dir, filePath), files[x]));
                    Utilities.ParallelLogger.Log($@"[INFO] Extracting {filePath}");
                }
            }
            extractor.WaitForCompletion();
            ArrayRental.Reset();
        }
        private static async Task ExtractWantedFiles(string directory)
        {
            if (!Directory.Exists(directory))
                return;

            var files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).Where(s => pakExtensions.Contains(Path.GetExtension(s).ToLower()));
            var extractionTasks = new List<Task>();
            foreach(string file in files)
            {
                extractionTasks.Add(Task.Run(() =>
                {
                    List<string> contents = binMerge.getFileContents(file).Select(x => x.ToLower()).ToList();
                    // Check if there are any files we want (or files that could have files we want) and unpack them if so
                    bool containersFound = contents.Exists(x => pakExtensions.Contains(Path.GetExtension(x).ToLower()));
                    if(contents.Exists(x => wantedFileExtensions.Contains(Path.GetExtension(x).ToLower()) || containersFound))
                    {
                        Utilities.ParallelLogger.Log($"[INFO] Unpacking {file}");
                        binMerge.PAKPackCMD($"unpack \"{file}\"");

                        // Search the location of the unpacked container for wanted files
                        if (containersFound)
                            extractionTasks.Add(ExtractWantedFiles(Path.Combine(Path.GetDirectoryName(file),Path.GetFileNameWithoutExtension(file))));
                    }
                }));
            }
            await Task.WhenAll(extractionTasks);
        }

        public static IEnumerable<IEnumerable<T>> Split<T>(this T[] array, int size)
        {
            for (var i = 0; i < (float)array.Length / size; i++)
            {
                yield return array.Skip(i * size).Take(size);
            }
        }

    }
}
