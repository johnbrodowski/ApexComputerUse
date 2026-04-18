namespace WinFormsApplication
{
    public partial class TortureTestForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.MenuStrip menuStripMain;
        private System.Windows.Forms.ToolStrip toolStripMain;
        private System.Windows.Forms.StatusStrip statusStripMain;
        private System.Windows.Forms.TabControl tabsMain;
        private System.Windows.Forms.TabPage tabIdentity;
        private System.Windows.Forms.TabPage tabNetwork;
        private System.Windows.Forms.TabPage tabScheduler;
        private System.Windows.Forms.TabPage tabLayout;
        private System.Windows.Forms.TabPage tabData;
        private System.Windows.Forms.TabPage tabLogs;
        private System.Windows.Forms.TabPage tabDisplay;
        private System.Windows.Forms.TabPage tabDates;
        private System.Windows.Forms.TabPage tabDialogs;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.menuStripMain = new System.Windows.Forms.MenuStrip();
            this.toolStripMain = new System.Windows.Forms.ToolStrip();
            this.statusStripMain = new System.Windows.Forms.StatusStrip();
            this.tabsMain = new System.Windows.Forms.TabControl();
            this.tabIdentity = new System.Windows.Forms.TabPage();
            this.tabNetwork = new System.Windows.Forms.TabPage();
            this.tabScheduler = new System.Windows.Forms.TabPage();
            this.tabLayout = new System.Windows.Forms.TabPage();
            this.tabData = new System.Windows.Forms.TabPage();
            this.tabLogs = new System.Windows.Forms.TabPage();
            this.tabDisplay = new System.Windows.Forms.TabPage();
            this.tabDates = new System.Windows.Forms.TabPage();
            this.tabDialogs = new System.Windows.Forms.TabPage();
            this.tabsMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStripMain
            // 
            this.menuStripMain.Location = new System.Drawing.Point(0, 0);
            this.menuStripMain.Name = "menuStripMain";
            this.menuStripMain.Size = new System.Drawing.Size(1134, 24);
            this.menuStripMain.TabIndex = 0;
            this.menuStripMain.Text = "menuStripMain";
            // 
            // toolStripMain
            // 
            this.toolStripMain.Location = new System.Drawing.Point(0, 24);
            this.toolStripMain.Name = "toolStripMain";
            this.toolStripMain.Size = new System.Drawing.Size(1134, 25);
            this.toolStripMain.TabIndex = 1;
            this.toolStripMain.Text = "toolStripMain";
            // 
            // statusStripMain
            // 
            this.statusStripMain.Location = new System.Drawing.Point(0, 789);
            this.statusStripMain.Name = "statusStripMain";
            this.statusStripMain.Size = new System.Drawing.Size(1134, 22);
            this.statusStripMain.TabIndex = 2;
            this.statusStripMain.Text = "statusStripMain";
            // 
            // tabsMain
            // 
            this.tabsMain.Controls.Add(this.tabIdentity);
            this.tabsMain.Controls.Add(this.tabNetwork);
            this.tabsMain.Controls.Add(this.tabScheduler);
            this.tabsMain.Controls.Add(this.tabLayout);
            this.tabsMain.Controls.Add(this.tabData);
            this.tabsMain.Controls.Add(this.tabLogs);
            this.tabsMain.Controls.Add(this.tabDisplay);
            this.tabsMain.Controls.Add(this.tabDates);
            this.tabsMain.Controls.Add(this.tabDialogs);
            this.tabsMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabsMain.Location = new System.Drawing.Point(0, 49);
            this.tabsMain.Name = "tabsMain";
            this.tabsMain.SelectedIndex = 0;
            this.tabsMain.Size = new System.Drawing.Size(1134, 740);
            this.tabsMain.TabIndex = 3;
            // 
            // tabIdentity
            // 
            this.tabIdentity.Location = new System.Drawing.Point(4, 24);
            this.tabIdentity.Name = "tabIdentity";
            this.tabIdentity.Padding = new System.Windows.Forms.Padding(3);
            this.tabIdentity.Size = new System.Drawing.Size(1126, 712);
            this.tabIdentity.TabIndex = 0;
            this.tabIdentity.Text = "Identity";
            this.tabIdentity.UseVisualStyleBackColor = true;
            // 
            // tabNetwork
            // 
            this.tabNetwork.Location = new System.Drawing.Point(4, 24);
            this.tabNetwork.Name = "tabNetwork";
            this.tabNetwork.Padding = new System.Windows.Forms.Padding(3);
            this.tabNetwork.Size = new System.Drawing.Size(1126, 712);
            this.tabNetwork.TabIndex = 1;
            this.tabNetwork.Text = "Network";
            this.tabNetwork.UseVisualStyleBackColor = true;
            // 
            // tabScheduler
            // 
            this.tabScheduler.Location = new System.Drawing.Point(4, 24);
            this.tabScheduler.Name = "tabScheduler";
            this.tabScheduler.Padding = new System.Windows.Forms.Padding(3);
            this.tabScheduler.Size = new System.Drawing.Size(1126, 712);
            this.tabScheduler.TabIndex = 2;
            this.tabScheduler.Text = "Scheduler";
            this.tabScheduler.UseVisualStyleBackColor = true;
            // 
            // tabLayout
            // 
            this.tabLayout.Location = new System.Drawing.Point(4, 24);
            this.tabLayout.Name = "tabLayout";
            this.tabLayout.Padding = new System.Windows.Forms.Padding(3);
            this.tabLayout.Size = new System.Drawing.Size(1126, 712);
            this.tabLayout.TabIndex = 3;
            this.tabLayout.Text = "Layout";
            this.tabLayout.UseVisualStyleBackColor = true;
            // 
            // tabData
            // 
            this.tabData.Location = new System.Drawing.Point(4, 24);
            this.tabData.Name = "tabData";
            this.tabData.Padding = new System.Windows.Forms.Padding(3);
            this.tabData.Size = new System.Drawing.Size(1126, 712);
            this.tabData.TabIndex = 4;
            this.tabData.Text = "Data";
            this.tabData.UseVisualStyleBackColor = true;
            // 
            // tabLogs
            // 
            this.tabLogs.Location = new System.Drawing.Point(4, 24);
            this.tabLogs.Name = "tabLogs";
            this.tabLogs.Padding = new System.Windows.Forms.Padding(3);
            this.tabLogs.Size = new System.Drawing.Size(1126, 712);
            this.tabLogs.TabIndex = 5;
            this.tabLogs.Text = "Logs";
            this.tabLogs.UseVisualStyleBackColor = true;
            // 
            // tabDisplay
            // 
            this.tabDisplay.Location = new System.Drawing.Point(4, 24);
            this.tabDisplay.Name = "tabDisplay";
            this.tabDisplay.Padding = new System.Windows.Forms.Padding(3);
            this.tabDisplay.Size = new System.Drawing.Size(1126, 712);
            this.tabDisplay.TabIndex = 6;
            this.tabDisplay.Text = "Display";
            this.tabDisplay.UseVisualStyleBackColor = true;
            // 
            // tabDates
            // 
            this.tabDates.Location = new System.Drawing.Point(4, 24);
            this.tabDates.Name = "tabDates";
            this.tabDates.Padding = new System.Windows.Forms.Padding(3);
            this.tabDates.Size = new System.Drawing.Size(1126, 712);
            this.tabDates.TabIndex = 7;
            this.tabDates.Text = "Dates";
            this.tabDates.UseVisualStyleBackColor = true;
            // 
            // tabDialogs
            // 
            this.tabDialogs.Location = new System.Drawing.Point(4, 24);
            this.tabDialogs.Name = "tabDialogs";
            this.tabDialogs.Padding = new System.Windows.Forms.Padding(3);
            this.tabDialogs.Size = new System.Drawing.Size(1126, 712);
            this.tabDialogs.TabIndex = 8;
            this.tabDialogs.Text = "Dialogs";
            this.tabDialogs.UseVisualStyleBackColor = true;
            // 
            // TortureTestForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1134, 811);
            this.Controls.Add(this.tabsMain);
            this.Controls.Add(this.statusStripMain);
            this.Controls.Add(this.toolStripMain);
            this.Controls.Add(this.menuStripMain);
            this.MainMenuStrip = this.menuStripMain;
            this.Name = "TortureTestForm";
            this.tabsMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

            BuildUI();
        }
    }
}
