using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace DroneSimulator
{
    public partial class SerialPortOverviewWindow : Window, INotifyPropertyChanged
    {
        public List<SerialPortConfig> Configs => SerialPortManager.Configs;

        public SerialPortOverviewWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}