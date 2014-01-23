﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace UserInterface.Views
{
    interface IMemoView
    {
        event EventHandler<EditorArgs> MemoUpdate;
    }

    /// <summary>
    /// The Presenter for a Memo component.
    /// </summary>
    public partial class MemoView : UserControl, IMemoView
    {
        public event EventHandler<EditorArgs> MemoUpdate;

        public MemoView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Set or get the text of the richedit
        /// </summary>
        public string MemoText
        {
            get { return richTextBox1.Text; }
            set { richTextBox1.Text = value; }
        }

        /// <summary>
        /// The memo has been updated and now send the changed text to the model.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void richTextBox1_Leave(object sender, EventArgs e)
        {
            if (MemoUpdate != null)
            {
                EditorArgs args = new EditorArgs();
                args.TextString = richTextBox1.Text;
                MemoUpdate(this, args);
            }
        }

    }

    /// <summary>
    /// Event arg returned from the view
    /// </summary>
    public class EditorArgs : EventArgs
    {
        public string TextString;
    }
}