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

using Windows.ApplicationModel;

using SQLite.Net.Attributes;
using System.Net;
using System.Threading.Tasks;
using Windows.Data.Json;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace enuSpace_IoT
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PageUserLogin : Page
    {
        MainPage m_pMainFrame = null;

        public PageUserLogin()
        {
            this.InitializeComponent();
        }

        public void SetParentFrame(MainPage pMainFrame)
        {
            m_pMainFrame = pMainFrame;

            String serverip = m_pMainFrame.GetAttributeValue("server-ip");
            if (String.IsNullOrEmpty(serverip))
                gui_server_ip.Text = "http://169.254.60.225:8080/";
            else
                gui_server_ip.Text = serverip;

            gui_login_in.Text = m_pMainFrame.GetAttributeValue("user-id");
            gui_login_pw.Password = m_pMainFrame.GetAttributeValue("user-pw");
            String value = m_pMainFrame.GetAttributeValue("auto-login");
            if (value == "true")
                gui_auto_login.IsChecked = true;
            else
                gui_auto_login.IsChecked = false;
        }
        

        private async Task<String> getResponse(String url, string data)
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
         
        private async void gui_user_login(object sender, RoutedEventArgs e)
        {
            try
            {
                String server_ip = gui_server_ip.Text;
                server_ip = server_ip.TrimEnd('/', '\'');
                server_ip = server_ip + "/";

                String url = server_ip + "login";
                String data = "userid=" + gui_login_in.Text + "&password=" + gui_login_pw.Password;

                String response = await getResponse(url, data);
                if (String.IsNullOrEmpty(response))
                {
                    gui_status.Text = "Could not connect to server.";
                    return;
                }
                JsonValue jsonValue = JsonValue.Parse(response);
                String return_flag = jsonValue.GetObject().GetNamedString("RESULT");
                if (return_flag == "OK")
                {
                    if (gui_auto_login.IsChecked == true)
                    {
                        m_pMainFrame.SetAttributeValue("auto-login", "true");
                        m_pMainFrame.SetAttributeValue("user-id", gui_login_in.Text);
                        m_pMainFrame.SetAttributeValue("user-pw", gui_login_pw.Password);
                        m_pMainFrame.SetAttributeValue("server-ip", gui_server_ip.Text);
                    }
                    else
                    {
                        m_pMainFrame.SetAttributeValue("user-id", gui_login_in.Text);
                        m_pMainFrame.SetAttributeValue("auto-login", "false");
                        m_pMainFrame.SetAttributeValue("server-ip", gui_server_ip.Text);
                        m_pMainFrame.DelAttributeValue("user-pw");
                    }

                    String strReg = m_pMainFrame.GetAttributeValue("device-register");
                    if (String.IsNullOrEmpty(strReg) || strReg == "false")
                    {
                        m_pMainFrame.GoDevicePage();
                    }
                    else
                    {
                        m_pMainFrame.GoMainPage();
                    }
                }
                else
                {
                    gui_status.Text = jsonValue.GetObject().GetNamedString("MESSAGE");
                }
            }
            catch (Exception ex)
            {
                gui_status.Text = ex.Message;
            }
        }

        private void gui_user_find_form(object sender, RoutedEventArgs e)
        {
            gui_enter.Visibility = Visibility.Collapsed;
            gui_login.Visibility = Visibility.Collapsed;
            gui_find.Visibility = Visibility.Visible;
        }

        private void gui_user_login_form(object sender, RoutedEventArgs e)
        {
            gui_enter.Visibility = Visibility.Collapsed;
            gui_login.Visibility = Visibility.Visible;
            gui_find.Visibility = Visibility.Collapsed;
        }

        private void gui_user_enter_form(object sender, RoutedEventArgs e)
        {
            gui_enter.Visibility = Visibility.Visible;
            gui_login.Visibility = Visibility.Collapsed;
            gui_find.Visibility = Visibility.Collapsed;

            String AgreeService = gui_server_ip.Text + "agree/service.htm";
            gui_agreeview_service.Navigate(new Uri(AgreeService));
        }

        private async void gui_user_enter(object sender, RoutedEventArgs e)
        {
            try
            {
                if (String.IsNullOrEmpty(gui_name.Text))
                {
                    gui_error.Text = "이름을 입력하여 주십시요.";
                    return;
                }
                if (String.IsNullOrEmpty(gui_email.Text))
                {
                    gui_error.Text = "이름을 입력하여 주십시요.";
                    return;
                }
                if (String.IsNullOrEmpty(gui_password.Password))
                {
                    gui_error.Text = "이름을 입력하여 주십시요.";
                    return;
                }
                if (String.IsNullOrEmpty(gui_password_confirm.Password))
                {
                    gui_error.Text = "이름을 입력하여 주십시요.";
                    return;
                }

                if (gui_password.Password != gui_password_confirm.Password)
                {
                    gui_error.Text = "비밀번호를 다시 확인하여 주십시요.";
                    return;
                }

                if (gui_service_agreement.IsChecked == false)
                {
                    gui_error.Text = "서비스 이용 약관에 동의하여 주십시요.";
                    return;
                }

                String server_ip = gui_server_ip.Text;

                String url = server_ip + "registeruser";
                String data = "userid=" + gui_email.Text + "&name=" + gui_name.Text + "&password=" + gui_password.Password;

                String response = await getResponse(url, data);
                if (String.IsNullOrEmpty(response))
                {
                    gui_status.Text = "Could not connect to server.";
                    return;
                }
                JsonValue jsonValue = JsonValue.Parse(response);
                String return_flag = jsonValue.GetObject().GetNamedString("RESULT");
                if (return_flag == "OK")
                {
                    gui_error.Text = "정상적으로 사용자가 등록되었습니다. 로그인 페이지로 이동하여 주십시요.";

                    if (gui_auto.IsChecked == true)
                    {
                        m_pMainFrame.SetAttributeValue("auto-login", "true");
                        m_pMainFrame.SetAttributeValue("user-id", gui_login_in.Text);
                        m_pMainFrame.SetAttributeValue("user-pw", gui_login_pw.Password);
                        m_pMainFrame.SetAttributeValue("server-ip", gui_server_ip.Text);
                    }
                    else
                    {
                        m_pMainFrame.SetAttributeValue("auto-login", "false");
                        m_pMainFrame.DelAttributeValue("user-pw");
                    }
                }
                else
                {
                    gui_error.Text = jsonValue.GetObject().GetNamedString("MESSAGE");
                }
            }
            catch (Exception ex)
            {
                gui_status.Text = ex.Message;
            }
        }
    }
}
