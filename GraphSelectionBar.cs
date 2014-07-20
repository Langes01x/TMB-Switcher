using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TMB_Switcher
{
    public partial class GraphSelectionBar : UserControl
    {
        private int maxValue = 24;
        private int leftValue = 23;
        private int rightValue = 24;
        private bool codeUpdate = false;

        public event EventHandler<int> LeftMoving;
        public event EventHandler<int> RightMoving;
        private int lastLeftValue = 23;
        private int lastRightValue = 24;
        private ToolTip tooltip = null;

        public event EventHandler<int> LeftValueChanged;
        public event EventHandler<int> RightValueChanged;

        public GraphSelectionBar()
        {
            InitializeComponent();
        }

        public int MaxValue
        {
            get { return maxValue; }
            set {
                if (value < 1)
                    value = 1;
                maxValue = value;
                if (rightValue > maxValue)
                    RightValue = maxValue;
                if (leftValue > maxValue)
                    LeftValue = rightValue - 1;
                else
                {
                    LeftValue = leftValue;
                    RightValue = rightValue;
                }
            }
        }

        public int SplitterSize
        {
            get { return leftSplitter.Width; }
            set
            {
                leftSplitter.Width = value;
                rightSplitter.Width = value;
            }
        }

        public double SectionSize
        {
            get { return (Width - 2.0 * SplitterSize) / MaxValue; }
        }

        public int LeftValue
        {
            get
            {
                return (int)Math.Ceiling(leftPanel.Width / SectionSize);
            }
            set
            {
                if (value < 0)
                    value = 0;
                else if (value >= MaxValue)
                    value = MaxValue - 1;
                if (value >= RightValue)
                    RightValue = value + 1;
                leftValue = value;
                codeUpdate = true;
                leftPanel.Width = (int)(leftValue * SectionSize);
                codeUpdate = false;
            }
        }

        public int RightValue
        {
            get
            {
                return (int)(MaxValue - (rightPanel.Width / SectionSize));
            }
            set
            {
                if (value < 1)
                    value = 1;
                else if (value > MaxValue)
                    value = MaxValue;
                if (value <= LeftValue)
                    LeftValue = value - 1;
                rightValue = value;
                codeUpdate = true;
                rightPanel.Width = (int)((MaxValue - rightValue) * SectionSize);
                codeUpdate = false;
            }
        }

        private void leftSplitter_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (codeUpdate)
                return;
            LeftValue = LeftValue;
            if (LeftValueChanged != null)
                LeftValueChanged(this, LeftValue);
            if (tooltip != null)
                clearTooltip(tooltip);
        }

        private void rightSplitter_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (codeUpdate)
                return;
            RightValue = RightValue;
            if (RightValueChanged != null)
                RightValueChanged(this, RightValue);
            if (tooltip != null)
                clearTooltip(tooltip);
        }

        private void GraphSelectionBar_SizeChanged(object sender, EventArgs e)
        {
            LeftValue = leftValue;
            RightValue = rightValue;
        }

        private void leftSplitter_SplitterMoving(object sender, SplitterEventArgs e)
        {
            int left = (int)(e.SplitX / SectionSize);
            if (left != lastLeftValue && LeftMoving != null)
            {
                LeftMoving(this, left);
                lastLeftValue = left;
            }
        }

        private void rightSplitter_SplitterMoving(object sender, SplitterEventArgs e)
        {
            int right = (int)(e.SplitX / SectionSize);
            if (right != lastRightValue && RightMoving != null)
            {
                RightMoving(this, right);
                lastRightValue = right;
            }
        }

        public void clearTooltip(ToolTip tooltip)
        {
            tooltip.SetToolTip(leftPanel, null);
            tooltip.SetToolTip(leftSplitter, null);
            tooltip.SetToolTip(middlePanel, null);
            tooltip.SetToolTip(rightSplitter, null);
            tooltip.SetToolTip(rightPanel, null);
        }

        public void setTooltip(ToolTip tooltip, string message)
        {
            tooltip.SetToolTip(leftPanel, message);
            tooltip.SetToolTip(leftSplitter, message);
            tooltip.SetToolTip(middlePanel, message);
            tooltip.SetToolTip(rightSplitter, message);
            tooltip.SetToolTip(rightPanel, message);
            this.tooltip = tooltip;
        }
    }
}
