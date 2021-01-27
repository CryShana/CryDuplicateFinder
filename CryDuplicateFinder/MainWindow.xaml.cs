using CryDuplicateFinder.Algorithms;

using FolderBrowserEx;

using System;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Diagnostics;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace CryDuplicateFinder
{
    public partial class MainWindow : Window
    {
        ViewModel vm;
        Timer speedtimer = new(300);

        public MainWindow()
        {
            InitializeComponent();

            vm = DataContext as ViewModel;
            vm.SelectedFileChanged += Vm_SelectedFileChanged;

            speedtimer.Elapsed += Speedtimer_Elapsed;
            speedtimer.Start();
        }

        private void Vm_SelectedFileChanged(object sender, FileEntry e)
        {
            if (e == null || !File.Exists(e.Path))
            {
                selectedImage.Source = null;
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new(e.Path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                selectedImage.Source = bitmap;
            }
            catch
            {
                selectedImage.Source = null;
            }
        }

        void Speedtimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            const int lastFilesToCheck = 20;

            if (vm.IsBusy == false && vm.ProgressValue < vm.ProgressMax) vm.SpeedStatus = "Waiting to start";
            else if (vm.IsBusy == false && vm.Files.Count > 1 && vm.ProgressValue == vm.ProgressMax)
            {
                var first = vm.Files.First();
                var last = vm.Files.Last();
                var elapsed = (last.FinishedAnalysis - first.StartedAnalysis).Value.TotalMilliseconds;
                vm.SpeedStatus = $"Finished (Elapsed: {getTimeText(elapsed)})";
            }
            else if (vm.IsBusy && vm.Files.Count > 1 && vm.ProgressValue == 0)
            {
                // show progress on checked files
                var first = vm.Files.First();
                vm.SpeedStatus = $"Computing characteristics... ({first.FilesChecked}/{first.FilesToCheck})";
            }
            else
            {
                int filesChecked = 0;
                double averageFileCheckMs = 0.0;
                double etaMs = 0.0;

                // calculate based on results so far
                for (int i = vm.ProgressValue; i >= 0 && i >= vm.ProgressValue - lastFilesToCheck; i--)
                {
                    var f = vm.Files[i];
                    if (f.StartedAnalysis == null || f.FinishedAnalysis == null) continue;

                    filesChecked++;
                    var elapsed = (f.FinishedAnalysis - f.StartedAnalysis).Value.TotalMilliseconds;
                    averageFileCheckMs += elapsed;
                }
                if (filesChecked == 0) return;

                averageFileCheckMs /= filesChecked;

                // eta
                var remainingFiles = vm.ProgressMax - vm.ProgressValue;
                etaMs = averageFileCheckMs * remainingFiles;

                vm.SpeedStatus = $"[{getTimeText(averageFileCheckMs)}/image] ETA: {getTimeText(etaMs)}";
            }

            string getTimeText(double ms)
            {
                return ms switch
                {
                    < 1000 => Math.Round(ms).ToString() + "ms",
                    <= 60 * 1000 => (ms / 1000).ToString("0.0") + "sec",
                    <= 60 * 60 * 1000 => ((ms / 1000) / 60).ToString("0.0") + "min",
                    _ => (((ms / 1000) / 60) / 60).ToString("0.0") + "h"
                };
            }
        }

        void btnRootDir_Click(object sender, RoutedEventArgs e)
        {
            var browser = new FolderBrowserDialog();
            browser.Title = "Select root directory";
            browser.InitialFolder = @"C:\";
            browser.AllowMultiSelect = false;
            if (browser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                vm.RootDirectory = browser.SelectedFolder;
            }
        }

        void btnStart_Click(object sender, RoutedEventArgs e)
        {
            var val = comboMode.SelectedItem as ComboBoxItem;
            var mode = (DuplicateCheckingMode)Enum.Parse(typeof(DuplicateCheckingMode), (string)val.Content);

            _ = vm.Start(mode);
        }

        void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var f = ((ListView)sender).SelectedItem as FileEntry;
            if (f == null) return;

            vm.SelectedFile = f;
        }


        DateTime lastMouseDown = DateTime.Now;
        FileEntry.SimilarFileEntry lastItem = null;
        FileEntry lastFile = null;

        void StackPanel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;

            var now = DateTime.Now;
            var elapsed = (now - lastMouseDown).TotalMilliseconds;
            var item = (FileEntry.SimilarFileEntry)(sender as StackPanel).DataContext;

            // detecting mouse double click
            if (elapsed < 400 && lastItem == item)
            {
                var pinfo = new ProcessStartInfo
                {
                    FileName = item.file.Path,
                    UseShellExecute = true
                };
                Process.Start(pinfo);
            }

            lastItem = item;
            lastMouseDown = now;
        }
        void StackPanel_MouseDown_1(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;

            var now = DateTime.Now;
            var elapsed = (now - lastMouseDown).TotalMilliseconds;
            var file = (FileEntry)(sender as StackPanel).DataContext;

            // detecting mouse double click
            if (elapsed < 400 && lastFile == file)
            {
                var pinfo = new ProcessStartInfo
                {
                    FileName = file.Path,
                    UseShellExecute = true
                };
                Process.Start(pinfo);
            }

            lastFile = file;
            lastMouseDown = now;
        }

        void DeleteLocalSimilarImages(object sender, RoutedEventArgs e)
        {
            var selected = vm.SelectedFile;
            var minSimilarity = vm.MinSimilarity / 100.0;

            if (MessageBox.Show($"This will delete all similar images to '{Path.GetFileName(selected.Path)}' that have similarity above {vm.MinSimilarity}%\n\n" +
                $"Are you sure?", "Confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            ProcessLocalDuplicates(minSimilarity, selected, f =>
            {
                if (File.Exists(f.file.Path)) File.Delete(f.file.Path);
            });
        }

        void DeleteGlobalSimilarImages(object sender, RoutedEventArgs e)
        {
            var minSimilarity = vm.MinSimilarity / 100.0;

            if (MessageBox.Show($"This will delete all similar images to all files that have similarity above {vm.MinSimilarity}%. " +
                $"Files with higher resolution will be prioritized.\n\n" +
                $"Are you sure?", "Confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            vm.SelectedFile = null;

            // delete all images (go through all files, retain the highest resolution duplicate, delete others)
            ProcessGlobalDuplicates(minSimilarity, f =>
            {
                try
                {
                    if (File.Exists(f.Path)) File.Delete(f.Path);
                }
                catch (Exception ex)
                {
                    // TODO: log
                }
            });
        }

        void MoveGlobalSimilarImages(object sender, RoutedEventArgs e)
        {
            var minSimilarity = vm.MinSimilarity / 100.0;

            var browser = new FolderBrowserDialog();
            browser.Title = "Select root directory to move duplicates to";
            browser.InitialFolder = @"C:\";
            browser.AllowMultiSelect = false;
            if (browser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var newRoot = browser.SelectedFolder;

            vm.SelectedFile = null;

            // delete all images (go through all files, retain the highest resolution duplicate, delete others)
            ProcessGlobalDuplicates(minSimilarity, f =>
            {
                try
                {
                    var relativePath = Path.GetRelativePath(vm.RootDirectory, f.Path);
                    var newPath = Path.Join(newRoot, relativePath);

                    if (File.Exists(f.Path))
                    {
                        // make sure dir exists
                        var dir = Path.GetDirectoryName(newPath);
                        Directory.CreateDirectory(dir);

                        File.Move(f.Path, newPath, true);
                    }
                }
                catch (Exception ex)
                {
                    // TODO: log
                }
            });
        }

        void ProcessLocalDuplicates(double minSimilarity, FileEntry selected, Action<FileEntry.SimilarFileEntry> action)
        {
            var affected = new List<FileEntry.SimilarFileEntry>();
            foreach (var f in selected.Duplicates)
            {
                if (f.similarity < minSimilarity) continue;
                affected.Add(f);
            }

            foreach (var f in affected)
            {
                action(f);

                vm.Files.Remove(f.file);
                foreach (var ff in vm.Files)
                    if (ff.Duplicates.Remove(f))
                        ff.DuplicatesView.View.Refresh();

                vm.FilesView.View.Refresh();
            }
        }

        void ProcessGlobalDuplicates(double minSimilarity, Action<FileEntry> action)
        {
            var inAffected = new HashSet<FileEntry>();
            var affected = new List<FileEntry>();

            foreach (var f in vm.Files)
            {
                if (f.Duplicates.Count == 0) continue;
                if (inAffected.Contains(f)) continue;
                _ = f.Resolution;
                f.GetResolutionTask().Wait();

                var gottaCheck = new List<FileEntry.SimilarFileEntry>() { new(f, 1, "", 0) };
                foreach (var ff in f.Duplicates)
                {
                    if (ff.similarity < minSimilarity || inAffected.Contains(ff.file)) continue;

                    // now select the highest res file
                    _ = ff.file.Resolution;
                    ff.file.GetResolutionTask().Wait();

                    gottaCheck.Add(ff);
                }

                if (gottaCheck.Count > 0)
                {
                    // sort by highest resolution
                    gottaCheck.Sort((a, b) => (b.file.Width * b.file.Height).CompareTo((a.file.Width * a.file.Height)));

                    for (int i = 1; i < gottaCheck.Count; i++)
                    {
                        var fl = gottaCheck[i].file;
                        if (inAffected.Contains(fl)) continue;

                        affected.Add(fl);
                        inAffected.Add(fl);
                    }
                }
            }

            foreach (var f in affected)
            {
                action(f);
                RemoveFile(f);
            }
        }

        void RemoveFile(FileEntry f)
        {
            vm.Files.Remove(f);
            foreach (var ff in vm.Files)
                for (int i = 0; i < ff.Duplicates.Count; i++)
                {
                    if (ff.Duplicates[i].file == f)
                    {
                        ff.Duplicates.RemoveAt(i);
                        i--;
                    }
                    ff.DuplicatesView.View.Refresh();
                }

            vm.FilesView.View.Refresh();
        }

        void HideImagesWithoutDuplicates(object sender, RoutedEventArgs e) => vm.HideFilesWithoutDuplicates();

        void MenuItem_SelectFileEntry(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem)sender;

            FileEntry f = null;
            if (menuItem.DataContext is FileEntry ff) f = ff;
            else f = (menuItem.DataContext as FileEntry.SimilarFileEntry)?.file;
            if (f == null) return;

            vm.SelectedFile = f;
        }

        void MenuItem_DeleteFileEntry(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem)sender;

            FileEntry f = null;
            if (menuItem.DataContext is FileEntry ff) f = ff;
            else f = (menuItem.DataContext as FileEntry.SimilarFileEntry)?.file;
            if (f == null) return;

            if (File.Exists(f.Path)) File.Delete(f.Path);

            RemoveFile(f);
        }

        void MenuItem_ShowInExplorer(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem)sender;

            FileEntry f = null;
            if (menuItem.DataContext is FileEntry ff) f = ff;
            else f = (menuItem.DataContext as FileEntry.SimilarFileEntry)?.file;
            if (f == null) return;

            string args = string.Format("/e, /select, \"{0}\"", f.Path);
            var info = new ProcessStartInfo
            {
                FileName = "explorer",
                Arguments = args
            };
            Process.Start(info);
        }

        void MenuItem_ExcludeFromSimilar(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem)sender;

            FileEntry f = null;
            if (menuItem.DataContext is FileEntry ff) f = ff;
            else f = (menuItem.DataContext as FileEntry.SimilarFileEntry)?.file;
            if (f == null) return;

            RemoveFile(f);
        }
    }
}
