using CryDuplicateFinder.Algorithms;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace CryDuplicateFinder
{
    public class FileEntry : INotifyPropertyChanged
    {
        object padlock = new();

        string path;
        int filesChecked = 0;
        int filesToCheck = 1;
        string resolution = null;
        CollectionViewSource dupsView;
        ObservableCollection<SimilarFileEntry> dups;

        public string Path { get => path; set { path = value; Changed(); } }
        public int FilesToCheck => filesToCheck;
        public int FilesChecked
        {
            get => filesChecked; set
            {
                filesChecked = value;
                Changed();
                Changed(nameof(Progress));
                Changed(nameof(ProgressColor));
                Changed(nameof(DuplicateCount));
                Changed(nameof(DuplicateColor));
            }
        }
        public string Progress => $"{((filesChecked / filesToCheck) * 100):0.00}%";
        public SolidColorBrush ProgressColor => filesChecked < filesToCheck ? Brushes.Red : Brushes.Green;
        public SolidColorBrush DuplicateColor => DuplicateCount == 0 ? Brushes.LightGray : Brushes.Black;

        public ObservableCollection<SimilarFileEntry> Duplicates { get => dups; set { dups = value; Changed(); InitializeView(); } }
        public CollectionViewSource DuplicatesView { get => dupsView; set { dupsView = value; Changed(); } }
        public int DuplicateCount => Duplicates?.Count ?? 0;
        public DateTime? StartedAnalysis { get; private set; }
        public DateTime? FinishedAnalysis { get; private set; }

        public int Width { get; set; }
        public int Height { get; set; }
        public string Resolution
        {
            get
            {
                if (resolution == null)
                {
                    if (Width == 0 || Height == 0)
                    {
                        _ = getResolution();
                        return "-";
                    }
                    else resolution = $"{Width}x{Height}";       
                }

                return resolution;
            }
            set
            {
                resolution = value;
                Changed();
            }
        }

        bool resRequest = false;
        TaskCompletionSource resolutionTask = new();
        public Task GetResolutionTask() => resolutionTask.Task;
        Task getResolution()
        {
            if (resRequest) return Task.CompletedTask;
            resRequest = true;

            return Task.Run(() =>
            {
                if (!File.Exists(Path)) Resolution = "Missing image";
                using (var mat = CvHelpers.OpenImage(Path))
                {
                    Width = mat.Width;
                    Height = mat.Height;
                    Resolution = $"{mat.Width}x{mat.Height}";
                    resolutionTask.SetResult();
                }
            });
        }

        public FileEntry() { }

        void InitializeView()
        {
            DuplicatesView = new();
            DuplicatesView.Source = Duplicates;
            DuplicatesView.SortDescriptions.Add(new SortDescription(nameof(SimilarFileEntry.similarity), ListSortDirection.Descending));
        }

        public Task CheckForDuplicates(IEnumerable<FileEntry> files, DuplicateCheckingMode mode, int maxThreads, CancellationToken token)
        {
            StartedAnalysis = DateTime.Now;
            filesToCheck = files.Count() - 1;

            // in case there are no files to check, just mark as completed
            if (filesToCheck == 0)
            {
                filesToCheck = 1;
                FilesChecked = 1;
                FinishedAnalysis = DateTime.Now;
                return Task.CompletedTask;
            }
            else FilesChecked = 0;

            if (Duplicates == null) Duplicates = new();

            var context = SynchronizationContext.Current;

            return Task.Run(() =>
            {
                var checker = GetDuplicateChecker(mode);
                checker.LoadImage(this);

                var minSim = checker.GetMinRequiredSimilarity();

                try
                {
                    Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, f =>
                    {
                        // ignore same file or file already part of duplicates
                        if (f == this || token.IsCancellationRequested) return;
                        if (Duplicates.Where(x => x.file == f).FirstOrDefault() != default)
                        {
                            context.Post(d =>
                            {
                                lock (padlock)
                                {
                                    if (filesChecked < FilesToCheck) filesChecked++;
                                }
                            }, null);
                            return;
                        }

                        bool isDuplicate = false;
                        double similarity = 0.0;
                        var sw = Stopwatch.StartNew();

                        try
                        {
                            // get similarity
                            similarity = checker.CalculateSimiliarityTo(f);
                            isDuplicate = similarity >= minSim;
                        }
                        catch (Exception ex)
                        {
                            // TODO: maybe log
                        }
                        finally
                        {
                            sw.Stop();

                            // then add to collection
                            context.Post(d =>
                            {
                                if (token.IsCancellationRequested) return;

                                if (isDuplicate) RegisterDuplicate(f, similarity, sw.Elapsed.TotalMilliseconds);
                                lock (padlock)
                                {
                                    if (filesChecked < FilesToCheck) filesChecked++;
                                }
                            }, null);
                        }
                    });
                }
                finally
                {
                    FilesChecked = FilesToCheck;
                    FinishedAnalysis = DateTime.Now;
                    checker.Dispose();
                }
            });
        }

        IDuplicateChecker GetDuplicateChecker(DuplicateCheckingMode mode) => mode switch
        {
            DuplicateCheckingMode.Histogram => new HistogramDuplicateChecker(),
            DuplicateCheckingMode.Features => new FeatureDuplicateChecker(),
            _ => throw new NotImplementedException()
        };

        void RegisterDuplicate(FileEntry f, double similarity, double elapsedMs)
        {
            Duplicates.Add(new(f, similarity, $"{(similarity * 100):0.00}%", elapsedMs));

            if (f.Duplicates == null) f.Duplicates = new();
            f.Duplicates.Add(new(this, similarity, $"{(similarity * 100):0.00}%", elapsedMs));
        }

        public override string ToString() => Path;

        public event PropertyChangedEventHandler PropertyChanged;
        void Changed([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new(name));

        public record SimilarFileEntry(FileEntry file, double similarity, string similarityText, double elapsedMs);
    }
}
