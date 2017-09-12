///////////////////////////////////////////////////////////////////////////////////////
// 설    명 : IoT DIY 프로젝트 개발자를 위한 GUI 기반 통합 저작도구 개발
// 사용방법 : NONE
// 작 성 자 : JIWOO LEE
// 작성일자 : 2017.02
// 제 작 사 : 이엔유주식회사, ENU Co., Ltd
// 참조정보 : 
// Copyright (C) ENU Corporation
// All rights reserved.
// 수정이력 : 
// 
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using SQLite.Net.Attributes;
using System.Net;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Devices.SerialCommunication;
using Windows.Devices.Enumeration;
using System.Collections.ObjectModel;
using System.Threading;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Shapes;
using Windows.UI;
using Microsoft.Maker.RemoteWiring;
using Microsoft.Maker.Firmata;
using Microsoft.Maker.Serial;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace enuSpace_IoT
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public class ConfigItem
        {
            public string Attribute { get; set; }
            public string Value { get; set; }
        }
        List<ConfigItem> m_configlist = null;

        SQLite.Net.SQLiteConnection m_conn;

        public class Configuration
        {
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }
            public string Attribute { get; set; }
            public string Value { get; set; }
        }

        public class NameValueItem
        {
            public double X { get; set; }
            public double Value { get; set; }
        }
        List<NameValueItem> m_newitems1 = null;
        List<NameValueItem> m_newitems2 = null;
        PinItem m_pAnalogItem = null;
        PinItem m_pDigitalItem = null;

        public class PinItem
        {
            public bool use { get; set; }
            public String name { get; set; }
            public byte pinnum { get; set; }
            public PinMode pinmode { get; set; }
            public int value { get; set; }
            public TextBox gui_val { get; set; }
            public CheckBox gui_chk { get; set; }
        }
        List<PinItem> m_pinAnaloglist = null;
        List<PinItem> m_pinDigitallist = null;

        bool m_bUpdatePinPort = false;
        bool m_bUpdate_D9 = false;
        bool m_bUpdate_D10 = false;
        bool m_bUpdate_D11 = false;
        bool m_bUpdate_D12 = false;


        public String m_server_ip;
        public String m_device_key;

        UsbSerial m_usbSerial = null;
        RemoteDevice m_arduino = null;
        
        private ObservableCollection<DeviceInformation> m_listOfDevices;
        private CancellationTokenSource m_ReadCancellationTokenSource;

        MainPage m_pMainPage = null;
        PageDevice m_pPageDevice = null;
        PageUserLogin m_pPageUserLogin = null;

        private DispatcherTimer timer;

        bool m_bLoaded = false;

        public MainPage()
        {
            this.InitializeComponent();
            
            m_newitems1 = new List<NameValueItem>();
            m_newitems2 = new List<NameValueItem>();

            this.Loaded += new RoutedEventHandler(EnterInit);
        }

        void timer_Tick(object sender, object e)
        {
            for (int i =0; i< m_pinAnaloglist.Count; i++)
            {
                if (m_pinAnaloglist[i].use)
                    m_pinAnaloglist[i].gui_val.Text = m_pinAnaloglist[i].value.ToString();
            }
            for (int i = 0; i < m_pinDigitallist.Count; i++)
            {
                if (m_pinDigitallist[i].use)
                {
                    if (m_pinDigitallist[i].pinmode == PinMode.PWM)
                        m_pinDigitallist[i].gui_val.Text = m_pinDigitallist[0].value.ToString();
                    else
                    {
                        if (m_pinDigitallist[i].value == 1)
                            m_pinDigitallist[i].gui_val.Text = "HIGH";
                        else
                            m_pinDigitallist[i].gui_val.Text = "LOW";
                    }
                }
            }

            if (m_pAnalogItem != null)
            {
                if (m_newitems1 != null)
                {
                    m_newitems1.RemoveAt(0);
                    m_newitems1.Insert(m_newitems1.Count, new NameValueItem { X = 0, Value = m_pAnalogItem.value });

                    List<NameValueItem>[] pList;
                    pList = new List<NameValueItem>[1];
                    pList[0] = m_newitems1;

                    double axisx_min = 0;
                    double axisx_max = 50;
                    double data_min = 0;
                    double data_max = 1024;

                    DrawChart(gui_chart1, pList, axisx_min, axisx_max, data_min, data_max);
                }
            }
            if (m_pDigitalItem != null)
            {
                if (m_newitems2 != null)
                {
                    m_newitems2.RemoveAt(0);
                    m_newitems2.Insert(m_newitems2.Count, new NameValueItem { X = 0, Value = m_pDigitalItem.value });

                    List<NameValueItem>[] pList;
                    pList = new List<NameValueItem>[1];
                    pList[0] = m_newitems2;

                    double axisx_min = 0;
                    double axisx_max = 50;
                    double data_min = 0;
                    double data_max = 1.2;

                    DrawChart(gui_chart2, pList, axisx_min, axisx_max, data_min, data_max);
                }
            }


            SendServerData();
        }

        public async void EnterInit(object sender, RoutedEventArgs e)
        {
            if (m_bLoaded == false)
            {
                m_bLoaded = true;

                m_pMainPage = this;
                LoadSqliteDB();

                String strReg = GetAttributeValue("device-register");
                if (strReg != "true")
                    SetDefaultPin();

                String strAutoLogin = GetAttributeValue("auto-login");
                String strUserID = GetAttributeValue("user-id");
                String strUserPW = GetAttributeValue("user-pw");

                Boolean bLogin = false;
                if (!String.IsNullOrEmpty(strAutoLogin) && !String.IsNullOrEmpty(strUserID) && !String.IsNullOrEmpty(strUserPW))
                {
                    if (strAutoLogin == "true")
                    {
                        bLogin = await AutoLogin();

                        strReg = GetAttributeValue("device-register");
                        if (String.IsNullOrEmpty(strReg) || strReg == "false")
                        {
                            GoDevicePage();
                        }
                        else
                        {
                            GoMainPage();
                        }
                    }
                }

                if (bLogin == false)
                {
                    GoLoginPage();
                }
            }
        }

        public async Task<Boolean> AutoLogin()
        {
            try
            {
                String server_ip = GetAttributeValue("server-ip");

                String user_id = GetAttributeValue("user-id");
                String user_pw = GetAttributeValue("user-pw");

                if (!String.IsNullOrEmpty(user_id) && !String.IsNullOrEmpty(user_pw))
                {
                    String url = server_ip + "login";
                    String data = "userid=" + user_id + "&password=" + user_pw;

                    String response = await getResponse(url, data);
                    JsonValue jsonValue = JsonValue.Parse(response);
                    String return_flag = jsonValue.GetObject().GetNamedString("RESULT");
                    if (return_flag == "OK")
                    {
                        return true;
                    }
                    else
                    {
                        gui_status.Text = jsonValue.GetObject().GetNamedString("MESSAGE");
                        return false;
                    }
                }
                else
                {
                    gui_status.Text = "login 정보가 없습니다.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                gui_status.Text = ex.Message;
                return false;
            }
        }

        private void GoLoginPage()
        {
            if (m_pPageUserLogin == null)
                m_pPageUserLogin = new PageUserLogin();

            Window.Current.Content = this.Frame;
            Window.Current.Activate();

            this.Frame.Content = m_pPageUserLogin;

            m_pPageUserLogin.SetParentFrame(this);
        }

        private void gui_button_logout_click(object sender, RoutedEventArgs e)
        {
            try
            {
                gui_status.Text = "";
                CancelReadTask();
                CloseDevice();
                ListAvailablePorts();

                for (int i = 0; i < m_pinAnaloglist.Count; i++)
                {
                    m_pinAnaloglist[i].gui_chk.IsEnabled = true;
                }
                for (int i = 2; i < m_pinDigitallist.Count; i++)
                {
                    m_pinDigitallist[i].gui_chk.IsEnabled = true;
                }
                gui_pin_D2.IsEnabled = true;
                gui_pin_D3.IsEnabled = true;
                gui_pin_D4.IsEnabled = true;
                gui_pin_D5.IsEnabled = true;
                gui_pin_D6.IsEnabled = true;
                gui_pin_D7.IsEnabled = true;
                gui_pin_D8.IsEnabled = true;
                gui_pin_D9.IsEnabled = true;
                gui_pin_D10.IsEnabled = true;
                gui_pin_D11.IsEnabled = true;
                gui_pin_D12.IsEnabled = true;
                gui_pin_D13.IsEnabled = true;

                GoLoginPage();
            }
            catch (Exception ex)
            {
                gui_status.Text = ex.Message;
            }
        }

        private void gui_button_reset_device_click(object sender, RoutedEventArgs e)
        {
            try
            {
                gui_status.Text = "";
                CancelReadTask();
                CloseDevice();
                ListAvailablePorts();

                var query = m_conn.Table<Configuration>();
                var result = query.ToList();
                foreach (var item in query)
                {
                    m_conn.Delete(item);
                }            
                m_configlist.Clear();

                GoLoginPage();
            }
            catch (Exception ex)
            {
                gui_status.Text = ex.Message;
            }
        }


        public void GoDevicePage()
        {
            if (m_pPageDevice == null)
                m_pPageDevice = new PageDevice();
            
            Window.Current.Content = this.Frame;
            Window.Current.Activate();

            this.Frame.Content = m_pPageDevice;

            m_pPageDevice.SetParentFrame(this);
        }

        public void GoMainPage()
        {
            Window.Current.Content = this.Frame;
            Window.Current.Activate();

            this.Frame.Content = this;

            gui_user_name.Text = GetAttributeValue("user-id");

            m_server_ip = GetAttributeValue("server-ip");
            m_device_key = GetAttributeValue("device-key");

            if (m_listOfDevices == null)
                m_listOfDevices = new ObservableCollection<DeviceInformation>();
            ListAvailablePorts();

            UpdatePinPort();
        }

        private async void ListAvailablePorts()
        {
            try
            {
                m_listOfDevices.Clear();

                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);

                gui_status.Text = "Select a device and connect";

                for (int i = 0; i < dis.Count; i++)
                {
                    String serialid = dis[i].Id;
                    string[] result = serialid.Split('#');
                    if (result.Count() == 4)
                    {
                        if (result[0].Contains("USB") == true)
                        {
                            m_listOfDevices.Add(dis[i]);
                        }
                    }
                }

                DeviceListSource.Source = m_listOfDevices;
                gui_button_connect.IsEnabled = true;
                gui_button_disconnect.IsEnabled = false;
                gui_serial_device.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                gui_status.Text = ex.Message;
            }
        }

        void LoadSqliteDB()
        {
            m_configlist = new List<ConfigItem>(); 

            String path = System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "enuspace.sqlite");
            m_conn = new SQLite.Net.SQLiteConnection(new SQLite.Net.Platform.WinRT.SQLitePlatformWinRT(), path);

            if (TableExist("Configuration", m_conn) == false)
                m_conn.CreateTable<Configuration>();
            else
            {
                var query = m_conn.Table<Configuration>();

                foreach (var message in query)
                {
                    String strAttribute = message.Attribute;
                    String strValue = message.Value;

                    m_configlist.Add(new ConfigItem { Attribute = strAttribute, Value = strValue });
                }
            }
        }

        public bool TableExist(String tableName, SQLite.Net.SQLiteConnection conn)
        {
            String tableExistsQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='" + tableName + "';";
            String result = conn.ExecuteScalar<string>(tableExistsQuery);

            if (result == tableName)
                return true;
            else
                return false;
        }

        public String GetAttributeValue(String attribute)
        {
            for (int i = 0; i < m_configlist.Count; i++)
            {
                if (m_configlist[i].Attribute == attribute)
                {
                    return m_configlist[i].Value;
                }
            }
            return "";
        }

        public bool SetAttributeValue(String attribute, String value)
        {
            for (int i = 0; i < m_configlist.Count; i++)
            {
                if (m_configlist[i].Attribute == attribute)
                {
                    m_configlist[i].Value = value;

                    ////////////////////////////////////////////////////////////////
                    // DB 추가 및 업데이트
                    var query = m_conn.Table<Configuration>().Where(x => x.Attribute == attribute);
                    var result = query.ToList();
                    if (result.Count == 0)
                        m_conn.Insert(new Configuration() { Attribute = attribute, Value = value });
                    else
                    {
                        result[0].Value = value;
                        m_conn.Update(result[0]);
                    }
                    ////////////////////////////////////////////////////////////////
                    return true;
                }
            }

            m_configlist.Add(new ConfigItem { Attribute = attribute, Value = value });

            ////////////////////////////////////////////////////////////////
            // DB 추가 및 업데이트
            var query1 = m_conn.Table<Configuration>().Where(x => x.Attribute == attribute);
            var result1 = query1.ToList();
            if (result1.Count == 0)
                m_conn.Insert(new Configuration() { Attribute = attribute, Value = value });
            else
            {
                result1[0].Value = value;
                m_conn.Update(result1[0]);
            }
            ////////////////////////////////////////////////////////////////
            return true;
        }

        public bool DelAttributeValue(String attribute)
        {
            for (int i = 0; i < m_configlist.Count; i++)
            {
                if (m_configlist[i].Attribute == attribute)
                {
                    ////////////////////////////////////////////////////////////////
                    // DB 추가 및 업데이트
                    var query = m_conn.Table<Configuration>().Where(x => x.Attribute == attribute);
                    var result = query.ToList();
                    foreach (var item in result)
                    {
                        m_conn.Delete(item);
                    }
                    ////////////////////////////////////////////////////////////////

                    m_configlist.RemoveAt(i);
                    return true;
                }
            }
            
            return false;
        }

        public async Task<String> getResponse(String url, string data)
        {
            try
            {
                WebRequest request = WebRequest.Create(url);

                request.ContentType = "application/json";
                byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(data);
                request.ContentType = "application/x-www-form-urlencoded";
                request.Method = "POST";

                using (var requestStream = await request.GetRequestStreamAsync())
                {
                    System.IO.StreamWriter writer = new System.IO.StreamWriter(requestStream);
                    writer.Write(data);
                    writer.Flush();

                    using (WebResponse response = await request.GetResponseAsync())
                    {
                        using (System.IO.Stream responseStream = response.GetResponseStream())
                        {
                            System.IO.StreamReader reader = new System.IO.StreamReader(responseStream);
                            String answer = reader.ReadToEnd();
                            return answer;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                gui_status.Text = ex.Message;
                return "";
            }
        }

        private void OnConnectionEstablished()
        {
            var action = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() => {
                
                gui_button_connect.IsEnabled = false;
                gui_button_disconnect.IsEnabled = true;

                gui_status.Text = "Connected.";

                for (int i = 0; i < m_pinAnaloglist.Count; i++)
                {
                    m_pinAnaloglist[i].gui_chk.IsEnabled = false;
                }
                for (int i = 2; i < m_pinDigitallist.Count; i++)
                {
                    m_pinDigitallist[i].gui_chk.IsEnabled = false;
                }
                gui_pin_D2.IsEnabled = false;
                gui_pin_D3.IsEnabled = false;
                gui_pin_D4.IsEnabled = false;
                gui_pin_D5.IsEnabled = false;
                gui_pin_D6.IsEnabled = false;
                gui_pin_D7.IsEnabled = false;
                gui_pin_D8.IsEnabled = false;
                gui_pin_D9.IsEnabled = false;
                gui_pin_D10.IsEnabled = false;
                gui_pin_D11.IsEnabled = false;
                gui_pin_D12.IsEnabled = false;
                gui_pin_D13.IsEnabled = false;

                if (gui_chk_autoconnect.IsChecked.Value == true)
                    SetAttributeValue("SERIAL_AUTOCONNECT", "true");
                else
                    SetAttributeValue("SERIAL_AUTOCONNECT", "false");

                if (timer != null)
                    timer = null;

                timer = new DispatcherTimer();
                timer.Tick += timer_Tick;
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Start();
            }));
        }

        private PinMode GetPinMode(String strPinName)
        {
            String strValue = GetAttributeValue(strPinName);
            if (strValue == "INPUT")
                return PinMode.INPUT;
            else if (strValue == "OUTPUT")
                return PinMode.OUTPUT;
            else if (strValue == "ANALOG")
                return PinMode.ANALOG;
            else if (strValue == "PWM")
                return PinMode.PWM;
            else if (strValue == "SERVO")
                return PinMode.SERVO;
            else if (strValue == "SHIFT")
                return PinMode.SHIFT;
            else if (strValue == "I2C")
                return PinMode.I2C;
            else if (strValue == "ONEWIRE")
                return PinMode.ONEWIRE;
            else if (strValue == "STEPPER")
                return PinMode.STEPPER;
            else if (strValue == "ENCODER")
                return PinMode.ENCODER;
            else if (strValue == "SERIAL")
                return PinMode.SERIAL;
            else if (strValue == "PULLUP")
                return PinMode.PULLUP;
            else if (strValue == "IGNORED")
                return PinMode.IGNORED;

            return PinMode.INPUT;
        }

        private bool GetPinUse(String strPinName)
        {
            String strValue = GetAttributeValue(strPinName);
            if (strValue == "true")
                return true;
            else
                return false;
        }

        public void SetDefaultPin()
        {
            SetAttributeValue("PIN_USE_A0", "true");
            SetAttributeValue("PIN_USE_A1", "true");
            SetAttributeValue("PIN_USE_A2", "true");
            SetAttributeValue("PIN_USE_A3", "true");
            SetAttributeValue("PIN_USE_A4", "true");
            SetAttributeValue("PIN_USE_A5", "true");

            SetAttributeValue("PIN_USE_D0", "false");
            SetAttributeValue("PIN_USE_D1", "false");
            SetAttributeValue("PIN_USE_D2", "true");
            SetAttributeValue("PIN_USE_D3", "true");
            SetAttributeValue("PIN_USE_D4", "true");
            SetAttributeValue("PIN_USE_D5", "true");
            SetAttributeValue("PIN_USE_D6", "true");
            SetAttributeValue("PIN_USE_D7", "true");
            SetAttributeValue("PIN_USE_D8", "true");
            SetAttributeValue("PIN_USE_D9", "true");
            SetAttributeValue("PIN_USE_D10", "true");
            SetAttributeValue("PIN_USE_D11", "true");
            SetAttributeValue("PIN_USE_D12", "true");
            SetAttributeValue("PIN_USE_D13", "true");
            ///////////////////////////////////////////////////////
            SetAttributeValue("PIN_A0", "ANALOG");
            SetAttributeValue("PIN_A1", "ANALOG");
            SetAttributeValue("PIN_A2", "ANALOG");
            SetAttributeValue("PIN_A3", "ANALOG");
            SetAttributeValue("PIN_A4", "ANALOG");
            SetAttributeValue("PIN_A5", "ANALOG");

            SetAttributeValue("PIN_D0", "IGNORED");
            SetAttributeValue("PIN_D1", "IGNORED");
            SetAttributeValue("PIN_D2", "OUTPUT");
            SetAttributeValue("PIN_D3", "PWM");
            SetAttributeValue("PIN_D4", "OUTPUT");
            SetAttributeValue("PIN_D5", "PWM");
            SetAttributeValue("PIN_D6", "PWM");
            SetAttributeValue("PIN_D7", "OUTPUT");
            SetAttributeValue("PIN_D8", "OUTPUT");
            SetAttributeValue("PIN_D9", "PWM");
            SetAttributeValue("PIN_D10", "PWM");
            SetAttributeValue("PIN_D11", "PWM");
            SetAttributeValue("PIN_D12", "OUTPUT");
            SetAttributeValue("PIN_D13", "OUTPUT");

            SetAttributeValue("SERIAL_BAUDRATE", "57600");
            SetAttributeValue("SERIAL_PARITY", "None");
            SetAttributeValue("SERIAL_STOPBITS", "One");
            SetAttributeValue("SERIAL_DATABITS", "9");
        }


        private void UpdatePinPort()
        {
            m_bUpdatePinPort = true;

            if (m_pinAnaloglist != null)
                m_pinAnaloglist = null;
            if (m_pinDigitallist != null)
                m_pinDigitallist = null;

            m_pinAnaloglist = new List<PinItem>();
            m_pinDigitallist = new List<PinItem>();

            m_pinAnaloglist.Add(new PinItem { use = GetPinUse("PIN_USE_A0"), pinnum = 0, name = "A0", pinmode = GetPinMode("PIN_A0"), value = 0, gui_val = gui_val_A0, gui_chk = gui_chk_A0 });
            m_pinAnaloglist.Add(new PinItem { use = GetPinUse("PIN_USE_A1"), pinnum = 1, name = "A1", pinmode = GetPinMode("PIN_A1"), value = 0, gui_val = gui_val_A1, gui_chk = gui_chk_A1 });
            m_pinAnaloglist.Add(new PinItem { use = GetPinUse("PIN_USE_A2"), pinnum = 2, name = "A2", pinmode = GetPinMode("PIN_A2"), value = 0, gui_val = gui_val_A2, gui_chk = gui_chk_A2 });
            m_pinAnaloglist.Add(new PinItem { use = GetPinUse("PIN_USE_A3"), pinnum = 3, name = "A3", pinmode = GetPinMode("PIN_A3"), value = 0, gui_val = gui_val_A3, gui_chk = gui_chk_A3 });
            m_pinAnaloglist.Add(new PinItem { use = GetPinUse("PIN_USE_A4"), pinnum = 4, name = "A4", pinmode = GetPinMode("PIN_A4"), value = 0, gui_val = gui_val_A4, gui_chk = gui_chk_A4 });
            m_pinAnaloglist.Add(new PinItem { use = GetPinUse("PIN_USE_A5"), pinnum = 5, name = "A5", pinmode = GetPinMode("PIN_A5"), value = 0, gui_val = gui_val_A5, gui_chk = gui_chk_A5 });

            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D0"), pinnum = 0, name = "D0", pinmode = GetPinMode("PIN_D0"), value = 0, gui_val = null, gui_chk = null });
            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D1"), pinnum = 1, name = "D1", pinmode = GetPinMode("PIN_D1"), value = 0, gui_val = null, gui_chk = null });
            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D2"), pinnum = 2, name = "D2", pinmode = GetPinMode("PIN_D2"), value = 0, gui_val = gui_val_D2, gui_chk = gui_chk_D2 });                // INPUT OR OUTPUT
            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D3"), pinnum = 3, name = "D3", pinmode = GetPinMode("PIN_D3"), value = 0, gui_val = gui_val_D3, gui_chk = gui_chk_D3 });          // INPUT OR OUTPUT
            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D4"), pinnum = 4, name = "D4", pinmode = GetPinMode("PIN_D4"), value = 0, gui_val = gui_val_D4, gui_chk = gui_chk_D4 });                // INPUT OR OUTPUT
            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D5"), pinnum = 5, name = "D5", pinmode = GetPinMode("PIN_D5"), value = 0, gui_val = gui_val_D5, gui_chk = gui_chk_D5 });          // INPUT OR OUTPUT
            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D6"), pinnum = 6, name = "D6", pinmode = GetPinMode("PIN_D6"), value = 0, gui_val = gui_val_D6, gui_chk = gui_chk_D6 });          // INPUT OR OUTPUT
            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D7"), pinnum = 7, name = "D7", pinmode = GetPinMode("PIN_D7"), value = 0, gui_val = gui_val_D7, gui_chk = gui_chk_D7 });                // INPUT OR OUTPUT
            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D8"), pinnum = 8, name = "D8", pinmode = GetPinMode("PIN_D8"), value = 0, gui_val = gui_val_D8, gui_chk = gui_chk_D8 });                // INPUT OR OUTPUT
            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D9"), pinnum = 9, name = "D9", pinmode = GetPinMode("PIN_D9"), value = 0, gui_val = gui_val_D9, gui_chk = gui_chk_D9 });          // INPUT OR OUTPUT   I2C
            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D10"), pinnum = 10, name = "D10", pinmode = GetPinMode("PIN_D10"), value = 0, gui_val = gui_val_D10, gui_chk = gui_chk_D10 });          // INPUT OR OUTPUT  I2C
            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D11"), pinnum = 11, name = "D11", pinmode = GetPinMode("PIN_D11"), value = 0, gui_val = gui_val_D11, gui_chk = gui_chk_D11 });          // INPUT OR OUTPUT  I2C
            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D12"), pinnum = 12, name = "D12", pinmode = GetPinMode("PIN_D12"), value = 0, gui_val = gui_val_D12, gui_chk = gui_chk_D12 });                // INPUT OR OUTPUT  I2C
            m_pinDigitallist.Add(new PinItem { use = GetPinUse("PIN_USE_D13"), pinnum = 13, name = "D13", pinmode = GetPinMode("PIN_D13"), value = 0, gui_val = gui_val_D13, gui_chk = gui_chk_D13 });                // INPUT OR OUTPUT

            gui_pin_D2.Items.Insert(0, "INPUT");
            gui_pin_D2.Items.Insert(1, "OUTPUT");
            gui_pin_D2.SelectedItem = GetAttributeValue("PIN_D2");

            gui_pin_D3.Items.Insert(0, "INPUT");
            gui_pin_D3.Items.Insert(1, "OUTPUT");
            gui_pin_D3.Items.Insert(2, "PWM");
            gui_pin_D3.Items.Insert(3, "SERVO");
            gui_pin_D3.SelectedItem = GetAttributeValue("PIN_D3");

            gui_pin_D4.Items.Insert(0, "INPUT");
            gui_pin_D4.Items.Insert(1, "OUTPUT");
            gui_pin_D4.SelectedItem = GetAttributeValue("PIN_D4");

            gui_pin_D5.Items.Insert(0, "INPUT");
            gui_pin_D5.Items.Insert(1, "OUTPUT");
            gui_pin_D5.Items.Insert(2, "PWM");
            gui_pin_D5.Items.Insert(3, "SERVO");
            gui_pin_D5.SelectedItem = GetAttributeValue("PIN_D5");

            gui_pin_D6.Items.Insert(0, "INPUT");
            gui_pin_D6.Items.Insert(1, "OUTPUT");
            gui_pin_D6.Items.Insert(2, "PWM");
            gui_pin_D6.Items.Insert(3, "SERVO");
            gui_pin_D6.SelectedItem = GetAttributeValue("PIN_D6");

            gui_pin_D7.Items.Insert(0, "INPUT");
            gui_pin_D7.Items.Insert(1, "OUTPUT");
            gui_pin_D7.SelectedItem = GetAttributeValue("PIN_D7");

            gui_pin_D8.Items.Insert(0, "INPUT");
            gui_pin_D8.Items.Insert(1, "OUTPUT");
            gui_pin_D8.SelectedItem = GetAttributeValue("PIN_D8");

            gui_pin_D9.Items.Insert(0, "INPUT");
            gui_pin_D9.Items.Insert(1, "OUTPUT");
            gui_pin_D9.Items.Insert(2, "PWM");
            gui_pin_D9.Items.Insert(3, "I2C");
            gui_pin_D9.Items.Insert(4, "SERVO");
            gui_pin_D9.SelectedItem = GetAttributeValue("PIN_D9");

            gui_pin_D10.Items.Insert(0, "INPUT");
            gui_pin_D10.Items.Insert(1, "OUTPUT");
            gui_pin_D10.Items.Insert(2, "PWM");
            gui_pin_D10.Items.Insert(3, "I2C");
            gui_pin_D10.Items.Insert(4, "SERVO");
            gui_pin_D10.SelectedItem = GetAttributeValue("PIN_D10");

            gui_pin_D11.Items.Insert(0, "INPUT");
            gui_pin_D11.Items.Insert(1, "OUTPUT");
            gui_pin_D11.Items.Insert(2, "PWM");
            gui_pin_D11.Items.Insert(3, "I2C");
            gui_pin_D11.Items.Insert(4, "SERVO");
            gui_pin_D11.SelectedItem = GetAttributeValue("PIN_D11");

            gui_pin_D12.Items.Insert(0, "INPUT");
            gui_pin_D12.Items.Insert(1, "OUTPUT");
            gui_pin_D12.Items.Insert(2, "I2C");
            gui_pin_D12.SelectedItem = GetAttributeValue("PIN_D12");

            gui_pin_D13.Items.Insert(0, "INPUT");
            gui_pin_D13.Items.Insert(1, "OUTPUT");
            gui_pin_D13.SelectedItem = GetAttributeValue("PIN_D13");

            for (int i = 0; i < m_pinAnaloglist.Count; i++)
            {
                m_pinAnaloglist[i].gui_chk.IsChecked = m_pinAnaloglist[i].use;
            }
            for (int i = 0; i < m_pinDigitallist.Count; i++)
            {
                if (m_pinDigitallist[i].gui_chk != null)
                    m_pinDigitallist[i].gui_chk.IsChecked = m_pinDigitallist[i].use;
            }

            String baudrate = GetAttributeValue("SERIAL_BAUDRATE");
            if (String.IsNullOrEmpty(baudrate))
                gui_baud_rate.Text = "57600";

            gui_serial_config.Items.Insert(0,  "NONE");
            gui_serial_config.Items.Insert(1,  "SERIAL_5E1");
            gui_serial_config.Items.Insert(2,  "SERIAL_5E2");
            gui_serial_config.Items.Insert(3,  "SERIAL_5N1");
            gui_serial_config.Items.Insert(4,  "SERIAL_5N2");
            gui_serial_config.Items.Insert(5,  "SERIAL_5O1");
            gui_serial_config.Items.Insert(6,  "SERIAL_5O2");
            gui_serial_config.Items.Insert(7,  "SERIAL_6E1");
            gui_serial_config.Items.Insert(8,  "SERIAL_6E2");
            gui_serial_config.Items.Insert(9,  "SERIAL_6N1");
            gui_serial_config.Items.Insert(10, "SERIAL_6N2");
            gui_serial_config.Items.Insert(11, "SERIAL_6O1");
            gui_serial_config.Items.Insert(12, "SERIAL_6O2");
            gui_serial_config.Items.Insert(13, "SERIAL_7E1");
            gui_serial_config.Items.Insert(14, "SERIAL_7E2");
            gui_serial_config.Items.Insert(15, "SERIAL_7N1");
            gui_serial_config.Items.Insert(16, "SERIAL_7N2");
            gui_serial_config.Items.Insert(17, "SERIAL_7O1");
            gui_serial_config.Items.Insert(18, "SERIAL_7O2");
            gui_serial_config.Items.Insert(19, "SERIAL_8E1");
            gui_serial_config.Items.Insert(20, "SERIAL_8E2");
            gui_serial_config.Items.Insert(21, "SERIAL_8N1");
            gui_serial_config.Items.Insert(22, "SERIAL_8N2");
            gui_serial_config.Items.Insert(23, "SERIAL_8O1");
            gui_serial_config.Items.Insert(24, "SERIAL_8O2");

            String serialconfig = GetAttributeValue("SERIAL_CONFIG");
            if (String.IsNullOrEmpty(serialconfig))
                gui_serial_config.SelectedItem = "SERIAL_8N1";
            else
                gui_serial_config.SelectedItem = serialconfig;
            
            for (int i = 0; i < m_pinAnaloglist.Count; i++)
            {
                gui_analog_chart.Items.Insert(i, m_pinAnaloglist[i].name);
            }
            for (int i = 2; i < m_pinDigitallist.Count; i++)
            {
                gui_digital_chart.Items.Insert(i-2, m_pinDigitallist[i].name);
            }

            String autoconnect = GetAttributeValue("SERIAL_AUTOCONNECT");
            if (String.IsNullOrEmpty(autoconnect))
            {
                gui_chk_autoconnect.IsChecked = false;
            }
            else
            {
                if (autoconnect == "true")
                    gui_chk_autoconnect.IsChecked = true;
                else
                    gui_chk_autoconnect.IsChecked = false;
            }
            
            if (gui_chk_autoconnect.IsChecked.Value == true)
            {
                String pid = GetAttributeValue("SERIAL_PID");
                String vid = GetAttributeValue("SERIAL_VID");

                ConnectSerial(pid, vid);
            }

            String series = GetAttributeValue("CHART_SERIES1");
            if (!String.IsNullOrEmpty(series))
            {
                gui_analog_chart.SelectedItem = series;
            }

            series = GetAttributeValue("CHART_SERIES2");
            if (!String.IsNullOrEmpty(series))
            {
                gui_digital_chart.SelectedItem = series;
            }
            
            m_bUpdatePinPort = false;
        }

        private SerialConfig GetSerialConfig(String serialConfig)
        {
              if (serialConfig == "NONE")
                return SerialConfig.NONE;

            else if (serialConfig == "SERIAL_5E1")
                return SerialConfig.SERIAL_5E1;
            else if (serialConfig == "SERIAL_5E2")
                return SerialConfig.SERIAL_5E2;
            else if (serialConfig == "SERIAL_5N1")
                return SerialConfig.SERIAL_5N1;
            else if (serialConfig == "SERIAL_5N2")
                return SerialConfig.SERIAL_5N2;

            else if (serialConfig == "SERIAL_5O1")
                return SerialConfig.SERIAL_5O1;
            else if (serialConfig == "SERIAL_5O2")
                return SerialConfig.SERIAL_5O2;
            else if (serialConfig == "SERIAL_6E1")
                return SerialConfig.SERIAL_6E1;
            else if (serialConfig == "SERIAL_6E2")
                return SerialConfig.SERIAL_6E2;

            else if (serialConfig == "SERIAL_6N1")
                return SerialConfig.SERIAL_6N1;
            else if (serialConfig == "SERIAL_6N2")
                return SerialConfig.SERIAL_6N2;
            else if (serialConfig == "SERIAL_6O1")
                return SerialConfig.SERIAL_6O2;
            else if (serialConfig == "SERIAL_6O2")
                return SerialConfig.SERIAL_6O2;

            else if (serialConfig == "SERIAL_7E1")
                return SerialConfig.SERIAL_7E1;
            else if (serialConfig == "SERIAL_7E2")
                return SerialConfig.SERIAL_7E2;
            else if (serialConfig == "SERIAL_7N1")
                return SerialConfig.SERIAL_7N1;
            else if (serialConfig == "SERIAL_7N2")
                return SerialConfig.SERIAL_7N2;

            else if (serialConfig == "SERIAL_7O1")
                return SerialConfig.SERIAL_7O1;
            else if (serialConfig == "SERIAL_7O2")
                return SerialConfig.SERIAL_7O2;
            else if (serialConfig == "SERIAL_8E1")
                return SerialConfig.SERIAL_8E1;
            else if (serialConfig == "SERIAL_8E2")
                return SerialConfig.SERIAL_8E2;

            else if (serialConfig == "SERIAL_8N1")
                return SerialConfig.SERIAL_8N1;
            else if (serialConfig == "SERIAL_8N2")
                return SerialConfig.SERIAL_8N2;
            else if (serialConfig == "SERIAL_8O1")
                return SerialConfig.SERIAL_8O1;
            else if (serialConfig == "SERIAL_8O2")
                return SerialConfig.SERIAL_8O2;

            return SerialConfig.NONE;
        }



        private void gui_button_connect_click(object sender, RoutedEventArgs e)
        {
            var selection = gui_serial_device.SelectedItems;

            if (selection.Count <= 0)
            {
                gui_status.Text = "Select a device and connect";
                return;
            }

            DeviceInformation entry = (DeviceInformation)selection[0];

            try
            {
                m_ReadCancellationTokenSource = new CancellationTokenSource();

                string vid = "";
                string pid = "";
                String serialid = entry.Id;
                string[] result = serialid.Split('#');
                if (result.Count() == 4)
                {
                    if (result[0].Contains("USB") == true)
                    {
                        string[] serial = result[1].Split('&');
                        if (serial.Count() == 2)
                        {
                            vid = serial[0];
                            pid = serial[1];
                        }
                    }
                }

                if (String.IsNullOrEmpty(vid) && String.IsNullOrEmpty(pid))
                {
                    gui_status.Text = "시리얼 정보를 가져오지 못하였습니다.";
                    return;
                }

                ConnectSerial(vid, pid);
            }
            catch (Exception ex)
            {
                gui_status.Text = ex.Message;
                gui_button_connect.IsEnabled = true;
                gui_button_disconnect.IsEnabled = false;
            }
        }

        private void ConnectSerial(String vid, String pid)
        {
            m_usbSerial = new UsbSerial(vid, pid);
            m_arduino = new RemoteDevice(m_usbSerial);
            m_usbSerial.ConnectionEstablished += OnConnectionEstablished;

            m_arduino.DeviceReady += MyDeviceReadyCallback;

            m_arduino.AnalogPinUpdated += MyAnalogPinUpdateCallback;
            m_arduino.DigitalPinUpdated += MyDigitalPinUpdateCallback;

            uint iBaudRate = Convert.ToUInt32(gui_baud_rate.Text);
            m_usbSerial.begin(iBaudRate, GetSerialConfig(gui_serial_config.SelectedItem.ToString()));

            SetAttributeValue("SERIAL_BAUDRATE", gui_baud_rate.Text);
            SetAttributeValue("SERIAL_CONFIG", gui_serial_config.SelectedItem.ToString());

            SetAttributeValue("SERIAL_PID", pid);
            SetAttributeValue("SERIAL_VID", vid);

            gui_status.Text = "Waiting for data...";
        }

        private void MyDeviceReadyCallback()
        {
            for (int i = 0; i < m_pinAnaloglist.Count; i++)
            {
                if (m_pinAnaloglist[i].use)
                {
                    m_arduino.pinMode(m_pinAnaloglist[i].name, m_pinAnaloglist[i].pinmode);
                }
            }

            for (int i=0; i< m_pinDigitallist.Count; i++)
            {
                if (m_pinDigitallist[i].use)
                {
                    m_arduino.pinMode(m_pinDigitallist[i].pinnum, m_pinDigitallist[i].pinmode);
                }
            }

            //throw new NotImplementedException();
        }

        private void MyDigitalPinUpdateCallback(byte pin, PinState state)
        {
            if (state == PinState.HIGH)
                m_pinDigitallist[pin].value = 1;
            else
                m_pinDigitallist[pin].value = 0;
        }

        private void MyAnalogPinUpdateCallback(string pin, ushort value)
        {
            if (pin == "A0")
                m_pinAnaloglist[0].value = value;
            else if (pin == "A1")
                m_pinAnaloglist[1].value = value;
            else if (pin == "A2")
                m_pinAnaloglist[2].value = value;
            else if (pin == "A3")
                m_pinAnaloglist[3].value = value;
            else if (pin == "A4")
                m_pinAnaloglist[4].value = value;
            else if (pin == "A5")
                m_pinAnaloglist[5].value = value;

            // throw new NotImplementedException();
        }


        public async void SendServerData()
        {
            String url = m_server_ip + "setdevicevalue_getevent";

            // http://localhost:8080/setdevice_event?device_value={ “devicekey”:”B34333DDEFF”, “A1”:”10”, “A2”:”20.01” }
            String text = "";
            text = "{\"devicekey\":\"" + m_device_key + "\"";

            for (int i = 0; i < m_pinAnaloglist.Count; i++)
            {
                if (m_pinAnaloglist[i].use)
                {
                     text = text + ",\"" + m_pinAnaloglist[i].name + "\":\"" + m_pinAnaloglist[i].value.ToString() + "\"";
                }
            }
            for (int i = 0; i < m_pinDigitallist.Count; i++)
            {
                if (m_pinDigitallist[i].use)
                {
                     text = text + ",\"" + m_pinDigitallist[i].name + "\":\"" + m_pinDigitallist[i].value.ToString() + "\"";
                }
            }

            text = text + "}";

            var strParam = "device_event=" + text;
            String response = await getResponse(url, strParam);

            if (String.IsNullOrEmpty(response))
            {
                gui_status.Text = "Disconnected from enuSpace server.";
                return;
            }
            // 리턴 샘플 : {  "RESULT":"OK" , "RESULT_CODE":"RESULT_OK" , "EVENT":[{  "VARIABLE":"D12" , "SETVALUE":"1"  },{  "VARIABLE":"D13" , "SETVALUE":"1"  }]  }

            JsonValue jsonValue = JsonValue.Parse(response);
            String return_flag = jsonValue.GetObject().GetNamedString("RESULT");
            if (return_flag == "OK")
            {
                JsonArray event_json = jsonValue.GetObject().GetNamedArray("EVENT");

                foreach (var item in event_json)
                {
                    var itemObject = item.GetObject();
                    String variable = itemObject.GetNamedString("VARIABLE");
                    String setvalue = itemObject.GetNamedString("SETVALUE");

                    for (int i = 0; i < m_pinDigitallist.Count; i++)
                    {
                        if (m_pinDigitallist[i].name == variable)
                        {
                            if (m_pinDigitallist[i].use)
                            {
                                int iValue = Convert.ToInt32(setvalue);

                                if (m_pinDigitallist[i].pinmode == PinMode.PWM || m_pinDigitallist[i].pinmode == PinMode.I2C || m_pinDigitallist[i].pinmode == PinMode.SERVO)
                                {
                                    m_arduino.analogWrite(m_pinDigitallist[i].pinnum, (ushort)iValue);
                                }
                                else
                                {
                                    if (iValue >= 1)
                                        m_arduino.digitalWrite(m_pinDigitallist[i].pinnum, PinState.HIGH);
                                    else
                                        m_arduino.digitalWrite(m_pinDigitallist[i].pinnum, PinState.LOW);
                                }

                                switch (i)
                                {
                                    case 2:
                                        gui_set_val_D2.Text = setvalue;
                                        break;
                                    case 3:
                                        gui_set_val_D3.Text = setvalue;
                                        break;
                                    case 4:
                                        gui_set_val_D4.Text = setvalue;
                                        break;
                                    case 5:
                                        gui_set_val_D5.Text = setvalue;
                                        break;
                                    case 6:
                                        gui_set_val_D6.Text = setvalue;
                                        break;
                                    case 7:
                                        gui_set_val_D7.Text = setvalue;
                                        break;
                                    case 8:
                                        gui_set_val_D8.Text = setvalue;
                                        break;
                                    case 9:
                                        gui_set_val_D9.Text = setvalue;
                                        break;
                                    case 10:
                                        gui_set_val_D10.Text = setvalue;
                                        break;
                                    case 11:
                                        gui_set_val_D11.Text = setvalue;
                                        break;
                                    case 12:
                                        gui_set_val_D12.Text = setvalue;
                                        break;
                                    case 13:
                                        gui_set_val_D13.Text = setvalue;
                                        break;
                                    default:

                                        break;
                                }
                            }
                        }
                    }
                }
            }


            gui_status.Text = response;

            /*
            String url = m_server_ip + "setvalue_package";
            // var text = "{\"" + "@" + m_device_key + ".A0" + "\":\"" + iVal1.ToString() + "\",\"" + "@" + m_device_key + ".A1" + "\":\"" + iVal2.ToString() + "\",\"" + "@" + m_device_key + ".A2" + "\":\"" + iVal3.ToString() + "\",\"" + "@" + m_device_key + ".A3" + "\":\"" + iVal4.ToString() + "\"}";

            String text = "";
            bool bFirst = true;
            for (int i = 0; i < m_pinAnaloglist.Count; i++)
            {
                if (m_pinAnaloglist[i].use)
                {
                    if (bFirst == true)
                    {
                        text = "{\"" + "@" + m_device_key + "." + m_pinAnaloglist[i].name + "\":\"" + m_pinAnaloglist[i].value.ToString() + "\"";
                        bFirst = false;
                    }
                    else
                        text = text + ",\"" + "@" + m_device_key + "." + m_pinAnaloglist[i].name + "\":\"" + m_pinAnaloglist[i].value.ToString() + "\"";
                }
            }
            for (int i = 0; i < m_pinDigitallist.Count; i++)
            {
                if (m_pinDigitallist[i].use)
                {
                    if (bFirst == true)
                    {
                        text = "{\"" + "@" + m_device_key + "." + m_pinDigitallist[i].name + "\":\"" + m_pinDigitallist[i].value.ToString() + "\"";
                        bFirst = false;
                    }
                    else
                        text = text + ",\"" + "@" + m_device_key + "." + m_pinDigitallist[i].name + "\":\"" + m_pinDigitallist[i].value.ToString() + "\"";
                }
            }

            text = text + "}";

            var strParam = "tagid_list=" + text;
            String response = await getResponse(url, strParam);
            */
        }

        private void set_digital_value(int ipin, int iValue)
        {
            if (m_pinDigitallist[ipin].pinmode == PinMode.PWM || m_pinDigitallist[ipin].pinmode == PinMode.I2C || m_pinDigitallist[ipin].pinmode == PinMode.SERVO)
            {
                m_arduino.analogWrite(m_pinDigitallist[ipin].pinnum, (ushort)iValue);
            }
            else
            {
                if (iValue >= 1)
                    m_arduino.digitalWrite(m_pinDigitallist[ipin].pinnum, PinState.HIGH);
                else
                    m_arduino.digitalWrite(m_pinDigitallist[ipin].pinnum, PinState.LOW);
            }
        }
        private void gui_set_d2_click(object sender, RoutedEventArgs e)
        {
            int i = 2;
            String setvalue = gui_set_val_D2.Text;
            int iValue = Convert.ToInt32(setvalue);

            set_digital_value(i, iValue);
        }
        private void gui_set_d3_click(object sender, RoutedEventArgs e)
        {
            int i = 3;
            String setvalue = gui_set_val_D3.Text;
            int iValue = Convert.ToInt32(setvalue);

            set_digital_value(i, iValue);
        }
        private void gui_set_d4_click(object sender, RoutedEventArgs e)
        {
            int i = 4;
            String setvalue = gui_set_val_D4.Text;
            int iValue = Convert.ToInt32(setvalue);

            set_digital_value(i, iValue);
        }
        private void gui_set_d5_click(object sender, RoutedEventArgs e)
        {
            int i = 5;
            String setvalue = gui_set_val_D5.Text;
            int iValue = Convert.ToInt32(setvalue);

            set_digital_value(i, iValue);
        }
        private void gui_set_d6_click(object sender, RoutedEventArgs e)
        {
            int i = 6;
            String setvalue = gui_set_val_D6.Text;
            int iValue = Convert.ToInt32(setvalue);

            set_digital_value(i, iValue);
        }
        private void gui_set_d7_click(object sender, RoutedEventArgs e)
        {
            int i = 7;
            String setvalue = gui_set_val_D7.Text;
            int iValue = Convert.ToInt32(setvalue);

            set_digital_value(i, iValue);
        }
        private void gui_set_d8_click(object sender, RoutedEventArgs e)
        {
            int i = 8;
            String setvalue = gui_set_val_D8.Text;
            int iValue = Convert.ToInt32(setvalue);

            set_digital_value(i, iValue);
        }
        private void gui_set_d9_click(object sender, RoutedEventArgs e)
        {
            int i = 9;
            String setvalue = gui_set_val_D9.Text;
            int iValue = Convert.ToInt32(setvalue);

            set_digital_value(i, iValue);
        }
        private void gui_set_d10_click(object sender, RoutedEventArgs e)
        {
            int i = 10;
            String setvalue = gui_set_val_D10.Text;
            int iValue = Convert.ToInt32(setvalue);

            set_digital_value(i, iValue);
        }
        private void gui_set_d11_click(object sender, RoutedEventArgs e)
        {
            int i = 11;
            String setvalue = gui_set_val_D11.Text;
            int iValue = Convert.ToInt32(setvalue);

            set_digital_value(i, iValue);
        }
        private void gui_set_d12_click(object sender, RoutedEventArgs e)
        {
            int i = 12;
            String setvalue = gui_set_val_D12.Text;
            int iValue = Convert.ToInt32(setvalue);

            set_digital_value(i, iValue);
        }
        private void gui_set_d13_click(object sender, RoutedEventArgs e)
        {
            int i = 13;
            String setvalue = gui_set_val_D13.Text;
            int iValue = Convert.ToInt32(setvalue);

            set_digital_value(i, iValue);
        }




        private void gui_button_disconnect_click(object sender, RoutedEventArgs e)
        {
            try
            {
                gui_status.Text = "";
                CancelReadTask();
                CloseDevice();
                ListAvailablePorts();

                for (int i = 0; i < m_pinAnaloglist.Count; i++)
                {
                    m_pinAnaloglist[i].gui_chk.IsEnabled = true;
                }
                for (int i = 2; i < m_pinDigitallist.Count; i++)
                {
                    m_pinDigitallist[i].gui_chk.IsEnabled = true;
                }
                gui_pin_D2.IsEnabled = true;
                gui_pin_D3.IsEnabled = true;
                gui_pin_D4.IsEnabled = true;
                gui_pin_D5.IsEnabled = true;
                gui_pin_D6.IsEnabled = true;
                gui_pin_D7.IsEnabled = true;
                gui_pin_D8.IsEnabled = true;
                gui_pin_D9.IsEnabled = true;
                gui_pin_D10.IsEnabled = true;
                gui_pin_D11.IsEnabled = true;
                gui_pin_D12.IsEnabled = true;
                gui_pin_D13.IsEnabled = true;
            }
            catch (Exception ex)
            {
                gui_status.Text = ex.Message;
            }
        }

        private void CancelReadTask()
        {
            if (m_ReadCancellationTokenSource != null)
            {
                if (!m_ReadCancellationTokenSource.IsCancellationRequested)
                {
                    m_ReadCancellationTokenSource.Cancel();
                }
            }
        }

        private void CloseDevice()
        {
            if (m_usbSerial != null)
                m_usbSerial.Dispose();
            m_usbSerial = null;

            if (m_arduino != null)
                m_arduino.Dispose();
            m_arduino = null;

            gui_button_connect.IsEnabled = true;
            m_listOfDevices.Clear();
        }


        private void DrawChart(Canvas canGraph, List<NameValueItem>[] pList, double axisx_min, double axisx_max, double data_min, double data_max)
        {
            ///////////////////////////////////////////////////////////////////////////////////
            ///////////////////////////////////////////////////////////////////////////////////
            canGraph.Children.Clear();

            double[] RectWall;
            RectWall = new double[4];
            RectWall[0] = 0;                // left 
            RectWall[1] = 0;                // top
            RectWall[2] = canGraph.ActualWidth;   // right
            RectWall[3] = canGraph.ActualHeight;  // bottom

            double[] RectGap;
            RectGap = new double[4];
            RectGap[0] = 50;    // left 
            RectGap[1] = 20;    // top
            RectGap[2] = 20;    // right
            RectGap[3] = 20;    // bottom

            double[] RectChart;
            RectChart = new double[4];
            RectChart[0] = RectWall[0] + RectGap[0]; // left 
            RectChart[1] = RectWall[1] + RectGap[1]; // top
            RectChart[2] = RectWall[2] - RectGap[2]; // right
            RectChart[3] = RectWall[3] - RectGap[3]; // bottom

            double fChartWidth = RectChart[2] - RectChart[0];
            double fChartHeight = RectChart[3] - RectChart[1];

            int xGridNum = 5;
            int yGridNum = 5;
            
            double fGridGapX = fChartWidth / xGridNum;
            double fGridGapY = fChartHeight / yGridNum;

            // 차트 그리기.
            Rectangle pChartRect = new Rectangle();
            pChartRect.StrokeThickness = 1;
            pChartRect.Fill = new SolidColorBrush(Colors.Black);
            pChartRect.Stroke = new SolidColorBrush(Colors.White);
            pChartRect.Width = fChartWidth;
            pChartRect.Height = fChartHeight;
            Canvas.SetLeft(pChartRect, RectChart[0]);
            Canvas.SetTop(pChartRect, RectChart[1]);
            canGraph.Children.Add(pChartRect);

            // x축 그리기.
            int iCount = 0;
            GeometryGroup xaxis_geom = new GeometryGroup();
            for (double x = 0; x <= fChartWidth; x += fGridGapX)
            {
                LineGeometry xtick = new LineGeometry();
                xtick.StartPoint = new Point(x + RectChart[0], RectChart[3] + 5);
                xtick.EndPoint = new Point(x + RectChart[0], RectChart[1]);
                xaxis_geom.Children.Add(xtick);

                TextBlock xlabel = new TextBlock();
                int ivalue = (int)axisx_min + (int)(axisx_max - axisx_min) / xGridNum * iCount;
                xlabel.Text = ivalue.ToString();
                xlabel.FontSize = 10;
                xlabel.Foreground = new SolidColorBrush(Colors.White);
                Canvas.SetLeft(xlabel, x + RectChart[0]);
                Canvas.SetTop(xlabel, RectChart[3] + 5);
                canGraph.Children.Add(xlabel);
                iCount = iCount + 1;
            }

            Windows.UI.Xaml.Shapes.Path xaxis_path = new Windows.UI.Xaml.Shapes.Path();
            xaxis_path.StrokeThickness = 1;
            xaxis_path.Stroke = new SolidColorBrush(Colors.White);
            xaxis_path.Data = xaxis_geom;
            canGraph.Children.Add(xaxis_path);

            // y축 그리기.
            iCount = 0;
            GeometryGroup yxaxis_geom = new GeometryGroup();
            for (double y = 0; y <= fChartHeight; y += fGridGapY)
            {
                LineGeometry ytick = new LineGeometry();
                ytick.StartPoint = new Point(RectChart[0] - 5, RectChart[3] - y);
                ytick.EndPoint = new Point(RectChart[2], RectChart[3] - y);
                yxaxis_geom.Children.Add(ytick);

                TextBlock ylabel = new TextBlock();
                double ivalue = data_min + (data_max - data_min) / yGridNum * iCount;
                ylabel.Text = ivalue.ToString();
                ylabel.FontSize = 10;
                ylabel.Foreground = new SolidColorBrush(Colors.White);
                Canvas.SetLeft(ylabel, RectChart[0] - 30);
                Canvas.SetTop(ylabel, RectChart[3] - y - ylabel.FontSize);
                canGraph.Children.Add(ylabel);
                iCount = iCount + 1;
            }

            Windows.UI.Xaml.Shapes.Path yaxis_path = new Windows.UI.Xaml.Shapes.Path();
            yaxis_path.StrokeThickness = 1;
            yaxis_path.Stroke = new SolidColorBrush(Colors.White);
            yaxis_path.Data = yxaxis_geom;
            canGraph.Children.Add(yaxis_path);

            // data 그리기.
            double x1 = 0;
            double y1 = 0;
            double x2 = 0;
            double y2 = 0;

            int idim = 0;
            idim = pList.Length;
            if (idim > 0)
            {
                for (int i = 0; i < idim; i++)
                {
                    GeometryGroup data_geom = new GeometryGroup();

                    List<NameValueItem> newitems = pList[i];
                    double xstep = fChartWidth / newitems.Count;

                    for (int j = 0; j < newitems.Count - 1; j++)
                    {
                        LineGeometry vline = new LineGeometry();
                       
                        x1 = RectChart[0] + xstep * j;
                        y1 = RectChart[3] - fChartHeight * ((newitems[j].Value - data_min) / (data_max - data_min));
                        if (y1 < RectChart[1])
                            y1 = RectChart[1];
                        if (y1 > RectChart[3])
                            y1 = RectChart[3];
                        x2 = RectChart[0] + xstep * (j + 1);
                        y2 = RectChart[3] - fChartHeight * ((newitems[j + 1].Value - data_min) / (data_max - data_min));
                        if (y2 < RectChart[1])
                            y2 = RectChart[1];
                        if (y2 > RectChart[3])
                            y2 = RectChart[3];
                        vline.StartPoint = new Point(x1, y1);
                        vline.EndPoint = new Point(x2, y2);
                        data_geom.Children.Add(vline);
                    }

                    Windows.UI.Xaml.Shapes.Path value_path = new Windows.UI.Xaml.Shapes.Path();
                    value_path.StrokeThickness = 2;
                    if (i == 0)
                        value_path.Stroke = new SolidColorBrush(Colors.GreenYellow);
                    else if (i == 1)
                        value_path.Stroke = new SolidColorBrush(Colors.Yellow);
                    else if (i == 2)
                        value_path.Stroke = new SolidColorBrush(Colors.Blue);
                    else
                        value_path.Stroke = new SolidColorBrush(Colors.Red);
                    value_path.Data = data_geom;
                    canGraph.Children.Add(value_path);
                }
            }
        }

        private PinItem GetPinItem(String name)
        {
            int icount = m_pinAnaloglist.Count;
            for (int i = 0; i < icount; i++)
            {
                if (m_pinAnaloglist[i].name == name)
                    return m_pinAnaloglist[i];
            }

            icount = m_pinDigitallist.Count;
            for (int i = 0; i < icount; i++)
            {
                if (m_pinDigitallist[i].name == name)
                    return m_pinDigitallist[i];
            }

            return null;
        }

        private void gui_chk_Checked(object sender, RoutedEventArgs e)
        {
            if (m_bUpdatePinPort == false)
            {
                CheckBox chk = (CheckBox)sender;

                String param = "PIN_USE_" + chk.Content.ToString();
                if (chk.IsChecked.Value == true)
                    SetAttributeValue(param, "true");
                else
                    SetAttributeValue(param, "false");

                PinItem pItem = GetPinItem(chk.Content.ToString());
                if (pItem != null)
                    pItem.use = chk.IsChecked.Value;
            }
        }

        private void gui_pin_D2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_bUpdatePinPort == false)
            {
                String param = "PIN_D2";
                String pinMode = gui_pin_D2.SelectedItem.ToString();

                SetAttributeValue(param, pinMode);

                PinItem pItem = GetPinItem("D2");
                if (pItem != null)
                    pItem.pinmode = GetPinMode(param);
            }
        }

        private void gui_pin_D3_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_bUpdatePinPort == false)
            {
                String param = "PIN_D3";
                String pinMode = gui_pin_D3.SelectedItem.ToString();

                SetAttributeValue(param, pinMode);

                PinItem pItem = GetPinItem("D3");
                if (pItem != null)
                    pItem.pinmode = GetPinMode(param);
            }
        }

        private void gui_pin_D4_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_bUpdatePinPort == false)
            {
                String param = "PIN_D4";
                String pinMode = gui_pin_D4.SelectedItem.ToString();

                SetAttributeValue(param, pinMode);

                PinItem pItem = GetPinItem("D4");
                if (pItem != null)
                    pItem.pinmode = GetPinMode(param);
            }
        }

        private void gui_pin_D5_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_bUpdatePinPort == false)
            {
                String param = "PIN_D5";
                String pinMode = gui_pin_D5.SelectedItem.ToString();

                SetAttributeValue(param, pinMode);

                PinItem pItem = GetPinItem("D5");
                if (pItem != null)
                    pItem.pinmode = GetPinMode(param);
            }
        }

        private void gui_pin_D6_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_bUpdatePinPort == false)
            {
                String param = "PIN_D6";
                String pinMode = gui_pin_D6.SelectedItem.ToString();

                SetAttributeValue(param, pinMode);

                PinItem pItem = GetPinItem("D6");
                if (pItem != null)
                    pItem.pinmode = GetPinMode(param);
            }
        }

        private void gui_pin_D7_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_bUpdatePinPort == false)
            {
                String param = "PIN_D7";
                String pinMode = gui_pin_D7.SelectedItem.ToString();

                SetAttributeValue(param, pinMode);

                PinItem pItem = GetPinItem("D7");
                if (pItem != null)
                    pItem.pinmode = GetPinMode(param);
            }
        }

        private void gui_pin_D8_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_bUpdatePinPort == false)
            {
                String param = "PIN_D8";
                String pinMode = gui_pin_D8.SelectedItem.ToString();

                SetAttributeValue(param, pinMode);

                PinItem pItem = GetPinItem("D8");
                if (pItem != null)
                    pItem.pinmode = GetPinMode(param);
            }
        }

        private void gui_pin_D9_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_bUpdatePinPort == false)
            {
                String param = "PIN_D9";
                String pinMode = gui_pin_D9.SelectedItem.ToString();

                m_bUpdate_D9 = true;
                if (m_bUpdate_D10==false && m_bUpdate_D11 == false && m_bUpdate_D12 == false)
                {
                    if (pinMode == "I2C")
                    {
                        gui_pin_D10.SelectedItem = "I2C";
                        gui_pin_D11.SelectedItem = "I2C";
                        gui_pin_D12.SelectedItem = "I2C";
                    }
                    else
                    {
                        String oldValue = GetAttributeValue("PIN_D9");
                        if (oldValue == "I2C")
                        {
                            gui_pin_D10.SelectedItem = "INPUT";
                            gui_pin_D11.SelectedItem = "INPUT";
                            gui_pin_D12.SelectedItem = "INPUT";
                        }
                    }

                }
                m_bUpdate_D9 = false;

                SetAttributeValue(param, pinMode);

                PinItem pItem = GetPinItem("D9");
                if (pItem != null)
                    pItem.pinmode = GetPinMode(param);
            }
        }

        private void gui_pin_D10_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_bUpdatePinPort == false)
            {
                String param = "PIN_D10";
                String pinMode = gui_pin_D10.SelectedItem.ToString();

                m_bUpdate_D10 = true;
                if (m_bUpdate_D9 == false && m_bUpdate_D11 == false && m_bUpdate_D12 == false)
                {
                    if (pinMode == "I2C")
                    {
                        gui_pin_D9.SelectedItem = "I2C";
                        gui_pin_D11.SelectedItem = "I2C";
                        gui_pin_D12.SelectedItem = "I2C";
                    }
                    else
                    {
                        String oldValue = GetAttributeValue("PIN_D10");
                        if (oldValue == "I2C")
                        {
                            gui_pin_D9.SelectedItem = "INPUT";
                            gui_pin_D11.SelectedItem = "INPUT";
                            gui_pin_D12.SelectedItem = "INPUT";
                        }
                    }
                }
                m_bUpdate_D10 = false;
                    
                SetAttributeValue(param, pinMode);

                PinItem pItem = GetPinItem("D10");
                if (pItem != null)
                    pItem.pinmode = GetPinMode(param);
            }
        }

        private void gui_pin_D11_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_bUpdatePinPort == false)
            {
                String param = "PIN_D11";
                String pinMode = gui_pin_D11.SelectedItem.ToString();

                m_bUpdate_D11 = true;
                if (m_bUpdate_D9 == false && m_bUpdate_D10 == false && m_bUpdate_D12 == false)
                {
                    if (pinMode == "I2C")
                    {
                        gui_pin_D9.SelectedItem = "I2C";
                        gui_pin_D10.SelectedItem = "I2C";
                        gui_pin_D12.SelectedItem = "I2C";
                    }
                    else
                    {
                        String oldValue = GetAttributeValue("PIN_D11");
                        if (oldValue == "I2C")
                        {
                            gui_pin_D9.SelectedItem = "INPUT";
                            gui_pin_D10.SelectedItem = "INPUT";
                            gui_pin_D12.SelectedItem = "INPUT";
                        }
                    }
                }
                m_bUpdate_D11 = false;
                     
                SetAttributeValue(param, pinMode);

                PinItem pItem = GetPinItem("D11");
                if (pItem != null)
                    pItem.pinmode = GetPinMode(param);
            }
        }

        private void gui_pin_D12_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_bUpdatePinPort == false)
            {
                String param = "PIN_D12";
                String pinMode = gui_pin_D12.SelectedItem.ToString();

                m_bUpdate_D12 = true;
                if (m_bUpdate_D9 == false && m_bUpdate_D10 == false && m_bUpdate_D11 == false)
                {
                    if (pinMode == "I2C")
                    {
                        gui_pin_D9.SelectedItem = "I2C";
                        gui_pin_D10.SelectedItem = "I2C";
                        gui_pin_D11.SelectedItem = "I2C";
                    }
                    else
                    {
                        String oldValue = GetAttributeValue("PIN_D12");
                        if (oldValue == "I2C")
                        {
                            gui_pin_D9.SelectedItem = "INPUT";
                            gui_pin_D10.SelectedItem = "INPUT";
                            gui_pin_D11.SelectedItem = "INPUT";
                        }
                    }
                }
                m_bUpdate_D12 = false;
                
                SetAttributeValue(param, pinMode);

                PinItem pItem = GetPinItem("D12");
                if (pItem != null)
                    pItem.pinmode = GetPinMode(param);
            }
        }

        private void gui_pin_D13_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_bUpdatePinPort == false)
            {
                String param = "PIN_D13";
                String pinMode = gui_pin_D13.SelectedItem.ToString();

                SetAttributeValue(param, pinMode);

                PinItem pItem = GetPinItem("D13");
                if (pItem != null)
                    pItem.pinmode = GetPinMode(param);
            }
        }

        private void gui_analog_chart_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_newitems1 != null)
                m_newitems1 = null;

            String strAnalog = gui_analog_chart.SelectedItem.ToString();
            m_pAnalogItem = GetPinItem(strAnalog);

            SetAttributeValue("CHART_SERIES1", strAnalog);

            m_newitems1 = new List<NameValueItem>();

            for (int i=0; i<50; i++)
                m_newitems1.Add(new NameValueItem { X = 0, Value = 0 });
        }

        private void gui_digital_chart_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_newitems2 != null)
                m_newitems2 = null;

            String strDigital = gui_digital_chart.SelectedItem.ToString();
            m_pDigitalItem = GetPinItem(strDigital);

            SetAttributeValue("CHART_SERIES2", strDigital);

            m_newitems2 = new List<NameValueItem>();

            for (int i = 0; i < 50; i++)
                m_newitems2.Add(new NameValueItem { X = 0, Value = 0 });
        }
    }
}
