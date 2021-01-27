using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using CryDuplicateFinder.Algorithms;

namespace CryDuplicateFinder
{
    public class ViewModel : INotifyPropertyChanged
    {
        FileEntry selectedFile = null;

        ObservableCollection<FileEntry> files = new();
        bool startReady = false, selectionReady = true, busy = false;
        string rootdir = null, status = null, speedStatus = null;
        int prgMax = 100, prgVal = 0;
        double minSimilarity = 90;

        public string RootDirectory
        {
            get => rootdir ?? "Select root directory"; set
            {
                rootdir = value;
                StartReady = rootdir != null && Directory.Exists(rootdir);

                Changed();
                Changed(nameof(StartReady));
            }
        }

        public int ProgressMax { get => prgMax; set { prgMax = value; Changed(); } }
        public int ProgressValue { get => prgVal; set { prgVal = value; Changed(); Changed(nameof(CanDeleteGlobal)); } }
        public string Status { get => status ?? "Idle"; set { status = value; Changed(); } }
        public string SpeedStatus { get => speedStatus ?? "Waiting to start"; set { speedStatus = value; Changed(); } }
        public ObservableCollection<FileEntry> Files { get => files; set { files = value; Changed(); } }

        public double MinSimilarity
        {
            get => minSimilarity; set
            {
                minSimilarity = value;
                if (minSimilarity > 100) minSimilarity = 100;
                else if (minSimilarity < 0) minSimilarity = 0;
                Changed();
            }
        }

        public bool StartReady { get => startReady; set { startReady = value; Changed(); } }
        public bool SelectionReady { get => selectionReady; set { selectionReady = value; Changed(); } }
        public bool IsBusy
        {
            get => busy; set
            {
                busy = value;
                StartReady = !busy;
                SelectionReady = !busy;

                Changed();
                Changed(nameof(StartReady));
                Changed(nameof(SelectionReady));
            }
        }

        public FileEntry SelectedFile { get => selectedFile; set { selectedFile = value; Changed(); Changed(nameof(CanDeleteLocal)); } }
        public bool CanDeleteLocal => SelectedFile != null && SelectedFile.FinishedAnalysis != null;
        public bool CanDeleteGlobal => ProgressValue == ProgressMax;

        public ViewModel()
        {

        }

        public async Task Start(DuplicateCheckingMode mode)
        {
            IsBusy = true;
            try
            {
                Status = "Getting files...";
                await AnalyzeDirectory();

                foreach (var f in Files)
                {
                    try
                    {
                        var fname = Path.GetFileNameWithoutExtension(f.Path);
                        Status = $"[{ProgressValue + 1}/{ProgressMax}] Finding duplicates for '{fname}'";

                        await FindDuplicates(f, mode);

                        //await Task.Delay(5);
                    }
                    catch (Exception ex)
                    {
                        // failed one - maybe log?
                    }
                    finally
                    {
                        ProgressValue++;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while analyzing files!\n" + ex.Message,
                    "Analysis error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Status = null;
                IsBusy = false;
            }
        }

        Task AnalyzeDirectory()
        {
            return Task.Run(() =>
            {
                // Get all images in all directories
                var files = Directory.GetFiles(RootDirectory, "*.*", SearchOption.AllDirectories)
                    .Where(x =>
                    {
                        var ext = Path.GetExtension(x).ToLower();
                        switch (ext)
                        {
                            // acceptable extensions
                            case ".jpg":
                            case ".jpeg":
                            case ".png":
                            case ".gif":
                            case ".webp":
                            case ".jfif":
                            case ".bmp":
                            case ".tif":
                            case ".tiff":
                                return true;
                            default:
                                return false;
                        }
                    }).Select(x => new FileEntry
                    {
                        Path = x
                    });

                // Set observable collection
                Files = new(files);
                ProgressMax = Files.Count;
                ProgressValue = 0;
            });
        }

        Task FindDuplicates(FileEntry file, DuplicateCheckingMode mode)
        {
            return file.CheckForDuplicates(Files, mode);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void Changed([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new(name));
    }
}
