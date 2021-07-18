using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;

namespace ESP8266_Controller
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
        private MemoryMappedViewAccessor Accessor;
        private IEnumerable<XElement> aidaElements;
        private Task serialReceiveTask;
        private CancellationTokenSource receiveTaskTokenSource;
        private CancellationToken receiveTaskToken;
        private Task serialSendTask;
        private CancellationTokenSource sendTaskTokenSource;
        private CancellationToken sendTaskToken;
        private DispatcherTimer sendIndexTimer;
        private int sendIndex;
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
            if (AIDAQuery())
            {
                GetAidaInfo();
                AddCheckBox();
                SendInfoButton.IsEnabled = true;
            }
            LoadJsonConfig();
            AddSerialComboItem();
            if (SerialPort.GetPortNames().Length == 1)
            {
                LogOut("未检测到串口，请插入设备");
            }
            else
            {
                sp = new SerialPort((SerialPortComboBox.SelectedItem as ComboBoxItem).Content.ToString(), Convert.ToInt32((BaudRateComboBox.SelectedItem as ComboBoxItem).Content.ToString()))
                {
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReceivedBytesThreshold = 13,
                    RtsEnable = false,
                    DtrEnable = false
                };
            }
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
            LogBox.Text = "";
            if (SerialPort.GetPortNames().Length == 1)
            {
                LogOut("未检测到串口，请插入设备");
            }
            else
            {
                sp = new SerialPort((SerialPortComboBox.SelectedItem as ComboBoxItem).Content.ToString(), Convert.ToInt32((BaudRateComboBox.SelectedItem as ComboBoxItem).Content.ToString()))
                {
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReceivedBytesThreshold = 13,
                    RtsEnable = false,
                    DtrEnable = false
                };
            }
            AddSerialComboItem();
            if (AIDAQuery())
            {
                GetAidaInfo();
                AddCheckBox();
                SendInfoButton.IsEnabled = true;
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (((ComboBoxItem) SerialPortComboBox.SelectedItem).Content.ToString().Equals("选择串口"))
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
            sp.Open();
            if (sp.IsOpen)
            {
                ConnectStatusTextBlock.Text = "已连接";
                FlushButton.IsEnabled = false;
                BaudRateComboBox.IsEnabled = false;
                SerialPortComboBox.IsEnabled = false;
                SendInfoButton.IsEnabled = true;
                ConnectButton.Content = "断开";
                LogOut("串口连接成功");
                serialReceiveTask = new Task(() =>
                {
                    string cmd = sp.ReadLine();
                    SerialCommand(cmd);
                    Dispatcher.Invoke(delegate
                    {
                        LogOut(cmd);
                    });
                });
                serialReceiveTask.Start();
            }
            else
            {
                LogOut("串口连接失败");
            }
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            SaveJsonConfig();
        }

        public void LogOut(string log)
        {
            LogBox.AppendText(log+Environment.NewLine);
            //LogBox.ScrollToEnd();
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
                LogOut("请启动AIDA64并开启内存共享否则无法获取到硬件数据！");
                return false;
            }
        }
        private void GetAidaInfo()
        {
            StringBuilder tmp = new StringBuilder();
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
            try
            {
                XDocument xmldoc = XDocument.Parse(tmp.ToString());
                aidaElements = xmldoc.Element("AIDA").Elements();
            }
            catch
            {

            }
        }

        public List<HardInfoItem> QuerySelectedInfo()
        {
            List<HardInfoItem> hardInfoList = new List<HardInfoItem>();
            foreach (var child in HardInfoPanel.Children)
            {
                HardInfoItem hii = new HardInfoItem();
                foreach (var a in ((StackPanel)child).Children.OfType<CheckBox>())
                {
                    if ((bool)a.IsChecked)
                    {
                        hii.contStr = (string)a.Content;
                        foreach (var b in ((StackPanel)a.Parent).Children.OfType<TextBox>())
                        {
                            if (b.Name.Equals(a.Content + "textBox"))
                            {
                                hii.labelStr = b.Text;
                                break;
                            }
                            if (b.Name.Equals(a.Content + "ProportionTextBox"))
                            {
                                hii.proportionStr = b.Text;
                                break;
                            }
                        }
                        foreach (var c in ((StackPanel)a.Parent).Children.OfType<ComboBox>())
                        {
                            if (c.Name.Equals(a.Content + "comboBox"))
                            {
                                hii.unitStr = c.SelectedItem.ToString();
                                break;
                            }
                        }
                        hardInfoList.Add(hii);
                    }
                }
            }
            return hardInfoList;
        }
        private void AddCheckBox()
        {
            foreach (var xElement in aidaElements)
            {
                CheckBox cb = new CheckBox
                {
                    Content = xElement.Element("id")?.Value,
                    Width = HardInfoPanel.ActualWidth / 2.5
                };
                TextBox tb = new TextBox
                {
                    Text = xElement.Element("label")?.Value,
                    Width = HardInfoPanel.ActualWidth / 4,
                    Margin = new Thickness(5,0,5,0),
                    Name = xElement.Element("id")?.Value + "textBox",
                };
                TextBox proportionTextBox = new TextBox()
                {
                    Text = "1",
                    Name = xElement.Element("id")?.Value + "ProportionTextBox",
                    Width = HardInfoPanel.ActualWidth / 16,
                    Margin = new Thickness(5, 0, 5, 0),
                    TextAlignment = TextAlignment.Center,
                };
                ComboBox cob = new ComboBox
                {
                    IsEditable = true,
                    SelectedIndex = 0,
                    Width = HardInfoPanel.ActualWidth / 8
                };
                cob.Items.Add("%");
                cob.Items.Add("°C");
                cob.Items.Add("Ghz");
                cob.Items.Add("Mhz");
                cob.Items.Add("GB");
                cob.Items.Add("MB/s");
                cob.Items.Add("KB/s");
                cob.Name = xElement.Element("id")?.Value + "comboBox";
                StackPanel sp = new StackPanel
                {
                    Margin = new Thickness(5, 5, 0, 0),
                    Orientation = Orientation.Horizontal
                };
                sp.Children.Add(cb);
                sp.Children.Add(tb);
                sp.Children.Add(proportionTextBox);
                sp.Children.Add(cob);
                HardInfoPanel.Children.Add(sp);
            }
        }
        private void AddSerialComboItem()
        {
            string[] ports;
            for (var i = 1; i < SerialPortComboBox.Items.Count; i++)
            {
                SerialPortComboBox.Items.Remove(SerialPortComboBox.Items[i]);
            }
            try
            {
                ports = SerialPort.GetPortNames();
            }
            catch
            {
                return;
            }
            foreach (var portName in ports)
            {
                ComboBoxItem cbi = new ComboBoxItem { Content = portName };
                SerialPortComboBox.Items.Add(cbi);
            }
            SerialPortComboBox.SelectedIndex = 1;
        }
        private void LoadJsonConfig()
        {
            if (!File.Exists("config.json"))
                return;
            JObject jobj = JObject.Parse(File.ReadAllText("config.json"));
            foreach (var jtoken in jobj)
            {
                foreach (var child in HardInfoPanel.Children)
                {
                    JToken infoJToken = jobj[jtoken.Key];
                    foreach (var a in ((StackPanel) child).Children.OfType<CheckBox>())
                    {
                        if (a.Content.ToString().Equals(jtoken.Key))
                        {
                            a.IsChecked = (bool)infoJToken["checked"];
                            break;
                        }
                    }
                    foreach (var b in ((StackPanel)child).Children.OfType<TextBox>())
                    {
                        if (b.Name.Equals(jtoken.Key + "ProportionTextBox"))
                        {
                            b.Text = infoJToken["proportion"].ToString();
                            break;
                        }
                    }
                    foreach (var c in ((StackPanel)child).Children.OfType<ComboBox>())
                    {
                        if (c.Name.Equals(jtoken.Key + "comboBox"))
                        {
                            if (c.Items.IndexOf(infoJToken["unit"]) !=-1)
                            {
                                c.SelectedIndex = c.Items.IndexOf(infoJToken["unit"].ToString());
                            }
                            else
                            {
                                c.Text = infoJToken["unit"].ToString();
                            }
                            break;
                        }
                    }
                }
            }
        }
        //将硬件信息的选择保存进配置文件
        private void SaveJsonConfig()
        {
            JObject jobj = new JObject();

            foreach (var child in HardInfoPanel.Children)
            {
                HardInfoItem hii = new HardInfoItem();
                foreach (var a in ((StackPanel)child).Children.OfType<CheckBox>())
                {
                    if ((bool)a.IsChecked)
                    {
                        hii.contStr = (string)a.Content;
                        foreach (var b in ((StackPanel)a.Parent).Children.OfType<TextBox>())
                        {
                            if (b.Name.Equals(a.Content + "ProportionTextBox"))
                            {
                                hii.proportionStr = b.Text;
                                break;
                            }
                        }
                        foreach (var c in ((StackPanel)a.Parent).Children.OfType<ComboBox>())
                        {
                            if (c.Name.Equals(a.Content + "comboBox"))
                            {
                                hii.unitStr = c.Text;
                                break;
                            }
                        }
                        jobj.Add(new JProperty((string) a.Content,
                            new JObject(
                                new JProperty("checked", true),
                                new JProperty("proportion", hii.proportionStr),
                                new JProperty("unit",hii.unitStr)
                                )));
                    }
                }
            }
            File.WriteAllText("config.json", jobj.ToString());
        }

        public void SerialCommand(string inputCommand)
        {
            if (inputCommand.Equals("ESP_ONLINE"))
            {
                LogOut("成功链接8266");
            }
        }

        public string UnitConversion(string unitStr,string proportionStr, string value)
        {
            if (unitStr.Equals("Ghz"))
            {
                return Convert.ToDouble(value) / Convert.ToDouble(proportionStr) + " " + unitStr;
            }
            return Convert.ToDouble(value) / Convert.ToDouble(proportionStr) + " " + unitStr;
        }
        public JObject MakeHardInfo()
        {
            JObject infoJObject = new JObject();
            List<HardInfoItem> hardInfoList = QuerySelectedInfo();
            if (hardInfoList.Count == 0)
            {
                LogOut("请选择要发送的数据");
                return null;
            }
            foreach (var aidaElement in aidaElements)
            {
                if (sendIndex >= hardInfoList.Count)
                {
                    sendIndex = 0;
                }
                if (hardInfoList[sendIndex].contStr.Equals(aidaElement.Element("id")?.Value))
                {
                    infoJObject = new JObject(new JProperty("l", hardInfoList[sendIndex].labelStr),
                        new JProperty("v", UnitConversion(hardInfoList[sendIndex].unitStr, hardInfoList[sendIndex].proportionStr, aidaElement.Element("value")?.Value)));
                }
            }
            return infoJObject;
        }
        //定时增加发送的Index
        private void SendIndexTimer_Tick(object sender, EventArgs e)
        {
            sendIndex++;
        }

        private void SendInfoButton_Click(object sender, RoutedEventArgs e)
        {
            if (sp.IsOpen)
            {
                LogOut("请开启串口再发送");
                return;
            }

            if (SendInfoButton.Content.ToString().Equals("发送"))
            {
                sendTaskTokenSource = new CancellationTokenSource();
                sendTaskToken = sendTaskTokenSource.Token;
                sendIndexTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(10)
                };
                sendIndexTimer.Tick += SendIndexTimer_Tick;
                sendIndexTimer.Start();
                serialSendTask = new Task(() =>
                {
                    while (true)
                    {
                        if (sendTaskToken.IsCancellationRequested)
                        {
                            sendIndexTimer.Stop();
                            return;
                        }
                        int delayTime = 500;
                        GetAidaInfo();
                        Dispatcher.Invoke(delegate
                        {
                            LogOut(MakeHardInfo().ToString());
                        });
                        Dispatcher.Invoke(delegate
                        {
                            delayTime = Convert.ToInt32(SendInfoDealyBox.Text);
                        });
                        delayTime = delayTime >= 100 && delayTime <= 500?delayTime:500;
                        Task.Delay(delayTime);
                        //TODO:串口发送JSON数据给下位机
                    }
                }, sendTaskToken);
                serialSendTask.Start();
                SendInfoButton.Content = "停止";
            }
            else
            {
                sendTaskTokenSource.Cancel();
                GC.Collect();
                SendInfoButton.Content = "发送";
            }
        }
    }
}
