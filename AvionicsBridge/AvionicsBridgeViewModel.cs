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
        public DEFINITION eDef = DEFINITION.Dummy;
        public REQUEST eRequest = REQUEST.Dummy;

        public string sName { get; set; }

        public double dValue
        {
            get { return m_dValue; }
            set { this.SetProperty(ref m_dValue, value); }
        }
        private double m_dValue = 0.0;

        public string sUnits { get; set; }

        public bool bPending = true;
        public bool bStillPending
        {
            get { return m_bStillPending; }
            set { this.SetProperty(ref m_bStillPending, value); }
        }
        private bool m_bStillPending = false;
    };

    public class AvionicsBridgeViewModel : BaseViewModel, IBaseSimConnectWrapper
    {
        #region IBaseSimConnectWrapper implementation

        /// User-defined win32 event
        public const int WM_USER_SIMCONNECT = 0x0402;

        /// Window handle
        private IntPtr m_hWnd = new IntPtr(0);

        /// SimConnect object
        private SimConnect m_oSimConnect = null;

        public bool bConnected
        {
            get { return m_bConnected; }
            private set { this.SetProperty(ref m_bConnected, value); }
        }
        private bool m_bConnected = false;

        private uint m_iCurrentDefinition = 0;
        private uint m_iCurrentRequest = 0;

        public int GetUserSimConnectWinEvent()
        {
            return WM_USER_SIMCONNECT;
        }

        public void ReceiveSimConnectMessage()
        {
            m_oSimConnect?.ReceiveMessage();
        }

        public void SetWindowHandle(IntPtr _hWnd)
        {
            m_hWnd = _hWnd;
        }

        public void Disconnect()
        {
            Console.WriteLine("Disconnect");

            m_oTimer.Stop();
            bOddTick = false;

            if (m_oSimConnect != null)
            {
                /// Dispose serves the same purpose as SimConnect_Close()
                m_oSimConnect.Dispose();
                m_oSimConnect = null;
            }

            sConnectButtonLabel = "Connect";
            bConnected = false;

            // Set all requests as pending
            LatitudeSimvarRequest.bPending = true;
            LatitudeSimvarRequest.bStillPending = true;
        }

        #endregion

        #region UI bindings

        public string sConnectButtonLabel
        {
            get { return m_sConnectButtonLabel; }
            private set { this.SetProperty(ref m_sConnectButtonLabel, value); }
        }
        private string m_sConnectButtonLabel = "Connect";

        public string sBroadcastButtonLabel
        {
            get { return m_sBroadcastButtonLabel; }
            private set { this.SetProperty(ref m_sBroadcastButtonLabel, value); }
        }
        private string m_sBroadcastButtonLabel = "Start Broadcast";

        public string sPort
        {
            get { return m_sPort; }
            set { this.SetProperty(ref m_sPort, value); }
        }
        private string m_sPort = "11000";

        public bool bOddTick
        {
            get { return m_bOddTick; }
            set { this.SetProperty(ref m_bOddTick, value); }
        }
        private bool m_bOddTick = false;

        public SimvarRequest LatitudeSimvarRequest
        {
            get { return m_LatitudeSimvarRequest; }
            set { this.SetProperty(ref m_LatitudeSimvarRequest, value); }
        }
        private SimvarRequest m_LatitudeSimvarRequest = null;

        public SimvarRequest LongitudeSimvarRequest
        {
            get { return m_LongitudeSimvarRequest; }
            set { this.SetProperty(ref m_LongitudeSimvarRequest, value); }
        }
        private SimvarRequest m_LongitudeSimvarRequest = null;

        public SimvarRequest GroundSpeedSimvarRequest
        {
            get { return m_GroundSpeedSimvarRequest; }
            set { this.SetProperty(ref m_GroundSpeedSimvarRequest, value); }
        }
        private SimvarRequest m_GroundSpeedSimvarRequest = null;

        public SimvarRequest TrueHeadingSimvarRequest
        {
            get { return m_TrueHeadingSimvarRequest; }
            set { this.SetProperty(ref m_TrueHeadingSimvarRequest, value); }
        }
        private SimvarRequest m_TrueHeadingSimvarRequest = null;

        public SimvarRequest TrueTrackSimvarRequest
        {
            get { return m_TrueTrackSimvarRequest; }
            set { this.SetProperty(ref m_TrueTrackSimvarRequest, value); }
        }
        private SimvarRequest m_TrueTrackSimvarRequest = null;

        public ObservableCollection<string> lErrorMessages { get; private set; }

        public BaseCommand cmdToggleConnect { get; private set; }
        public BaseCommand cmdToggleBroadcast { get; private set; }

        #endregion

        #region Real time

        private DispatcherTimer m_oTimer = new DispatcherTimer();

        #endregion

        private UdpClient m_oUdpClient;
        private int m_iPort;
        private Socket socket;
        private IPEndPoint endpoint;
        
        public AvionicsBridgeViewModel()
        {
            //lObjectIDs = new ObservableCollection<uint>();
            //lObjectIDs.Add(1);

            //lSimvarRequests = new ObservableCollection<SimvarRequest>();
            lErrorMessages = new ObservableCollection<string>();

            cmdToggleConnect = new BaseCommand((p) => { ToggleConnect(); });
            cmdToggleBroadcast = new BaseCommand((p) => { ToggleBroadcast(); });
            //cmdAddRequest = new BaseCommand((p) => { AddRequest(null, null); });
            //cmdRemoveSelectedRequest = new BaseCommand((p) => { RemoveSelectedRequest(); });
            //cmdTrySetValue = new BaseCommand((p) => { TrySetValue(); });
            //cmdLoadFiles = new BaseCommand((p) => { LoadFiles(); });
            //cmdSaveFile = new BaseCommand((p) => { SaveFile(false); });

            m_oTimer.Interval = new TimeSpan(0, 0, 0, 1, 0);
            m_oTimer.Tick += new EventHandler(OnTick);

            SetupRequests();
        }

        void SetupRequests()
        {
            m_LatitudeSimvarRequest = SetupRequest("GPS POSITION LAT", "degrees");
            m_LongitudeSimvarRequest = SetupRequest("GPS POSITION LON", "degrees");
            m_GroundSpeedSimvarRequest = SetupRequest("GPS GROUND SPEED", "meter/second");
            m_TrueHeadingSimvarRequest = SetupRequest("GPS GROUND TRUE HEADING", "radians");
            m_TrueTrackSimvarRequest = SetupRequest("GPS GROUND TRUE TRACK", "radians");
        }

        SimvarRequest SetupRequest(string _sSimvarRequest, string _sUnitRequest)
        {
            SimvarRequest oSimvarRequest = new SimvarRequest
            {
                eDef = (DEFINITION)m_iCurrentDefinition,
                eRequest = (REQUEST)m_iCurrentRequest,
                sName = _sSimvarRequest,
                sUnits = _sUnitRequest
            };

            oSimvarRequest.bPending = !RegisterToSimConnect(oSimvarRequest);
            oSimvarRequest.bStillPending = oSimvarRequest.bPending;

            ++m_iCurrentDefinition;
            ++m_iCurrentRequest;

            return oSimvarRequest;
        }

        private void Connect()
        {
            Console.WriteLine("Connect");

            try
            {
                /// The constructor is similar to SimConnect_Open in the native API
                m_oSimConnect = new SimConnect("Simconnect - Simvar test", m_hWnd, WM_USER_SIMCONNECT, null, 0);

                /// Listen to connect and quit msgs
                m_oSimConnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(SimConnect_OnRecvOpen);
                m_oSimConnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(SimConnect_OnRecvQuit);

                /// Listen to exceptions
                m_oSimConnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(SimConnect_OnRecvException);

                /// Catch a simobject data request
                m_oSimConnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(SimConnect_OnRecvSimobjectDataBytype);
            }
            catch (COMException ex)
            {
                Console.WriteLine("Connection to KH failed: " + ex.Message);
            }
        }

        void RegisterIfPending(SimvarRequest simvar)
        {
            if (simvar.bPending)
            {
                simvar.bPending = !RegisterToSimConnect(simvar);
                simvar.bStillPending = simvar.bPending;
            }
        }

        private void SimConnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Console.WriteLine("SimConnect_OnRecvOpen");
            Console.WriteLine("Connected to KH");

            sConnectButtonLabel = "Disconnect";
            bConnected = true;

            // Register pending requests
            RegisterIfPending(m_LatitudeSimvarRequest);
            RegisterIfPending(m_LongitudeSimvarRequest);
            RegisterIfPending(m_GroundSpeedSimvarRequest);
            RegisterIfPending(m_TrueHeadingSimvarRequest);
            RegisterIfPending(m_TrueTrackSimvarRequest);

            m_oTimer.Start();
            bOddTick = false;
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
            SIMCONNECT_EXCEPTION eException = (SIMCONNECT_EXCEPTION)data.dwException;
            Console.WriteLine("SimConnect_OnRecvException: " + eException.ToString());

            lErrorMessages.Add("SimConnect : " + eException.ToString());
        }

        void HandleRequest(SimvarRequest simvar, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            if (data.dwRequestID == (uint)simvar.eRequest)
            {
                double dValue = (double)data.dwData[0];
                simvar.dValue = dValue;
                simvar.bPending = false;
                simvar.bStillPending = false;
            }
        }

        private void SimConnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            Console.WriteLine("SimConnect_OnRecvSimobjectDataBytype");

            uint iRequest = data.dwRequestID;
            uint iObject = data.dwObjectID;

            HandleRequest(m_LatitudeSimvarRequest, data);
            HandleRequest(m_LongitudeSimvarRequest, data);
            HandleRequest(m_GroundSpeedSimvarRequest, data);
            HandleRequest(m_TrueHeadingSimvarRequest, data);
            HandleRequest(m_TrueTrackSimvarRequest, data);
        }

        void RequestIfNotPending(SimvarRequest simvar)
        {
            if (!simvar.bPending)
            {
                m_oSimConnect?.RequestDataOnSimObjectType(simvar.eRequest, simvar.eDef, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                simvar.bPending = true;
            }
            else
            {
                simvar.bStillPending = true;
            }
        }

        // May not be the best way to achive regular requests.
        // See SimConnect.RequestDataOnSimObject
        private void OnTick(object sender, EventArgs e)
        {
            Console.WriteLine("OnTick");

            bOddTick = !bOddTick;

            RequestIfNotPending(m_LatitudeSimvarRequest);
            RequestIfNotPending(m_LongitudeSimvarRequest);
            RequestIfNotPending(m_GroundSpeedSimvarRequest);
            RequestIfNotPending(m_TrueHeadingSimvarRequest);
            RequestIfNotPending(m_TrueTrackSimvarRequest);

            // broadcast current data via UDP
            /*if (m_oUdpClient != null)
            {
                var data = BitConverter.GetBytes(m_LatitudeSimvarRequest.dValue);
                m_oUdpClient.Send(data, data.Length, "192.168.1.255", m_iPort);
            }*/
            if (socket != null)
            {
                byte[] data = new byte[48];
                var now = BitConverter.GetBytes(DateTime.Now.Ticks);
                Buffer.BlockCopy(now, 0, data, 0, 8);
                var latitude = BitConverter.GetBytes(m_LatitudeSimvarRequest.dValue);
                Buffer.BlockCopy(latitude, 0, data, 8, 8);
                var longitude = BitConverter.GetBytes(m_LongitudeSimvarRequest.dValue);
                Buffer.BlockCopy(longitude, 0, data, 16, 8);
                var speed = BitConverter.GetBytes(m_GroundSpeedSimvarRequest.dValue);
                Buffer.BlockCopy(speed, 0, data, 24, 8);
                var heading = BitConverter.GetBytes(m_TrueHeadingSimvarRequest.dValue);
                Buffer.BlockCopy(heading, 0, data, 32, 8);
                var track = BitConverter.GetBytes(m_TrueTrackSimvarRequest.dValue);
                Buffer.BlockCopy(track, 0, data, 40, 8);
                //socket.SendTo(data, endpoint);
                //socket.Send(data, data.Length, SocketFlags.None);
                //socket.SendTo(data, endpoint);
                socket.Send(data);
            }
        }

        private void ToggleConnect()
        {
            if (m_oSimConnect == null)
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
            /*if (m_oUdpClient == null)
            {
                int iPort = 0;
                if (int.TryParse(m_sPort, out iPort))
                {
                    try
                    {
                        m_oUdpClient = new UdpClient();
                        m_oUdpClient.DontFragment = true;
                        m_oUdpClient.EnableBroadcast = true;
                        m_oUdpClient.Client.Bind(new IPEndPoint(IPAddress.Any, iPort));
                        m_iPort = iPort;
                        sBroadcastButtonLabel = "Stop Broadcast";
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Unable to start UDP broadcast: " + ex.Message);
                    }
                }
            }
            else
            {
                m_oUdpClient.Close();
                m_oUdpClient = null;
                sBroadcastButtonLabel = "Start Broadcast";
            }*/
            if (socket == null)
            {
                int iPort = 0;
                if (int.TryParse(m_sPort, out iPort))
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.Connect(IPAddress.Parse("192.168.1.41"), iPort);
                    //IPAddress multicast = IPAddress.Parse("224.168.100.2");
                    //IPAddress localIp = IPAddress.Parse("192.168.1.59");
                    //EndPoint localEP = (EndPoint)new IPEndPoint(localIp, 0);
                    
                    //socket.Bind(localEP);
                    //socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multicast, localIp));
                    //socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 10);
                    //endpoint = new IPEndPoint(multicast, iPort);
                    //socket.Connect(endpoint);
                    sBroadcastButtonLabel = "Stop Broadcast";
                    //m_oTimer.Start();
                }
            }
            else
            {
                socket.Close();
                socket = null;
                sBroadcastButtonLabel = "Start Broadcast";
                //m_oTimer.Stop();
            }
        }

        private bool RegisterToSimConnect(SimvarRequest _oSimvarRequest)
        {
            if (m_oSimConnect != null)
            {
                /// Define a data structure
                m_oSimConnect.AddToDataDefinition(_oSimvarRequest.eDef, _oSimvarRequest.sName, _oSimvarRequest.sUnits, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                /// IMPORTANT: Register it with the simconnect managed wrapper marshaller
                /// If you skip this step, you will only receive a uint in the .dwData field.
                m_oSimConnect.RegisterDataDefineStruct<double>(_oSimvarRequest.eDef);

                return true;
            }
            else
            {
                return false;
            }
        }

        public void SetTickSliderValue(int _iValue)
        {
            m_oTimer.Interval = new TimeSpan(0, 0, 0, 0, (int)(_iValue));
        }
    }
}
