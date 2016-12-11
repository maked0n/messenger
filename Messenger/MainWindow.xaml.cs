﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Messenger {
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private CModel m_model;
        private const long MAX_FILE_SIZE = 209715200; //200 Mb
        public MainWindow() {
            InitializeComponent();
            m_model = new CModel();
        }

        private void LoginWindow(out string user_id, out string password, out string server_address, out ushort port, out bool use_encryption) {
            LoginWindow window = new LoginWindow();
            window.ShowDialog();
            user_id = window.user_id;
            password = window.password;
            server_address = window.server_address;
            port = window.port;
            use_encryption = window.encryption_enabled;
        }
        private void Send_Click(object sender, RoutedEventArgs e) {
            if (!m_model.m_is_logged_in) {
                string user_id, password, server_address;
                ushort port;
                bool use_encryption;
                LoginWindow(out user_id, out password, out server_address, out port, out use_encryption);
                m_model.Login(user_id, password, server_address, port, use_encryption);
                if(m_model.m_is_logged_in) {
                    MessengerWindow.login_button.Content = "Send";
                    MessengerWindow.send_file_button.IsEnabled = true;
                }
            }
            else {
                byte[] message = System.Text.Encoding.UTF8.GetBytes(this.message_input_textbox.Text);
                m_model.SendMessage(ref message, CModel.EMessageType.Text);
            }
        }

        private void Attach_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.InitialDirectory = "c:\\";
            fileDialog.Filter = "Image Files(*.bmp;*.jpg;*.gif)|*.bmp;*.jpg;*.gif|Video files(*.avi, *.mkv, *.mp4)|*.avi;*.mkv;*.mp4";
            fileDialog.FilterIndex = 1;
            bool? res = fileDialog.ShowDialog();
            if (res == true) {
                string filename = fileDialog.FileName;
                long filesize = new System.IO.FileInfo(filename).Length;
                if (filesize > MAX_FILE_SIZE) {
                    MessageBox.Show("Max file size is 200MB", "Too large file", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                Char delimiter = '.';
                var string_list = filename.Split(delimiter);
                string filetype = string_list[string_list.Length - 1];
                CModel.EMessageType message_type = CModel.EMessageType.Image;
                if (filename == "avi" || filename == "mkv" || filename == "mp4")
                    message_type = CModel.EMessageType.Video;
                byte[] file_content = File.ReadAllBytes(filename);
                m_model.SendMessage(ref file_content, message_type);
            }
        }
    }
}