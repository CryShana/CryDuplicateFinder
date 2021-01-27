using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Threading;
using System.Windows.Data;
using System.ComponentModel;
using System.Threading.Tasks;
using CryDuplicateFinder.Algorithms;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace CryDuplicateFinder
{
    public class ViewModel : INotifyPropertyChanged
    {
        FileEntry selectedFile = null;

        CollectionViewSource fview;
        ObservableCollection<FileEntry> files = new();
        bool startReady = false, selectionReady = true, busy = false, hiding = true;
        string rootdir = null, status = null, speedStatus = null;
        int prgMax = 100, prgVal = 0, mxThreads = 16;
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
        public int ProgressValue
        {
            get => prgVal; set
            {
                prgVal = value;
                Changed();
                Changed(nameof(CanHide));
            }
        }
        public string Status { get => status ?? "Idle"; set { status = value; Changed(); } }
        public string SpeedStatus { get => speedStatus ?? "Waiting to start"; set { speedStatus = value; Changed(); } }
        public ObservableCollection<FileEntry> Files
        {
            get => files; set
            {
                files = value; Changed();
                FilesView = new();
                FilesView.Source = files;
                Changed(nameof(FilesView));
            }
        }
        public CollectionViewSource FilesView { get => fview; set { fview = value; Changed(); } }
        public string StartButtonText => IsBusy ? "Stop" : "Start";

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
        public int MaxThreads
        {
            get => mxThreads; set
            {
                mxThreads = value;
                if (MaxThreads < 1) MaxThreads = 1;
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
                SelectionReady = !busy;

                Changed();
                Changed(nameof(StartReady));
                Changed(nameof(SelectionReady));
                Changed(nameof(StartButtonText));
                Changed(nameof(CanDeleteLocal));
                Changed(nameof(CanDeleteGlobal));
            }
        }

        public FileEntry SelectedFile
        {
            get => selectedFile; set
            {
                selectedFile = value;
                Changed();
                Changed(nameof(CanDeleteLocal));
                SelectedFileChanged?.Invoke(this, selectedFile);
            }
        }
        public bool CanDeleteLocal => SelectedFile != null && SelectedFile.FinishedAnalysis != null && !IsBusy;
        public bool CanDeleteGlobal => !IsBusy && Files.Count > 0;

        public bool IsHiding { get => hiding; private set { hiding = value; Changed(); Changed(nameof(CanHide)); } }
        public bool CanHide => !IsHiding && ProgressValue > 1;

        public event EventHandler<FileEntry> SelectedFileChanged;

        public ViewModel()
        {
            DetermineThreadCount();
        }

        void DetermineThreadCount()
        {
            MaxThreads = Environment.ProcessorCount;
        }

        CancellationTokenSource csc = null;
        public async Task Start(DuplicateCheckingMode mode)
        {
            if (csc != null)
            {
                csc.Cancel();
                return;
            }

            csc = new();
            IsBusy = true;
            IsHiding = false;
            SelectedFile = null;
            Status = "Starting...";

            try
            {
                var token = csc.Token;

                // give GUI time to catch up
                await Task.Delay(10);

                Status = "Getting files...";
                await AnalyzeDirectory();

                // reset all cache in checkers
                HistogramDuplicateChecker.ClearCache();
                FeatureDuplicateChecker.ClearCache();

                // give GUI time to catch up
                await Task.Delay(10);

                foreach (var f in Files)
                {
                    if (token.IsCancellationRequested) break;

                    try
                    {
                        var fname = Path.GetFileNameWithoutExtension(f.Path);
                        Status = $"[{ProgressValue + 1}/{ProgressMax}] Finding duplicates for '{fname}'";

                        await FindDuplicates(f, mode, MaxThreads, token);

                        // give GUI time to catch up
                        await Task.Delay(10);
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
                csc = null;
                Status = null;
                IsBusy = false;
            }
        }

        Task AnalyzeDirectory()
        {
            var context = SynchronizationContext.Current;
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
                context.Post(d =>
                {
                    Files = new(files);
                    ProgressMax = Files.Count;
                    ProgressValue = 0;
                }, null);
            });
        }

        Task FindDuplicates(FileEntry file, DuplicateCheckingMode mode, int maxThreads, CancellationToken token) => file.CheckForDuplicates(Files, mode, maxThreads, token);

        public void HideFilesWithoutDuplicates()
        {
            if (IsHiding || Files == null || FilesView == null) return;

            if (FilesView.View.Filter == null)
            {
                FilesView.View.Filter = (a) =>
                {
                    var f = (FileEntry)a;
                    if (f.FinishedAnalysis != null && f.Duplicates.Count == 0)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                };
            }
            else FilesView.View.Refresh();
        }


        public event PropertyChangedEventHandler PropertyChanged;
        void Changed([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new(name));
    }
}
