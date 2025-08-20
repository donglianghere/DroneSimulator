using RJCP.IO.Ports;

namespace DroneSimulator
{
    public class SerialPortConfig
    {
        public string PortName { get; set; } = "";
        public int BaudRate { get; set; } = 9600;
        public RJCP.IO.Ports.Parity Parity { get; set; } = RJCP.IO.Ports.Parity.None;
        public RJCP.IO.Ports.StopBits StopBits { get; set; } = RJCP.IO.Ports.StopBits.One;
    }
}