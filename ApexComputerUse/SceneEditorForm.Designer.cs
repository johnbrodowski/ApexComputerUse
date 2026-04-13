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
        private System.Windows.Forms.Panel toolboxPanel;
        private System.Windows.Forms.Button btnToolArrow;
        private System.Windows.Forms.Button btnToolRect;
        private System.Windows.Forms.Button btnToolEllipse;
        private System.Windows.Forms.Button btnToolCircle;
        private System.Windows.Forms.Button btnToolLine;
        private System.Windows.Forms.Button btnToolText;
        private System.Windows.Forms.Button btnToolTriangle;
        private System.Windows.Forms.Button btnToolArc;
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
            components = new System.ComponentModel.Container();
            toolFlow = new FlowLayoutPanel();
            lblFillLabel = new Label();
            btnFillColor = new Button();
            toolboxPanel = new Panel();
            btnToolArrow = new Button();
            btnToolRect = new Button();
            btnToolEllipse = new Button();
            btnToolCircle = new Button();
            btnToolLine = new Button();
            btnToolText = new Button();
            btnToolTriangle = new Button();
            btnToolArc = new Button();
            btnDeleteShape = new Button();
            scenePanel = new Panel();
            lstScenes = new ListBox();
            sceneHeader = new Label();
            btnDeleteScene = new Button();
            btnNewScene = new Button();
            splitOuter = new SplitContainer();
            splitInner = new SplitContainer();
            canvasPanel = new Panel();
            splitRight = new SplitContainer();
            layersPanel = new Panel();
            lstLayers = new ListBox();
            layersHeader = new Label();
            layerBtnRow = new FlowLayoutPanel();
            btnLayerUp = new Button();
            btnLayerDown = new Button();
            btnAddLayer = new Button();
            btnDeleteLayer = new Button();
            rightBottom = new Panel();
            pnlProps = new FlowLayoutPanel();
            propsHeader = new Label();
            statusStrip = new StatusStrip();
            lblCursor = new ToolStripStatusLabel();
            lblSelected = new ToolStripStatusLabel();
            lblSceneInfo = new ToolStripStatusLabel();
            toolTip1 = new ToolTip(components);
            refreshTimer = new System.Windows.Forms.Timer(components);
            toolFlow.SuspendLayout();
            toolboxPanel.SuspendLayout();
            scenePanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitOuter).BeginInit();
            splitOuter.Panel1.SuspendLayout();
            splitOuter.Panel2.SuspendLayout();
            splitOuter.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitInner).BeginInit();
            splitInner.Panel1.SuspendLayout();
            splitInner.Panel2.SuspendLayout();
            splitInner.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitRight).BeginInit();
            splitRight.Panel1.SuspendLayout();
            splitRight.Panel2.SuspendLayout();
            splitRight.SuspendLayout();
            layersPanel.SuspendLayout();
            layerBtnRow.SuspendLayout();
            rightBottom.SuspendLayout();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // toolFlow
            // 
            toolFlow.BackColor = Color.FromArgb(45, 45, 48);
            toolFlow.Controls.Add(lblFillLabel);
            toolFlow.Controls.Add(btnFillColor);
            toolFlow.Dock = DockStyle.Top;
            toolFlow.Location = new Point(0, 0);
            toolFlow.Name = "toolFlow";
            toolFlow.Padding = new Padding(4, 4, 0, 0);
            toolFlow.Size = new Size(1200, 34);
            toolFlow.TabIndex = 0;
            toolFlow.WrapContents = false;
            // 
            // lblFillLabel
            // 
            lblFillLabel.AutoSize = true;
            lblFillLabel.Font = new Font("Segoe UI", 8.5F);
            lblFillLabel.ForeColor = Color.FromArgb(180, 180, 180);
            lblFillLabel.Location = new Point(12, 4);
            lblFillLabel.Margin = new Padding(8, 0, 4, 0);
            lblFillLabel.Name = "lblFillLabel";
            lblFillLabel.Size = new Size(25, 15);
            lblFillLabel.TabIndex = 0;
            lblFillLabel.Text = "Fill:";
            lblFillLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // btnFillColor
            // 
            btnFillColor.BackColor = Color.FromArgb(74, 144, 217);
            btnFillColor.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
            btnFillColor.FlatStyle = FlatStyle.Flat;
            btnFillColor.Location = new Point(41, 9);
            btnFillColor.Margin = new Padding(0, 5, 0, 0);
            btnFillColor.Name = "btnFillColor";
            btnFillColor.Size = new Size(24, 24);
            btnFillColor.TabIndex = 0;
            toolTip1.SetToolTip(btnFillColor, "Fill color — click to change");
            btnFillColor.UseVisualStyleBackColor = false;
            btnFillColor.Click += btnFillColor_Click;
            // 
            // toolboxPanel
            // 
            toolboxPanel.BackColor = Color.FromArgb(37, 37, 38);
            toolboxPanel.Controls.Add(btnToolArrow);
            toolboxPanel.Controls.Add(btnToolRect);
            toolboxPanel.Controls.Add(btnToolEllipse);
            toolboxPanel.Controls.Add(btnToolCircle);
            toolboxPanel.Controls.Add(btnToolLine);
            toolboxPanel.Controls.Add(btnToolText);
            toolboxPanel.Controls.Add(btnToolTriangle);
            toolboxPanel.Controls.Add(btnToolArc);
            toolboxPanel.Controls.Add(btnDeleteShape);
            toolboxPanel.Dock = DockStyle.Left;
            toolboxPanel.Location = new Point(0, 0);
            toolboxPanel.Name = "toolboxPanel";
            toolboxPanel.Padding = new Padding(5, 8, 5, 8);
            toolboxPanel.Size = new Size(70, 693);
            toolboxPanel.TabIndex = 1;
            // 
            // btnToolArrow
            // 
            btnToolArrow.BackColor = Color.FromArgb(45, 45, 48);
            btnToolArrow.FlatAppearance.BorderColor = Color.FromArgb(0, 153, 204);
            btnToolArrow.FlatAppearance.BorderSize = 2;
            btnToolArrow.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 66);
            btnToolArrow.FlatStyle = FlatStyle.Flat;
            btnToolArrow.Font = new Font("Segoe UI", 14F);
            btnToolArrow.ForeColor = Color.FromArgb(212, 212, 212);
            btnToolArrow.Location = new Point(5, 8);
            btnToolArrow.Margin = new Padding(0, 0, 0, 4);
            btnToolArrow.Name = "btnToolArrow";
            btnToolArrow.Size = new Size(60, 50);
            btnToolArrow.TabIndex = 0;
            btnToolArrow.Text = "↖";
            toolTip1.SetToolTip(btnToolArrow, "Select / Move (V)");
            btnToolArrow.UseVisualStyleBackColor = false;
            btnToolArrow.Click += btnToolArrow_Click;
            // 
            // btnToolRect
            // 
            btnToolRect.BackColor = Color.FromArgb(45, 45, 48);
            btnToolRect.FlatAppearance.BorderSize = 0;
            btnToolRect.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 66);
            btnToolRect.FlatStyle = FlatStyle.Flat;
            btnToolRect.Font = new Font("Segoe UI", 14F);
            btnToolRect.ForeColor = Color.FromArgb(212, 212, 212);
            btnToolRect.Location = new Point(5, 62);
            btnToolRect.Margin = new Padding(0, 0, 0, 4);
            btnToolRect.Name = "btnToolRect";
            btnToolRect.Size = new Size(60, 50);
            btnToolRect.TabIndex = 1;
            btnToolRect.Text = "▭";
            toolTip1.SetToolTip(btnToolRect, "Rectangle (R)");
            btnToolRect.UseVisualStyleBackColor = false;
            btnToolRect.Click += btnToolRect_Click;
            // 
            // btnToolEllipse
            // 
            btnToolEllipse.BackColor = Color.FromArgb(45, 45, 48);
            btnToolEllipse.FlatAppearance.BorderSize = 0;
            btnToolEllipse.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 66);
            btnToolEllipse.FlatStyle = FlatStyle.Flat;
            btnToolEllipse.Font = new Font("Segoe UI", 14F);
            btnToolEllipse.ForeColor = Color.FromArgb(212, 212, 212);
            btnToolEllipse.Location = new Point(5, 116);
            btnToolEllipse.Margin = new Padding(0, 0, 0, 4);
            btnToolEllipse.Name = "btnToolEllipse";
            btnToolEllipse.Size = new Size(60, 50);
            btnToolEllipse.TabIndex = 2;
            btnToolEllipse.Text = "◯";
            toolTip1.SetToolTip(btnToolEllipse, "Ellipse (E)");
            btnToolEllipse.UseVisualStyleBackColor = false;
            btnToolEllipse.Click += btnToolEllipse_Click;
            // 
            // btnToolCircle
            // 
            btnToolCircle.BackColor = Color.FromArgb(45, 45, 48);
            btnToolCircle.FlatAppearance.BorderSize = 0;
            btnToolCircle.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 66);
            btnToolCircle.FlatStyle = FlatStyle.Flat;
            btnToolCircle.Font = new Font("Segoe UI", 14F);
            btnToolCircle.ForeColor = Color.FromArgb(212, 212, 212);
            btnToolCircle.Location = new Point(5, 170);
            btnToolCircle.Margin = new Padding(0, 0, 0, 4);
            btnToolCircle.Name = "btnToolCircle";
            btnToolCircle.Size = new Size(60, 50);
            btnToolCircle.TabIndex = 3;
            btnToolCircle.Text = "○";
            toolTip1.SetToolTip(btnToolCircle, "Circle (C)");
            btnToolCircle.UseVisualStyleBackColor = false;
            btnToolCircle.Click += btnToolCircle_Click;
            // 
            // btnToolLine
            // 
            btnToolLine.BackColor = Color.FromArgb(45, 45, 48);
            btnToolLine.FlatAppearance.BorderSize = 0;
            btnToolLine.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 66);
            btnToolLine.FlatStyle = FlatStyle.Flat;
            btnToolLine.Font = new Font("Segoe UI", 14F);
            btnToolLine.ForeColor = Color.FromArgb(212, 212, 212);
            btnToolLine.Location = new Point(5, 224);
            btnToolLine.Margin = new Padding(0, 0, 0, 4);
            btnToolLine.Name = "btnToolLine";
            btnToolLine.Size = new Size(60, 50);
            btnToolLine.TabIndex = 4;
            btnToolLine.Text = "╱";
            toolTip1.SetToolTip(btnToolLine, "Line (L)");
            btnToolLine.UseVisualStyleBackColor = false;
            btnToolLine.Click += btnToolLine_Click;
            // 
            // btnToolText
            // 
            btnToolText.BackColor = Color.FromArgb(45, 45, 48);
            btnToolText.FlatAppearance.BorderSize = 0;
            btnToolText.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 66);
            btnToolText.FlatStyle = FlatStyle.Flat;
            btnToolText.Font = new Font("Segoe UI", 14F);
            btnToolText.ForeColor = Color.FromArgb(212, 212, 212);
            btnToolText.Location = new Point(5, 278);
            btnToolText.Margin = new Padding(0, 0, 0, 4);
            btnToolText.Name = "btnToolText";
            btnToolText.Size = new Size(60, 50);
            btnToolText.TabIndex = 5;
            btnToolText.Text = "T";
            toolTip1.SetToolTip(btnToolText, "Text (T)");
            btnToolText.UseVisualStyleBackColor = false;
            btnToolText.Click += btnToolText_Click;
            // 
            // btnToolTriangle
            // 
            btnToolTriangle.BackColor = Color.FromArgb(45, 45, 48);
            btnToolTriangle.FlatAppearance.BorderSize = 0;
            btnToolTriangle.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 66);
            btnToolTriangle.FlatStyle = FlatStyle.Flat;
            btnToolTriangle.Font = new Font("Segoe UI", 14F);
            btnToolTriangle.ForeColor = Color.FromArgb(212, 212, 212);
            btnToolTriangle.Location = new Point(5, 332);
            btnToolTriangle.Margin = new Padding(0, 0, 0, 4);
            btnToolTriangle.Name = "btnToolTriangle";
            btnToolTriangle.Size = new Size(60, 50);
            btnToolTriangle.TabIndex = 6;
            btnToolTriangle.Text = "△";
            toolTip1.SetToolTip(btnToolTriangle, "Triangle (G)");
            btnToolTriangle.UseVisualStyleBackColor = false;
            btnToolTriangle.Click += btnToolTriangle_Click;
            // 
            // btnToolArc
            // 
            btnToolArc.BackColor = Color.FromArgb(45, 45, 48);
            btnToolArc.FlatAppearance.BorderSize = 0;
            btnToolArc.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 66);
            btnToolArc.FlatStyle = FlatStyle.Flat;
            btnToolArc.Font = new Font("Segoe UI", 14F);
            btnToolArc.ForeColor = Color.FromArgb(212, 212, 212);
            btnToolArc.Location = new Point(5, 386);
            btnToolArc.Margin = new Padding(0, 0, 0, 4);
            btnToolArc.Name = "btnToolArc";
            btnToolArc.Size = new Size(60, 50);
            btnToolArc.TabIndex = 7;
            btnToolArc.Text = "⌒";
            toolTip1.SetToolTip(btnToolArc, "Arc (A)");
            btnToolArc.UseVisualStyleBackColor = false;
            btnToolArc.Click += btnToolArc_Click;
            // 
            // btnDeleteShape
            // 
            btnDeleteShape.BackColor = Color.FromArgb(45, 45, 48);
            btnDeleteShape.FlatAppearance.BorderSize = 0;
            btnDeleteShape.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 66);
            btnDeleteShape.FlatStyle = FlatStyle.Flat;
            btnDeleteShape.Font = new Font("Segoe UI", 14F);
            btnDeleteShape.ForeColor = Color.FromArgb(212, 212, 212);
            btnDeleteShape.Location = new Point(5, 450);
            btnDeleteShape.Margin = new Padding(0, 12, 0, 4);
            btnDeleteShape.Name = "btnDeleteShape";
            btnDeleteShape.Size = new Size(60, 50);
            btnDeleteShape.TabIndex = 8;
            btnDeleteShape.Text = "🗑";
            toolTip1.SetToolTip(btnDeleteShape, "Delete selected (Del)");
            btnDeleteShape.UseVisualStyleBackColor = false;
            btnDeleteShape.Click += btnDeleteShape_Click;
            // 
            // scenePanel
            // 
            scenePanel.BackColor = Color.FromArgb(30, 30, 30);
            scenePanel.Controls.Add(lstScenes);
            scenePanel.Controls.Add(sceneHeader);
            scenePanel.Controls.Add(btnDeleteScene);
            scenePanel.Controls.Add(btnNewScene);
            scenePanel.Dock = DockStyle.Fill;
            scenePanel.Location = new Point(0, 0);
            scenePanel.Name = "scenePanel";
            scenePanel.Size = new Size(180, 693);
            scenePanel.TabIndex = 0;
            // 
            // lstScenes
            // 
            lstScenes.BackColor = Color.FromArgb(30, 30, 30);
            lstScenes.BorderStyle = BorderStyle.None;
            lstScenes.Dock = DockStyle.Fill;
            lstScenes.Font = new Font("Consolas", 8.5F);
            lstScenes.ForeColor = Color.FromArgb(212, 212, 212);
            lstScenes.Location = new Point(0, 22);
            lstScenes.Name = "lstScenes";
            lstScenes.Size = new Size(180, 617);
            lstScenes.TabIndex = 0;
            lstScenes.SelectedIndexChanged += lstScenes_SelectedIndexChanged;
            // 
            // sceneHeader
            // 
            sceneHeader.BackColor = Color.FromArgb(45, 45, 48);
            sceneHeader.Dock = DockStyle.Top;
            sceneHeader.Font = new Font("Segoe UI", 7.5F);
            sceneHeader.ForeColor = Color.Gray;
            sceneHeader.Location = new Point(0, 0);
            sceneHeader.Name = "sceneHeader";
            sceneHeader.Padding = new Padding(6, 0, 0, 0);
            sceneHeader.Size = new Size(180, 22);
            sceneHeader.TabIndex = 2;
            sceneHeader.Text = "SCENES";
            sceneHeader.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // btnDeleteScene
            // 
            btnDeleteScene.BackColor = Color.FromArgb(45, 45, 48);
            btnDeleteScene.Dock = DockStyle.Bottom;
            btnDeleteScene.Enabled = false;
            btnDeleteScene.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            btnDeleteScene.FlatStyle = FlatStyle.Flat;
            btnDeleteScene.ForeColor = Color.FromArgb(224, 82, 82);
            btnDeleteScene.Location = new Point(0, 639);
            btnDeleteScene.Name = "btnDeleteScene";
            btnDeleteScene.Size = new Size(180, 26);
            btnDeleteScene.TabIndex = 1;
            btnDeleteScene.Text = "Delete";
            btnDeleteScene.UseVisualStyleBackColor = false;
            btnDeleteScene.Click += btnDeleteScene_Click;
            // 
            // btnNewScene
            // 
            btnNewScene.BackColor = Color.FromArgb(45, 45, 48);
            btnNewScene.Dock = DockStyle.Bottom;
            btnNewScene.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            btnNewScene.FlatStyle = FlatStyle.Flat;
            btnNewScene.ForeColor = Color.FromArgb(212, 212, 212);
            btnNewScene.Location = new Point(0, 665);
            btnNewScene.Name = "btnNewScene";
            btnNewScene.Size = new Size(180, 28);
            btnNewScene.TabIndex = 3;
            btnNewScene.Text = "+ New Scene";
            btnNewScene.UseVisualStyleBackColor = false;
            btnNewScene.Click += btnNewScene_Click;
            // 
            // splitOuter
            // 
            splitOuter.BackColor = Color.FromArgb(63, 63, 70);
            splitOuter.Dock = DockStyle.Fill;
            splitOuter.Location = new Point(0, 34);
            splitOuter.Name = "splitOuter";
            // 
            // splitOuter.Panel1
            // 
            splitOuter.Panel1.Controls.Add(scenePanel);
            splitOuter.Panel1MinSize = 120;
            // 
            // splitOuter.Panel2
            // 
            splitOuter.Panel2.Controls.Add(splitInner);
            splitOuter.Size = new Size(1200, 693);
            splitOuter.SplitterDistance = 180;
            splitOuter.TabIndex = 1;
            // 
            // splitInner
            // 
            splitInner.BackColor = Color.FromArgb(63, 63, 70);
            splitInner.Dock = DockStyle.Fill;
            splitInner.Location = new Point(0, 0);
            splitInner.Name = "splitInner";
            // 
            // splitInner.Panel1
            // 
            splitInner.Panel1.Controls.Add(canvasPanel);
            splitInner.Panel1.Controls.Add(toolboxPanel);
            // 
            // splitInner.Panel2
            // 
            splitInner.Panel2.Controls.Add(splitRight);
            splitInner.Panel2MinSize = 180;
            splitInner.Size = new Size(1016, 693);
            splitInner.SplitterDistance = 780;
            splitInner.TabIndex = 0;
            // 
            // canvasPanel
            // 
            canvasPanel.BackColor = Color.FromArgb(20, 20, 20);
            canvasPanel.Dock = DockStyle.Fill;
            canvasPanel.Location = new Point(70, 0);
            canvasPanel.Name = "canvasPanel";
            canvasPanel.Size = new Size(710, 693);
            canvasPanel.TabIndex = 0;
            canvasPanel.Paint += canvasPanel_Paint;
            canvasPanel.MouseDown += canvasPanel_MouseDown;
            canvasPanel.MouseMove += canvasPanel_MouseMove;
            canvasPanel.MouseUp += canvasPanel_MouseUp;
            canvasPanel.MouseWheel += canvasPanel_MouseWheel;
            canvasPanel.Resize += canvasPanel_Resize;
            // 
            // splitRight
            // 
            splitRight.BackColor = Color.FromArgb(63, 63, 70);
            splitRight.Dock = DockStyle.Fill;
            splitRight.Location = new Point(0, 0);
            splitRight.Name = "splitRight";
            splitRight.Orientation = Orientation.Horizontal;
            // 
            // splitRight.Panel1
            // 
            splitRight.Panel1.Controls.Add(layersPanel);
            splitRight.Panel1MinSize = 80;
            // 
            // splitRight.Panel2
            // 
            splitRight.Panel2.Controls.Add(rightBottom);
            splitRight.Panel2MinSize = 60;
            splitRight.Size = new Size(232, 693);
            splitRight.SplitterDistance = 179;
            splitRight.TabIndex = 0;
            // 
            // layersPanel
            // 
            layersPanel.BackColor = Color.FromArgb(30, 30, 30);
            layersPanel.Controls.Add(lstLayers);
            layersPanel.Controls.Add(layersHeader);
            layersPanel.Controls.Add(layerBtnRow);
            layersPanel.Dock = DockStyle.Fill;
            layersPanel.Location = new Point(0, 0);
            layersPanel.Name = "layersPanel";
            layersPanel.Size = new Size(232, 179);
            layersPanel.TabIndex = 0;
            // 
            // lstLayers
            // 
            lstLayers.BackColor = Color.FromArgb(30, 30, 30);
            lstLayers.BorderStyle = BorderStyle.None;
            lstLayers.Dock = DockStyle.Fill;
            lstLayers.Font = new Font("Consolas", 8.5F);
            lstLayers.ForeColor = Color.FromArgb(212, 212, 212);
            lstLayers.Location = new Point(0, 22);
            lstLayers.Name = "lstLayers";
            lstLayers.Size = new Size(232, 131);
            lstLayers.TabIndex = 0;
            lstLayers.SelectedIndexChanged += lstLayers_SelectedIndexChanged;
            lstLayers.MouseDown += lstLayers_MouseDown;
            // 
            // layersHeader
            // 
            layersHeader.BackColor = Color.FromArgb(45, 45, 48);
            layersHeader.Dock = DockStyle.Top;
            layersHeader.Font = new Font("Segoe UI", 7.5F);
            layersHeader.ForeColor = Color.Gray;
            layersHeader.Location = new Point(0, 0);
            layersHeader.Name = "layersHeader";
            layersHeader.Padding = new Padding(6, 0, 0, 0);
            layersHeader.Size = new Size(232, 22);
            layersHeader.TabIndex = 1;
            layersHeader.Text = "LAYERS";
            layersHeader.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // layerBtnRow
            // 
            layerBtnRow.BackColor = Color.FromArgb(45, 45, 48);
            layerBtnRow.Controls.Add(btnLayerUp);
            layerBtnRow.Controls.Add(btnLayerDown);
            layerBtnRow.Controls.Add(btnAddLayer);
            layerBtnRow.Controls.Add(btnDeleteLayer);
            layerBtnRow.Dock = DockStyle.Bottom;
            layerBtnRow.Location = new Point(0, 153);
            layerBtnRow.Name = "layerBtnRow";
            layerBtnRow.Padding = new Padding(2);
            layerBtnRow.Size = new Size(232, 26);
            layerBtnRow.TabIndex = 2;
            // 
            // btnLayerUp
            // 
            btnLayerUp.BackColor = Color.FromArgb(45, 45, 48);
            btnLayerUp.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            btnLayerUp.FlatStyle = FlatStyle.Flat;
            btnLayerUp.ForeColor = Color.FromArgb(212, 212, 212);
            btnLayerUp.Location = new Point(5, 5);
            btnLayerUp.Name = "btnLayerUp";
            btnLayerUp.Size = new Size(28, 22);
            btnLayerUp.TabIndex = 2;
            btnLayerUp.Text = "▲";
            toolTip1.SetToolTip(btnLayerUp, "Move layer up");
            btnLayerUp.UseVisualStyleBackColor = false;
            btnLayerUp.Click += btnLayerUp_Click;
            // 
            // btnLayerDown
            // 
            btnLayerDown.BackColor = Color.FromArgb(45, 45, 48);
            btnLayerDown.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            btnLayerDown.FlatStyle = FlatStyle.Flat;
            btnLayerDown.ForeColor = Color.FromArgb(212, 212, 212);
            btnLayerDown.Location = new Point(39, 5);
            btnLayerDown.Name = "btnLayerDown";
            btnLayerDown.Size = new Size(28, 22);
            btnLayerDown.TabIndex = 3;
            btnLayerDown.Text = "▼";
            toolTip1.SetToolTip(btnLayerDown, "Move layer down");
            btnLayerDown.UseVisualStyleBackColor = false;
            btnLayerDown.Click += btnLayerDown_Click;
            // 
            // btnAddLayer
            // 
            btnAddLayer.BackColor = Color.FromArgb(45, 45, 48);
            btnAddLayer.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            btnAddLayer.FlatStyle = FlatStyle.Flat;
            btnAddLayer.ForeColor = Color.FromArgb(212, 212, 212);
            btnAddLayer.Location = new Point(73, 5);
            btnAddLayer.Name = "btnAddLayer";
            btnAddLayer.Size = new Size(28, 22);
            btnAddLayer.TabIndex = 0;
            btnAddLayer.Text = "+";
            btnAddLayer.UseVisualStyleBackColor = false;
            btnAddLayer.Click += btnAddLayer_Click;
            // 
            // btnDeleteLayer
            // 
            btnDeleteLayer.BackColor = Color.FromArgb(45, 45, 48);
            btnDeleteLayer.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            btnDeleteLayer.FlatStyle = FlatStyle.Flat;
            btnDeleteLayer.ForeColor = Color.FromArgb(224, 82, 82);
            btnDeleteLayer.Location = new Point(107, 5);
            btnDeleteLayer.Name = "btnDeleteLayer";
            btnDeleteLayer.Size = new Size(28, 22);
            btnDeleteLayer.TabIndex = 1;
            btnDeleteLayer.Text = "✕";
            btnDeleteLayer.UseVisualStyleBackColor = false;
            btnDeleteLayer.Click += btnDeleteLayer_Click;
            // 
            // rightBottom
            // 
            rightBottom.BackColor = Color.FromArgb(30, 30, 30);
            rightBottom.Controls.Add(pnlProps);
            rightBottom.Controls.Add(propsHeader);
            rightBottom.Dock = DockStyle.Fill;
            rightBottom.Location = new Point(0, 0);
            rightBottom.Name = "rightBottom";
            rightBottom.Size = new Size(232, 510);
            rightBottom.TabIndex = 0;
            // 
            // pnlProps
            // 
            pnlProps.AutoScroll = true;
            pnlProps.BackColor = Color.FromArgb(30, 30, 30);
            pnlProps.Dock = DockStyle.Fill;
            pnlProps.FlowDirection = FlowDirection.TopDown;
            pnlProps.Location = new Point(0, 22);
            pnlProps.Name = "pnlProps";
            pnlProps.Padding = new Padding(4);
            pnlProps.Size = new Size(232, 488);
            pnlProps.TabIndex = 0;
            // 
            // propsHeader
            // 
            propsHeader.BackColor = Color.FromArgb(45, 45, 48);
            propsHeader.Dock = DockStyle.Top;
            propsHeader.Font = new Font("Segoe UI", 7.5F);
            propsHeader.ForeColor = Color.Gray;
            propsHeader.Location = new Point(0, 0);
            propsHeader.Name = "propsHeader";
            propsHeader.Padding = new Padding(6, 0, 0, 0);
            propsHeader.Size = new Size(232, 22);
            propsHeader.TabIndex = 1;
            propsHeader.Text = "PROPERTIES";
            propsHeader.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // statusStrip
            // 
            statusStrip.BackColor = Color.FromArgb(45, 45, 48);
            statusStrip.Items.AddRange(new ToolStripItem[] { lblCursor, lblSelected, lblSceneInfo });
            statusStrip.Location = new Point(0, 727);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(1200, 22);
            statusStrip.SizingGrip = false;
            statusStrip.TabIndex = 2;
            // 
            // lblCursor
            // 
            lblCursor.ForeColor = Color.Gray;
            lblCursor.Name = "lblCursor";
            lblCursor.Size = new Size(60, 17);
            lblCursor.Text = "x: —  y: —";
            // 
            // lblSelected
            // 
            lblSelected.ForeColor = Color.Gray;
            lblSelected.Name = "lblSelected";
            lblSelected.Size = new Size(1125, 17);
            lblSelected.Spring = true;
            lblSelected.Text = "nothing selected";
            // 
            // lblSceneInfo
            // 
            lblSceneInfo.Alignment = ToolStripItemAlignment.Right;
            lblSceneInfo.ForeColor = Color.FromArgb(156, 220, 254);
            lblSceneInfo.Name = "lblSceneInfo";
            lblSceneInfo.Size = new Size(0, 17);
            // 
            // refreshTimer
            // 
            refreshTimer.Interval = 400;
            refreshTimer.Tick += refreshTimer_Tick;
            // 
            // SceneEditorForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(30, 30, 30);
            ClientSize = new Size(1200, 749);
            Controls.Add(splitOuter);
            Controls.Add(toolFlow);
            Controls.Add(statusStrip);
            Font = new Font("Segoe UI", 9F);
            ForeColor = Color.FromArgb(212, 212, 212);
            KeyPreview = true;
            MinimumSize = new Size(800, 500);
            Name = "SceneEditorForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Scene Editor";
            Load += SceneEditorForm_Load;
            toolFlow.ResumeLayout(false);
            toolFlow.PerformLayout();
            toolboxPanel.ResumeLayout(false);
            scenePanel.ResumeLayout(false);
            splitOuter.Panel1.ResumeLayout(false);
            splitOuter.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitOuter).EndInit();
            splitOuter.ResumeLayout(false);
            splitInner.Panel1.ResumeLayout(false);
            splitInner.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitInner).EndInit();
            splitInner.ResumeLayout(false);
            splitRight.Panel1.ResumeLayout(false);
            splitRight.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitRight).EndInit();
            splitRight.ResumeLayout(false);
            layersPanel.ResumeLayout(false);
            layerBtnRow.ResumeLayout(false);
            rightBottom.ResumeLayout(false);
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}
