namespace ApexComputerUse
{
    partial class SceneEditorForm
    {
        private System.ComponentModel.IContainer components = null;

        // ── Toolbar ───────────────────────────────────────────────────────
        private System.Windows.Forms.FlowLayoutPanel toolFlow;
        private System.Windows.Forms.Label lblFillLabel;
        private System.Windows.Forms.Button btnFillColor;

        // ── Toolbox ───────────────────────────────────────────────────────
        private System.Windows.Forms.FlowLayoutPanel toolboxPanel;
        private System.Windows.Forms.Button btnToolArrow;
        private System.Windows.Forms.Button btnToolRect;
        private System.Windows.Forms.Button btnToolEllipse;
        private System.Windows.Forms.Button btnToolCircle;
        private System.Windows.Forms.Button btnToolLine;
        private System.Windows.Forms.Button btnToolText;
        private System.Windows.Forms.Button btnDeleteShape;

        // ── Scene list ────────────────────────────────────────────────────
        private System.Windows.Forms.Panel scenePanel;
        private System.Windows.Forms.Label sceneHeader;
        private System.Windows.Forms.ListBox lstScenes;
        private System.Windows.Forms.Button btnNewScene;
        private System.Windows.Forms.Button btnDeleteScene;

        // ── Splits ────────────────────────────────────────────────────────
        private System.Windows.Forms.SplitContainer splitOuter;
        private System.Windows.Forms.SplitContainer splitInner;
        private System.Windows.Forms.SplitContainer splitRight;

        // ── Canvas ────────────────────────────────────────────────────────
        private System.Windows.Forms.Panel canvasPanel;

        // ── Layers ────────────────────────────────────────────────────────
        private System.Windows.Forms.Panel layersPanel;
        private System.Windows.Forms.Label layersHeader;
        private System.Windows.Forms.ListBox lstLayers;
        private System.Windows.Forms.FlowLayoutPanel layerBtnRow;
        private System.Windows.Forms.Button btnLayerUp;
        private System.Windows.Forms.Button btnLayerDown;
        private System.Windows.Forms.Button btnAddLayer;
        private System.Windows.Forms.Button btnDeleteLayer;

        // ── Properties ────────────────────────────────────────────────────
        private System.Windows.Forms.Panel rightBottom;
        private System.Windows.Forms.Label propsHeader;
        private System.Windows.Forms.FlowLayoutPanel pnlProps;

        // ── Status strip ──────────────────────────────────────────────────
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblCursor;
        private System.Windows.Forms.ToolStripStatusLabel lblSelected;
        private System.Windows.Forms.ToolStripStatusLabel lblSceneInfo;

        // ── Tooltip ───────────────────────────────────────────────────────
        private System.Windows.Forms.ToolTip toolTip1;

        // ── Refresh timer ─────────────────────────────────────────────────
        private System.Windows.Forms.Timer refreshTimer;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            _cacheBitmap?.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.toolFlow = new System.Windows.Forms.FlowLayoutPanel();
            this.lblFillLabel = new System.Windows.Forms.Label();
            this.btnFillColor = new System.Windows.Forms.Button();
            this.toolboxPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.btnToolArrow = new System.Windows.Forms.Button();
            this.btnToolRect = new System.Windows.Forms.Button();
            this.btnToolEllipse = new System.Windows.Forms.Button();
            this.btnToolCircle = new System.Windows.Forms.Button();
            this.btnToolLine = new System.Windows.Forms.Button();
            this.btnToolText = new System.Windows.Forms.Button();
            this.btnDeleteShape = new System.Windows.Forms.Button();
            this.scenePanel = new System.Windows.Forms.Panel();
            this.lstScenes = new System.Windows.Forms.ListBox();
            this.sceneHeader = new System.Windows.Forms.Label();
            this.btnDeleteScene = new System.Windows.Forms.Button();
            this.btnNewScene = new System.Windows.Forms.Button();
            this.splitOuter = new System.Windows.Forms.SplitContainer();
            this.splitInner = new System.Windows.Forms.SplitContainer();
            this.canvasPanel = new System.Windows.Forms.Panel();
            this.splitRight = new System.Windows.Forms.SplitContainer();
            this.layersPanel = new System.Windows.Forms.Panel();
            this.lstLayers = new System.Windows.Forms.ListBox();
            this.layersHeader = new System.Windows.Forms.Label();
            this.layerBtnRow = new System.Windows.Forms.FlowLayoutPanel();
            this.btnLayerUp = new System.Windows.Forms.Button();
            this.btnLayerDown = new System.Windows.Forms.Button();
            this.btnAddLayer = new System.Windows.Forms.Button();
            this.btnDeleteLayer = new System.Windows.Forms.Button();
            this.rightBottom = new System.Windows.Forms.Panel();
            this.pnlProps = new System.Windows.Forms.FlowLayoutPanel();
            this.propsHeader = new System.Windows.Forms.Label();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblCursor = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblSelected = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblSceneInfo = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.refreshTimer = new System.Windows.Forms.Timer(this.components);
            this.toolFlow.SuspendLayout();
            this.toolboxPanel.SuspendLayout();
            this.scenePanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitOuter)).BeginInit();
            this.splitOuter.Panel1.SuspendLayout();
            this.splitOuter.Panel2.SuspendLayout();
            this.splitOuter.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitInner)).BeginInit();
            this.splitInner.Panel1.SuspendLayout();
            this.splitInner.Panel2.SuspendLayout();
            this.splitInner.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitRight)).BeginInit();
            this.splitRight.Panel1.SuspendLayout();
            this.splitRight.Panel2.SuspendLayout();
            this.splitRight.SuspendLayout();
            this.layersPanel.SuspendLayout();
            this.layerBtnRow.SuspendLayout();
            this.rightBottom.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // toolFlow
            //
            this.toolFlow.AutoSize = false;
            this.toolFlow.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.toolFlow.Controls.Add(this.lblFillLabel);
            this.toolFlow.Controls.Add(this.btnFillColor);
            this.toolFlow.Dock = System.Windows.Forms.DockStyle.Top;
            this.toolFlow.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.toolFlow.Location = new System.Drawing.Point(0, 0);
            this.toolFlow.Name = "toolFlow";
            this.toolFlow.Padding = new System.Windows.Forms.Padding(4, 4, 0, 0);
            this.toolFlow.Size = new System.Drawing.Size(1200, 34);
            this.toolFlow.TabIndex = 0;
            this.toolFlow.WrapContents = false;
            //
            // lblFillLabel
            //
            this.lblFillLabel.AutoSize = true;
            this.lblFillLabel.Font = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblFillLabel.ForeColor = System.Drawing.Color.FromArgb(180, 180, 180);
            this.lblFillLabel.Margin = new System.Windows.Forms.Padding(8, 0, 4, 0);
            this.lblFillLabel.Name = "lblFillLabel";
            this.lblFillLabel.Size = new System.Drawing.Size(24, 15);
            this.lblFillLabel.Text = "Fill:";
            this.lblFillLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // btnFillColor
            //
            this.btnFillColor.BackColor = System.Drawing.Color.FromArgb(74, 144, 217);
            this.btnFillColor.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(120, 120, 120);
            this.btnFillColor.FlatAppearance.BorderSize = 1;
            this.btnFillColor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnFillColor.Location = new System.Drawing.Point(44, 5);
            this.btnFillColor.Margin = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.btnFillColor.Name = "btnFillColor";
            this.btnFillColor.Size = new System.Drawing.Size(24, 24);
            this.btnFillColor.TabIndex = 0;
            this.btnFillColor.Text = "";
            this.toolTip1.SetToolTip(this.btnFillColor, "Fill color — click to change");
            this.btnFillColor.UseVisualStyleBackColor = false;
            this.btnFillColor.Click += new System.EventHandler(this.btnFillColor_Click);
            //
            // toolboxPanel
            //
            this.toolboxPanel.AutoSize = false;
            this.toolboxPanel.BackColor = System.Drawing.Color.FromArgb(37, 37, 38);
            this.toolboxPanel.Controls.Add(this.btnToolArrow);
            this.toolboxPanel.Controls.Add(this.btnToolRect);
            this.toolboxPanel.Controls.Add(this.btnToolEllipse);
            this.toolboxPanel.Controls.Add(this.btnToolCircle);
            this.toolboxPanel.Controls.Add(this.btnToolLine);
            this.toolboxPanel.Controls.Add(this.btnToolText);
            this.toolboxPanel.Controls.Add(this.btnDeleteShape);
            this.toolboxPanel.Dock = System.Windows.Forms.DockStyle.Left;
            this.toolboxPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.toolboxPanel.Location = new System.Drawing.Point(0, 0);
            this.toolboxPanel.Name = "toolboxPanel";
            this.toolboxPanel.Padding = new System.Windows.Forms.Padding(5, 8, 5, 8);
            this.toolboxPanel.Size = new System.Drawing.Size(70, 694);
            this.toolboxPanel.TabIndex = 1;
            this.toolboxPanel.WrapContents = false;
            //
            // btnToolArrow
            //
            this.btnToolArrow.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.btnToolArrow.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(0, 153, 204);
            this.btnToolArrow.FlatAppearance.BorderSize = 2;
            this.btnToolArrow.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(62, 62, 66);
            this.btnToolArrow.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToolArrow.Font = new System.Drawing.Font("Segoe UI", 14F);
            this.btnToolArrow.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.btnToolArrow.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.btnToolArrow.Name = "btnToolArrow";
            this.btnToolArrow.Size = new System.Drawing.Size(60, 50);
            this.btnToolArrow.TabIndex = 0;
            this.btnToolArrow.Text = "↖";
            this.toolTip1.SetToolTip(this.btnToolArrow, "Select / Move (V)");
            this.btnToolArrow.UseVisualStyleBackColor = false;
            this.btnToolArrow.Click += new System.EventHandler(this.btnToolArrow_Click);
            //
            // btnToolRect
            //
            this.btnToolRect.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.btnToolRect.FlatAppearance.BorderSize = 0;
            this.btnToolRect.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(62, 62, 66);
            this.btnToolRect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToolRect.Font = new System.Drawing.Font("Segoe UI", 14F);
            this.btnToolRect.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.btnToolRect.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.btnToolRect.Name = "btnToolRect";
            this.btnToolRect.Size = new System.Drawing.Size(60, 50);
            this.btnToolRect.TabIndex = 1;
            this.btnToolRect.Text = "▭";
            this.toolTip1.SetToolTip(this.btnToolRect, "Rectangle (R)");
            this.btnToolRect.UseVisualStyleBackColor = false;
            this.btnToolRect.Click += new System.EventHandler(this.btnToolRect_Click);
            //
            // btnToolEllipse
            //
            this.btnToolEllipse.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.btnToolEllipse.FlatAppearance.BorderSize = 0;
            this.btnToolEllipse.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(62, 62, 66);
            this.btnToolEllipse.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToolEllipse.Font = new System.Drawing.Font("Segoe UI", 14F);
            this.btnToolEllipse.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.btnToolEllipse.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.btnToolEllipse.Name = "btnToolEllipse";
            this.btnToolEllipse.Size = new System.Drawing.Size(60, 50);
            this.btnToolEllipse.TabIndex = 2;
            this.btnToolEllipse.Text = "◯";
            this.toolTip1.SetToolTip(this.btnToolEllipse, "Ellipse (E)");
            this.btnToolEllipse.UseVisualStyleBackColor = false;
            this.btnToolEllipse.Click += new System.EventHandler(this.btnToolEllipse_Click);
            //
            // btnToolCircle
            //
            this.btnToolCircle.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.btnToolCircle.FlatAppearance.BorderSize = 0;
            this.btnToolCircle.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(62, 62, 66);
            this.btnToolCircle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToolCircle.Font = new System.Drawing.Font("Segoe UI", 14F);
            this.btnToolCircle.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.btnToolCircle.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.btnToolCircle.Name = "btnToolCircle";
            this.btnToolCircle.Size = new System.Drawing.Size(60, 50);
            this.btnToolCircle.TabIndex = 3;
            this.btnToolCircle.Text = "○";
            this.toolTip1.SetToolTip(this.btnToolCircle, "Circle (C)");
            this.btnToolCircle.UseVisualStyleBackColor = false;
            this.btnToolCircle.Click += new System.EventHandler(this.btnToolCircle_Click);
            //
            // btnToolLine
            //
            this.btnToolLine.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.btnToolLine.FlatAppearance.BorderSize = 0;
            this.btnToolLine.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(62, 62, 66);
            this.btnToolLine.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToolLine.Font = new System.Drawing.Font("Segoe UI", 14F);
            this.btnToolLine.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.btnToolLine.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.btnToolLine.Name = "btnToolLine";
            this.btnToolLine.Size = new System.Drawing.Size(60, 50);
            this.btnToolLine.TabIndex = 4;
            this.btnToolLine.Text = "╱";
            this.toolTip1.SetToolTip(this.btnToolLine, "Line (L)");
            this.btnToolLine.UseVisualStyleBackColor = false;
            this.btnToolLine.Click += new System.EventHandler(this.btnToolLine_Click);
            //
            // btnToolText
            //
            this.btnToolText.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.btnToolText.FlatAppearance.BorderSize = 0;
            this.btnToolText.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(62, 62, 66);
            this.btnToolText.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToolText.Font = new System.Drawing.Font("Segoe UI", 14F);
            this.btnToolText.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.btnToolText.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.btnToolText.Name = "btnToolText";
            this.btnToolText.Size = new System.Drawing.Size(60, 50);
            this.btnToolText.TabIndex = 5;
            this.btnToolText.Text = "T";
            this.toolTip1.SetToolTip(this.btnToolText, "Text (T)");
            this.btnToolText.UseVisualStyleBackColor = false;
            this.btnToolText.Click += new System.EventHandler(this.btnToolText_Click);
            //
            // btnDeleteShape
            //
            this.btnDeleteShape.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.btnDeleteShape.FlatAppearance.BorderSize = 0;
            this.btnDeleteShape.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(62, 62, 66);
            this.btnDeleteShape.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDeleteShape.Font = new System.Drawing.Font("Segoe UI", 14F);
            this.btnDeleteShape.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.btnDeleteShape.Margin = new System.Windows.Forms.Padding(0, 12, 0, 4);
            this.btnDeleteShape.Name = "btnDeleteShape";
            this.btnDeleteShape.Size = new System.Drawing.Size(60, 50);
            this.btnDeleteShape.TabIndex = 6;
            this.btnDeleteShape.Text = "🗑";
            this.toolTip1.SetToolTip(this.btnDeleteShape, "Delete selected (Del)");
            this.btnDeleteShape.UseVisualStyleBackColor = false;
            this.btnDeleteShape.Click += new System.EventHandler(this.btnDeleteShape_Click);
            //
            // scenePanel
            //
            this.scenePanel.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.scenePanel.Controls.Add(this.lstScenes);
            this.scenePanel.Controls.Add(this.sceneHeader);
            this.scenePanel.Controls.Add(this.btnDeleteScene);
            this.scenePanel.Controls.Add(this.btnNewScene);
            this.scenePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.scenePanel.Location = new System.Drawing.Point(0, 0);
            this.scenePanel.Name = "scenePanel";
            this.scenePanel.Size = new System.Drawing.Size(180, 694);
            this.scenePanel.TabIndex = 0;
            //
            // lstScenes
            //
            this.lstScenes.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.lstScenes.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lstScenes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstScenes.Font = new System.Drawing.Font("Consolas", 8.5F);
            this.lstScenes.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.lstScenes.Location = new System.Drawing.Point(0, 22);
            this.lstScenes.Name = "lstScenes";
            this.lstScenes.SelectionMode = System.Windows.Forms.SelectionMode.One;
            this.lstScenes.Size = new System.Drawing.Size(180, 618);
            this.lstScenes.TabIndex = 0;
            this.lstScenes.SelectedIndexChanged += new System.EventHandler(this.lstScenes_SelectedIndexChanged);
            //
            // sceneHeader
            //
            this.sceneHeader.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.sceneHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.sceneHeader.Font = new System.Drawing.Font("Segoe UI", 7.5F);
            this.sceneHeader.ForeColor = System.Drawing.Color.Gray;
            this.sceneHeader.Location = new System.Drawing.Point(0, 0);
            this.sceneHeader.Name = "sceneHeader";
            this.sceneHeader.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.sceneHeader.Size = new System.Drawing.Size(180, 22);
            this.sceneHeader.TabIndex = 2;
            this.sceneHeader.Text = "SCENES";
            this.sceneHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // btnDeleteScene
            //
            this.btnDeleteScene.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.btnDeleteScene.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.btnDeleteScene.Enabled = false;
            this.btnDeleteScene.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 80, 80);
            this.btnDeleteScene.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDeleteScene.ForeColor = System.Drawing.Color.FromArgb(224, 82, 82);
            this.btnDeleteScene.Location = new System.Drawing.Point(0, 640);
            this.btnDeleteScene.Name = "btnDeleteScene";
            this.btnDeleteScene.Size = new System.Drawing.Size(180, 26);
            this.btnDeleteScene.TabIndex = 1;
            this.btnDeleteScene.Text = "Delete";
            this.btnDeleteScene.UseVisualStyleBackColor = false;
            this.btnDeleteScene.Click += new System.EventHandler(this.btnDeleteScene_Click);
            //
            // btnNewScene
            //
            this.btnNewScene.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.btnNewScene.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.btnNewScene.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 80, 80);
            this.btnNewScene.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnNewScene.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.btnNewScene.Location = new System.Drawing.Point(0, 666);
            this.btnNewScene.Name = "btnNewScene";
            this.btnNewScene.Size = new System.Drawing.Size(180, 28);
            this.btnNewScene.TabIndex = 3;
            this.btnNewScene.Text = "+ New Scene";
            this.btnNewScene.UseVisualStyleBackColor = false;
            this.btnNewScene.Click += new System.EventHandler(this.btnNewScene_Click);
            //
            // splitOuter
            //
            this.splitOuter.BackColor = System.Drawing.Color.FromArgb(63, 63, 70);
            this.splitOuter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitOuter.Location = new System.Drawing.Point(0, 34);
            this.splitOuter.Name = "splitOuter";
            this.splitOuter.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.splitOuter.Panel1MinSize = 120;
            //
            // splitOuter.Panel1
            //
            this.splitOuter.Panel1.Controls.Add(this.scenePanel);
            //
            // splitOuter.Panel2
            //
            this.splitOuter.Panel2.Controls.Add(this.splitInner);
            this.splitOuter.Size = new System.Drawing.Size(1200, 694);
            this.splitOuter.SplitterDistance = 180;
            this.splitOuter.TabIndex = 1;
            //
            // splitInner
            //
            this.splitInner.BackColor = System.Drawing.Color.FromArgb(63, 63, 70);
            this.splitInner.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitInner.Location = new System.Drawing.Point(0, 0);
            this.splitInner.Name = "splitInner";
            this.splitInner.Orientation = System.Windows.Forms.Orientation.Vertical;
            this.splitInner.Panel2MinSize = 180;
            //
            // splitInner.Panel1
            //
            this.splitInner.Panel1.Controls.Add(this.toolboxPanel);
            this.splitInner.Panel1.Controls.Add(this.canvasPanel);
            //
            // splitInner.Panel2
            //
            this.splitInner.Panel2.Controls.Add(this.splitRight);
            this.splitInner.Size = new System.Drawing.Size(1016, 694);
            this.splitInner.SplitterDistance = 780;
            this.splitInner.TabIndex = 0;
            //
            // canvasPanel
            //
            this.canvasPanel.BackColor = System.Drawing.Color.FromArgb(20, 20, 20);
            this.canvasPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.canvasPanel.Location = new System.Drawing.Point(0, 0);
            this.canvasPanel.Name = "canvasPanel";
            this.canvasPanel.Size = new System.Drawing.Size(780, 694);
            this.canvasPanel.TabIndex = 0;
            this.canvasPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.canvasPanel_Paint);
            this.canvasPanel.MouseDown += new System.Windows.Forms.MouseEventHandler(this.canvasPanel_MouseDown);
            this.canvasPanel.MouseMove += new System.Windows.Forms.MouseEventHandler(this.canvasPanel_MouseMove);
            this.canvasPanel.MouseUp += new System.Windows.Forms.MouseEventHandler(this.canvasPanel_MouseUp);
            this.canvasPanel.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.canvasPanel_MouseWheel);
            this.canvasPanel.Resize += new System.EventHandler(this.canvasPanel_Resize);
            //
            // splitRight
            //
            this.splitRight.BackColor = System.Drawing.Color.FromArgb(63, 63, 70);
            this.splitRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitRight.Location = new System.Drawing.Point(0, 0);
            this.splitRight.Name = "splitRight";
            this.splitRight.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitRight.Panel1MinSize = 80;
            //
            // splitRight.Panel1
            //
            this.splitRight.Panel1.Controls.Add(this.layersPanel);
            this.splitRight.Panel2MinSize = 60;
            //
            // splitRight.Panel2
            //
            this.splitRight.Panel2.Controls.Add(this.rightBottom);
            this.splitRight.Size = new System.Drawing.Size(232, 694);
            this.splitRight.SplitterDistance = 180;
            this.splitRight.TabIndex = 0;
            //
            // layersPanel
            //
            this.layersPanel.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.layersPanel.Controls.Add(this.lstLayers);
            this.layersPanel.Controls.Add(this.layersHeader);
            this.layersPanel.Controls.Add(this.layerBtnRow);
            this.layersPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layersPanel.Location = new System.Drawing.Point(0, 0);
            this.layersPanel.Name = "layersPanel";
            this.layersPanel.Size = new System.Drawing.Size(232, 180);
            this.layersPanel.TabIndex = 0;
            //
            // lstLayers
            //
            this.lstLayers.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.lstLayers.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lstLayers.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstLayers.Font = new System.Drawing.Font("Consolas", 8.5F);
            this.lstLayers.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.lstLayers.Location = new System.Drawing.Point(0, 22);
            this.lstLayers.Name = "lstLayers";
            this.lstLayers.SelectionMode = System.Windows.Forms.SelectionMode.One;
            this.lstLayers.Size = new System.Drawing.Size(232, 132);
            this.lstLayers.TabIndex = 0;
            this.lstLayers.SelectedIndexChanged += new System.EventHandler(this.lstLayers_SelectedIndexChanged);
            this.lstLayers.MouseDown += new System.Windows.Forms.MouseEventHandler(this.lstLayers_MouseDown);
            //
            // layersHeader
            //
            this.layersHeader.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.layersHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.layersHeader.Font = new System.Drawing.Font("Segoe UI", 7.5F);
            this.layersHeader.ForeColor = System.Drawing.Color.Gray;
            this.layersHeader.Location = new System.Drawing.Point(0, 0);
            this.layersHeader.Name = "layersHeader";
            this.layersHeader.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.layersHeader.Size = new System.Drawing.Size(232, 22);
            this.layersHeader.TabIndex = 1;
            this.layersHeader.Text = "LAYERS";
            this.layersHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // layerBtnRow
            //
            this.layerBtnRow.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.layerBtnRow.Controls.Add(this.btnLayerUp);
            this.layerBtnRow.Controls.Add(this.btnLayerDown);
            this.layerBtnRow.Controls.Add(this.btnAddLayer);
            this.layerBtnRow.Controls.Add(this.btnDeleteLayer);
            this.layerBtnRow.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.layerBtnRow.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.layerBtnRow.Location = new System.Drawing.Point(0, 154);
            this.layerBtnRow.Name = "layerBtnRow";
            this.layerBtnRow.Padding = new System.Windows.Forms.Padding(2);
            this.layerBtnRow.Size = new System.Drawing.Size(232, 26);
            this.layerBtnRow.TabIndex = 2;
            //
            // btnLayerUp
            //
            this.btnLayerUp.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.btnLayerUp.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 80, 80);
            this.btnLayerUp.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLayerUp.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.btnLayerUp.Location = new System.Drawing.Point(4, 2);
            this.btnLayerUp.Name = "btnLayerUp";
            this.btnLayerUp.Size = new System.Drawing.Size(28, 22);
            this.btnLayerUp.TabIndex = 2;
            this.btnLayerUp.Text = "▲";
            this.toolTip1.SetToolTip(this.btnLayerUp, "Move layer up");
            this.btnLayerUp.UseVisualStyleBackColor = false;
            this.btnLayerUp.Click += new System.EventHandler(this.btnLayerUp_Click);
            //
            // btnLayerDown
            //
            this.btnLayerDown.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.btnLayerDown.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 80, 80);
            this.btnLayerDown.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLayerDown.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.btnLayerDown.Location = new System.Drawing.Point(34, 2);
            this.btnLayerDown.Name = "btnLayerDown";
            this.btnLayerDown.Size = new System.Drawing.Size(28, 22);
            this.btnLayerDown.TabIndex = 3;
            this.btnLayerDown.Text = "▼";
            this.toolTip1.SetToolTip(this.btnLayerDown, "Move layer down");
            this.btnLayerDown.UseVisualStyleBackColor = false;
            this.btnLayerDown.Click += new System.EventHandler(this.btnLayerDown_Click);
            //
            // btnAddLayer
            //
            this.btnAddLayer.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.btnAddLayer.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 80, 80);
            this.btnAddLayer.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAddLayer.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.btnAddLayer.Location = new System.Drawing.Point(4, 2);
            this.btnAddLayer.Name = "btnAddLayer";
            this.btnAddLayer.Size = new System.Drawing.Size(28, 22);
            this.btnAddLayer.TabIndex = 0;
            this.btnAddLayer.Text = "+";
            this.btnAddLayer.UseVisualStyleBackColor = false;
            this.btnAddLayer.Click += new System.EventHandler(this.btnAddLayer_Click);
            //
            // btnDeleteLayer
            //
            this.btnDeleteLayer.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.btnDeleteLayer.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 80, 80);
            this.btnDeleteLayer.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDeleteLayer.ForeColor = System.Drawing.Color.FromArgb(224, 82, 82);
            this.btnDeleteLayer.Location = new System.Drawing.Point(34, 2);
            this.btnDeleteLayer.Name = "btnDeleteLayer";
            this.btnDeleteLayer.Size = new System.Drawing.Size(28, 22);
            this.btnDeleteLayer.TabIndex = 1;
            this.btnDeleteLayer.Text = "✕";
            this.btnDeleteLayer.UseVisualStyleBackColor = false;
            this.btnDeleteLayer.Click += new System.EventHandler(this.btnDeleteLayer_Click);
            //
            // rightBottom
            //
            this.rightBottom.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.rightBottom.Controls.Add(this.pnlProps);
            this.rightBottom.Controls.Add(this.propsHeader);
            this.rightBottom.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightBottom.Location = new System.Drawing.Point(0, 0);
            this.rightBottom.Name = "rightBottom";
            this.rightBottom.Size = new System.Drawing.Size(232, 510);
            this.rightBottom.TabIndex = 0;
            //
            // pnlProps
            //
            this.pnlProps.AutoScroll = true;
            this.pnlProps.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.pnlProps.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlProps.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.pnlProps.Location = new System.Drawing.Point(0, 22);
            this.pnlProps.Name = "pnlProps";
            this.pnlProps.Padding = new System.Windows.Forms.Padding(4);
            this.pnlProps.Size = new System.Drawing.Size(232, 488);
            this.pnlProps.TabIndex = 0;
            //
            // propsHeader
            //
            this.propsHeader.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.propsHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.propsHeader.Font = new System.Drawing.Font("Segoe UI", 7.5F);
            this.propsHeader.ForeColor = System.Drawing.Color.Gray;
            this.propsHeader.Location = new System.Drawing.Point(0, 0);
            this.propsHeader.Name = "propsHeader";
            this.propsHeader.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.propsHeader.Size = new System.Drawing.Size(232, 22);
            this.propsHeader.TabIndex = 1;
            this.propsHeader.Text = "PROPERTIES";
            this.propsHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // statusStrip
            //
            this.statusStrip.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblCursor,
            this.lblSelected,
            this.lblSceneInfo});
            this.statusStrip.Location = new System.Drawing.Point(0, 728);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.SizingGrip = false;
            this.statusStrip.Size = new System.Drawing.Size(1200, 22);
            this.statusStrip.TabIndex = 2;
            //
            // lblCursor
            //
            this.lblCursor.ForeColor = System.Drawing.Color.Gray;
            this.lblCursor.Name = "lblCursor";
            this.lblCursor.Size = new System.Drawing.Size(70, 17);
            this.lblCursor.Text = "x: —  y: —";
            //
            // lblSelected
            //
            this.lblSelected.ForeColor = System.Drawing.Color.Gray;
            this.lblSelected.Name = "lblSelected";
            this.lblSelected.Size = new System.Drawing.Size(100, 17);
            this.lblSelected.Spring = true;
            this.lblSelected.Text = "nothing selected";
            //
            // lblSceneInfo
            //
            this.lblSceneInfo.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.lblSceneInfo.ForeColor = System.Drawing.Color.FromArgb(156, 220, 254);
            this.lblSceneInfo.Name = "lblSceneInfo";
            this.lblSceneInfo.Size = new System.Drawing.Size(0, 17);
            this.lblSceneInfo.Text = "";
            //
            // refreshTimer
            //
            this.refreshTimer.Interval = 400;
            this.refreshTimer.Tick += new System.EventHandler(this.refreshTimer_Tick);
            //
            // SceneEditorForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.ClientSize = new System.Drawing.Size(1200, 750);
            this.Controls.Add(this.splitOuter);
            this.Controls.Add(this.toolFlow);
            this.Controls.Add(this.statusStrip);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.KeyPreview = true;
            this.MinimumSize = new System.Drawing.Size(800, 500);
            this.Name = "SceneEditorForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Scene Editor";
            this.Load += new System.EventHandler(this.SceneEditorForm_Load);
            this.toolFlow.ResumeLayout(false);
            this.scenePanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitOuter)).EndInit();
            this.splitOuter.Panel1.ResumeLayout(false);
            this.splitOuter.Panel2.ResumeLayout(false);
            this.splitOuter.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitInner)).EndInit();
            this.splitInner.Panel1.ResumeLayout(false);
            this.splitInner.Panel2.ResumeLayout(false);
            this.splitInner.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitRight)).EndInit();
            this.splitRight.Panel1.ResumeLayout(false);
            this.splitRight.Panel2.ResumeLayout(false);
            this.splitRight.ResumeLayout(false);
            this.layersPanel.ResumeLayout(false);
            this.layerBtnRow.ResumeLayout(false);
            this.rightBottom.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
    }
}
