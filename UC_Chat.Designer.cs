namespace UBCS2_A
{
    partial class UC_Chat
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            txtUserName = new TextBox();
            txtChatInput = new TextBox();
            cbColor = new ComboBox();
            chkBold = new CheckBox();
            dgvChat = new DataGridView();
            ((System.ComponentModel.ISupportInitialize)dgvChat).BeginInit();
            SuspendLayout();
            // 
            // txtUserName
            // 
            txtUserName.Location = new Point(14, 12);
            txtUserName.Name = "txtUserName";
            txtUserName.Size = new Size(94, 23);
            txtUserName.TabIndex = 5;
            txtUserName.Text = "User Name";
            // 
            // txtChatInput
            // 
            txtChatInput.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            txtChatInput.Location = new Point(14, 595);
            txtChatInput.Name = "txtChatInput";
            txtChatInput.Size = new Size(218, 23);
            txtChatInput.TabIndex = 4;
            txtChatInput.KeyDown += txtChatInput_KeyDown;
            // 
            // cbColor
            // 
            cbColor.FormattingEnabled = true;
            cbColor.Items.AddRange(new object[] { "Black", "Blue", "Red", "Green", "Orange", "Purple" });
            cbColor.Location = new Point(114, 12);
            cbColor.Name = "cbColor";
            cbColor.Size = new Size(57, 23);
            cbColor.TabIndex = 7;
            // 
            // chkBold
            // 
            chkBold.AutoSize = true;
            chkBold.Location = new Point(177, 14);
            chkBold.Name = "chkBold";
            chkBold.Size = new Size(63, 19);
            chkBold.TabIndex = 8;
            chkBold.Text = "in đậm";
            chkBold.UseVisualStyleBackColor = true;
            // 
            // dgvChat
            // 
            dgvChat.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            dgvChat.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvChat.Location = new Point(14, 53);
            dgvChat.Name = "dgvChat";
            dgvChat.Size = new Size(218, 534);
            dgvChat.TabIndex = 9;
            // 
            // UC_Chat
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(dgvChat);
            Controls.Add(chkBold);
            Controls.Add(cbColor);
            Controls.Add(txtUserName);
            Controls.Add(txtChatInput);
            Name = "UC_Chat";
            Size = new Size(250, 631);
            ((System.ComponentModel.ISupportInitialize)dgvChat).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox txtUserName;
        private TextBox txtChatInput;
        private ComboBox cbColor;
        private CheckBox chkBold;
        private DataGridView dgvChat;
    }
}
