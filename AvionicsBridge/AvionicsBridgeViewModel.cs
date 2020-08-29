using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Threading;

using Microsoft.FlightSimulator.SimConnect;

namespace AvionicsBridge
{
    public enum DEFINITION
    {
        Dummy = 0
    };

    public enum REQUEST
    {
        Dummy = 0
    };

    public class SimvarRequest : ObservableObject
    {
        public DEFINITION Definition = DEFINITION.Dummy;
        public REQUEST Request = REQUEST.Dummy;

        public string Name { get; set; }

        public double Value
        {
            get { return _value; }
            set { this.SetProperty(ref _value, value); }
        }
        private double _value = 0.0;

        public string Units { get; set; }

        public bool Pending = true;
        public bool StillPending
        {
            get { return _stillPending; }
            set { this.SetProperty(ref _stillPending, value); }
        }
        private bool _stillPending = false;
    };

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

        private uint _currentDefinition = 0;
        private uint _currentRequest = 0;

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
            LatitudeSimvarRequest.Pending = true;
            LatitudeSimvarRequest.StillPending = true;
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

        public bool OddTick
        {
            get { return _oddTick; }
            set { this.SetProperty(ref _oddTick, value); }
        }
        private bool _oddTick = false;

        public SimvarRequest LatitudeSimvarRequest
        {
            get { return _latitudeSimvarRequest; }
            set { this.SetProperty(ref _latitudeSimvarRequest, value); }
        }
        private SimvarRequest _latitudeSimvarRequest = null;

        public SimvarRequest LongitudeSimvarRequest
        {
            get { return _longitudeSimvarRequest; }
            set { this.SetProperty(ref _longitudeSimvarRequest, value); }
        }
        private SimvarRequest _longitudeSimvarRequest = null;

        public SimvarRequest GroundSpeedSimvarRequest
        {
            get { return _groundSpeedSimvarRequest; }
            set { this.SetProperty(ref _groundSpeedSimvarRequest, value); }
        }
        private SimvarRequest _groundSpeedSimvarRequest = null;

        public SimvarRequest TrueHeadingSimvarRequest
        {
            get { return _trueHeadingSimvarRequest; }
            set { this.SetProperty(ref _trueHeadingSimvarRequest, value); }
        }
        private SimvarRequest _trueHeadingSimvarRequest = null;

        public SimvarRequest TrueTrackSimvarRequest
        {
            get { return _trueTrackSimvarRequest; }
            set { this.SetProperty(ref _trueTrackSimvarRequest, value); }
        }
        private SimvarRequest _trueTrackSimvarRequest = null;

        public ObservableCollection<string> ErrorMessages { get; private set; }

        public BaseCommand ToggleConnectCommand { get; private set; }
        public BaseCommand ToggleBroadcastCommand { get; private set; }

        public ConnectionSettingsViewModel ConnectionSettingsViewModel { get; private set; }

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

            SetupRequests();

            this.ConnectionSettingsViewModel = new ConnectionSettingsViewModel();
        }

        void SetupRequests()
        {
            _latitudeSimvarRequest = SetupRequest("GPS POSITION LAT", "degrees");
            _longitudeSimvarRequest = SetupRequest("GPS POSITION LON", "degrees");
            _groundSpeedSimvarRequest = SetupRequest("GPS GROUND SPEED", "meter/second");
            _trueHeadingSimvarRequest = SetupRequest("GPS GROUND TRUE HEADING", "radians");
            _trueTrackSimvarRequest = SetupRequest("GPS GROUND TRUE TRACK", "radians");
        }

        SimvarRequest SetupRequest(string requestName, string requestUnit)
        {
            SimvarRequest simvarRequest = new SimvarRequest
            {
                Definition = (DEFINITION)_currentDefinition,
                Request = (REQUEST)_currentRequest,
                Name = requestName,
                Units = requestUnit
            };

            simvarRequest.Pending = !RegisterToSimConnect(simvarRequest);
            simvarRequest.StillPending = simvarRequest.Pending;

            ++_currentDefinition;
            ++_currentRequest;

            return simvarRequest;
        }

        private void Connect()
        {
            Console.WriteLine("Connect");

            try
            {
                /// The constructor is similar to SimConnect_Open in the native API
                _simConnect = new SimConnect("Simconnect - Simvar test", _windowHandle, WM_USER_SIMCONNECT, null, 0);

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

        void RegisterIfPending(SimvarRequest simvar)
        {
            if (simvar.Pending)
            {
                simvar.Pending = !RegisterToSimConnect(simvar);
                simvar.StillPending = simvar.Pending;
            }
        }

        private void SimConnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Console.WriteLine("SimConnect_OnRecvOpen");
            Console.WriteLine("Connected to KH");

            ConnectButtonLabel = "Disconnect";
            Connected = true;

            // Register pending requests
            RegisterIfPending(_latitudeSimvarRequest);
            RegisterIfPending(_longitudeSimvarRequest);
            RegisterIfPending(_groundSpeedSimvarRequest);
            RegisterIfPending(_trueHeadingSimvarRequest);
            RegisterIfPending(_trueTrackSimvarRequest);

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

        void HandleRequest(SimvarRequest simvar, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            if (data.dwRequestID == (uint)simvar.Request)
            {
                double value = (double)data.dwData[0];
                simvar.Value = value;
                simvar.Pending = false;
                simvar.StillPending = false;
            }
        }

        private void SimConnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            Console.WriteLine("SimConnect_OnRecvSimobjectDataBytype");

            HandleRequest(_latitudeSimvarRequest, data);
            HandleRequest(_longitudeSimvarRequest, data);
            HandleRequest(_groundSpeedSimvarRequest, data);
            HandleRequest(_trueHeadingSimvarRequest, data);
            HandleRequest(_trueTrackSimvarRequest, data);
        }

        void RequestIfNotPending(SimvarRequest simvar)
        {
            if (!simvar.Pending)
            {
                _simConnect?.RequestDataOnSimObjectType(simvar.Request, simvar.Definition, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                simvar.Pending = true;
            }
            else
            {
                simvar.StillPending = true;
            }
        }

        // May not be the best way to achive regular requests.
        // See SimConnect.RequestDataOnSimObject
        private void OnTick(object sender, EventArgs e)
        {
            Console.WriteLine("OnTick");

            OddTick = !OddTick;

            RequestIfNotPending(_latitudeSimvarRequest);
            RequestIfNotPending(_longitudeSimvarRequest);
            RequestIfNotPending(_groundSpeedSimvarRequest);
            RequestIfNotPending(_trueHeadingSimvarRequest);
            RequestIfNotPending(_trueTrackSimvarRequest);

            // broadcast current data via UDP
            if (socket != null)
            {
                byte[] data = new byte[48];
                var now = BitConverter.GetBytes(DateTime.Now.Ticks);
                Buffer.BlockCopy(now, 0, data, 0, 8);
                var latitude = BitConverter.GetBytes(_latitudeSimvarRequest.Value);
                Buffer.BlockCopy(latitude, 0, data, 8, 8);
                var longitude = BitConverter.GetBytes(_longitudeSimvarRequest.Value);
                Buffer.BlockCopy(longitude, 0, data, 16, 8);
                var speed = BitConverter.GetBytes(_groundSpeedSimvarRequest.Value);
                Buffer.BlockCopy(speed, 0, data, 24, 8);
                var heading = BitConverter.GetBytes(_trueHeadingSimvarRequest.Value);
                Buffer.BlockCopy(heading, 0, data, 32, 8);
                var track = BitConverter.GetBytes(_trueTrackSimvarRequest.Value);
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
                var maybeSettings = ConnectionSettingsViewModel.GetConnectionSettings();
                if (maybeSettings.HasValue)
                {
                    var settings = maybeSettings.Value;
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.DontFragment = true;
                    if (settings.ConnectionType != ConnectionType.Broadcast)
                    {
                        socket.Connect(settings.IPAddress, settings.Port);
                    }
                    else
                    {
                        socket.EnableBroadcast = true;
                        socket.MulticastLoopback = false;
                        socket.Connect("255.255.255.255", settings.Port);
                    }
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

        private bool RegisterToSimConnect(SimvarRequest simvarRequest)
        {
            if (_simConnect != null)
            {
                /// Define a data structure
                _simConnect.AddToDataDefinition(simvarRequest.Definition, simvarRequest.Name, simvarRequest.Units, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                /// IMPORTANT: Register it with the simconnect managed wrapper marshaller
                /// If you skip this step, you will only receive a uint in the .dwData field.
                _simConnect.RegisterDataDefineStruct<double>(simvarRequest.Definition);

                return true;
            }
            else
            {
                return false;
            }
        }

        public void SetTickSliderValue(int value)
        {
            _timer.Interval = new TimeSpan(0, 0, 0, 0, (int)(value));
        }
    }
}
