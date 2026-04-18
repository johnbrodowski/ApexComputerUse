namespace WinFormsApplication
{
    partial class TortureTestForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            menuStrip1 = new System.Windows.Forms.MenuStrip();
            fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStrip1 = new System.Windows.Forms.ToolStrip();
            toolStripButtonNew = new System.Windows.Forms.ToolStripButton();
            toolStripButtonOpen = new System.Windows.Forms.ToolStripButton();
            statusStrip1 = new System.Windows.Forms.StatusStrip();
            toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            tabControl1 = new System.Windows.Forms.TabControl();
            tabPageInputs = new System.Windows.Forms.TabPage();
            label1 = new System.Windows.Forms.Label();
            textBox1 = new System.Windows.Forms.TextBox();
            maskedTextBox1 = new System.Windows.Forms.MaskedTextBox();
            richTextBox1 = new System.Windows.Forms.RichTextBox();
            comboBox1 = new System.Windows.Forms.ComboBox();
            checkedListBox1 = new System.Windows.Forms.CheckedListBox();
            listBox1 = new System.Windows.Forms.ListBox();
            checkBox1 = new System.Windows.Forms.CheckBox();
            radioButton1 = new System.Windows.Forms.RadioButton();
            radioButton2 = new System.Windows.Forms.RadioButton();
            linkLabel1 = new System.Windows.Forms.LinkLabel();
            tabPageData = new System.Windows.Forms.TabPage();
            dataGridView1 = new System.Windows.Forms.DataGridView();
            listView1 = new System.Windows.Forms.ListView();
            columnHeader1 = new System.Windows.Forms.ColumnHeader();
            columnHeader2 = new System.Windows.Forms.ColumnHeader();
            treeView1 = new System.Windows.Forms.TreeView();
            imageList1 = new System.Windows.Forms.ImageList(components);
            tabPageRange = new System.Windows.Forms.TabPage();
            progressBar1 = new System.Windows.Forms.ProgressBar();
            trackBar1 = new System.Windows.Forms.TrackBar();
            hScrollBar1 = new System.Windows.Forms.HScrollBar();
            vScrollBar1 = new System.Windows.Forms.VScrollBar();
            numericUpDown1 = new System.Windows.Forms.NumericUpDown();
            domainUpDown1 = new System.Windows.Forms.DomainUpDown();
            tabPageDate = new System.Windows.Forms.TabPage();
            dateTimePicker1 = new System.Windows.Forms.DateTimePicker();
            monthCalendar1 = new System.Windows.Forms.MonthCalendar();
            tabPageContainers = new System.Windows.Forms.TabPage();
            groupBox1 = new System.Windows.Forms.GroupBox();
            panel1 = new System.Windows.Forms.Panel();
            flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            pictureBox1 = new System.Windows.Forms.PictureBox();
            button1 = new System.Windows.Forms.Button();
            button2 = new System.Windows.Forms.Button();
            button3 = new System.Windows.Forms.Button();
            button4 = new System.Windows.Forms.Button();
            button5 = new System.Windows.Forms.Button();
            toolTip1 = new System.Windows.Forms.ToolTip(components);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackBar1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown1).BeginInit();
            groupBox1.SuspendLayout();
            panel1.SuspendLayout();
            flowLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { fileToolStripMenuItem });
            menuStrip1.Location = new System.Drawing.Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new System.Drawing.Size(1184, 24);
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { exitToolStripMenuItem });
            fileToolStripMenuItem.Text = "File";
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.Click += ExitMenuItem_Click;
            // 
            // toolStrip1
            // 
            toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStripButtonNew, toolStripButtonOpen });
            toolStrip1.Location = new System.Drawing.Point(0, 24);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new System.Drawing.Size(1184, 25);
            // 
            // toolStripButtonNew
            // 
            toolStripButtonNew.Text = "New";
            // 
            // toolStripButtonOpen
            // 
            toolStripButtonOpen.Text = "Open";
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStripStatusLabel1 });
            statusStrip1.Location = new System.Drawing.Point(0, 739);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new System.Drawing.Size(1184, 22);
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.Text = "Ready";
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPageInputs);
            tabControl1.Controls.Add(tabPageData);
            tabControl1.Controls.Add(tabPageRange);
            tabControl1.Controls.Add(tabPageDate);
            tabControl1.Controls.Add(tabPageContainers);
            tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            tabControl1.Location = new System.Drawing.Point(0, 49);
            tabControl1.Name = "tabControl1";
            tabControl1.Size = new System.Drawing.Size(1184, 690);
            // 
            // tabPageInputs
            // 
            tabPageInputs.Controls.Add(label1);
            tabPageInputs.Controls.Add(textBox1);
            tabPageInputs.Controls.Add(maskedTextBox1);
            tabPageInputs.Controls.Add(richTextBox1);
            tabPageInputs.Controls.Add(comboBox1);
            tabPageInputs.Controls.Add(checkedListBox1);
            tabPageInputs.Controls.Add(listBox1);
            tabPageInputs.Controls.Add(checkBox1);
            tabPageInputs.Controls.Add(radioButton1);
            tabPageInputs.Controls.Add(radioButton2);
            tabPageInputs.Controls.Add(linkLabel1);
            tabPageInputs.Location = new System.Drawing.Point(4, 24);
            tabPageInputs.Name = "tabPageInputs";
            tabPageInputs.Padding = new System.Windows.Forms.Padding(8);
            tabPageInputs.Size = new System.Drawing.Size(1176, 662);
            tabPageInputs.Text = "Inputs";
            tabPageInputs.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(12, 16);
            label1.Text = "Label";
            // 
            // textBox1
            // 
            textBox1.Location = new System.Drawing.Point(12, 40);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(250, 23);
            textBox1.Text = "TextBox";
            // 
            // maskedTextBox1
            // 
            maskedTextBox1.Location = new System.Drawing.Point(12, 72);
            maskedTextBox1.Mask = "(999) 000-0000";
            maskedTextBox1.Name = "maskedTextBox1";
            maskedTextBox1.Size = new System.Drawing.Size(250, 23);
            // 
            // richTextBox1
            // 
            richTextBox1.Location = new System.Drawing.Point(12, 104);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new System.Drawing.Size(350, 120);
            richTextBox1.Text = "RichTextBox";
            // 
            // comboBox1
            // 
            comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            comboBox1.FormattingEnabled = true;
            comboBox1.Items.AddRange(new object[] { "Item 1", "Item 2", "Item 3" });
            comboBox1.Location = new System.Drawing.Point(12, 236);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new System.Drawing.Size(250, 23);
            comboBox1.SelectedIndex = 0;
            // 
            // checkedListBox1
            // 
            checkedListBox1.Items.AddRange(new object[] { "Checked 1", "Checked 2", "Checked 3" });
            checkedListBox1.Location = new System.Drawing.Point(12, 270);
            checkedListBox1.Name = "checkedListBox1";
            checkedListBox1.Size = new System.Drawing.Size(250, 94);
            // 
            // listBox1
            // 
            listBox1.Items.AddRange(new object[] { "List 1", "List 2", "List 3" });
            listBox1.Location = new System.Drawing.Point(280, 236);
            listBox1.Name = "listBox1";
            listBox1.Size = new System.Drawing.Size(180, 94);
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Location = new System.Drawing.Point(280, 340);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new System.Drawing.Size(81, 19);
            checkBox1.Text = "CheckBox";
            // 
            // radioButton1
            // 
            radioButton1.AutoSize = true;
            radioButton1.Location = new System.Drawing.Point(280, 365);
            radioButton1.Name = "radioButton1";
            radioButton1.Size = new System.Drawing.Size(99, 19);
            radioButton1.Text = "RadioButton 1";
            // 
            // radioButton2
            // 
            radioButton2.AutoSize = true;
            radioButton2.Location = new System.Drawing.Point(280, 390);
            radioButton2.Name = "radioButton2";
            radioButton2.Size = new System.Drawing.Size(99, 19);
            radioButton2.Text = "RadioButton 2";
            // 
            // linkLabel1
            // 
            linkLabel1.AutoSize = true;
            linkLabel1.Location = new System.Drawing.Point(280, 420);
            linkLabel1.Name = "linkLabel1";
            linkLabel1.Size = new System.Drawing.Size(62, 15);
            linkLabel1.TabStop = true;
            linkLabel1.Text = "LinkLabel";
            // 
            // tabPageData
            // 
            tabPageData.Controls.Add(dataGridView1);
            tabPageData.Controls.Add(listView1);
            tabPageData.Controls.Add(treeView1);
            tabPageData.Location = new System.Drawing.Point(4, 24);
            tabPageData.Name = "tabPageData";
            tabPageData.Padding = new System.Windows.Forms.Padding(8);
            tabPageData.Size = new System.Drawing.Size(1176, 662);
            tabPageData.Text = "Data";
            tabPageData.UseVisualStyleBackColor = true;
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.Add("Col1", "Column 1");
            dataGridView1.Columns.Add("Col2", "Column 2");
            dataGridView1.Rows.Add("A", "1");
            dataGridView1.Rows.Add("B", "2");
            dataGridView1.Location = new System.Drawing.Point(12, 12);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.Size = new System.Drawing.Size(420, 220);
            // 
            // listView1
            // 
            listView1.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { columnHeader1, columnHeader2 });
            listView1.Items.Add(new System.Windows.Forms.ListViewItem(new[] { "Item 1", "Value 1" }));
            listView1.Items.Add(new System.Windows.Forms.ListViewItem(new[] { "Item 2", "Value 2" }));
            listView1.Location = new System.Drawing.Point(12, 246);
            listView1.Name = "listView1";
            listView1.Size = new System.Drawing.Size(420, 180);
            listView1.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            columnHeader1.Text = "Name";
            columnHeader1.Width = 180;
            // 
            // columnHeader2
            // 
            columnHeader2.Text = "Value";
            columnHeader2.Width = 180;
            // 
            // treeView1
            // 
            treeView1.Location = new System.Drawing.Point(450, 12);
            treeView1.Name = "treeView1";
            treeView1.Size = new System.Drawing.Size(300, 414);
            treeView1.Nodes.Add("Root").Nodes.Add("Child");
            // 
            // tabPageRange
            // 
            tabPageRange.Controls.Add(progressBar1);
            tabPageRange.Controls.Add(trackBar1);
            tabPageRange.Controls.Add(hScrollBar1);
            tabPageRange.Controls.Add(vScrollBar1);
            tabPageRange.Controls.Add(numericUpDown1);
            tabPageRange.Controls.Add(domainUpDown1);
            tabPageRange.Location = new System.Drawing.Point(4, 24);
            tabPageRange.Name = "tabPageRange";
            tabPageRange.Padding = new System.Windows.Forms.Padding(8);
            tabPageRange.Size = new System.Drawing.Size(1176, 662);
            tabPageRange.Text = "Range";
            tabPageRange.UseVisualStyleBackColor = true;
            // 
            // progressBar1
            // 
            progressBar1.Location = new System.Drawing.Point(12, 20);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new System.Drawing.Size(300, 23);
            progressBar1.Value = 55;
            // 
            // trackBar1
            // 
            trackBar1.Location = new System.Drawing.Point(12, 55);
            trackBar1.Maximum = 10;
            trackBar1.Name = "trackBar1";
            trackBar1.Size = new System.Drawing.Size(300, 45);
            trackBar1.Value = 5;
            // 
            // hScrollBar1
            // 
            hScrollBar1.Location = new System.Drawing.Point(12, 110);
            hScrollBar1.Name = "hScrollBar1";
            hScrollBar1.Size = new System.Drawing.Size(300, 18);
            // 
            // vScrollBar1
            // 
            vScrollBar1.Location = new System.Drawing.Point(330, 20);
            vScrollBar1.Name = "vScrollBar1";
            vScrollBar1.Size = new System.Drawing.Size(17, 120);
            // 
            // numericUpDown1
            // 
            numericUpDown1.Location = new System.Drawing.Point(12, 150);
            numericUpDown1.Name = "numericUpDown1";
            numericUpDown1.Size = new System.Drawing.Size(120, 23);
            // 
            // domainUpDown1
            // 
            domainUpDown1.Items.Add("Alpha");
            domainUpDown1.Items.Add("Beta");
            domainUpDown1.Items.Add("Gamma");
            domainUpDown1.Location = new System.Drawing.Point(150, 150);
            domainUpDown1.Name = "domainUpDown1";
            domainUpDown1.Size = new System.Drawing.Size(120, 23);
            domainUpDown1.Text = "Alpha";
            // 
            // tabPageDate
            // 
            tabPageDate.Controls.Add(dateTimePicker1);
            tabPageDate.Controls.Add(monthCalendar1);
            tabPageDate.Location = new System.Drawing.Point(4, 24);
            tabPageDate.Name = "tabPageDate";
            tabPageDate.Padding = new System.Windows.Forms.Padding(8);
            tabPageDate.Size = new System.Drawing.Size(1176, 662);
            tabPageDate.Text = "Date";
            tabPageDate.UseVisualStyleBackColor = true;
            // 
            // dateTimePicker1
            // 
            dateTimePicker1.Location = new System.Drawing.Point(12, 18);
            dateTimePicker1.Name = "dateTimePicker1";
            dateTimePicker1.Size = new System.Drawing.Size(260, 23);
            // 
            // monthCalendar1
            // 
            monthCalendar1.Location = new System.Drawing.Point(12, 55);
            monthCalendar1.Name = "monthCalendar1";
            // 
            // tabPageContainers
            // 
            tabPageContainers.Controls.Add(groupBox1);
            tabPageContainers.Controls.Add(flowLayoutPanel1);
            tabPageContainers.Location = new System.Drawing.Point(4, 24);
            tabPageContainers.Name = "tabPageContainers";
            tabPageContainers.Padding = new System.Windows.Forms.Padding(8);
            tabPageContainers.Size = new System.Drawing.Size(1176, 662);
            tabPageContainers.Text = "Containers & Dialogs";
            tabPageContainers.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(panel1);
            groupBox1.Location = new System.Drawing.Point(12, 12);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new System.Drawing.Size(420, 250);
            groupBox1.Text = "Panel";
            // 
            // panel1
            // 
            panel1.AutoScroll = true;
            panel1.Controls.Add(pictureBox1);
            panel1.Location = new System.Drawing.Point(12, 22);
            panel1.Name = "panel1";
            panel1.Size = new System.Drawing.Size(390, 210);
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Controls.Add(button1);
            flowLayoutPanel1.Controls.Add(button2);
            flowLayoutPanel1.Controls.Add(button3);
            flowLayoutPanel1.Controls.Add(button4);
            flowLayoutPanel1.Controls.Add(button5);
            flowLayoutPanel1.Location = new System.Drawing.Point(12, 278);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new System.Drawing.Size(600, 50);
            // 
            // pictureBox1
            // 
            pictureBox1.BackColor = System.Drawing.Color.LightBlue;
            pictureBox1.Location = new System.Drawing.Point(0, 0);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new System.Drawing.Size(700, 500);
            // 
            // button1
            // 
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(100, 30);
            button1.Text = "Open File";
            button1.Click += OpenFileButton_Click;
            // 
            // button2
            // 
            button2.Name = "button2";
            button2.Size = new System.Drawing.Size(100, 30);
            button2.Text = "Save File";
            button2.Click += SaveFileButton_Click;
            // 
            // button3
            // 
            button3.Name = "button3";
            button3.Size = new System.Drawing.Size(100, 30);
            button3.Text = "Pick Color";
            button3.Click += PickColorButton_Click;
            // 
            // button4
            // 
            button4.Name = "button4";
            button4.Size = new System.Drawing.Size(100, 30);
            button4.Text = "Pick Font";
            button4.Click += PickFontButton_Click;
            // 
            // button5
            // 
            button5.Name = "button5";
            button5.Size = new System.Drawing.Size(130, 30);
            button5.Text = "Message Box";
            button5.Click += MessageButton_Click;
            // 
            // toolTip1
            // 
            toolTip1.SetToolTip(textBox1, "Textbox control");
            toolTip1.SetToolTip(trackBar1, "TrackBar control");
            // 
            // TortureTestForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1184, 761);
            Controls.Add(tabControl1);
            Controls.Add(toolStrip1);
            Controls.Add(statusStrip1);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            MinimumSize = new System.Drawing.Size(1000, 700);
            Name = "TortureTestForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "ApexUIBridge Test Application - WinForms Torture Test";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackBar1).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown1).EndInit();
            groupBox1.ResumeLayout(false);
            panel1.ResumeLayout(false);
            flowLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton toolStripButtonNew;
        private System.Windows.Forms.ToolStripButton toolStripButtonOpen;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPageInputs;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.MaskedTextBox maskedTextBox1;
        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.CheckedListBox checkedListBox1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.RadioButton radioButton1;
        private System.Windows.Forms.RadioButton radioButton2;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.TabPage tabPageData;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.ImageList imageList1;
        private System.Windows.Forms.TabPage tabPageRange;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.TrackBar trackBar1;
        private System.Windows.Forms.HScrollBar hScrollBar1;
        private System.Windows.Forms.VScrollBar vScrollBar1;
        private System.Windows.Forms.NumericUpDown numericUpDown1;
        private System.Windows.Forms.DomainUpDown domainUpDown1;
        private System.Windows.Forms.TabPage tabPageDate;
        private System.Windows.Forms.DateTimePicker dateTimePicker1;
        private System.Windows.Forms.MonthCalendar monthCalendar1;
        private System.Windows.Forms.TabPage tabPageContainers;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Button button5;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
