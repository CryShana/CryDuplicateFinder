using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

using static CryDuplicateFinder.DuplicateChecker;
using static CryDuplicateFinder.ViewModel;

namespace CryDuplicateFinder
{
    public class FileEntry : INotifyPropertyChanged
    {
        object padlock = new();

        string path;
        int filesChecked = 0;
        int filesToCheck = 1;
        ObservableCollection<FileEntry> dups = new();

        public string Path { get => path; set { path = value; Changed(); } }
        public int FilesChecked
        {
            get => filesChecked; set
            {
                filesChecked = value;
                Changed();
                Changed(nameof(Progress));
                Changed(nameof(ProgressColor));
            }
        }
        public string Progress => $"{((filesChecked / filesToCheck) * 100):0.00}%";
        public SolidColorBrush ProgressColor => filesChecked < filesToCheck ? Brushes.Red : Brushes.Green;
        public ObservableCollection<FileEntry> Duplicates { get => dups; set { dups = value; Changed(); } }

        public FileEntry()
        {

        }

        public Task CheckForDuplicates(IEnumerable<FileEntry> files, DuplicateCheckingMode mode)
        {
            filesToCheck = files.Count() - 1;
            FilesChecked = 0;

            var context = SynchronizationContext.Current;

            return Task.Run(async () =>
            {
                Parallel.ForEach(files, f =>
                {
                    if (f == this) return;

                    bool isDuplicate = false;

                    // TODO: do stuff

                    // then add to collection

                    context.Post(d =>
                    {
                        lock (padlock) FilesChecked++;
                        if (isDuplicate) Duplicates.Add(f);
                    }, null);
                });
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void Changed([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new(name));
    }
}
