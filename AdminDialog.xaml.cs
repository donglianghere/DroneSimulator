using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;

namespace DroneSimulator
{
    public partial class AdminDialog : Window
    {
        private SerialPort? serialPort;

        public AdminDialog()
        {
            InitializeComponent();
            Loaded += AdminDialog_Loaded;
        }

        private void AdminDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // 检测可用串口
            PortComboBox.ItemsSource = SerialPort.GetPortNames();

            // 默认初始化
            int defaultBaudIndex = 0;
            int defaultParityIndex = 0;
            int defaultStopBitsIndex = 1;

            // 读取配置文件
            if (File.Exists("config_serialport.json"))
            {
                try
                {
                    var json = File.ReadAllText("config_serialport.json");
                    var config = JsonSerializer.Deserialize<SerialPortConfig>(json);

                    // 设置串口
                    if (!string.IsNullOrEmpty(config.PortName))
                    {
                        for (int i = 0; i < PortComboBox.Items.Count; i++)
                        {
                            if (PortComboBox.Items[i]?.ToString() == config.PortName)
                            {
                                PortComboBox.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                    else if (PortComboBox.Items.Count > 0)
                    {
                        PortComboBox.SelectedIndex = 0;
                    }

                    // 设置波特率
                    for (int i = 0; i < BaudRateComboBox.Items.Count; i++)
                    {
                        if (((ComboBoxItem)BaudRateComboBox.Items[i]).Content.ToString() == config.BaudRate.ToString())
                        {
                            BaudRateComboBox.SelectedIndex = i;
                            break;
                        }
                    }

                    // 设置奇偶校验
                    ParityComboBox.SelectedIndex = config.Parity == Parity.None ? 0 : 1;

                    // 设置停止位
                    if (config.StopBits == StopBits.None)
                        StopBitsComboBox.SelectedIndex = 0;
                    else if (config.StopBits == StopBits.One)
                        StopBitsComboBox.SelectedIndex = 1;
                    else if (config.StopBits == StopBits.Two)
                        StopBitsComboBox.SelectedIndex = 2;
                    else
                        StopBitsComboBox.SelectedIndex = 1;
                }
                catch
                {
                    // 配置文件异常时，使用默认值
                    if (PortComboBox.Items.Count > 0)
                        PortComboBox.SelectedIndex = 0;
                    BaudRateComboBox.SelectedIndex = defaultBaudIndex;
                    ParityComboBox.SelectedIndex = defaultParityIndex;
                    StopBitsComboBox.SelectedIndex = defaultStopBitsIndex;
                }
            }
            else
            {
                // 没有配置文件，使用默认值
                if (PortComboBox.Items.Count > 0)
                    PortComboBox.SelectedIndex = 0;
                BaudRateComboBox.SelectedIndex = defaultBaudIndex;
                ParityComboBox.SelectedIndex = defaultParityIndex;
                StopBitsComboBox.SelectedIndex = defaultStopBitsIndex;
            }
        }

        private void OpenPort_Click(object sender, RoutedEventArgs e)
        {
            if (PortComboBox.SelectedItem == null)
            {
                MessageBox.Show("请选择串口！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string portName = PortComboBox.SelectedItem.ToString()!;
            int baudRate = int.Parse(((ComboBoxItem)BaudRateComboBox.SelectedItem!).Content.ToString()!);
            Parity parity = Parity.None;
            if (((ComboBoxItem)ParityComboBox.SelectedItem!).Content.ToString() == "有")
                parity = Parity.Even;
            int stopBitsValue = int.Parse(((ComboBoxItem)StopBitsComboBox.SelectedItem!).Content.ToString()!);
            StopBits stopBits = stopBitsValue switch
            {
                0 => StopBits.None,
                1 => StopBits.One,
                2 => StopBits.Two,
                _ => StopBits.One
            };

            try
            {
                serialPort = new SerialPort(portName, baudRate, parity, 8, stopBits);
                serialPort.Open();
                MessageBox.Show("串口打开成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                serialPort.Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"串口打开失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            var config = new SerialPortConfig
            {
                PortName = PortComboBox.Text,
                BaudRate = int.Parse(BaudRateComboBox.Text),
                Parity = ParityComboBox.SelectedIndex == 0 ? System.IO.Ports.Parity.None : System.IO.Ports.Parity.Odd,
                StopBits = (StopBitsComboBox.Text == "1") ? System.IO.Ports.StopBits.One :
                           (StopBitsComboBox.Text == "2") ? System.IO.Ports.StopBits.Two : System.IO.Ports.StopBits.None
            };

            string json = JsonSerializer.Serialize(config);
            File.WriteAllText("config_serialport.json", json);
            MessageBox.Show("串口参数已保存。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}