﻿/*
 * This file is part of VitalSignsCaptureWave v1.011.
 * Copyright (C) 2015-22 John George K., xeonfusion@users.sourceforge.net

    VitalSignsCapture is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    VitalSignsCapture is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with VitalSignsCapture.  If not, see <http://www.gnu.org/licenses/>.*/

using log4net;
using System.IO.Ports;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace VSCaptureWave
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static EventHandler dataEvent;
        public static string DeviceID;

        public static string JSONPostUrl;
        public static string JSONPostUser;
        public static string JSONPostPassw;
        public static string JSONPostKafka;

        public static string MQTTUrl;
        public static string MQTTtopic;
        public static string MQTTuser;
        public static string MQTTpassw;

        static void Main(string[] args)
        {
            log.Info("Starting VitalSignsCaptureWave v1.012 (C)2015-22 John George K., 2023 Evgenii Balakhonov");
            Console.WriteLine("For command line usage: -help");
            Console.WriteLine();

            // Create a new SerialPort object with default settings.
            DSerialPort _serialPort = DSerialPort.GetInstance;
            string portName;
            string sInterval;
            string sWaveformSet;

            var parser = new CommandLineParser();
            parser.Parse(args);

            if (parser.Arguments.ContainsKey("help"))
            {
                Console.WriteLine("VSCaptureWave.exe -port [portname] -interval [number] -waveset [number]");
                Console.WriteLine("-waveset [number] -export [number] -devid [name] -url [name]");
                Console.WriteLine("-port <Set serial port name>");
                Console.WriteLine("-interval <Set numeric transmission interval>");
                Console.WriteLine("-waveset <Set waveform transmission set option>");
                Console.WriteLine("-export <Set data export 1 - CSV, 2 - JSON, 3 - MQTT option>");
                Console.WriteLine("-exportDataFile <Set file name for CSV export");                
                Console.WriteLine("-devid <Set device ID for MQTT or JSON export>");
                Console.WriteLine("-url <Set MQTT or JSON export url>");
                Console.WriteLine("-topic <Set topic for MQTT export>");
                Console.WriteLine("-kafka <Set Kafka REST Proxy mode (y/n)");
                Console.WriteLine("-user <Set username for MQTT export>");
                Console.WriteLine("-passw <Set password for MQTT export>");
                Console.WriteLine();
                return;
            }

            if (parser.Arguments.ContainsKey("port"))
            {
                portName = parser.Arguments["port"][0];
            }
            else
            {
                Console.WriteLine("Select the Port to which GE Datex S/5 Monitor is to be connected, Available Ports:");
                foreach (string s in SerialPort.GetPortNames())
                {
                    Console.WriteLine(" {0}", s);
                }

                Console.Write("COM port({0}): ", _serialPort.PortName.ToString());
                portName = Console.ReadLine();
            }
            log.Info($"Selected COM port: {portName}");

            if (portName != "")
            {
                // Allow the user to set the appropriate properties.
                _serialPort.PortName = portName;
            }

            try
            {
                _serialPort.Open();

                if (_serialPort.OSIsUnix())
                {
                    dataEvent += new EventHandler((object sender, EventArgs e) => ReadData(sender));
                }

                if (!_serialPort.OSIsUnix())
                {
                    _serialPort.DataReceived += new SerialDataReceivedEventHandler(p_DataReceived);
                }

                if (!parser.Arguments.ContainsKey("port"))
                {
                    Console.WriteLine("You may now connect the serial cable to the GE Datex S/5 Monitor");
                    Console.WriteLine("Press any Key to continue..");

                    Console.ReadKey(true);

                }

                {
                    if (parser.Arguments.ContainsKey("interval"))
                    {
                        sInterval = parser.Arguments["interval"][0];
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.Write("Enter Numeric data Transmission interval (seconds):");
                        sInterval = Console.ReadLine();
                    }

                    short nInterval = 5;
                    if (sInterval != "") nInterval = Convert.ToInt16(sInterval);
                    if (nInterval < 5) nInterval = 5;

                    string sDataExportset;
                    if (parser.Arguments.ContainsKey("export"))
                    {
                        sDataExportset = parser.Arguments["export"][0];
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine("Data export options:");
                        Console.WriteLine("1. Export as CSV files");
                        Console.WriteLine("2. Export as JSON to URL");
                        Console.WriteLine("3. Export as MQTT to URL");
                        Console.WriteLine();
                        Console.Write("Choose data export option (1-3):");

                        sDataExportset = Console.ReadLine();
                    }
                    log.Info($"Selected data export option: {sDataExportset}");

                    int nDataExportset = 1;
                    if (sDataExportset != "") nDataExportset = Convert.ToInt32(sDataExportset);

                    switch (nDataExportset)
                    {
                        case 1:
                            {
                                if (parser.Arguments.ContainsKey("exportDataFile"))
                                {
                                    _serialPort.CsvExport = new(parser.Arguments["exportDataFile"][0]);
                                }
                                else
                                {
                                    Console.WriteLine("Enter a CSV file name to save the receiving data ('" + CsvExport.DEFAULT_EXPORT_FILE_NAME + "' by default):");
                                    string csvFileName = Console.ReadLine();
                                    _serialPort.CsvExport = new(!String.IsNullOrEmpty(csvFileName) ? 
                                        (csvFileName.Contains('/') || csvFileName.Contains('\\') ? csvFileName : Path.Combine(Directory.GetCurrentDirectory(), csvFileName)) :
                                        Path.Combine(Directory.GetCurrentDirectory(), CsvExport.DEFAULT_EXPORT_FILE_NAME));
                                }
                                log.Info($"Data will be written to CSV file {_serialPort.CsvExport.ExportFileName}");
                            }
                            break;

                        case 2:
                            {
                                if (parser.Arguments.ContainsKey("devid"))
                                {
                                    DeviceID = parser.Arguments["devid"][0];
                                }
                                else
                                {
                                    Console.Write("Enter Device ID/Name:");
                                    DeviceID = Console.ReadLine();

                                }

                                if (parser.Arguments.ContainsKey("url"))
                                {
                                    JSONPostUrl = parser.Arguments["url"][0];
                                }
                                else
                                {
                                    Console.Write("Enter JSON Data Export URL(http://):");
                                    JSONPostUrl = Console.ReadLine();
                                }

                                if (parser.Arguments.ContainsKey("user"))
                                {
                                    JSONPostUser = parser.Arguments["user"][0];
                                }
                                else
                                {
                                    Console.Write("Enter JSON Server Username:");
                                    JSONPostUser = Console.ReadLine();
                                }

                                if (parser.Arguments.ContainsKey("passw"))
                                {
                                    JSONPostPassw = parser.Arguments["passw"][0];
                                }
                                else
                                {
                                    Console.Write("Enter JSON Server Password:");
                                    JSONPostPassw = Console.ReadLine();
                                }

                                if (parser.Arguments.ContainsKey("kafka"))
                                {
                                    JSONPostKafka = parser.Arguments["kafka"][0];
                                }
                                else
                                {
                                    Console.Write("Enable Kafka REST Proxy mode (y/n)?");
                                    JSONPostKafka = Console.ReadLine();
                                }

                                _serialPort.JsonServerClient = new(JSONPostUrl, JSONPostUser, JSONPostPassw, 
                                    JSONPostKafka != null && string.Equals("y", JSONPostKafka, StringComparison.OrdinalIgnoreCase));
                                log.Info($"Data will be sent to JSON Server {_serialPort.JsonServerClient.Uri}, KafkaProxyMode = {_serialPort.JsonServerClient.KafkaProxyMode}");
                            }
                            break;
                        case 3:
                            {

                                if (parser.Arguments.ContainsKey("devid"))
                                {
                                    DeviceID = parser.Arguments["devid"][0];
                                }
                                else
                                {
                                    Console.Write("Enter Device ID/Name:");
                                    DeviceID = Console.ReadLine();

                                }

                                if (parser.Arguments.ContainsKey("url"))
                                {
                                    MQTTUrl = parser.Arguments["url"][0];
                                }
                                else
                                {
                                    Console.Write("Enter MQTT WebSocket Server URL(ws://):");
                                    MQTTUrl = Console.ReadLine();

                                }

                                if (parser.Arguments.ContainsKey("topic"))
                                {
                                    MQTTtopic = parser.Arguments["topic"][0];
                                }
                                else
                                {
                                    Console.Write("Enter MQTT Topic:");
                                    MQTTtopic = Console.ReadLine();

                                }

                                if (parser.Arguments.ContainsKey("user"))
                                {
                                    MQTTuser = parser.Arguments["user"][0];
                                }
                                else
                                {
                                    Console.Write("Enter MQTT Username:");
                                    MQTTuser = Console.ReadLine();

                                }

                                if (parser.Arguments.ContainsKey("passw"))
                                {
                                    MQTTpassw = parser.Arguments["passw"][0];
                                }
                                else
                                {
                                    Console.Write("Enter MQTT Password:");
                                    MQTTpassw = Console.ReadLine();

                                }

                                _serialPort.MQTTClient = new(MQTTUrl, MQTTtopic, MQTTuser, MQTTpassw);
                                log.Info($"Data will be sent to MQTT broker {_serialPort.MQTTClient.MQTTUrl}");
                            }
                            break;
                    }

                    _serialPort.m_DeviceID = DeviceID;

                    if (nDataExportset > 0 && nDataExportset < 4) _serialPort.m_dataexportset = nDataExportset;

                    if (parser.Arguments.ContainsKey("waveset"))
                    {
                        sWaveformSet = parser.Arguments["waveset"][0];
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine("Waveform data Transmission sets:");
                        Console.WriteLine("0. None");
                        Console.WriteLine("1. ECG1, INVP1, INVP2, PLETH");
                        Console.WriteLine("2. ECG1, INVP1, PLETH, CO2, RESP");
                        Console.WriteLine("3. ECG1, PLETH, CO2, RESP, AWP, VOL, FLOW");
                        Console.WriteLine("4. CO2, O2, N2O, AA");
                        Console.WriteLine("5. EEG1, EEG2, EEG3, EEG4");
                        Console.WriteLine("6. ECG1, ECG2, ECG3");
                        Console.WriteLine("7. ECG1, INVP1, INVP2, INVP3");
                        Console.WriteLine("8. INVP1, INVP2, INVP3, INVP4, INVP5, INVP6");
                        Console.WriteLine("9. ECG1, INVP1, PLETH, ENTROPY");
                        Console.WriteLine("10. ECG1, BIS");
                        Console.WriteLine();
                        Console.Write("Choose Waveform data Transmission set (0-10):");

                        sWaveformSet = Console.ReadLine();
                    }

                    log.Info($"Enabled Waveform data Transmission set: {sWaveformSet}");

                    short nWaveformSet = 0;
                    if (sWaveformSet != "") nWaveformSet = Convert.ToInt16(sWaveformSet);

                    if (nWaveformSet > 0 && nDataExportset == 1)
                    {
                        log.Info($"Waveform data will be written to multiple CSV files in same folder with file {_serialPort.CsvExport.ExportFileName}");
                    }

                    log.Info($"Requesting {nInterval} second Transmission from monitor");

                    //Console.WriteLine("Requesting Transmission from monitor");
                                        
                    //_serialPort.RequestTransfer(DataConstants.DRI_PH_DISPL, nInterval); // Add Request Transmission

                    //_serialPort.RequestTransfer(DataConstants.DRI_PH_DISPL, -1); // Add Single Request Transmission

                    //_serialPort.RequestTransfer(DataConstants.DRI_PH_60S_TREND, 60); // Add Trend Request Transmission

                    //Request transfer based on the DRI level of the monitor
                    _serialPort.RequestTransfer(DataConstants.DRI_PH_DISPL, nInterval, DataConstants.DRI_LEVEL_2015); // Add Request Transmission
                    _serialPort.RequestTransfer(DataConstants.DRI_PH_DISPL, nInterval, DataConstants.DRI_LEVEL_2009); // Add Request Transmission
                    _serialPort.RequestTransfer(DataConstants.DRI_PH_DISPL, nInterval, DataConstants.DRI_LEVEL_2005); // Add Request Transmission
                    _serialPort.RequestTransfer(DataConstants.DRI_PH_DISPL, nInterval, DataConstants.DRI_LEVEL_2003); // Add Request Transmission
                    _serialPort.RequestTransfer(DataConstants.DRI_PH_DISPL, nInterval, DataConstants.DRI_LEVEL_2001); // Add Request Transmission

                    //Request only a single waveform 
                    //_serialPort.RequestWaveTransfer(DataConstants.DRI_WF_ECG1, DataConstants.WF_REQ_CONT_START);
                    //_serialPort.RequestWaveTransfer(DataConstants.DRI_WF_INVP1, DataConstants.WF_REQ_CONT_START);

                    //Request upto 8 waveforms but total sample rate should be less than 600 samples/sec
                    //Sample rate for ECG is 300, INVP 100, PLETH 100, respiratory 25 each
                    byte[] WaveTrtype = new byte[8];

                    CreateWaveformSet(nWaveformSet, WaveTrtype);

                    if (nWaveformSet != 0)
                    {
                        _serialPort.RequestMultipleWaveTransfer(WaveTrtype, DataConstants.WF_REQ_CONT_START, DataConstants.DRI_LEVEL_2015);
                        _serialPort.RequestMultipleWaveTransfer(WaveTrtype, DataConstants.WF_REQ_CONT_START, DataConstants.DRI_LEVEL_2009);
                        _serialPort.RequestMultipleWaveTransfer(WaveTrtype, DataConstants.WF_REQ_CONT_START, DataConstants.DRI_LEVEL_2005);
                        _serialPort.RequestMultipleWaveTransfer(WaveTrtype, DataConstants.WF_REQ_CONT_START, DataConstants.DRI_LEVEL_2003);
                        _serialPort.RequestMultipleWaveTransfer(WaveTrtype, DataConstants.WF_REQ_CONT_START, DataConstants.DRI_LEVEL_2001);
                    }
                }

                Console.WriteLine("Press Escape button to Stop");

                if (_serialPort.OSIsUnix())
                {
                    do
                    {
                        if (_serialPort.BytesToRead != 0)
                        {
                            dataEvent.Invoke(_serialPort, new EventArgs());
                        }

                        if (Console.KeyAvailable == true)
                        {
                            if (Console.ReadKey(true).Key == ConsoleKey.Escape) break;
                        }
                    }
                    while (Console.KeyAvailable == false);

                }

                if (!_serialPort.OSIsUnix())
                {
                    ConsoleKeyInfo cki;

                    do
                    {
                        cki = Console.ReadKey(true);
                    }
                    while (cki.Key != ConsoleKey.Escape);
                }


            }
            catch (Exception ex)
            {
                log.Error($"Error opening/writing to serial port :: {ex.Message}", ex);
            }
            finally
            {
                _serialPort.StopTransfer();
                _serialPort.StopwaveTransfer();
                _serialPort.Close();
            }
        }

        static void p_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            ReadData(sender);

        }

        public static void ReadData(object sender)
        {
            try
            {
                (sender as DSerialPort).ReadBuffer();

            }
            catch (TimeoutException) { }
        }

        public static void WaitForSeconds(int nsec)
        {
            DateTime dt = DateTime.Now;
            DateTime dt2 = dt.AddSeconds(nsec);
            do
            {
                dt = DateTime.Now;
            }
            while (dt2 > dt);

        }

        public static void CreateWaveformSet(int nWaveSetType, byte[] WaveTrtype)
        {
            //Request upto 8 waveforms but total sample rate should be less than 600 samples/sec
            //Sample rate for ECG is 300, INVP 100, PLETH 100, respiratory 25 each

            switch (nWaveSetType)
            {
                case 0:
                    break;
                case 1:
                    WaveTrtype[0] = DataConstants.DRI_WF_ECG1;
                    WaveTrtype[1] = DataConstants.DRI_WF_INVP1;
                    WaveTrtype[2] = DataConstants.DRI_WF_INVP2;
                    WaveTrtype[3] = DataConstants.DRI_WF_PLETH;
                    WaveTrtype[4] = DataConstants.DRI_EOL_SUBR_LIST;
                    break;
                case 2:
                    WaveTrtype[0] = DataConstants.DRI_WF_ECG1;
                    WaveTrtype[1] = DataConstants.DRI_WF_INVP1;
                    WaveTrtype[2] = DataConstants.DRI_WF_PLETH;
                    WaveTrtype[3] = DataConstants.DRI_WF_CO2;
                    WaveTrtype[4] = DataConstants.DRI_WF_RESP;
                    WaveTrtype[5] = DataConstants.DRI_EOL_SUBR_LIST;
                    break;
                case 3:
                    WaveTrtype[0] = DataConstants.DRI_WF_ECG1;
                    WaveTrtype[1] = DataConstants.DRI_WF_PLETH;
                    WaveTrtype[2] = DataConstants.DRI_WF_CO2;
                    WaveTrtype[3] = DataConstants.DRI_WF_RESP;
                    WaveTrtype[4] = DataConstants.DRI_WF_AWP;
                    WaveTrtype[5] = DataConstants.DRI_WF_VOL;
                    WaveTrtype[6] = DataConstants.DRI_WF_FLOW;
                    WaveTrtype[7] = DataConstants.DRI_EOL_SUBR_LIST;
                    break;
                case 4:
                    WaveTrtype[0] = DataConstants.DRI_WF_CO2;
                    WaveTrtype[1] = DataConstants.DRI_WF_O2;
                    WaveTrtype[2] = DataConstants.DRI_WF_N2O;
                    WaveTrtype[3] = DataConstants.DRI_WF_AA;
                    WaveTrtype[4] = DataConstants.DRI_EOL_SUBR_LIST;
                    break;
                case 5:
                    WaveTrtype[0] = DataConstants.DRI_WF_EEG1;
                    WaveTrtype[1] = DataConstants.DRI_WF_EEG2;
                    WaveTrtype[2] = DataConstants.DRI_WF_EEG3;
                    WaveTrtype[3] = DataConstants.DRI_WF_EEG4;
                    WaveTrtype[4] = DataConstants.DRI_EOL_SUBR_LIST;
                    break;
                case 6:
                    WaveTrtype[0] = DataConstants.DRI_WF_ECG1;
                    WaveTrtype[1] = DataConstants.DRI_WF_ECG2;
                    WaveTrtype[2] = DataConstants.DRI_WF_ECG3;
                    WaveTrtype[3] = DataConstants.DRI_EOL_SUBR_LIST;
                    break;
                case 7:
                    WaveTrtype[0] = DataConstants.DRI_WF_ECG1;
                    WaveTrtype[1] = DataConstants.DRI_WF_INVP1;
                    WaveTrtype[2] = DataConstants.DRI_WF_INVP2;
                    WaveTrtype[3] = DataConstants.DRI_WF_INVP3;
                    WaveTrtype[4] = DataConstants.DRI_EOL_SUBR_LIST;
                    break;
                case 8:
                    WaveTrtype[0] = DataConstants.DRI_WF_INVP1;
                    WaveTrtype[1] = DataConstants.DRI_WF_INVP2;
                    WaveTrtype[2] = DataConstants.DRI_WF_INVP3;
                    WaveTrtype[3] = DataConstants.DRI_WF_INVP4;
                    WaveTrtype[4] = DataConstants.DRI_WF_INVP5;
                    WaveTrtype[5] = DataConstants.DRI_WF_INVP6;
                    WaveTrtype[6] = DataConstants.DRI_EOL_SUBR_LIST;
                    break;
                case 9:
                    WaveTrtype[0] = DataConstants.DRI_WF_ECG1;
                    WaveTrtype[1] = DataConstants.DRI_WF_INVP1;
                    WaveTrtype[2] = DataConstants.DRI_WF_PLETH;
                    WaveTrtype[3] = DataConstants.DRI_WF_ENT_100;
                    WaveTrtype[4] = DataConstants.DRI_EOL_SUBR_LIST;
                    break;
                case 10:
                    WaveTrtype[0] = DataConstants.DRI_WF_ECG1;
                    WaveTrtype[1] = DataConstants.DRI_WF_EEG_BIS;
                    WaveTrtype[2] = DataConstants.DRI_EOL_SUBR_LIST;
                    break;
                default:
                    WaveTrtype[0] = DataConstants.DRI_WF_ECG1;
                    WaveTrtype[1] = DataConstants.DRI_WF_INVP1;
                    WaveTrtype[2] = DataConstants.DRI_WF_INVP2;
                    WaveTrtype[3] = DataConstants.DRI_WF_PLETH;
                    WaveTrtype[4] = DataConstants.DRI_EOL_SUBR_LIST;
                    break;
            }

        }

    }

    public class CommandLineParser
    {
        public CommandLineParser()
        {
            Arguments = new Dictionary<string, string[]>();
        }

        public IDictionary<string, string[]> Arguments { get; private set; }

        public void Parse(string[] args)
        {
            string currentName = "";
            var values = new List<string>();
            foreach (string arg in args)
            {
                if (arg.StartsWith("-", StringComparison.InvariantCulture))
                {
                    if (currentName != "" && values.Count != 0)
                        Arguments[currentName] = values.ToArray();

                    else
                    {
                        values.Add("");
                        Arguments[currentName] = values.ToArray();
                    }
                    values.Clear();
                    currentName = arg.Substring(1);
                }
                else if (currentName == "")
                    Arguments[arg] = new string[0];
                else
                    values.Add(arg);
            }

            if (currentName != "")
                Arguments[currentName] = values.ToArray();
        }

        public bool Contains(string name)
        {
            return Arguments.ContainsKey(name);
        }
    }

}


