using CryDuplicateFinder.Algorithms;

using FolderBrowserEx;

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace CryDuplicateFinder
{
    public partial class MainWindow : Window
    {
        ViewModel vm;

        public MainWindow()
        {
            InitializeComponent();

            vm = DataContext as ViewModel;
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
