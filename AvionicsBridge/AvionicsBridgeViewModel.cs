using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Threading;

using Microsoft.FlightSimulator.SimConnect;

namespace AvionicsBridge
{
    public class AvionicsBridgeViewModel : BaseViewModel, IBaseSimConnectWrapper
    {
        #region IBaseSimConnectWrapper implementation

        /// User-defined win32 event
        public const int WM_USER_SIMCONNECT = 0x0402;

        /// Window handle
        private IntPtr _windowHandle = new IntPtr(0);

        /// SimConnect object
        private SimConnect _simConnect = null;

        public bool Connected
        {
            get { return _connected; }
            private set { this.SetProperty(ref _connected, value); }
        }
        private bool _connected = false;

        public int GetUserSimConnectWinEvent()
        {
            return WM_USER_SIMCONNECT;
        }

        public void ReceiveSimConnectMessage()
        {
            _simConnect?.ReceiveMessage();
        }

        public void SetWindowHandle(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        public void Disconnect()
        {
            Console.WriteLine("Disconnect");

            _timer.Stop();
            OddTick = false;

            if (_simConnect != null)
            {
                /// Dispose serves the same purpose as SimConnect_Close()
                _simConnect.Dispose();
                _simConnect = null;
            }

            ConnectButtonLabel = "Connect";
            Connected = false;

            // Set all requests as pending
            SimVarsViewModel.ResetAllRequests();
        }

        #endregion

        #region UI bindings

        public string ConnectButtonLabel
        {
            get { return _connectButtonLabel; }
            private set { this.SetProperty(ref _connectButtonLabel, value); }
        }
        private string _connectButtonLabel = "Connect";

        public string BroadcastButtonLabel
        {
            get { return _broadcastButtonLabel; }
            private set { this.SetProperty(ref _broadcastButtonLabel, value); }
        }
        private string _broadcastButtonLabel = "Start Broadcast";

        public string Port
        {
            get { return _port; }
            set { this.SetProperty(ref _port, value); }
        }
        private string _port = "11000";

        public bool OddTick
        {
            get { return _oddTick; }
            set { this.SetProperty(ref _oddTick, value); }
        }
        private bool _oddTick = false;

        public ObservableCollection<string> ErrorMessages { get; private set; }

        public BaseCommand ToggleConnectCommand { get; private set; }
        public BaseCommand ToggleBroadcastCommand { get; private set; }

        public SimVarsViewModel SimVarsViewModel { get; private set; }

        #endregion

        #region Real time

        private DispatcherTimer _timer = new DispatcherTimer();

        #endregion

        private UdpClient m_oUdpClient;
        private int m_iPort;
        private Socket socket;
        private IPEndPoint endpoint;
        
        public AvionicsBridgeViewModel()
        {
            ErrorMessages = new ObservableCollection<string>();

            ToggleConnectCommand = new BaseCommand((p) => { ToggleConnect(); });
            ToggleBroadcastCommand = new BaseCommand((p) => { ToggleBroadcast(); });

            _timer.Interval = new TimeSpan(0, 0, 0, 1, 0);
            _timer.Tick += new EventHandler(OnTick);

            SimVarsViewModel = new SimVarsViewModel();
        }

        private void Connect()
        {
            Console.WriteLine("Connect");

            try
            {
                /// The constructor is similar to SimConnect_Open in the native API
                _simConnect = new SimConnect("Simconnect - Simvar test", _windowHandle, WM_USER_SIMCONNECT, null, 0);
                SimVarsViewModel.SimConnect = _simConnect;

                /// Listen to connect and quit msgs
                _simConnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(SimConnect_OnRecvOpen);
                _simConnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(SimConnect_OnRecvQuit);

                /// Listen to exceptions
                _simConnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(SimConnect_OnRecvException);

                /// Catch a simobject data request
                _simConnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(SimConnect_OnRecvSimobjectDataBytype);
            }
            catch (COMException ex)
            {
                Console.WriteLine("Connection to KH failed: " + ex.Message);
            }
        }

        private void SimConnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Console.WriteLine("SimConnect_OnRecvOpen");
            Console.WriteLine("Connected to KH");

            ConnectButtonLabel = "Disconnect";
            Connected = true;

            SimVarsViewModel.RegisterAllPendingRequests();

            _timer.Start();
            OddTick = false;
        }

        /// The case where the user closes game
        private void SimConnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Console.WriteLine("SimConnect_OnRecvQuit");
            Console.WriteLine("KH has exited");

            Disconnect();
        }

        private void SimConnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            SIMCONNECT_EXCEPTION exception = (SIMCONNECT_EXCEPTION)data.dwException;
            Console.WriteLine("SimConnect_OnRecvException: " + exception.ToString());

            ErrorMessages.Add("SimConnect : " + exception.ToString());
        }

        private void SimConnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            Console.WriteLine("SimConnect_OnRecvSimobjectDataBytype");

            SimVarsViewModel.HandleReceivedData(data);
        }

        // May not be the best way to achive regular requests.
        // See SimConnect.RequestDataOnSimObject
        private void OnTick(object sender, EventArgs e)
        {
            Console.WriteLine("OnTick");

            OddTick = !OddTick;

            SimVarsViewModel.RequestAllIfNotPending();

            // broadcast current data via UDP
            if (socket != null)
            {
                byte[] data = new byte[48];
                var now = BitConverter.GetBytes(DateTime.Now.Ticks);
                Buffer.BlockCopy(now, 0, data, 0, 8);
                var latitude = BitConverter.GetBytes(SimVarsViewModel.LatitudeSimvarRequest.Value);
                Buffer.BlockCopy(latitude, 0, data, 8, 8);
                var longitude = BitConverter.GetBytes(SimVarsViewModel.LongitudeSimvarRequest.Value);
                Buffer.BlockCopy(longitude, 0, data, 16, 8);
                var speed = BitConverter.GetBytes(SimVarsViewModel.GroundSpeedSimvarRequest.Value);
                Buffer.BlockCopy(speed, 0, data, 24, 8);
                var heading = BitConverter.GetBytes(SimVarsViewModel.TrueHeadingSimvarRequest.Value);
                Buffer.BlockCopy(heading, 0, data, 32, 8);
                var track = BitConverter.GetBytes(SimVarsViewModel.TrueTrackSimvarRequest.Value);
                Buffer.BlockCopy(track, 0, data, 40, 8);

                socket.Send(data);
            }
        }

        private void ToggleConnect()
        {
            if (_simConnect == null)
            {
                try
                {
                    Connect();
                }
                catch (COMException ex)
                {
                    Console.WriteLine("Unable to connect to KH: " + ex.Message);
                }
            }
            else
            {
                Disconnect();
            }
        }

        private void ToggleBroadcast()
        {
            if (socket == null)
            {
                int port;
                if (int.TryParse(_port, out port))
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.Connect(IPAddress.Parse("192.168.1.41"), port);
                    BroadcastButtonLabel = "Stop Broadcast";
                }
            }
            else
            {
                socket.Close();
                socket = null;
                BroadcastButtonLabel = "Start Broadcast";
            }
        }

        public void SetTickSliderValue(int value)
        {
            _timer.Interval = new TimeSpan(0, 0, 0, 0, (int)(value));
        }
    }
}
