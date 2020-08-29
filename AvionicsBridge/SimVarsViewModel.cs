using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    public class SimVarsViewModel : BaseViewModel
    {
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

        public ObservableCollection<SimvarRequest> SimVars { get; private set; }

        public SimConnect SimConnect { get; set; } = null;

        public SimVarsViewModel()
        {
            SetupRequests();

            SimVars = new ObservableCollection<SimvarRequest>();
            SimVars.Add(_latitudeSimvarRequest);
            SimVars.Add(_longitudeSimvarRequest);
            SimVars.Add(_groundSpeedSimvarRequest);
            SimVars.Add(_trueHeadingSimvarRequest);
            SimVars.Add(_trueTrackSimvarRequest);
        }

        private uint _currentDefinition = 0;
        private uint _currentRequest = 0;

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

        void RegisterIfPending(SimvarRequest simvar)
        {
            if (simvar.Pending)
            {
                simvar.Pending = !RegisterToSimConnect(simvar);
                simvar.StillPending = simvar.Pending;
            }
        }

        public void RegisterAllPendingRequests()
        {
            RegisterIfPending(_latitudeSimvarRequest);
            RegisterIfPending(_longitudeSimvarRequest);
            RegisterIfPending(_groundSpeedSimvarRequest);
            RegisterIfPending(_trueHeadingSimvarRequest);
            RegisterIfPending(_trueTrackSimvarRequest);
        }

        void ResetRequest(SimvarRequest simvar)
        {
            simvar.Pending = true;
            simvar.StillPending = true;
        }

        public void ResetAllRequests()
        {
            ResetRequest(_latitudeSimvarRequest);
            ResetRequest(_longitudeSimvarRequest);
            ResetRequest(_groundSpeedSimvarRequest);
            ResetRequest(_trueHeadingSimvarRequest);
            ResetRequest(_trueTrackSimvarRequest);
        }

        void RequestIfNotPending(SimvarRequest simvar)
        {
            if (!simvar.Pending)
            {
                SimConnect?.RequestDataOnSimObjectType(simvar.Request, simvar.Definition, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                simvar.Pending = true;
            }
            else
            {
                simvar.StillPending = true;
            }
        }

        public void RequestAllIfNotPending()
        {
            RequestIfNotPending(_latitudeSimvarRequest);
            RequestIfNotPending(_longitudeSimvarRequest);
            RequestIfNotPending(_groundSpeedSimvarRequest);
            RequestIfNotPending(_trueHeadingSimvarRequest);
            RequestIfNotPending(_trueTrackSimvarRequest);
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

        public void HandleReceivedData(SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            HandleRequest(_latitudeSimvarRequest, data);
            HandleRequest(_longitudeSimvarRequest, data);
            HandleRequest(_groundSpeedSimvarRequest, data);
            HandleRequest(_trueHeadingSimvarRequest, data);
            HandleRequest(_trueTrackSimvarRequest, data);
        }

        private bool RegisterToSimConnect(SimvarRequest simvarRequest)
        {
            if (SimConnect != null)
            {
                /// Define a data structure
                SimConnect.AddToDataDefinition(simvarRequest.Definition, simvarRequest.Name, simvarRequest.Units, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                /// IMPORTANT: Register it with the simconnect managed wrapper marshaller
                /// If you skip this step, you will only receive a uint in the .dwData field.
                SimConnect.RegisterDataDefineStruct<double>(simvarRequest.Definition);

                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
