using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AvionicsBridge
{
    public enum ConnectionType
    {
        Broadcast,
        Unicast
    }

    public struct ConnectionSettings
    {
        public ConnectionType ConnectionType { get; internal set; }
        public IPAddress IPAddress { get; internal set; }
        public UInt16 Port { get; internal set; }
    }
    
    public class ConnectionSettingsViewModel : BaseViewModel
    {
        public IEnumerable<ConnectionType> ConnectionTypes
        {
            get
            {
                return Enum.GetValues(typeof(ConnectionType)).Cast<ConnectionType>();
            }
        }

        public ConnectionType SelectedConnectionType 
        {
            get { return _selectedConnectionType; }
            set
            {
                this.SetProperty(ref _selectedConnectionType, value);
                this.OnPropertyChanged("IpVisibility");
            }
        }
        private ConnectionType _selectedConnectionType = ConnectionType.Broadcast;

        public string Port
        {
            get { return _port; }
            set
            {
                this.SetProperty(ref _port, value);
            }
        }
        private string _port = "11000";

        public string IP
        {
            get { return _ip; }
            set
            {
                this.SetProperty(ref _ip, value);
            }
        }
        private string _ip;

        public Visibility IpVisibility
        {
            get
            {
                return SelectedConnectionType != ConnectionType.Broadcast ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public ConnectionSettings? GetConnectionSettings()
        {
            try
            {
                return new ConnectionSettings
                {
                    ConnectionType = SelectedConnectionType,
                    IPAddress = SelectedConnectionType != ConnectionType.Broadcast ? IPAddress.Parse(IP) : IPAddress.Any,
                    Port = UInt16.Parse(Port)
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
