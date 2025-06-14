﻿using AddInManager.Common;
using ExcelDna.Integration;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace AddInManager {
    internal class Controller {
        public Controller() {
            LoadGeneralOptions();
            if (!Directory.Exists(installedDir)) {
                //Initialize();
                return;
            }
                

            if (generalOptions.autoUpdateAddIns)
                AutoUpdateAddIns();

            foreach (string xll in Directory.GetFiles(installedDir, "*.del")) {
                try {
                    File.Delete(xll);
                } catch { }
            }

            foreach (var i in GetInstalledAddins()) {
                Register(i.Path!);
            }
        }

        public void Initialize() {
            this.generalOptions.autoUpdateAddIns = true;
            this.generalOptions.sources = [
                new AddInsSource {
                    source =
                        @"\\172.20.4.11\Data\Reactor Data\Shared\System monitoring\Monitoring Excel Addin\"
                }
            ];
            Storage.SaveGeneralOptions(this.generalOptions);
        }

        public void OnInstall() {
            InstallDialog dialog = new InstallDialog(GetSourceAddins());
            if (dialog.ShowDialog().GetValueOrDefault()) {
                var installedAddIns = GetInstalledAddins();
                foreach (var i in dialog.GetSelectedAddins()!) {
                    Install(i, installedAddIns, true);
                }
            }
        }

        public void OnManage() {
            ManageDialog dialog = new ManageDialog(GetInstalledAddins());
            if (dialog.ShowDialog().GetValueOrDefault()) {
                foreach (var i in dialog.GetAddinsForUninstall()!) {
                    Uninstall(i.Path!);
                }
            }
        }

        public void OnOptions() {
            OptionsDialog dialog = new OptionsDialog(generalOptions);
            if (dialog.ShowDialog().GetValueOrDefault()) {
                Storage.SaveGeneralOptions(generalOptions);
            }
        }

        private void AutoUpdateAddIns() {
            var installedVersionedAddins = GetInstalledAddins().Where(i => i.IsVersioned).ToList();
            if (installedVersionedAddins.Count() == 0)
                return;

            var sourceAddIns = GetSourceAddins().Where(i => i.IsVersioned);
            foreach (var installedAddIn in installedVersionedAddins) {
                var latestSourceAddin = sourceAddIns.Where(i => SameProduct(i, installedAddIn))
                    .OrderByDescending(i => i.Version).FirstOrDefault();
                if (latestSourceAddin != null && latestSourceAddin.Version > installedAddIn.Version)
                    Install(latestSourceAddin, installedVersionedAddins, false);
            }
        }

        private static void Install(AddInVersionInfo addin, List<AddInVersionInfo> installedAddins, bool register) {
            if (addin.IsVersioned) {
                foreach (var i in installedAddins.Where(i => SameProduct(i, addin))) {
                    Uninstall(i.Path!);
                }
            }

            string installedXllPath = Path.Combine(installedDir, Path.GetFileName(addin.Path ?? addin.Uri!.LocalPath));
            if (File.Exists(installedXllPath)) {
                Uninstall(installedXllPath);
            }

            Storage.CreateDirectoryForFile(installedXllPath);
            if (addin.Path != null) {
                File.Copy(addin.Path, installedXllPath, true);
            } else {
#pragma warning disable SYSLIB0014
                using (System.Net.WebClient wc = new())
                    wc.DownloadFile(addin.Uri!, installedXllPath);
#pragma warning restore SYSLIB0014
            }

            if (register)
                Register(installedXllPath);
        }

        private static void Uninstall(string xllFileName) {
            string installedXllPath = Path.Combine(installedDir, xllFileName);
            Unregister(installedXllPath);

            string delXllPath = installedXllPath + ".del";
            File.Move(installedXllPath, delXllPath, true);
        }

        private static List<AddInVersionInfo> GetInstalledAddins() {
            List<AddInVersionInfo> addins = new();
            if (Directory.Exists(installedDir)) {
                addins = Directory.GetFiles(installedDir, "*.xll").Select(i => new AddInVersionInfo(i))
                    .Where(i => SameProcessBitness(i.Bitness)).ToList();
            }

            return addins;
        }

        private List<AddInVersionInfo> GetSourceAddins() {
            List<AddInVersionInfo> addins = new();
            if (generalOptions.sources != null) {
                foreach (var addinSource in generalOptions.sources)
                    addins.AddRange(GetSourceAddins(addinSource.source).Where(i => SameProcessBitness(i.Bitness)));
            }

            return addins;
        }

        private static IEnumerable<AddInVersionInfo> GetSourceAddins(string? source) {
            IEnumerable<AddInVersionInfo> sourceAddins = new AddInVersionInfo[0];
            if (Uri.IsWellFormedUriString(source, UriKind.Absolute)) {
                if (Uri.TryCreate(source, UriKind.Absolute, out Uri? sourceUri) &&
                    Uri.TryCreate(sourceUri, Utils.IndexFileName, out Uri? indexFileUri)) {
                    List<AddInFile> addinFiles = new();
                    try {
                        addinFiles = XmlSerializer.XmlDeserialize<List<AddInFile>>(indexFileUri.AbsoluteUri);
                    } catch (ApplicationException e) {
                        ExceptionHandler.ShowException(e);
                    }

                    sourceAddins = addinFiles.Select(i => new AddInVersionInfo(sourceUri, i));
                }
            } else if (Directory.Exists(source)) {
                string indexFile = Path.Combine(source, Utils.IndexFileName);
                if (File.Exists(indexFile)) {
                    List<AddInFile> addinFiles = new();
                    try {
                        addinFiles = XmlSerializer.XmlDeserialize<List<AddInFile>>(indexFile);
                    } catch (ApplicationException e) {
                        ExceptionHandler.ShowException(e);
                    }

                    sourceAddins = addinFiles.Select(i => new AddInVersionInfo(source, i));
                } else {
                    sourceAddins = Directory.GetFiles(source, "*.xll").Select(i => new AddInVersionInfo(i));
                }
            }

            return sourceAddins;
        }

        private static void Register(string xllPath) {
            ExcelAsyncUtil.QueueAsMacro(() => { ExcelIntegration.RegisterXLL(xllPath); });
        }

        private static void Unregister(string xllPath) {
            ExcelAsyncUtil.QueueAsMacro(() => { ExcelIntegration.UnregisterXLL(xllPath); });
        }

        private static bool SameProduct(AddInVersionInfo a1, AddInVersionInfo a2) {
            return a1.IsVersioned && a2.IsVersioned && a1.CompanyName == a2.CompanyName &&
                   a1.ProductName == a2.ProductName;
        }

        private static bool SameProcessBitness(Bitness bitness) {
            if (Environment.Is64BitProcess)
                return bitness == Bitness.Bit64;
            else
                return bitness == Bitness.Bit32;
        }

        [MemberNotNull(nameof(generalOptions))]
        private void LoadGeneralOptions() {
            try {
                generalOptions = Storage.LoadGeneralOptions() ?? null!;
            } catch (System.ApplicationException e) {
                System.Windows.MessageBox.Show(e.ToString(), "AddInManager Load general options",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }

            if (generalOptions == null)
                generalOptions = new GeneralOptions();
        }

        private static string installedDir = Storage.GetInstalledAddinsDirectory();
        private GeneralOptions generalOptions;
    }
}