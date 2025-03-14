﻿using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;


namespace WinDurango.UI.Dialogs
{
    public class NoticeDialog
    {
        private string _content;
        private string _title;
        private ContentDialog _messageDialog;

        public NoticeDialog(string content, string title = "Information")
        {
            _content = content;
            _title = title;
        }

        public async Task ShowAsync()
        {
            _messageDialog = new ContentDialog
            {
                Content = _content,
                Title = _title,
                CloseButtonText = "OK",
                XamlRoot = App.MainWindow.Content.XamlRoot
            };

            _ = await _messageDialog.ShowAsync();
        }

        public string Content
        {
            get => _content;
            set
            {
                _content = value;
                UpdateDialog();
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                UpdateDialog();
            }
        }

        private void UpdateDialog()
        {
            if (_messageDialog != null)
            {
                _messageDialog.Content = _content;
                _messageDialog.Title = _title;
            }
        }

        public void Remove()
        {
            _messageDialog = null;
        }
    }
}