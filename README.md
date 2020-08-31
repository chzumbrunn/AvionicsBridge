# AvionicsBridge

Simple App to broadcast avionics data of your Microsoft Flight Simulator instance via UDP e.g. to display an ownship symbol on a mobile moving map.
It currently supports both broadcasting (on 255.255.255.255) and unicast to any IP.

## Installation

### 1. Microsoft Flight Simulator SDK
In order to install the required SDK follow these steps:
1. Run the sim
2. Turn on developer mode via Options -> General -> Developers
3. Download the SDK via Help -> SDK Installer from the new menu appearing in the upper left corner
4. Install to your desired location

### 2. Build Project
1. Download this repository and open the solution in Visual Studio
2. Adjust the reference to **Microsoft.FlightSimulator.SimConnect** to the managed SimConnect library that was installed as part of the SDK (**YOUR_SDK_PATH/SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll**)
3. Build the solution
4. Copy the native SimConnect DLL (**YOUR_SDK_PATH/SimConnect SDK\lib\SimConnect.dll**) to your build directory
5. You should now be able to run the application, have fun! :)
