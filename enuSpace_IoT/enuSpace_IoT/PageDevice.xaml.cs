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

using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Data.Json;
using System.Threading.Tasks;

using System.Net;


using System;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System.Profile;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace enuSpace_IoT
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PageDevice : Page
    {

        const int DEV_TYPE_UNKNOWN = 0;
        const int DEV_TYPE_ARDUINO_UNO = 1;
        const int DEV_TYPE_ARDUINO_DUE = 2;
        const int DEV_TYPE_CUSTORM = 3;

        public class VariableItem
        {
            public string type { get; set; }
            public string variable { get; set; }
        }

        public class ListItem
        {
            public String Description { get; set; }
        }


        MainPage m_pMainFrame = null;

        public PageDevice()
        {
            this.InitializeComponent();

            gui_device_combo.Items.Insert(0, "Arduino uno");
            gui_device_combo.Items.Insert(1, "Arduino due");
            gui_device_combo.SelectedItem = "Arduino uno";

            gui_device_name.Text = GetHostName();
            // 디바이스 키를 이름으로 설정.
            gui_device_key.Text = gui_device_name.Text;
        }
        
        public void SetParentFrame(MainPage pMainFrame)
        {
            m_pMainFrame = pMainFrame;
        }

        private String GetHostName()
        {
            foreach (Windows.Networking.HostName name in Windows.Networking.Connectivity.NetworkInformation.GetHostNames())
            {
                if (Windows.Networking.HostNameType.DomainName == name.Type)
                {
                    return name.DisplayName;
                }
            }
            return "";
        }

        private async void gui_device_register(object sender, RoutedEventArgs e)
        {
            String selDevice = gui_device_combo.SelectedItem.ToString();
            int iDevice = DEV_TYPE_UNKNOWN;

            if (selDevice == "Arduino uno")
            {
                iDevice = DEV_TYPE_ARDUINO_UNO;
            }
            else if (selDevice == "Arduino due")
            {
                iDevice = DEV_TYPE_ARDUINO_DUE;
            }
            else
            {
                gui_status.Text = "Raspberry PI에 연결된 디바이스를 선택하여 주십시요.";
                return;
            }

            if (String.IsNullOrEmpty(gui_device_name.Text))
            {
                gui_status.Text = "디바이스 이름을 입력하여 주십시요.";
                return;
            }

            if (String.IsNullOrEmpty(gui_device_key.Text))
            {
                gui_status.Text = "디바이스 키값을 획득하지 못하였습니다.";
                return;
            }

            try
            {
                String serip = m_pMainFrame.GetAttributeValue("server-ip");
                String userid = m_pMainFrame.GetAttributeValue("user-id");

                String url = serip + "adddevice";

                String data = "userid=" + userid + "&devicename=" + gui_device_name.Text + "&devicekey=" + gui_device_key.Text + "&description=" + selDevice + ":" + gui_device_desc.Text;

                String response = await m_pMainFrame.getResponse(url, data);
                JsonValue jsonValue = JsonValue.Parse(response);
                String return_flag = jsonValue.GetObject().GetNamedString("RESULT");
                if (return_flag == "OK")
                {
                    m_pMainFrame.SetAttributeValue("device-type", selDevice);
                    m_pMainFrame.SetAttributeValue("device-key", gui_device_key.Text);
                    m_pMainFrame.SetAttributeValue("device-name", gui_device_name.Text);

                    gui_status.Text = "디바이스를 등록하였습니다.";
                    
                    String msg = gui_device_key.Text + ": Device Add.";
                    ListItem item = new ListItem { Description = msg };
                    gui_listBox.Items.Insert(0, item);
                    
                    bool bComplete = await AddDeviceVariable(iDevice);

                    if (bComplete)
                    {
                        m_pMainFrame.SetAttributeValue("device-register", "true");
                        m_pMainFrame.SetAttributeValue("device-key", gui_device_key.Text);
                        m_pMainFrame.SetAttributeValue("device-name", gui_device_name.Text);

                        gui_device.Visibility = Visibility.Collapsed;
                        gui_go_main.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        gui_status.Text = "변수등록을 완료하지 못하였습니다. 재등록 버튼을 이용하여 추가하여 주십시요.";
                        gui_regbutton.Visibility = Visibility.Collapsed;
                        gui_re_regbutton.Visibility = Visibility.Visible;
                    }   
                }
                else
                {
                    String msg = jsonValue.GetObject().GetNamedString("MESSAGE");
                    gui_status.Text = msg;

                    String result_code = jsonValue.GetObject().GetNamedString("RESULT_CODE");
                    if (result_code == "CODE_EXIST_DEVICEKEY")
                    {
                        m_pMainFrame.SetAttributeValue("device-register", "true");
                        m_pMainFrame.SetAttributeValue("device-key", gui_device_key.Text);
                        m_pMainFrame.SetAttributeValue("device-name", gui_device_name.Text);

                        gui_device.Visibility = Visibility.Collapsed;
                        gui_go_main.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                gui_status.Text = ex.Message;
            }
        }

        private void gui_device_re_register(object sender, RoutedEventArgs e)
        {

        }

        private void gui_go_mainpage(object sender, RoutedEventArgs e)
        {
            m_pMainFrame.GoMainPage();
            String reg = m_pMainFrame.GetAttributeValue("device-register");
            if (reg != "true")
                m_pMainFrame.m_device_key = gui_device_key.Text;
        }

        private async Task<bool> AddDeviceVariable(int iType)
        {
            bool bUpdateAll = true;

            DateTime currTime = DateTime.Now;
            String time = currTime.ToString("yyyy") + "/" + currTime.ToString("MM") + "/" + currTime.ToString("dd") + " " + currTime.ToString("HH:mm:ss");

            if (iType == DEV_TYPE_ARDUINO_UNO)
            {
                List<VariableItem> varlist = null;
                varlist = new List<VariableItem>();
                varlist.Add(new VariableItem { type = "int", variable = "A0" });
                varlist.Add(new VariableItem { type = "int", variable = "A1" });
                varlist.Add(new VariableItem { type = "int", variable = "A2" });
                varlist.Add(new VariableItem { type = "int", variable = "A3" });
                varlist.Add(new VariableItem { type = "int", variable = "A4" });
                varlist.Add(new VariableItem { type = "int", variable = "A5" });

                varlist.Add(new VariableItem { type = "int", variable = "D0" });
                varlist.Add(new VariableItem { type = "int", variable = "D1" });
                varlist.Add(new VariableItem { type = "int", variable = "D2" });
                varlist.Add(new VariableItem { type = "int", variable = "D3" });
                varlist.Add(new VariableItem { type = "int", variable = "D4" });
                varlist.Add(new VariableItem { type = "int", variable = "D5" });
                varlist.Add(new VariableItem { type = "int", variable = "D6" });
                varlist.Add(new VariableItem { type = "int", variable = "D7" });
                varlist.Add(new VariableItem { type = "int", variable = "D8" });
                varlist.Add(new VariableItem { type = "int", variable = "D9" });
                varlist.Add(new VariableItem { type = "int", variable = "D10" });
                varlist.Add(new VariableItem { type = "int", variable = "D11" });
                varlist.Add(new VariableItem { type = "int", variable = "D12" });
                varlist.Add(new VariableItem { type = "int", variable = "D13" });

                for (int i=0; i< varlist.Count; i++)
                {
                    bool bUpdate = await AddVariable(varlist[i].variable, varlist[i].type, time);
                    if (bUpdate == false)
                    {
                        bUpdateAll = false;
                    }
                }

                if (bUpdateAll == false)
                {
                    gui_status.Text = "정상적으로 변수를 업데이트하지 못하였습니다.";
                }
            }
            return bUpdateAll;
        }

        private async Task<bool> AddVariable(String variable, String type, String time)
        {
            String serip = m_pMainFrame.GetAttributeValue("server-ip");
            String userid = m_pMainFrame.GetAttributeValue("user-id");
            String device_type = m_pMainFrame.GetAttributeValue("device-type");
            String device_key = m_pMainFrame.GetAttributeValue("device-key");
            String device_name = m_pMainFrame.GetAttributeValue("device-name");

            String url = serip + "addvariable";
            String data = "devicekey=" + device_key + "&mode=" + device_type + "&type=" + type + "&variable=" + variable + "&initvalue=0" + "&history=true" + "&description=" + time;

            String response = await m_pMainFrame.getResponse(url, data);
            JsonValue jsonValue = JsonValue.Parse(response);
            String return_flag = jsonValue.GetObject().GetNamedString("RESULT");
            if (return_flag == "OK")
            {
                String msg = type + " @" + gui_device_key.Text + "." + variable + " TagID 등록.";
                ListItem item = new ListItem { Description = msg};
                gui_listBox.Items.Insert(0, item);
                return true;
            }
            else
            {
                gui_status.Text = jsonValue.GetObject().GetNamedString("MESSAGE");
                return false;
            }
        }
    }
}
