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
        System.Timers.Timer speedtimer = new(300);

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
            if (e == null)
            {
                selectedImage.Source = null;
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new(e.Path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            selectedImage.Source = bitmap;
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
        void StackPanel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
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

        void DeleteLocalSimilarImages(object sender, RoutedEventArgs e)
        {
            var selected = vm.SelectedFile;
            var minSimilarity = vm.MinSimilarity / 100.0;

            if (MessageBox.Show($"This will delete all similar images to '{Path.GetFileName(selected.Path)}' that have similarity above {vm.MinSimilarity}%\n\n" +
                $"Are you sure?", "Confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            // delete local images (duplicates for this selected file)
            var toDelete = new List<FileEntry.SimilarFileEntry>();
            foreach (var f in selected.Duplicates)
            {
                if (f.similarity < minSimilarity) continue;
                toDelete.Add(f);
            }

            foreach (var f in toDelete)
            {
                if (File.Exists(f.file.Path)) File.Delete(f.file.Path);

                vm.Files.Remove(f.file);
                selected.Duplicates.Remove(f);

                vm.FilesView.View.Refresh(); // CHECK: for some reason this view needs to be refreshed manually - but the SelectedFiles view does it automatically
            }
        }

        void DeleteGlobalSimilarImages(object sender, RoutedEventArgs e)
        {
            var minSimilarity = vm.MinSimilarity / 100.0;

            if (MessageBox.Show($"This will delete all similar images to all files that have similarity above {vm.MinSimilarity}%. " +
                $"Files with higher resolution will be prioritized.\n\n" +
                $"Are you sure?", "Confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            // TODO: delete global images (prioritize higher resolutions)
            /*var toDelete = new List<FileEntry.SimilarFileEntry>();
            foreach (var f in vm.Files)
            {
                if (f.similarity < minSimilarity) continue;
                toDelete.Add(f);
            }

            foreach (var f in toDelete)
            {
                if (File.Exists(f.file.Path)) File.Delete(f.file.Path);

                vm.Files.Remove(f.file);
                selected.Duplicates.Remove(f);

                vm.FilesView.View.Refresh();
            }*/
        }

        void HideImagesWithoutDuplicates(object sender, RoutedEventArgs e) => vm.HideFilesWithoutDuplicates();   
    }
}
