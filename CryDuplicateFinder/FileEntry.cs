using CryDuplicateFinder.Algorithms;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
        
        public ObservableCollection<SimilarFileEntry> Duplicates { get => dups; set { dups = value; Changed(); InitializeView();  } }
        public CollectionViewSource DuplicatesView { get => dupsView; set { dupsView = value; Changed(); } }
        public int DuplicateCount => Duplicates?.Count ?? 0;

        public FileEntry()
        {
            
        }

        void InitializeView()
        {
            DuplicatesView = new();
            DuplicatesView.Source = Duplicates;
            DuplicatesView.SortDescriptions.Add(new SortDescription(nameof(SimilarFileEntry.similarity), ListSortDirection.Descending));
        }

        public Task CheckForDuplicates(IEnumerable<FileEntry> files, DuplicateCheckingMode mode)
        {
            // minimum similarity value to consider image as possible duplicate 
            const double similarityThreshold = 0.61;

            filesToCheck = files.Count() - 1;

            // in case there are no files to check, just mark as completed
            if (filesToCheck == 0)
            {
                filesToCheck = 1;
                FilesChecked = 1;
                return Task.CompletedTask;
            }
            else FilesChecked = 0;

            if (Duplicates == null) Duplicates = new();

            var context = SynchronizationContext.Current;

            return Task.Run(() =>
            {
                var checker = GetDuplicateChecker(mode);
                checker.LoadImage(Path);

                try
                {
                    Parallel.ForEach(files, f =>
                    {
                        // ignore same file or file already part of duplicates
                        if (f == this) return;
                        if (Duplicates.Where(x => x.file == f).FirstOrDefault() != default)
                        {
                            context.Post(d =>
                            {
                                lock (padlock) FilesChecked++;
                            }, null);
                            return;
                        }

                        bool isDuplicate = false;

                        // get similarity
                        var similarity = checker.CalculateSimiliarityTo(f.Path);
                        isDuplicate = similarity >= similarityThreshold;

                        // then add to collection
                        context.Post(d =>
                        {      
                            if (isDuplicate)
                            {
                                Duplicates.Add(new(f, similarity, $"{(similarity * 100):0.00}%"));

                                if (f.Duplicates == null) f.Duplicates = new();
                                f.Duplicates.Add(new(this, similarity, $"{(similarity * 100):0.00}%"));
                            }

                            lock (padlock) FilesChecked++;
                        }, null);
                    });
                }
                finally
                {
                    checker.Dispose();
                }
            });
        }

        IDuplicateChecker GetDuplicateChecker(DuplicateCheckingMode mode) => mode switch
        {
            DuplicateCheckingMode.Fast => new HistogramDuplicateChecker(),
            DuplicateCheckingMode.Slow => new FeatureDuplicateChecker(),
            DuplicateCheckingMode.Slowest => new TemplateDuplicateChecker(),
            _ => throw new NotImplementedException()
        };

        public event PropertyChangedEventHandler PropertyChanged;
        void Changed([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new(name));

        public record SimilarFileEntry(FileEntry file, double similarity, string similarityText);
    }
}
