using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.IO.Ports;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ESP8266_Controller_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private PlayImage pi = new();
        private SerialPort sp;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BackButton.DataContext = pi;
            ForwardButton.DataContext = pi;
            StopButton.DataContext = pi;
            PathButton.DataContext = pi;
            PathTextBox.DataContext = pi;
            WidthComboBox.DataContext = pi;
            HeightComboBox.DataContext = pi;
            FpsComboBox.DataContext = pi;
            ColorModeComboBox.DataContext = pi;
        }
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            PlayButtonChange();
        }

        private void LimitInputNumber(object sender, TextCompositionEventArgs e)
        {
            Regex re = new("[^0-9]+");
            e.Handled = re.IsMatch(e.Text);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            pi.IsPlaying = false;
            pi.IsPuase = false;
            PlayButton.Content = "播放";
        }

        private void PlayButtonChange()
        {
            if (PlayButton.Content.Equals("播放"))
            {
                //if (string.IsNullOrEmpty(PathTextBox.Text))
                //{
                //    LogBox.AppendText("请选择图片路径" + Environment.NewLine);
                //    return;
                //}
                PlayButton.Content = "暂停";
                pi.IsPlaying = true;
                pi.IsPuase = false;
            }
            else
            {
                PlayButton.Content = "播放";
                pi.IsPuase = true;
            }
        }

        private void PathButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
            if (CommonFileDialogResult.Ok == dialog.ShowDialog())
            {
                PathTextBox.Text = dialog.FileName;
            }
        }

        private void FlushButton_Click(object sender, RoutedEventArgs e)
        {
            if (SerialPort.GetPortNames().Length == 0)
            {
                LogBox.AppendText("未检测到串口，请插入设备" + Environment.NewLine);
            }
            for (var i = 1; i < SerialPortComboBox.Items.Count; i++)
            {
                SerialPortComboBox.Items.Remove(SerialPortComboBox.Items[i]);
            }
            foreach (var portName in SerialPort.GetPortNames())
            {
                ComboBoxItem cbi = new ComboBoxItem {Content = portName};
                SerialPortComboBox.Items.Add(cbi);
            }
            SerialPortComboBox.SelectedIndex = 1;
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if ((SerialPortComboBox.SelectedItem as ComboBoxItem).Content.ToString().Equals("选择串口"))
            {
                LogBox.AppendText("请选择串口，再尝试链接"+Environment.NewLine);
                return;
            }
            if (ConnectButton.Content.ToString().Equals("断开"))
            {
                sp.Close();
                ConnectButton.Content = "连接";
                FlushButton.IsEnabled = true;
                return;
            }
            sp = new SerialPort((SerialPortComboBox.SelectedItem as ComboBoxItem).Content.ToString(), Convert.ToInt32((BaudRateComboBox.SelectedItem as ComboBoxItem).Content.ToString()));
            if (sp.IsOpen)
            {
                ConnectStatusTextBlock.Text = "已连接";
                FlushButton.IsEnabled = false;
                ConnectButton.Content = "断开";
                LogBox.AppendText("串口连接成功" + Environment.NewLine);
            }
            else
            {

                LogBox.AppendText("串口连接失败" + Environment.NewLine);
            }
        }
    }
}
