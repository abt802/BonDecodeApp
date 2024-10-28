using Microsoft.Win32;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;

using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Threading;
using System.IO;

namespace BonDecodeGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            WeakReferenceMessenger.Default.Register<MainWindow, ProcessMessage>(this, static (s, e) =>
            {
                var boxImage = e.IsSuccess ? MessageBoxImage.Information : MessageBoxImage.Error;
                s.Dispatcher.Invoke((Action)(() => 
                {
                    MessageBox.Show(s, e.Value, "Result", MessageBoxButton.OK, boxImage);
                }));
            });
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new()
            {
                Title = "Open Files",
                Filter = "mmts,ts(*.mmts;*.ts)|*.mmts;*.ts|mmts(*.mmts)|*.mmts|ts(*.ts)|*.ts|All Files(*.*)|*.*",
                RestoreDirectory = true,
                CheckFileExists = true,
                Multiselect = true,
            };
            if (ofd.ShowDialog() == true)
            {
                TargetsTextBox.Text = string.Join(Environment.NewLine, ofd.FileNames);
                TargetsTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            }
        }

        private void OpenDestinationButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new()
            {
                FileName = "Select Folder",
                Title = "Destination Folder",
                Filter = "Folder|.",
                RestoreDirectory = true,
                CheckFileExists = false,
            };
            if (ofd.ShowDialog() == true)
            {
                var path = Path.GetDirectoryName(ofd.FileName);
                if (path != null)
                {
                    DestinationFolderTextBox.Text = path;
                    DestinationFolderTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                }
            }
        }


        private void TargetsTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;

        }
        private void TargetsTextBox_Drop(object sender, DragEventArgs e)
        {
            var fileNames = e.Data.GetData(DataFormats.FileDrop, true) as string[];
            if (fileNames != null)
            {
                TargetsTextBox.Text = string.Join(Environment.NewLine, fileNames);
                TargetsTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            }
        }

        private void DestinationFolderTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DestinationFolderTextBox_Drop(object sender, DragEventArgs e)
        {
            var fileNames = e.Data.GetData(DataFormats.FileDrop, true) as string[];
            if (fileNames != null)
            {
                var folder = fileNames[0];
                if (Directory.Exists(folder))
                {
                    DestinationFolderTextBox.Text = fileNames[0];
                }
                else if (File.Exists(folder))
                {
                    DestinationFolderTextBox.Text = Path.GetDirectoryName(folder);
                }
                DestinationFolderTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            }
        }
    }
}