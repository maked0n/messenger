﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Messenger {
    class CModel {
        public enum EMessageType {
            Text,
            Image, 
            Video
        }
        public enum ERequestStatus {
            Ok,
            AuthError,
            NetworkError,
            InternalError
        }
        private Dictionary<string, CMessage> m_messages;
        private List<string> m_new_messages;
        private List<string> m_users;
        private CBackendController m_backend;
        private ERequestStatus m_user_request_status;
        private ERequestStatus m_login_request_status;
        private bool m_encryption;
        private bool m_login_probe;
        private UpdateUserListDelegate m_update_user_list_delegate;
        private GetMessageDocumentEnd m_get_message_document_end;
        private IncomingMessage m_incoming_message;
        public CModel(UpdateUserListDelegate upd_delegate, GetMessageDocumentEnd get_msg_doc_delegate,
            IncomingMessage incoming_message_delegate) {
            m_is_logged_in = false;
            m_messages = new Dictionary<string, CMessage>();
            m_new_messages = new List<string>();
            m_login_probe = false;
            m_update_user_list_delegate = upd_delegate;
            m_get_message_document_end = get_msg_doc_delegate;
            m_incoming_message = incoming_message_delegate;
        }
        public delegate void UpdateUserListDelegate(List<string> user_list);
        public delegate TextPointer GetMessageDocumentEnd();
        public delegate void IncomingMessage(string msg_id);
        public bool m_is_logged_in { get; set; }
        public string m_user_id { get; set; }
        public void Login(string user_id, string password, string server_address, ushort port, bool use_encryption) {
            m_user_id = user_id;
            m_encryption = use_encryption;
            m_backend = new CBackendController(server_address, port, 
                new CBackendController.RequestUsersCallBack(ProcessUserRequestStatus),
                new CBackendController.LoginRequestCallback(ProcessLoginRequestStatus),
                new CBackendController.MessageStatusChangeCallback(OnMessageStatusChange),
                new CBackendController.MessageReceivedCallback(OnMessageReceived));
            m_backend.Login(user_id, password, use_encryption);
        }
        public void RequestActiveUsers() {
            m_backend.RequestActiveUsers();
        }
        public string GetLoginError() {
            if (m_login_request_status == ERequestStatus.AuthError)
                return "Authorisation error";
            else if (m_login_request_status == ERequestStatus.InternalError)
                return "Internal error";
            else return "Network error";
        }
        private void _SetStatus(int status, ref ERequestStatus request_status) {
            switch (status) {
                case 0:         //Ok
                    request_status = ERequestStatus.Ok;
                    break;
                case 1:         //AuthError
                    request_status = ERequestStatus.AuthError;
                    break;
                case 2:         //NetworkError
                    request_status = ERequestStatus.NetworkError;
                    break;
                case 3:         //InternalError
                    request_status = ERequestStatus.InternalError;
                    break;
            }
        }
        public void ProcessLoginRequestStatus(int status) {
            _SetStatus(status, ref m_login_request_status);
            m_login_probe = true;
            if (m_login_request_status == ERequestStatus.Ok)
                m_is_logged_in = true;
        }
        public void ProcessUserRequestStatus(int status) {
            _SetStatus(status, ref m_user_request_status);
            if (m_user_request_status == ERequestStatus.Ok) {
                m_users = m_backend.GetUserList();
            }
            else if (m_user_request_status == ERequestStatus.AuthError) {
                m_users = new List<string>();
                m_users.Add("Authorisation error");
            }
            else if (m_user_request_status == ERequestStatus.InternalError) {
                m_users = new List<string>();
                m_users.Add("Internal error");
            }
            else {
                m_users = new List<string>();
                m_users.Add("Network error");
            }
            m_update_user_list_delegate(m_users);
        }
        public void OnMessageStatusChange(IntPtr msg_id, int status) {
            string key = Marshal.PtrToStringUni(msg_id);
            CMessage message = m_messages[key];
            switch (status) {
                case 0:         //Sending
                    message.UpdateRepresentation(EStatus.Sending);
                    break;
                case 1:         //Sent
                    message.UpdateRepresentation(EStatus.Sent);
                    break;
                case 2:         //Failed to send
                    message.UpdateRepresentation(EStatus.FailedToSend);
                    break;
                case 3:         //Delivered
                    message.UpdateRepresentation(EStatus.Delivered);
                    break;
                case 4:         //Seen
                    message.UpdateRepresentation(EStatus.Seen);
                    m_messages.Remove(key);
                    break;
            }
        }
        public void OnMessageReceived(IntPtr user_id, IntPtr msg_id, int time, int type, byte[] data) {
            string uid = Marshal.PtrToStringUni(user_id);
            string message_id = Marshal.PtrToStringUni(msg_id);
            EMessageType msg_type = EMessageType.Text;
            switch (type) {
                case 0:
                    msg_type = EMessageType.Text;
                    break;
                case 1:
                    msg_type = EMessageType.Image;
                    break;
                case 2:
                    msg_type = EMessageType.Video;
                    break;
            }
            DateTime msg_date = new DateTime(1970, 1, 1).ToLocalTime().AddSeconds(time);
            CMessage msg = new CMessage(m_user_id, uid, ref data, msg_type, EStatus.Incoming, m_get_message_document_end(), msg_date);
            m_incoming_message(message_id);
        }
        public bool WasLoginProbe() {
            return m_login_probe;
        }
        public void ResetLoginProbe() {
            m_login_probe = false;
        }
        public CMessage SendMessage(ref byte[] message, EMessageType message_type, TextPointer msg_pointer) {
            int type = 1;
            switch (message_type) {
                case EMessageType.Text:
                    type = 1;
                    break;
                case EMessageType.Image:
                    type = 2;
                    break;
                case EMessageType.Video:
                    type = 3;
                    break;
            }
            m_backend.SendMessage(m_user_id, ref message, m_encryption, type);
            string key = m_backend.GetLastMessageId();
            DateTime msg_date = m_backend.GetLastMessageDate();
            CMessage msg = new CMessage(m_user_id, m_user_id, ref message, message_type, EStatus.Sending, msg_pointer, msg_date);
            m_messages.Add(key, msg);
            return msg;
        }
        public void AddUnreadMessage(string id) {
            m_new_messages.Add(id);
        }
        public void CloseConnection() {
            if(m_is_logged_in) m_backend.Disconnect();
        }
        public CMessage GetMessageById(string id) {
            return m_messages[id];
        }
        public void SendMessageSeen(string id) {
            m_backend.SendMessageSeen(m_user_id, id);
        }
        public void AllMessagesSeen() {
            foreach (string id in m_new_messages) 
                SendMessageSeen(id);
            m_new_messages.Clear();
        }
    }
}
