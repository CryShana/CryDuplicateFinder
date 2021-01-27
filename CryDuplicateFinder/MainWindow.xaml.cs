using CryDuplicateFinder.Algorithms;

using FolderBrowserEx;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Controls;

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
            context = SynchronizationContext.Current;

            speedtimer.Elapsed += Speedtimer_Elapsed;
            speedtimer.Start();
        }

        void Speedtimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            const int lastFilesToCheck = 20;

            if (vm.IsBusy == false && vm.ProgressValue < vm.ProgressMax) vm.SpeedStatus = "Waiting to start";
            else if (vm.IsBusy == false && vm.Files.Count > 1 && vm.ProgressMax == vm.ProgressMax)
            {
                var first = vm.Files.First();
                var last = vm.Files.Last();
                var elapsed = (last.FinishedAnalysis - first.StartedAnalysis).Value.TotalMilliseconds;  
                vm.SpeedStatus = $"Finished (Elapsed: {getTimeText(elapsed)})";
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

                vm.SpeedStatus = $"[{getTimeText(averageFileCheckMs)}] ETA: {getTimeText(etaMs)}";
            }

            string getTimeText(double ms)
            {
                return ms switch
                {
                    < 1000 => Math.Round(ms).ToString() + "ms",
                    <= 60 * 1000 => (ms / 1000).ToString("0.0") + "sec",
                    _ => ((ms / 1000) / 60).ToString("0.0") + "min"
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

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            var val = comboMode.SelectedItem as ComboBoxItem;
            var mode = (DuplicateCheckingMode)Enum.Parse(typeof(DuplicateCheckingMode), (string)val.Content);

            _ = vm.Start(mode);
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var f = ((ListView)sender).SelectedItem as FileEntry;
            if (f == null) return;

            vm.SelectedFile = f;
        }


        DateTime lastMouseDown = DateTime.Now;
        FileEntry.SimilarFileEntry lastItem = null;
        private void StackPanel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
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
    }
}
