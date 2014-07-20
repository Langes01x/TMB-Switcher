namespace TMB_Switcher
{
    partial class GraphSelectionBar
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
            this.leftSplitter = new System.Windows.Forms.Splitter();
            this.leftPanel = new System.Windows.Forms.Panel();
            this.rightPanel = new System.Windows.Forms.Panel();
            this.rightSplitter = new System.Windows.Forms.Splitter();
            this.middlePanel = new System.Windows.Forms.Panel();
            this.SuspendLayout();
            // 
            // leftSplitter
            // 
            this.leftSplitter.BackColor = System.Drawing.Color.Navy;
            this.leftSplitter.Location = new System.Drawing.Point(479, 0);
            this.leftSplitter.MinExtra = 0;
            this.leftSplitter.MinSize = 0;
            this.leftSplitter.Name = "leftSplitter";
            this.leftSplitter.Size = new System.Drawing.Size(5, 27);
            this.leftSplitter.TabIndex = 0;
            this.leftSplitter.TabStop = false;
            this.leftSplitter.SplitterMoving += new System.Windows.Forms.SplitterEventHandler(this.leftSplitter_SplitterMoving);
            this.leftSplitter.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.leftSplitter_SplitterMoved);
            // 
            // leftPanel
            // 
            this.leftPanel.BackColor = System.Drawing.Color.Transparent;
            this.leftPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.leftPanel.Dock = System.Windows.Forms.DockStyle.Left;
            this.leftPanel.Location = new System.Drawing.Point(0, 0);
            this.leftPanel.Name = "leftPanel";
            this.leftPanel.Size = new System.Drawing.Size(479, 27);
            this.leftPanel.TabIndex = 1;
            // 
            // rightPanel
            // 
            this.rightPanel.BackColor = System.Drawing.Color.Transparent;
            this.rightPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rightPanel.Dock = System.Windows.Forms.DockStyle.Right;
            this.rightPanel.Location = new System.Drawing.Point(500, 0);
            this.rightPanel.Name = "rightPanel";
            this.rightPanel.Size = new System.Drawing.Size(0, 27);
            this.rightPanel.TabIndex = 2;
            // 
            // rightSplitter
            // 
            this.rightSplitter.BackColor = System.Drawing.Color.Navy;
            this.rightSplitter.Dock = System.Windows.Forms.DockStyle.Right;
            this.rightSplitter.Location = new System.Drawing.Point(495, 0);
            this.rightSplitter.MinExtra = 0;
            this.rightSplitter.MinSize = 0;
            this.rightSplitter.Name = "rightSplitter";
            this.rightSplitter.Size = new System.Drawing.Size(5, 27);
            this.rightSplitter.TabIndex = 3;
            this.rightSplitter.TabStop = false;
            this.rightSplitter.SplitterMoving += new System.Windows.Forms.SplitterEventHandler(this.rightSplitter_SplitterMoving);
            this.rightSplitter.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.rightSplitter_SplitterMoved);
            // 
            // middlePanel
            // 
            this.middlePanel.BackColor = System.Drawing.Color.DodgerBlue;
            this.middlePanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.middlePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.middlePanel.Location = new System.Drawing.Point(484, 0);
            this.middlePanel.Name = "middlePanel";
            this.middlePanel.Size = new System.Drawing.Size(11, 27);
            this.middlePanel.TabIndex = 4;
            // 
            // GraphSelectionBar
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.middlePanel);
            this.Controls.Add(this.rightSplitter);
            this.Controls.Add(this.rightPanel);
            this.Controls.Add(this.leftSplitter);
            this.Controls.Add(this.leftPanel);
            this.MinimumSize = new System.Drawing.Size(100, 10);
            this.Name = "GraphSelectionBar";
            this.Size = new System.Drawing.Size(500, 27);
            this.SizeChanged += new System.EventHandler(this.GraphSelectionBar_SizeChanged);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Splitter leftSplitter;
        private System.Windows.Forms.Panel leftPanel;
        private System.Windows.Forms.Panel rightPanel;
        private System.Windows.Forms.Splitter rightSplitter;
        private System.Windows.Forms.Panel middlePanel;
    }
}
