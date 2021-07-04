using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Ports;
using System.Text;
using System.Windows.Controls;
using System.Xml.Linq;

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
        private PlayImage pi = new PlayImage();
        private SerialPort sp;
        MemoryMappedViewAccessor Accessor;
        public List<string> id = new List<string>();
        public List<string> value = new List<string>();
        public List<string> hddId = new List<string>();
        public List<string> hddValue = new List<string>();
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
            AIDAQuery();

        }
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            PlayButtonChange();
        }

        private void LimitInputNumber(object sender, TextCompositionEventArgs e)
        {
            Regex re = new Regex("[^0-9]+");
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
                if (string.IsNullOrEmpty(PathTextBox.Text))
                {
                    LogOut("请选择图片路径");
                    return;
                }
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
                LogOut("未检测到串口，请插入设备");
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
                LogOut("请选择串口，再尝试链接");
                return;
            }
            if (ConnectButton.Content.ToString().Equals("断开"))
            {
                sp.Close();
                ConnectButton.Content = "连接";
                FlushButton.IsEnabled = true;
                BaudRateComboBox.IsEnabled = true;
                SerialPortComboBox.IsEnabled = true;
                return;
            }
            sp = new SerialPort((SerialPortComboBox.SelectedItem as ComboBoxItem).Content.ToString(), Convert.ToInt32((BaudRateComboBox.SelectedItem as ComboBoxItem).Content.ToString()));
            sp.DataBits = 8;
            sp.Parity = Parity.None;
            sp.StopBits = StopBits.One;
            sp.Handshake = Handshake.None;
            sp.ReceivedBytesThreshold = 13;
            sp.RtsEnable = false;
            sp.DtrEnable = false;
            sp.Open();
            if (sp.IsOpen)
            {
                ConnectStatusTextBlock.Text = "已连接";
                FlushButton.IsEnabled = false;
                BaudRateComboBox.IsEnabled = false;
                SerialPortComboBox.IsEnabled = false;
                ConnectButton.Content = "断开";
                LogOut("串口连接成功");
            }
            else
            {
                LogOut("串口连接失败");
            }
        }

        public void LogOut(string log)
        {
            LogBox.AppendText(log+Environment.NewLine);
            LogBox.ScrollToEnd();
        }
        private bool AIDAQuery()
        {
            try
            {
                Accessor = MemoryMappedFile.OpenExisting("AIDA64_SensorValues").CreateViewAccessor();
                return true;
            }
            catch
            {
                LogOut("请启动AIDA64否则无法获取到硬件数据！");
                return false;
            }
        }
        public void GetAidaInfo()
        {
            StringBuilder tmp = new StringBuilder();
            try
            {
                MemoryStream ms = new MemoryStream();
                for (int i = 0; i < Accessor.Capacity; i++)
                {
                    byte c = Accessor.ReadByte(i);
                    if (c == '\0')
                        break;
                    ms.WriteByte(c);
                }
                tmp.Append("<AIDA>");
                tmp.Append(Encoding.Default.GetString(ms.ToArray()));
                tmp.Append("</AIDA>");
                XDocument xmldoc = XDocument.Parse(tmp.ToString());
                IEnumerable<XElement> sysEnumerator = xmldoc.Element("AIDA").Elements("sys");
                InsertInfo(sysEnumerator);
                IEnumerable<XElement> tempEnumerator = xmldoc.Element("AIDA").Elements("temp");
                InsertInfo(tempEnumerator);
                IEnumerable<XElement> fanEnumerator = xmldoc.Element("AIDA").Elements("fan");
                InsertInfo(fanEnumerator);
                IEnumerable<XElement> voltEnumerator = xmldoc.Element("AIDA").Elements("volt");
                InsertInfo(voltEnumerator);
                IEnumerable<XElement> pwrEnumerator = xmldoc.Element("AIDA").Elements("pwr");
                InsertInfo(pwrEnumerator);
            }
            catch (Exception)
            {
                return;
            }
        }
        public void InsertInfo(IEnumerable<XElement> xel)
        {
            foreach (var element in xel)
            {
                for (int i = 1; i < 11; i++)
                {
                    if (element.Element("id").Value == "THDD" + i)
                    {
                        hddId.Add(element.Element("id").Value);
                        hddValue.Add(element.Element("value").Value);
                    }
                }
                switch (element.Element("id").Value)
                {
                    case "SCPUCLK": //CPU频率
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "SCPUUTI": //CPU使用率
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "SMEMUTI": //内存使用率
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "SGPU1CLK": //GPU频率
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "SGPU1UTI": //GPU使用率
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "SVMEMUSAGE": //显存使用率
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "TMOBO": //主板温度
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "TCPU": //CPU温度
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "TGPU1DIO": //GPU温度
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "FCPU": //CPU风扇转速
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "FGPU1": //GPU风扇转速
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "VCPU": //CPU电压
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "VGPU1": //GPU电压
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "PCPUPKG": //CPU Package功耗
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    case "PGPU1TDPP": //GPU TDP
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                    /*  备用代码   */
                    /*
                    case "":
                        id.Add(element.Element("id").Value);
                        value.Add(element.Element("value").Value);
                        break;
                     */
                }
            }
        }
    }
}
