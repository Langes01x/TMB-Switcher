#region Namespaces Used
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Reflection;
using System.IO;
using System.Windows.Forms.DataVisualization.Charting;
using System.Diagnostics;
using System.Globalization;
#endregion

namespace TMB_Switcher
{
    public partial class Switcher : Form
    {
        #region Variables

        // Thread and API handles
        private static Thread bgThread = null;
        private static TMB_API tmbAPI = null;
        private static Miner_API minerAPI = null;

        // Configuration variables
        private static string configFileLocation = "TMBSwitcher.conf";
        private static bool verbose = false;
        private static bool noAPI = false;
        private static Dictionary<string, object> config = null;
        private static string profitLogFile = null;
        private static string minerName = "sgminer";

        // Refresh variables
        private static bool refreshPool = true;
        private static bool refreshMiner = true;
        private static bool refreshSettings = true;
        private static DateTime lastPoolRefresh = DateTime.Now;
        private static DateTime lastMinerRefresh = DateTime.Now;
        private static DateTime lastUpdateCheck = DateTime.Now.AddHours(-1);

        // Historical data
        private static DataTable poolProfitInfo = null;
        private static DataTable minerInfo = null;
        private static int profitRowCount = 0;
        private static double averageProfit = 0.0;
        private static double minProfit = double.NaN;
        private static double maxProfit = double.NaN;
        private static int graphMovingAveragePoints = 30;

        // Tray icon
        private static NotifyIcon trayIcon = null;

        // State information
        private static string currentAlgo = "none";
        private static string previousAlgo = "none";
        private static MiningState currentState = MiningState.Starting;
        private static DateTime lastStartTime = DateTime.Now;
        private static DateTime lastSwitch = DateTime.Now;
        private static bool switchingAlgorithm = false;
        private static string statusFormat = null;

        private enum MiningState
        {
            Stopped,
            Paused,
            Mining,
            Starting
        }

        // Version information
        private static string currentVersionFormat = null;
        private static string latestVersionFormat = null;
        private static Version lastVersionSeen = null;

        private static DataTable algorithms = null;

        // Current miner data
        private static DataTable poolList = null;
        private static int numGPU = 0;

        // Chart last X mouse position
        private static DataPoint lastProfitHitPoint = null;
        private static string lastProfitSeriesName = null;
        private static DataPoint lastMinerHitPoint = null;
        private static string lastMinerSeriesName = null;

        #endregion

        #region Initialization

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            if (!parseArguments(args))
            {
                Application.Exit();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Switcher());
        }

        public Switcher()
        {
            InitializeComponent();
            // Auto-select None on startup
            switchingAlgorithm = true;
            currentlyMiningCombo.SelectedIndex = 0;
            switchingAlgorithm = false;
            statusFormat = Text;
            latestVersionFormat = latestVersionLabel.Text;
            currentVersionFormat = currentVersionLabel.Text;
            trayIcon = new NotifyIcon();
            setStatus(Properties.Resources.redIcon, "Starting Up");
            trayIcon.Visible = true;

            linkLabel.Links[0].LinkData = null;
            btcAddress.Links[0].LinkData = null;
            ltcAddress.Links[0].LinkData = null;
            drkAddress.Links[0].LinkData = null;

            if (noAPI)
            {
                tabControl.TabPages.Remove(poolsPage);
                tabControl.TabPages.Remove(devicesPage);
                tabControl.TabPages.Remove(minerHistoryPage);
                minerAddressText.Enabled = false;
                minerPortText.Enabled = false;
                batchFileButton.Enabled = false;
                batchFileText.Enabled = false;
            }
        }

        private void Switcher_Load(object sender, EventArgs e)
        {
            try
            {
                // Setup basic algorithm information
                algorithms = new DataTable();
                DataColumn name = algorithms.Columns.Add("name", typeof(string));
                algorithms.Columns.Add("displayName", typeof(string));
                algorithms.Columns.Add("type", typeof(string));
                algorithms.Columns.Add("kernels", typeof(string));
                algorithms.PrimaryKey = new DataColumn[] { name };

                // Populate list of algorithms
                algorithms.Rows.Add("scrypt", "Scrypt", "Scrypt", "*");
                algorithms.Rows.Add("nscrypt", "N-Scrypt", "NScrypt", "");
                algorithms.Rows.Add("x11", "X11", "X11", "darkcoin,x11");
                algorithms.Rows.Add("x13", "X13", "X13", "marucoin,x13");
                algorithms.Rows.Add("x15", "X15", "X15", "bitblock,x15");
                algorithms.Rows.Add("nist5", "NIST5", "NIST", "talkcoin,nist");
                //algorithms.Rows.Add("keccak", "Keccak", "Keccak", "maxcoin,keccak");

                // Extra algorithm information
                algorithms.Columns.Add("enabled", typeof(bool));
                algorithms.Columns.Add("graphCheckbox", typeof(CheckBox));
                algorithms.Columns.Add("settingCheckbox", typeof(CheckBox));
                algorithms.Columns.Add("multiplierTextbox", typeof(TextBox));
                algorithms.Columns.Add("offsetTextbox", typeof(TextBox));
                algorithms.Columns.Add("batchTextbox", typeof(TextBox));
                algorithms.Columns.Add("batchButton", typeof(Button));

                // Set up profit table and generate per-algorithm controls
                poolProfitInfo = new DataTable();
                poolProfitInfo.Columns.Add("time", typeof(DateTime));
                poolProfitInfo.Columns.Add("composite", typeof(double));
                int i = 1;
                foreach (DataRow algorithm in algorithms.Rows)
                {
                    poolProfitInfo.Columns.Add(algorithm["name"].ToString(), typeof(double));
                    string displayName = algorithm["displayName"].ToString();
                    currentlyMiningCombo.Items.Add(displayName);

                    // Generate profit graph checkbox
                    CheckBox checkbox = new CheckBox();
                    algorithm["graphCheckBox"] = checkbox;
                    checkbox.Text = displayName;
                    checkbox.CheckedChanged += graphCheckbox_CheckedChanged;
                    profitFlowLayoutPanel.Controls.Add(checkbox);
                    checkbox.Tag = algorithm;
                    checkbox.AutoSize = true;

                    settingTableLayoutPanel.RowCount++;

                    // Generate setting checkbox
                    checkbox = new CheckBox();
                    algorithm["settingCheckbox"] = checkbox;
                    checkbox.Text = displayName + ":";
                    tooltip.SetToolTip(checkbox, String.Format("Whether mining of {0} is enabled or not", displayName));
                    settingTableLayoutPanel.Controls.Add(checkbox, 0, i);
                    checkbox.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                    checkbox.AutoSize = true;

                    // Generate multiplier textbox
                    TextBox textbox = new TextBox();
                    algorithm["multiplierTextbox"] = textbox;
                    textbox.Text = "1.0";
                    tooltip.SetToolTip(textbox, String.Format("Multiplier applied to {0} profitability (applied before offset)", displayName));
                    settingTableLayoutPanel.Controls.Add(textbox, 1, i);
                    textbox.Dock = DockStyle.Fill;

                    // Generate offset textbox
                    textbox = new TextBox();
                    algorithm["offsetTextbox"] = textbox;
                    textbox.Text = "0.0";
                    tooltip.SetToolTip(textbox, String.Format("Offset applied to {0} profitability (applied after multiplier)", displayName));
                    settingTableLayoutPanel.Controls.Add(textbox, 2, i);
                    textbox.Dock = DockStyle.Fill;

                    // Generate batch textbox
                    textbox = new TextBox();
                    algorithm["batchTextbox"] = textbox;
                    tooltip.SetToolTip(textbox, String.Format("Location of the batch file used to start up the {0} miner (if not using sgminer v5.0)", displayName));
                    settingTableLayoutPanel.Controls.Add(textbox, 3, i);
                    textbox.Dock = DockStyle.Fill;

                    // Generate batch button
                    Button button = new Button();
                    algorithm["batchButton"] = button;
                    button.Text = "...";
                    button.Click += batchButton_Click;
                    tooltip.SetToolTip(button, String.Format("Select the batch file used to start up the {0} miner (if not using sgminer v5.0)", displayName));
                    settingTableLayoutPanel.Controls.Add(button, 4, i);
                    button.Tag = algorithm;
                    button.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                    button.AutoSize = true;

                    i++;
                }

                // Read config file
                readConfigFile();

                // Set up miner table
                minerInfo = new DataTable();
                minerInfo.Columns.Add("time", typeof(DateTime));
                minerInfo.Columns.Add("totalHash", typeof(double));
                // Per GPU temp and hashrate columns added later

                setupCharts();

                bgThread = new Thread(new ThreadStart(backgroundThread));
                bgThread.IsBackground = true;
                bgThread.Start();
            }
            catch (Exception ex)
            {
                // Log failure
                Utilities.WriteLog(ex.Message);
                Utilities.WriteLog(ex.StackTrace);
                if (verbose)
                    MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        private void setupCharts()
        {
            profitChart.DataSource = poolProfitInfo;
            foreach (DataRow algorithm in algorithms.Rows)
            {
                string algorithmName = algorithm["name"].ToString();
                Series series = profitChart.Series.Add(algorithmName);
                series.XValueMember = "time";
                series.YValueMembers = algorithmName;
                series.XValueType = ChartValueType.DateTime;
                series.YValueType = ChartValueType.Double;
                series.Enabled = false;
            }
            Series compositeSeries = profitChart.Series.Add("composite");
            compositeSeries.XValueMember = "time";
            compositeSeries.YValueMembers = "composite";
            compositeSeries.XValueType = ChartValueType.DateTime;
            compositeSeries.YValueType = ChartValueType.Double;
            compositeSeries.Enabled = false;
            profitChart.ChartAreas[0].AxisX.LabelStyle.Format = "dd/MM/yyyy\nhh:mm:ss";

            minerChart.DataSource = minerInfo;
            minerChart.ChartAreas[0].AxisX.LabelStyle.Format = "dd/MM/yyyy\nhh:mm:ss";
        }

        private static bool parseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-h":
                    case "--help":
                    case "/?":
                        printUsage();
                        return false;
                    case "-c":
                    case "--config":
                        if (args.Length <= i + 1 || args[i + 1][0] == '-' || args[i + 1][0] == '/')
                        {
                            Console.Out.WriteLine("Missing argument for -c.\n");
                            printUsage();
                            return false;
                        }
                        configFileLocation = args[++i].Trim('"');
                        break;
                    case "-v":
                    case "-verbose":
                        verbose = true;
                        break;
                    case "-n":
                    case "--no-api":
                        noAPI = true;
                        break;
                    default:
                        Console.Out.WriteLine("Bad argument: " + args[i] + "\n");
                        printUsage();
                        return false;
                }
            }
            return true;
        }

        private static void printUsage()
        {
            StringBuilder usage = new StringBuilder();
            usage.Append("TMB Switcher Version: ");
            usage.AppendLine(Assembly.GetEntryAssembly().GetName().Version.ToString());
            usage.AppendLine("\n\nArguments:");
            usage.AppendLine("    -h,--help\tThis help message.");
            usage.AppendLine("    -c,--config\tThe location of the config file. (Default: TMBSwitcher.conf)");
            usage.AppendLine("    -n,--no-api\tUsed for miners that don't expose an API (i.e. cudaminer/ccminer)");
            usage.AppendLine("    -v,--verbose\tDisplays verbose usage information (do not use this unattended as it will lock up the switcher)");
            MessageBox.Show(usage.ToString());
        }

        private void Switcher_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Check if the user wished to exit
            if (MessageBox.Show("Are you sure you want to close TMB Switcher?", this.Text, MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            // Kill miner on exit if told to do so
            if (config.GetBool("killMinerOnExit"))
                killMiner();

            // Kill background thread and wait for it to exit
            bgThread.Abort();
            bgThread.Join(1000);
            trayIcon.Visible = false;
        }

        #endregion

        #region Background Thread

        private void backgroundThread()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            while (true)
            {
                try
                {
                    DateTime currentTime = DateTime.Now;

                    if (lastUpdateCheck.AddHours(1) < currentTime)
                    {
                        Invoke(new MethodInvoker(delegate { checkForUpdate(); }));
                    }

                    // If user has changed any settings and applied them then we need to
                    // update our pool and miner connectivity
                    Invoke(new MethodInvoker(delegate { updateSettings(); }));

                    // If both miner and pool API are not connected update stats and go back to sleep
                    if (minerAPI == null && tmbAPI == null)
                    {
                        setStatus(Properties.Resources.redIcon, "APIs Disconnected");
                        clearMinerStats();
                        clearPoolStats();

                        Thread.Sleep(1000);
                        continue;
                    }

                    if (minerAPI != null || noAPI)
                    {
                        // Figure out if we want to auto-update the miner stats
                        double minerRefreshRate = config.GetDouble("minerRefreshRate");
                        if (config.GetBool("monitorMiner") && lastMinerRefresh.AddSeconds(minerRefreshRate) < currentTime)
                        {
                            refreshMiner = true;
                            lastMinerRefresh = currentTime;
                        }

                        // Need to verify miner is active before attempting any switching
                        if (refreshMiner)
                        {
                            if (noAPI)
                            {
                                Miner_API.IsActive = currentState == MiningState.Stopped || minerRunning();

                                // Switch to mining state if starting and process is alive
                                if (currentState == MiningState.Starting && Miner_API.IsActive)
                                    currentState = MiningState.Mining;

                                // Check if miner is running and if necessary restart it
                                if (!Miner_API.IsActive)
                                {
                                    // Give the miner X seconds to get started before trying to restart it (default 30)
                                    int startupDeadDelay = config.GetInt("startupDeadDelay");
                                    if (startupDeadDelay < 0)
                                        startupDeadDelay = 30;
                                    if (currentState != MiningState.Starting || DateTime.Now > lastStartTime.AddSeconds(startupDeadDelay))
                                    {
                                        // If we don't want to mine anything then don't restart the miner
                                        // (unless we want to start mining which is handled later)
                                        if (currentAlgo == "none" || currentAlgo == "off")
                                            Miner_API.IsActive = true;
                                        else
                                        {
                                            // Kill existing miner in case it was just having temporary issues
                                            // so it doesn't get in the way of the newly started miner
                                            string batchFile = config.GetString(currentAlgo + "Batch");
                                            if (!string.IsNullOrEmpty(batchFile) && File.Exists(batchFile))
                                            {
                                                killMiner();
                                                startMiner(batchFile);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Gather and store miner info
                                poolList = minerAPI.pools();
                                Dictionary<string, double> summary = minerAPI.summary();
                                DataTable deviceList = minerAPI.devices();

                                // Update table with miner info
                                Invoke(new MethodInvoker(delegate { updateMinerInfo(deviceList); }));

                                Invoke(new MethodInvoker(delegate { updateMinerUI(summary, deviceList); }));
                            }

                            refreshMiner = false;
                        }
                    }

                    if (tmbAPI != null)
                    {
                        // Figure out if we want to auto-update the pool stats
                        double poolRefreshRate = config.GetDouble("poolRefreshRate");
                        if (config.GetBool("monitorPool") && lastPoolRefresh.AddSeconds(poolRefreshRate) < currentTime)
                        {
                            refreshPool = true;
                            lastPoolRefresh = currentTime;
                        }

                        if (refreshPool)
                        {
                            // Get pool hash info for display and add it to history
                            Dictionary<string, double> hashInfo = tmbAPI.hashInfo();

                            // Get balance data for display
                            Dictionary<string, double> balanceInfo = tmbAPI.balance();

                            // Get profit info for switching/logging/display
                            Dictionary<string, double> profitInfo = tmbAPI.bestAlgo();
                            if (profitInfo != null)
                            {
                                adjustProfit(profitInfo);
                                if (config.GetBool("enableProfitSwitching") && Miner_API.IsActive &&
                                     ((minerAPI != null && poolList != null && poolList.Rows.Count > 0) || noAPI) &&
                                    (currentState == MiningState.Mining || currentState == MiningState.Starting))
                                {
                                    if (currentState == MiningState.Starting)
                                        currentState = MiningState.Mining;
                                    // Figure out if we need to switch
                                    string bestAlgo = checkProfit(profitInfo);
                                    if (bestAlgo != "none" && bestAlgo != currentAlgo)
                                    {
                                        // Perform Switch
                                        switchAlgorithm(bestAlgo);
                                    }
                                }

                                // Add composite value to profit info
                                double compositeValue = 0.0;
                                if (currentAlgo != "none" && currentAlgo != "off")
                                    compositeValue = profitInfo[currentAlgo];
                                profitInfo.Add("composite", compositeValue);

                                // Update log with profit info
                                if (!string.IsNullOrEmpty(profitLogFile))
                                    Utilities.WriteProfitLog(profitInfo, profitLogFile);

                                // Add profit info to history
                                Utilities.AddToTable(profitInfo, poolProfitInfo);
                                // Delete any rows older than 24 hours to keep memory usage lower
                                Utilities.DeleteOlderThan24H(poolProfitInfo);
                            }

                            // Update UI with pool info
                            Invoke(new MethodInvoker(delegate { updatePoolUI(poolProfitInfo, hashInfo, balanceInfo); }));
                            refreshPool = false;
                        }
                    }

                    // Show status in the HMI
                    Invoke(new MethodInvoker(delegate { setHMIStatus(); }));
                }
                catch (Exception ex)
                {
                    // Log failure
                    Utilities.WriteLog(ex.Message);
                    Utilities.WriteLog(ex.StackTrace);
                    if (verbose)
                        MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace);
                }

                // Sleep for a second since the user can request a refresh at any time
                Thread.Sleep(1000);
            }
        }

        #endregion

        #region HMI Update Methods

        private void setHMIStatus()
        {
            if (!TMB_API.IsActive && !Miner_API.IsActive)
                setStatus(Properties.Resources.redIcon, "APIs Disconnected");
            else if (!Miner_API.IsActive)
                setStatus(Properties.Resources.redIcon, "Miner Disconnected");
            else if (!TMB_API.IsActive)
                setStatus(Properties.Resources.redIcon, "Pool Disconnected");
            else if (currentState == MiningState.Stopped)
                setStatus(Properties.Resources.redIcon, "Mining Stopped");
            else if (currentState == MiningState.Paused)
                setStatus(Properties.Resources.redIcon, "Mining Paused");
            else if (currentState == MiningState.Starting)
                setStatus(Properties.Resources.redIcon, "Starting Miner");
            else if (currentAlgo == "off" || currentAlgo == "none")
                setStatus(Properties.Resources.redIcon, "Not Mining");
            else
                setStatus(Properties.Resources.greenIcon, "Mining " + mapInternalToDisplay(currentAlgo));
            setStatusButtons();
        }

        private string mapInternalToDisplay(string algorithm)
        {
            DataRow row = algorithms.Rows.Find(algorithm);
            if (row == null)
                return "None";
            else
                return row["displayName"].ToString();
        }

        private void checkForUpdate()
        {
            Version currentVersion = Assembly.GetEntryAssembly().GetName().Version;
            currentVersionLabel.Text = string.Format(currentVersionFormat, currentVersion);
            string updateMessage = null;
            Version latestVersion = Utilities.getLatestVersion(out updateMessage);
            if (latestVersion != null)
            {
                latestVersionLabel.Text = string.Format(latestVersionFormat, latestVersion);
                versionMessage.Text = updateMessage;
                if (latestVersion > currentVersion && lastVersionSeen != latestVersion)
                {
                    lastVersionSeen = latestVersion;
                    trayIcon.ShowBalloonTip(2000, "New Version Available", string.Format("Version {0} is now available", latestVersion), ToolTipIcon.Info);
                }
            }
            else
            {
                latestVersionLabel.Text = string.Format(latestVersionFormat, "Could not find latest version");
            }
        }

        private void updateMinerUI(Dictionary<string, double> summary, DataTable deviceList)
        {
            if (poolList != null)
            {
                // Reload pool list making sure that it refreshes without the user noticing
                int selectedRow = -1;
                if (poolView.SelectedRows.Count > 0)
                    selectedRow = poolView.SelectedRows[0].Index;
                int horizScroll = poolView.HorizontalScrollingOffset;
                int vertScroll = poolView.FirstDisplayedScrollingRowIndex;

                poolView.DataSource = poolList;

                // Hide unnecessary columns
                // Sort by priority so that when user changes priority they sort appropriately
                foreach (DataGridViewColumn col in poolView.Columns)
                {
                    switch (col.Name.ToLowerInvariant())
                    {
                        case "pool":
                        case "url":
                        case "status":
                        case "user":
                        case "algorithm":
                        case "name":
                            col.Visible = true;
                            col.SortMode = DataGridViewColumnSortMode.NotSortable;
                            break;
                        case "priority":
                            col.Visible = true;
                            col.SortMode = DataGridViewColumnSortMode.Programmatic;
                            poolView.Sort(col, ListSortDirection.Ascending);
                            break;
                        default:
                            col.Visible = false;
                            break;
                    }
                }

                if (selectedRow >= 0 && selectedRow < poolView.Rows.Count)
                    poolView.Rows[selectedRow].Selected = true;
                poolView.HorizontalScrollingOffset = horizScroll;
                if (vertScroll >= 0)
                    poolView.FirstDisplayedScrollingRowIndex = vertScroll;

                // Find last pool that had a share submitted and isn't disabled
                // Not perfect detection but other methods don't seem reliable
                DataRow currentPoolRow = null;
                int lastShareTime = -1;
                foreach (DataRow row in poolList.Rows)
                {
                    int rowLastShare = Convert.ToInt32(row["Last Share Time"]);
                    if (rowLastShare > lastShareTime &&
                        !string.Equals(row["status"].ToString(), "disabled", StringComparison.InvariantCultureIgnoreCase))
                    {
                        lastShareTime = rowLastShare;
                        currentPoolRow = row;
                    }
                }
                if (currentPoolRow != null)
                {
                    // Only sgminer 5.0 supports Name
                    string currentPool = null;
                    if (poolList.Columns.Contains("Name"))
                        currentPool = currentPoolRow["Name"].ToString();
                    else
                        currentPool = currentPoolRow["URL"].ToString();
                    currentPoolAtText.Text = currentPool;
                    currentPoolText.Text = currentPool;
                    currentAlgo = detectAlgo(currentPoolRow, currentAlgo);
                }
                else
                {
                    currentPoolAtText.Text = "None";
                    currentPoolText.Text = "None";
                }

                // Update currently mining info
                switchingAlgorithm = true;
                currentlyMiningCombo.Text = mapInternalToDisplay(currentAlgo);
                switchingAlgorithm = false;
            }

            if (summary != null)
            {
                currentHashrateText.Text = Utilities.formatHashSpeed(summary["KHS 5s"], Utilities.hashUnits.Kh);
                averageHashrateText.Text = Utilities.formatHashSpeed(summary["KHS av"], Utilities.hashUnits.Kh);

                string adpFormatString = "{0} {1} ({2}%)";
                acceptedText.Text = string.Format("{0} {1}", summary["Accepted"], summary["Difficulty Accepted"]);
                rejectedText.Text = string.Format(adpFormatString, summary["Rejected"], summary["Difficulty Rejected"], summary["Pool Rejected%"]);
                staleText.Text = string.Format(adpFormatString, summary["Stale"], summary["Difficulty Stale"], summary["Pool Stale%"]);
                hardwareErrorText.Text = string.Format("{0} ({1}%)", summary["Hardware Errors"], summary["Device Hardware%"]);

                workUtilityText.Text = summary["Work Utility"].ToString() + " / " + summary["Utility"].ToString();
                getworkText.Text = summary["Getworks"].ToString();
                getworkFailureText.Text = summary["Get Failures"].ToString();
                remoteFailureText.Text = summary["Remote Failures"].ToString();
                foundBlocksText.Text = summary["Found Blocks"].ToString();
                bestShareText.Text = summary["Best Share"].ToString();
            }

            if (deviceList != null)
            {
                // Reload pool list making sure that it refreshes without the user noticing
                int selectedRow = -1;
                if (deviceView.SelectedRows.Count > 0)
                    selectedRow = deviceView.SelectedRows[0].Index;
                int horizScroll = deviceView.HorizontalScrollingOffset;
                int vertScroll = deviceView.FirstDisplayedScrollingRowIndex;

                deviceView.DataSource = deviceList;

                // Hide unnecessary columns
                // Sort by ID so that they appear in the correct order
                foreach (DataGridViewColumn col in deviceView.Columns)
                {
                    switch (col.Name.ToLowerInvariant())
                    {
                        case "model":
                        case "khs 5s":
                        case "temperature":
                        case "gpu clock":
                        case "fan speed":
                        case "device rejected%":
                        case "memory clock":
                        case "hardware errors":
                        case "status":
                        case "utility":
                            col.Visible = true;
                            col.SortMode = DataGridViewColumnSortMode.NotSortable;
                            break;
                        case "gpu":
                            col.Visible = true;
                            col.SortMode = DataGridViewColumnSortMode.Programmatic;
                            deviceView.Sort(col, ListSortDirection.Ascending);
                            break;
                        default:
                            col.Visible = false;
                            break;
                    }
                }

                if (selectedRow >= 0 && selectedRow < deviceView.Rows.Count)
                    deviceView.Rows[selectedRow].Selected = true;
                deviceView.HorizontalScrollingOffset = horizScroll;
                if (vertScroll >= 0)
                    deviceView.FirstDisplayedScrollingRowIndex = vertScroll;
            }

            // If all three API commands failed it's likely that the miner is dead
            // We want to restart it unless the miner is stopped
            if (poolList == null && summary == null && deviceList == null &&
                currentState != MiningState.Stopped)
            {
                // Give the miner X seconds to get started before trying to restart it (default 30)
                int startupDeadDelay = config.GetInt("startupDeadDelay");
                if (startupDeadDelay < 0)
                    startupDeadDelay = 30;
                if (currentState != MiningState.Starting || DateTime.Now > lastStartTime.AddSeconds(startupDeadDelay))
                {
                    // Kill existing miner in case it was just having temporary issues
                    // so it doesn't get in the way of the newly started miner
                    string batchFile = config.GetString(currentAlgo + "Batch");
                    if (string.IsNullOrEmpty(batchFile) && !noAPI)
                        batchFile = config.GetString("batchFile");
                    if (!string.IsNullOrEmpty(batchFile) && File.Exists(batchFile))
                    {
                        killMiner();
                        startMiner(batchFile);
                    }
                }
            }

            // Only show the selected period of history instead of all 24 hours
            StringBuilder origSeries = new StringBuilder();
            StringBuilder tempSeries = new StringBuilder();
            StringBuilder newSeries = new StringBuilder();
            foreach (DataColumn col in minerInfo.Columns)
            {
                if (col.Caption == "time")
                    continue;
                origSeries.Append(col.Caption);
                origSeries.Append(',');
                newSeries.AppendFormat("filtered{0},", col.Caption);
                tempSeries.AppendFormat("temp{0},", col.Caption);
            }
            origSeries.Length = origSeries.Length - 1;
            tempSeries.Length = tempSeries.Length - 1;
            newSeries.Length = newSeries.Length - 1;
            string tempSeriesString = tempSeries.ToString();
            minerChart.DataManipulator.Filter(CompareMethod.LessThan, DateTime.Now.AddHours(minerHistoryBar.LeftValue - 24).ToOADate(), origSeries.ToString(), tempSeriesString, "X");
            minerChart.DataManipulator.Filter(CompareMethod.MoreThan, DateTime.Now.AddHours(minerHistoryBar.RightValue - 24).ToOADate(), tempSeriesString, newSeries.ToString(), "X");
            foreach (DataColumn col in minerInfo.Columns)
            {
                if (col.Caption == "time")
                    continue;
                Series series = minerChart.Series["filtered" + col.Caption];
                StringBuilder seriesText = new StringBuilder(col.Caption);
                if (seriesText[0] == 'G')
                    seriesText.Insert(3, ' ');
                else if (seriesText[0] == 't')
                    seriesText[0] = 'T';
                seriesText.Insert(seriesText.Length - 4, ' ');
                
                // Allow user to disable total hash, GPU hash or GPU temp
                bool enabled = false;
                if (col.Caption == "totalHash")
                    enabled = graphTotalCheck.Checked;
                else if (col.Caption.Contains("Hash"))
                    enabled = graphHashCheck.Checked;
                else
                    enabled = graphTempCheck.Checked;

                formatSeries(series, seriesText.ToString(), enabled, 4, 2);

                // Put temperature on the second axis
                if (seriesText[seriesText.Length - 4] == 'T')
                    series.YAxisType = AxisType.Secondary;
                minerChart.Series["temp" + col.Caption].Enabled = false;
            }
            minerChart.DataBind();
        }

        private void updatePoolUI(DataTable profit, Dictionary<string, double> hashInfo, Dictionary<string, double> balanceInfo)
        {
            // Profit-related information
            if (profit.Rows.Count > 0 && currentAlgo != "none" && currentAlgo != "off")
            {
                double currentProfit = Convert.ToDouble(profit.Rows[profit.Rows.Count - 1][currentAlgo]);
                currentProfitText.Text = Utilities.formatProfit(currentProfit);

                if (double.IsNaN(minProfit) || minProfit > currentProfit)
                {
                    minProfit = currentProfit;
                    lowProfitText.Text = Utilities.formatProfit(minProfit);
                }

                if (double.IsNaN(maxProfit) || maxProfit < currentProfit)
                {
                    maxProfit = currentProfit;
                    highProfitText.Text = Utilities.formatProfit(maxProfit);
                }

                averageProfit = ((averageProfit * profitRowCount) + currentProfit) / (profitRowCount + 1);
                profitRowCount++;
                averageProfitText.Text = Utilities.formatProfit(averageProfit);
            }

            // Only show the selected period of history instead of all 24 hours
            StringBuilder origSeries = new StringBuilder("composite");
            StringBuilder tempSeries = new StringBuilder("tempcomposite");
            StringBuilder newSeries = new StringBuilder("filteredcomposite");
            foreach (DataRow algorithm in algorithms.Rows)
            {
                string algorithmName = algorithm["name"].ToString();
                origSeries.Append(',');
                origSeries.Append(algorithmName);
                newSeries.AppendFormat(",filtered{0}", algorithmName);
                tempSeries.AppendFormat(",temp{0}", algorithmName);
            }
            string tempSeriesString = tempSeries.ToString();
            profitChart.DataManipulator.Filter(CompareMethod.LessThan, DateTime.Now.AddHours(profitHistoryBar.LeftValue - 24).ToOADate(), origSeries.ToString(), tempSeriesString, "X");
            profitChart.DataManipulator.Filter(CompareMethod.MoreThan, DateTime.Now.AddHours(profitHistoryBar.RightValue - 24).ToOADate(), tempSeriesString, newSeries.ToString(), "X");
            Series compositeSeries = profitChart.Series["filteredcomposite"];
            formatSeries(compositeSeries, "Your Profit", graphCompositeCheck.Checked, 6, 3);
            profitChart.Series["tempcomposite"].Enabled = false;
            // Set up the moving average (requires copying some data to force alignment)
            if (compositeSeries.Points.Count > 0)
            {
                int points = Math.Min(graphMovingAveragePoints, compositeSeries.Points.Count);
                compositeSeries = profitChart.Series.FindByName("MA");
                if (compositeSeries == null)
                    compositeSeries = profitChart.Series.Add("MA");
                for (int i = 0; i < points; i++)
                    compositeSeries.Points.AddXY(profitChart.Series["filteredcomposite"].Points[i].XValue, profitChart.Series["filteredcomposite"].Points[i].YValues[0]);
                profitChart.DataManipulator.FinancialFormula(FinancialFormula.ExponentialMovingAverage, points.ToString(), "filteredcomposite", "MA");
                formatSeries(compositeSeries, "Average Profit", graphAverageCheck.Checked, 6, 3);
            }
            foreach (DataRow algorithm in algorithms.Rows)
            {
                Series series = profitChart.Series["filtered" + algorithm["name"].ToString()];
                formatSeries(series, algorithm["displayName"].ToString(), ((CheckBox)algorithm["graphCheckbox"]).Checked, 4, 2);
                profitChart.Series["temp" + algorithm["name"].ToString()].Enabled = false;
            }
            profitChart.DataBind();

            // Pool hash and pool-side user hash rate
            if (hashInfo != null)
            {
                poolHashText.Text = Utilities.formatHashSpeed(hashInfo["pool_hash"], Utilities.hashUnits.h);
                userHashText.Text = Utilities.formatHashSpeed(hashInfo["user_hash"], Utilities.hashUnits.h);
            }

            // Autoexchange and coin-related balances
            if (balanceInfo != null)
            {
                DataTable coinList = new DataTable();
                coinList.Columns.Add("Coin Name");
                coinList.Columns.Add("Confirmed", typeof(double));
                coinList.Columns.Add("Unconfirmed", typeof(double));
                foreach (string key in balanceInfo.Keys)
                {
                    double value = balanceInfo[key];
                    switch (key)
                    {
                        case "est_total":
                            totalText.Text = Utilities.formatProfit(value);
                            break;
                        case "unexchanged":
                            unexchangedText.Text = Utilities.formatProfit(value);
                            break;
                        case "exchanged":
                            exchangedText.Text = Utilities.formatProfit(value);
                            break;
                        case "alltime":
                            allTimeText.Text = Utilities.formatProfit(value);
                            break;
                        default:
                            if (key.EndsWith("-Confirmed"))
                            {
                                string coinName = key.Substring(0, key.Length - 10);
                                double unconfirmed = balanceInfo[coinName + "-Unconfirmed"];
                                if (filterBalancesCheck.Checked && value == 0.0 && unconfirmed == 0.0)
                                    continue;
                                DataRow row = coinList.NewRow();
                                row[0] = coinName;
                                row[1] = value;
                                row[2] = unconfirmed;
                                coinList.Rows.Add(row);
                            }
                            break;
                    }
                }

                // Reload balance list making sure that it refreshes without the user noticing
                int selectedRow = -1;
                if (coinView.SelectedRows.Count > 0)
                    selectedRow = coinView.SelectedRows[0].Index;
                int horizScroll = coinView.HorizontalScrollingOffset;
                string sortColumn = null;
                if (coinView.SortedColumn != null)
                    sortColumn = coinView.SortedColumn.Name;
                SortOrder sortOrder = coinView.SortOrder;
                int vertScroll = coinView.FirstDisplayedScrollingRowIndex;

                coinView.DataSource = coinList;

                if (sortColumn != null)
                {
                    coinView.Sort(coinView.Columns[sortColumn],
                        sortOrder == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending);
                }
                if (selectedRow >= 0 && selectedRow < coinView.Rows.Count)
                    coinView.Rows[selectedRow].Selected = true;
                coinView.HorizontalScrollingOffset = horizScroll;
                if (vertScroll >= 0)
                    coinView.FirstDisplayedScrollingRowIndex = vertScroll;
            }

            // Update currently mining info here as we could have switched
            switchingAlgorithm = true;
            currentlyMiningCombo.Text = mapInternalToDisplay(currentAlgo);
            switchingAlgorithm = false;
        }

        private void formatSeries(Series series, string displayName, bool enabled, int markerSize, int borderWidth)
        {
            series.LegendText = displayName;
            series.XValueType = ChartValueType.DateTime;
            series.YValueType = ChartValueType.Double;
            series.ChartType = SeriesChartType.Line;
            series.Enabled = enabled;
            series.BorderWidth = borderWidth;
            series.MarkerStyle = MarkerStyle.Circle;
            series.MarkerSize = markerSize;
            series.MarkerStep = 1;
        }

        private void clearMinerStats()
        {
            // Miner info
            string blank = "--";
            currentHashrateText.Text = blank;
            averageHashrateText.Text = blank;
            workUtilityText.Text = blank;
            hardwareErrorText.Text = blank;
            acceptedText.Text = blank;
            getworkText.Text = blank;
            rejectedText.Text = blank;
            getworkFailureText.Text = blank;
            bestShareText.Text = blank;
            remoteFailureText.Text = blank;
            foundBlocksText.Text = blank;
            staleText.Text = blank;
        }

        private void clearPoolStats()
        {
            // Pool info
            string blank = "--";
            currentProfitText.Text = blank;
            averageProfitText.Text = blank;
            highProfitText.Text = blank;
            lowProfitText.Text = blank;
            userHashText.Text = blank;
            poolHashText.Text = blank;
        }

        #endregion

        #region Background Update Methods

        private void updateMinerInfo(DataTable deviceList)
        {
            if (deviceList != null)
            {
                // If proper number of columns are not set up do that first
                while (numGPU < deviceList.Rows.Count)
                {
                    numGPU++;
                    string colName = "GPU" + numGPU + "Hash";
                    minerInfo.Columns.Add(colName, typeof(double));
                    Series series = minerChart.Series.Add(colName);
                    series.XValueMember = "time";
                    series.YValueMembers = colName;
                    series.XValueType = ChartValueType.DateTime;
                    series.YValueType = ChartValueType.Double;
                    series.Enabled = false;

                    colName = "GPU" + numGPU + "Temp";
                    minerInfo.Columns.Add(colName, typeof(double));
                    series = minerChart.Series.Add(colName);
                    series.XValueMember = "time";
                    series.YValueMembers = colName;
                    series.XValueType = ChartValueType.DateTime;
                    series.YValueType = ChartValueType.Double;
                    series.Enabled = false;
                }

                // Populate table
                double totalHash = 0.0;
                DataRow row = minerInfo.NewRow();
                for (int i = 0; i < deviceList.Rows.Count; i++)
                {
                    string colPrefix = "GPU" + (i+1);
                    double hashValue = Convert.ToDouble(deviceList.Rows[i]["KHS 5s"]);
                    row[colPrefix + "Hash"] = hashValue;
                    totalHash += hashValue;
                    row[colPrefix + "Temp"] = Convert.ToDouble(deviceList.Rows[i]["Temperature"]);
                }
                // Shouldn't be possible to remove a device while running but just to be safe
                for (int i = deviceList.Rows.Count; i < numGPU; i++)
                {
                    string colPrefix = "GPU" + (i+1);
                    row[colPrefix + "Hash"] = 0.0;
                    row[colPrefix + "Temp"] = 0.0;
                }
                row["time"] = DateTime.Now;
                row["totalHash"] = totalHash;
                minerInfo.Rows.Add(row);

                // Delete any rows older than 24 hours to keep memory usage lower
                Utilities.DeleteOlderThan24H(minerInfo);
            }
        }

        private void updateSettings()
        {
            if (refreshSettings)
            {
                string apiKey = config.GetString("poolAPIKey");
                if (!string.IsNullOrEmpty(apiKey))
                    tmbAPI = new TMB_API(apiKey);
                else if (tmbAPI != null)
                    tmbAPI = null;
                profitLogFile = config.GetString("profitLogFile");

                string address = config.GetString("minerAddress");
                int port = config.GetInt("minerPort");
                if (!string.IsNullOrEmpty(address) && port != -666)
                    minerAPI = new Miner_API(address, port);
                else if (minerAPI != null)
                    minerAPI = null;

                // Update algorithm list with enabled flags
                foreach (DataRow algorithm in algorithms.Rows)
                    algorithm["enabled"] = config.GetBool(algorithm["name"].ToString() + "Enabled");

                string mName = config.GetString("minerName");
                if (!string.IsNullOrEmpty(mName))
                    minerName = mName;
                else
                    minerName = "sgminer";

                refreshSettings = false;
            }
        }

        #endregion

        #region Profit Switching

        private string detectAlgo(DataRow poolRow, string defaultAlgo)
        {
            // If the miner doesn't return an algorithm column we can't detect the algorithm
            // Just return the default algorithm that was passed in
            if (!poolRow.Table.Columns.Contains("algorithm"))
                return defaultAlgo;

            // Use algorithm type if provided as it can discern between Scrypt and N-Scrypt
            if (poolRow.Table.Columns.Contains("Algorithm Type"))
            {
                string algorithmType = poolRow["Algorithm Type"].ToString();
                foreach (DataRow algorithm in algorithms.Rows)
                {
                    if (algorithm["type"].ToString() == algorithmType)
                        return algorithm["name"].ToString();
                }
                return "none";
            }
            
            // Return the algorithm that corresponds to the pool algorithm
            string poolAlgorithm = poolRow["algorithm"].ToString();

            defaultAlgo = "none";
            foreach (DataRow algorithm in algorithms.Rows)
            {
                string[] kernels = algorithm["kernels"].ToString().Split(',');
                string algorithmName = algorithm["name"].ToString();
                foreach (string kernel in kernels)
                {
                    if (kernel == "*")
                        defaultAlgo = algorithmName;
                    else if (poolAlgorithm.StartsWith(kernel, StringComparison.InvariantCultureIgnoreCase))
                        return algorithmName;
                }
            }
            return defaultAlgo;
        }

        private void switchAlgorithm(string bestAlgo)
        {
            // Figure out if we need to restart the miner to switch algorithms
            // If the miner has no API we always need to restart the miner
            bool restartMiner = false;
            if (noAPI)
                restartMiner = true;
            else
            {
                switch (currentAlgo)
                {
                    case "none":
                    case "off":
                        if (previousAlgo != bestAlgo)
                        {
                            // If previous algorithm or algorithm we are switching to has a batch file
                            // then we need to restart the miner
                            if (!string.IsNullOrEmpty(config.GetString(bestAlgo + "Batch")) ||
                                !string.IsNullOrEmpty(config.GetString(previousAlgo + "Batch")))
                                restartMiner = true;
                        }
                        break;
                    default:
                        // If algorithm we are switching from or to has a separate batch file
                        // then we need to restart the miner
                        switch (bestAlgo)
                        {
                            case "none":
                            case "off":
                                // If we are stopping mining don't kill the miner
                                // Just disable all the pools so if we go back to
                                // the same algorithm we can just enable them again
                                break;
                            default:
                                // If algorithm we are switching to or from has a batch file
                                // then we need to restart the miner
                                if (!string.IsNullOrEmpty(config.GetString(bestAlgo + "Batch")) ||
                                    !string.IsNullOrEmpty(config.GetString(currentAlgo + "Batch")))
                                    restartMiner = true;
                                break;
                        }
                        break;
                }
            }

            // If we need to restart the miner then kill the current one and start up the new one
            if (restartMiner)
            {
                if (bestAlgo == "none" || bestAlgo == "off")
                {
                    // If the miner has no API we have to kill the miner to disable it
                    killMiner();
                    currentState = MiningState.Mining;
                }
                else
                {
                    string batchFile = config.GetString(bestAlgo + "Batch");
                    if (string.IsNullOrEmpty(batchFile) && !noAPI)
                        batchFile = config.GetString("batchFile");
                    if (string.IsNullOrEmpty(batchFile) || !File.Exists(batchFile))
                    {
                        Utilities.WriteLog("Could not switch to " + bestAlgo + " because no batch file was specified or batch file does not exist.");
                        return;
                    }
                    killMiner();
                    startMiner(batchFile);
                }
            }
            else if (poolList != null)
            {
                // Enable the most profitable pools and disable all others using the pool algorithms
                bool firstPool = true;
                foreach (DataRow pool in poolList.Rows)
                {
                    bool isBestAlgo = bestAlgo != "none" && bestAlgo != "off" && bestAlgo == detectAlgo(pool, bestAlgo);
                    bool isDisabled = string.Equals(pool["status"].ToString(), "disabled", StringComparison.InvariantCultureIgnoreCase);
                    string poolNumber = pool["POOL"].ToString();
                    if (isBestAlgo && isDisabled)
                        minerAPI.runCommand("enablepool", poolNumber);
                    else if (!isBestAlgo && !isDisabled)
                        minerAPI.runCommand("disablepool", poolNumber);
                    if (isBestAlgo && firstPool)
                    {
                        minerAPI.runCommand("switchpool", poolNumber);
                        firstPool = false;
                    }
                }
            }

            DateTime currentTime = DateTime.Now;
            string message = string.Format("Switching from {0} to {1} after {2}", currentAlgo, bestAlgo, currentTime - lastSwitch);
            Utilities.WriteLog(message);
            trayIcon.ShowBalloonTip(2000, "Switching Algorithm", message, ToolTipIcon.Info);
            lastSwitch = currentTime;

            previousAlgo = currentAlgo;
            currentAlgo = bestAlgo;
        }

        private string checkProfit(Dictionary<string, double> profitInfo)
        {
            double bestScore = 0.0;
            string bestAlgo = "none";
            foreach (string key in profitInfo.Keys)
            {
                DataRow algorithm = algorithms.Rows.Find(key);
                // Only switch between algorithms the user has enabled
                if (profitInfo[key] > bestScore &&
                    algorithm != null && (bool)algorithm["enabled"])
                {
                    bestScore = profitInfo[key];
                    bestAlgo = key;
                }
            }

            if (currentAlgo == "none")
                return bestAlgo;
            else if (currentAlgo != "off" && poolProfitInfo.Rows.Count > 0)
            {
                // Check if the current algorithm just dropped in profitability
                // This could indicate a coin switch will occur so don't switch algorithms yet
                double lastProfit = Convert.ToDouble(poolProfitInfo.Rows[poolProfitInfo.Rows.Count - 1][currentAlgo]);
                if (lastProfit > profitInfo[currentAlgo])
                    return "none";
            }

            double historicalDelay = config.GetDouble("historicalDelay");

            // If it's not profitable to mine anything
            // still then keep the miner off
            double profitCutoff = config.GetDouble("profitCutoff");
            if (currentAlgo == "off")
            {
                // Only start mining again if we've been off for at least X minutes
                if (DateTime.Now < lastSwitch.AddMinutes(historicalDelay))
                    return "off";

                if (bestScore < profitCutoff)
                    return "off";
                else
                    return bestAlgo;
            }

            double currentScore = profitInfo[currentAlgo];

            // Prevent divide by 0
            if (currentScore == 0.0)
                return bestAlgo;

            // Check instant limit
            double instantDiff = config.GetDouble("instantDiff");
            if ((bestScore - currentScore) / currentScore >= (instantDiff / 100))
            {
                if (verbose)
                    trayIcon.ShowBalloonTip(2000, "Switching Algorithm (Debug)", string.Format("Instant switch from {0} {1} to {2} {3}", currentAlgo, currentScore, bestAlgo, bestScore), ToolTipIcon.Info);
                return bestAlgo;
            }

            // Only switch algorithms using historical data if we've been mining the same algorithm for at least X minutes
            if (DateTime.Now < lastSwitch.AddMinutes(historicalDelay))
                return "none";

            // Copy data as we will need to keep a rolling tally for the next parts
            Dictionary<string, double> historicalProfit = new Dictionary<string, double>();
            historicalProfit.Add(currentAlgo, profitInfo[currentAlgo]);
            if (currentAlgo != bestAlgo)
                historicalProfit.Add(bestAlgo, profitInfo[bestAlgo]);

            int nextIndex = poolProfitInfo.Rows.Count - 1;
            int rowCount = 0;

            // If not enough historical data don't do historical checks
            if (nextIndex == -1)
                return "none";

            // First check is 5 minutes ago
            DateTime historicalLimit = DateTime.Now.AddMinutes(-5.0);
            double fiveMinDiff = config.GetDouble("fiveMinDiff");
            bestAlgo = checkHistoricalProfit(ref nextIndex, ref rowCount, historicalLimit, historicalProfit, currentAlgo, fiveMinDiff);
            if (bestAlgo != "none")
                return bestAlgo;

            // If not enough historical data don't do the other checks
            if (nextIndex == -1)
                return "none";

            // Second check is 10 minutes ago
            historicalLimit = historicalLimit.AddMinutes(-5.0);
            double tenMinDiff = config.GetDouble("tenMinDiff");
            bestAlgo = checkHistoricalProfit(ref nextIndex, ref rowCount, historicalLimit, historicalProfit, currentAlgo, tenMinDiff);
            if (bestAlgo != "none")
                return bestAlgo;

            // If not enough historical data don't do the other check
            if (nextIndex == -1)
                return "none";

            // Third check is 30 minutes ago
            historicalLimit = historicalLimit.AddMinutes(-20.0);
            double thirtyMinDiff = config.GetDouble("thirtyMinDiff");
            bestAlgo = checkHistoricalProfit(ref nextIndex, ref rowCount, historicalLimit, historicalProfit, currentAlgo, thirtyMinDiff);
            if (bestAlgo != "none")
                return bestAlgo;

            return "none";
        }

        private string checkHistoricalProfit(ref int nextIndex, ref int rowCount, DateTime historicalLimit,
            Dictionary<string, double> historicalProfit, string currentAlgo, double diff)
        {
            int i = nextIndex;
            for (; i >= 0; i--)
            {
                DateTime rowTime = Convert.ToDateTime(poolProfitInfo.Rows[i]["time"]);
                // Rows before the historical limit are not part of this check
                if (rowTime < historicalLimit)
                {
                    nextIndex = i;
                    break;
                }
                string[] keys = new string[historicalProfit.Keys.Count];
                historicalProfit.Keys.CopyTo(keys, 0);
                foreach (string key in keys)
                {
                    historicalProfit[key] = historicalProfit[key] + Convert.ToDouble(poolProfitInfo.Rows[i][key]);
                }
                rowCount++;
            }

            // If there is not enough historical data for this check then skip it
            if (i != nextIndex)
            {
                nextIndex = -1;
                return "none";
            }

            if (rowCount != 0)
            {
                double bestScore = 0.0;
                string bestAlgo = "none";
                foreach (string key in historicalProfit.Keys)
                {
                    if (historicalProfit[key] > bestScore)
                    {
                        bestScore = historicalProfit[key];
                        bestAlgo = key;
                    }
                }
                bestScore = bestScore / rowCount;
                double currentScore = historicalProfit[currentAlgo] / rowCount;

                // If it's not profitable to mine anything currently and
                // for the last 5+ minutes then temporarily turn off the miner
                double profitCutoff = config.GetDouble("profitCutoff");
                double currentBestProfit = Convert.ToDouble(poolProfitInfo.Rows[poolProfitInfo.Rows.Count - 1][bestAlgo]);
                if (bestScore < profitCutoff && currentBestProfit < profitCutoff)
                    return "off";

                // Prevent divide by 0
                if (currentScore == 0)
                    return bestAlgo;

                // Check X minute limit
                if ((bestScore - currentScore) / currentScore >= (diff / 100))
                {
                    if (verbose)
                        trayIcon.ShowBalloonTip(2000, "Switching Algorithm (Debug)", string.Format("Historical switch from {0} {1} to {2} {3}", currentAlgo, currentScore, bestAlgo, bestScore), ToolTipIcon.Info);
                    return bestAlgo;
                }
            }
            return "none";
        }

        private void adjustProfit(Dictionary<string, double> profit)
        {
            string[] keys = new string[profit.Keys.Count];
            profit.Keys.CopyTo(keys, 0);
            foreach (string key in keys)
            {
                double mult = config.GetDouble(key + "Mult");
                double off = config.GetDouble(key + "Off");
                profit[key] = profit[key] * mult + off;
            }
        }

        #endregion

        #region Status Helper Methods

        private void setStatus(Icon icon, string status)
        {
            trayIcon.Text = status;
            Text = string.Format(statusFormat, status);
            Icon = icon;
            trayIcon.Icon = icon;
        }

        private void setStatusButtons()
        {
            // Can only pause when mining and the miner API is connected
            pauseButton.Enabled = currentState == MiningState.Mining && minerAPI != null;
            // Same conditions for switching algorithm as pausing but take into account miners without APIs
            currentlyMiningCombo.Enabled = currentState == MiningState.Mining && (minerAPI != null || noAPI);
            // Can only start when paused or stopped
            startButton.Enabled = currentState == MiningState.Paused || currentState == MiningState.Stopped;
            // Can not stop when already stopped (any other state is fine)
            stopButton.Enabled = currentState != MiningState.Stopped;
            // Same conditions for restarting the miner as changing algorithm
            restartButton.Enabled = currentlyMiningCombo.Enabled;
        }

        #endregion

        #region Configuration

        private void readConfigFile()
        {
            // Restore configuration from a file
            if (File.Exists(configFileLocation))
            {
                string[] configFile = File.ReadAllLines(configFileLocation);
                config = Utilities.ParseConfigFile(configFile);
                if (config != null)
                {
                    restoreConfig();
                    return;
                }
            }
            else
            {
                trayIcon.ShowBalloonTip(2000, "Warning", "Problem reading configuration file. Using defaults.", ToolTipIcon.Warning);
            }
            if (config == null)
            {
                // Use defaults if file fails to load or they didn't specify a file
                config = new Dictionary<string, object>();
                storeConfig();
            }
        }

        private bool storeConfig()
        {
            List<string> problems = new List<string>();
            config["poolAPIKey"] = poolKeyText.Text;
            config["profitLogFile"] = profitLogFileText.Text;
            config["minerAddress"] = minerAddressText.Text;
            config["batchFile"] = batchFileText.Text;
            config["minerName"] = minerNameText.Text;

            StoreInt(minerPortText.Text, "minerPort", problems);
            config["minerRefreshRate"] = Convert.ToDouble(minerRefreshNum.Value);
            config["poolRefreshRate"] = Convert.ToDouble(poolRefreshNum.Value);
            config["historicalDelay"] = Convert.ToDouble(historicalDelayNum.Value);
            config["startupDeadDelay"] = Convert.ToDouble(startupDeadDelayNum.Value);
            StoreDouble(profitCutoffText.Text, "profitCutoff", problems);

            config["monitorMiner"] = enableMinerMonitorCheck.Checked;
            config["monitorPool"] = enablePoolMonitorCheck.Checked;
            config["enableProfitSwitching"] = enableSwitchingCheck.Checked;
            config["startMinerMinimized"] = startMinerMinimizedCheck.Checked;
            config["killMinerOnExit"] = killMinerOnExitCheck.Checked;

            StoreDouble(instantDiffText.Text, "instantDiff", problems);
            StoreDouble(fiveDiffText.Text, "fiveMinDiff", problems);
            StoreDouble(tenDiffText.Text, "tenMinDiff", problems);
            StoreDouble(thirtyDiffText.Text, "thirtyMinDiff", problems);

            foreach (DataRow algorithm in algorithms.Rows)
            {
                string algorithmName = algorithm["name"].ToString();
                StoreDouble(((TextBox)algorithm["multiplierTextbox"]).Text, algorithmName + "Mult", problems);
                StoreDouble(((TextBox)algorithm["offsetTextbox"]).Text, algorithmName + "Off", problems);
                config[algorithmName + "Batch"] = ((TextBox)algorithm["batchTextbox"]).Text;
                config[algorithmName + "Enabled"] = ((CheckBox)algorithm["settingCheckbox"]).Checked;
            }

            if (problems.Count > 0)
            {
                string problemMessage = "The following settings are invalid:\n";
                foreach(string problem in problems)
                {
                    problemMessage += problem + "\n";
                }
                MessageBox.Show(problemMessage);
                return false;
            }
            return true;
        }

        private void StoreInt(string text, string keyName, List<string> problems)
        {
            int intValue = -666;
            if (int.TryParse(text, out intValue))
                config[keyName] = Convert.ToDouble(intValue);
            else
                problems.Add(keyName);
        }

        private void StoreDouble(string text, string keyName, List<string> problems)
        {
            double doubleValue = double.NaN;
            if (double.TryParse(text, out doubleValue))
                config[keyName] = doubleValue;
            else
                problems.Add(keyName);
        }

        private void restoreConfig()
        {
            poolKeyText.Text = RestoreString("poolAPIKey", "");
            profitLogFileText.Text = RestoreString("profitLogFile", "");
            minerAddressText.Text = RestoreString("minerAddress", minerAddressText.Text);
            batchFileText.Text = RestoreString("batchFile", batchFileText.Text);
            minerNameText.Text = RestoreString("minerName", minerNameText.Text);

            minerPortText.Text = RestoreInt("minerPort", minerPortText.Text);
            minerRefreshNum.Value = RestoreInt("minerRefreshRate", Convert.ToInt32(minerRefreshNum.Value));
            poolRefreshNum.Value = RestoreInt("poolRefreshRate", Convert.ToInt32(poolRefreshNum.Value));
            historicalDelayNum.Value = RestoreInt("historicalDelay", Convert.ToInt32(historicalDelayNum.Value));
            startupDeadDelayNum.Value = RestoreInt("startupDeadDelay", Convert.ToInt32(startupDeadDelayNum.Value));
            profitCutoffText.Text = RestoreDouble("profitCutoff", profitCutoffText.Text);

            enableMinerMonitorCheck.Checked = config.GetBool("monitorMiner");
            enablePoolMonitorCheck.Checked = config.GetBool("monitorPool");
            enableSwitchingCheck.Checked = config.GetBool("enableProfitSwitching");
            startMinerMinimizedCheck.Checked = config.GetBool("startMinerMinimized");
            killMinerOnExitCheck.Checked = config.GetBool("killMinerOnExit");

            instantDiffText.Text = RestoreDouble("instantDiff", instantDiffText.Text);
            fiveDiffText.Text = RestoreDouble("fiveMinDiff", fiveDiffText.Text);
            tenDiffText.Text = RestoreDouble("tenMinDiff", tenDiffText.Text);
            thirtyDiffText.Text = RestoreDouble("thirtyMinDiff", thirtyDiffText.Text);

            foreach (DataRow algorithm in algorithms.Rows)
            {
                string algorithmName = algorithm["name"].ToString();
                TextBox textbox = (TextBox)algorithm["multiplierTextbox"];
                textbox.Text = RestoreDouble(algorithmName + "Mult", textbox.Text);
                textbox = (TextBox)algorithm["offsetTextbox"];
                textbox.Text = RestoreDouble(algorithmName + "Off", textbox.Text);
                textbox = (TextBox)algorithm["batchTextbox"];
                textbox.Text = RestoreString(algorithmName + "Batch", textbox.Text);
                ((CheckBox)algorithm["graphCheckbox"]).Checked =
                    ((CheckBox)algorithm["settingCheckbox"]).Checked =
                        config.GetBool(algorithmName + "Enabled");
            }
        }

        private string RestoreDouble(string keyName, string text)
        {
            double doubleValue = config.GetDouble(keyName);
            if (!double.IsNaN(doubleValue))
                return doubleValue.ToString();
            else
                config[keyName] = Convert.ToDouble(text);
            return text;
        }

        private string RestoreInt(string keyName, string text)
        {
            int intValue = config.GetInt(keyName);
            if (intValue != -666)
                return intValue.ToString();
            else
                config[keyName] = Convert.ToDouble(text);
            return text;
        }

        private int RestoreInt(string keyName, int value)
        {
            int intValue = config.GetInt(keyName);
            if (intValue != -666)
                return intValue;
            else
                config[keyName] = Convert.ToDouble(value);
            return value;
        }

        private string RestoreString(string keyName, string text)
        {
            string stringValue = config.GetString(keyName);
            if (stringValue != null)
                return stringValue;
            else
                config[keyName] = text;
            return text;
        }

        #endregion

        #region Event Handlers

        #region Menu Button Event Handlers

        private void closeButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            // Force a refresh of the information on the page
            if (tabControl.SelectedTab == statsPage ||
                tabControl.SelectedTab == profitHistoryPage)
                refreshMiner = refreshPool = true;
            else if (tabControl.SelectedTab == poolsPage ||
                tabControl.SelectedTab == devicesPage)
                refreshMiner = true;
            else if (tabControl.SelectedTab == balancesPage)
                refreshPool = true;
            else if (tabControl.SelectedTab == settingsPage)
                refreshSettings = true;
        }

        #endregion

        #region Miner Graph Event Handlers

        private void minerChart_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                string caption = "";
                HitTestResult[] results = minerChart.HitTest(e.Location.X, e.Location.Y, true, ChartElementType.DataPoint);
                if (results[0].ChartElementType == ChartElementType.DataPoint)
                {
                    DataPoint hitPoint = results[0].Object as DataPoint;
                    if (hitPoint != null)
                    {
                        if (hitPoint == lastMinerHitPoint)
                            return;
                        int pointIndex = results[0].PointIndex;
                        if (lastMinerHitPoint != null)
                            lastMinerHitPoint.MarkerSize = 4;
                        lastMinerHitPoint = hitPoint;
                        lastMinerSeriesName = results[0].Series.Name;
                        caption += string.Format("Time: {0}\n", DateTime.FromOADate(hitPoint.XValue));
                        hitPoint.MarkerSize = 10;
                        foreach (DataColumn col in minerInfo.Columns)
                        {
                            if (col.Caption == "time")
                                continue;
                            Series series = minerChart.Series["filtered" + col.Caption];
                            DataPoint point = series.Points[results[0].PointIndex];
                            string yValue = null;
                            if (col.Caption.EndsWith("Hash"))
                                yValue = Utilities.formatHashSpeed(point.YValues[0], Utilities.hashUnits.Kh);
                            else
                                yValue = point.YValues[0] + " °C";
                            caption += string.Format("{0}: {1}\n", series.LegendText, yValue);
                        }
                    }
                }
                else
                    tooltip.SetToolTip(minerChart, null);

                if (!string.IsNullOrEmpty(caption))
                    tooltip.SetToolTip(minerChart, caption);
            }
            catch { }
        }

        private void graphTotalCheck_CheckedChanged(object sender, EventArgs e)
        {
            minerChart.Series["filteredtotalHash"].Enabled = graphTotalCheck.Checked;
        }

        private void minerHistoryBar_LeftMoving(object sender, int e)
        {
            DateTime now = DateTime.Now;
            minerHistoryBar.setTooltip(tooltip, string.Format("{0} - {1}", now.AddHours(24 - e), now.AddHours(24 - minerHistoryBar.RightValue)));
        }

        private void minerHistoryBar_RightMoving(object sender, int e)
        {
            DateTime now = DateTime.Now;
            minerHistoryBar.setTooltip(tooltip, string.Format("{0} - {1}", now.AddHours(24 - minerHistoryBar.LeftValue), now.AddHours(24 - e)));
        }

        private void minerHistoryBar_ValueChanged(object sender, int e)
        {
            minerHistoryLabel.Text = string.Format("History ({0} to {1} hours ago):", 24 - minerHistoryBar.LeftValue, 24 - minerHistoryBar.RightValue);
        }

        #endregion

        #region Start/Stop Miner Event Handlers

        private void pauseButton_Click(object sender, EventArgs e)
        {
            pauseMiner();
            setStatusButtons();
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            if (currentState == MiningState.Paused)
            {
                resumeMiner();
            }
            else if (currentState == MiningState.Stopped)
            {
                string batchFile = config.GetString(currentAlgo + "Batch");
                if (string.IsNullOrEmpty(batchFile) && !noAPI)
                    batchFile = config.GetString("batchFile");
                if (string.IsNullOrEmpty(batchFile) || !File.Exists(batchFile))
                {
                    Utilities.WriteLog("Could not start up " + currentAlgo + " because no batch file was specified or batch file does not exist.");
                    return;
                }
                startMiner(batchFile);
            }
            setStatusButtons();
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            killMiner();
            setStatusButtons();
        }

        private void restartButton_Click(object sender, EventArgs e)
        {
            string batchFile = config.GetString(currentAlgo + "Batch");
            if (string.IsNullOrEmpty(batchFile) && !noAPI)
                batchFile = config.GetString("batchFile");
            if (string.IsNullOrEmpty(batchFile) || !File.Exists(batchFile))
            {
                Utilities.WriteLog("Could not switch to " + currentAlgo + " because no batch file was specified or batch file does not exist.");
                return;
            }
            killMiner();
            startMiner(batchFile);
            setStatusButtons();
        }

        #endregion

        #region Settings Event Handlers

        private void batchFileButton_Click(object sender, EventArgs e)
        {
            string fileName;
            if (getBatchFile("sgminer v5.0", out fileName) == DialogResult.OK)
                batchFileText.Text = fileName;
        }

        private void batchButton_Click(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button == null)
                return;
            DataRow row = button.Tag as DataRow;
            if (row == null)
                return;
            string fileName;
            if (getBatchFile(row["displayName"].ToString(), out fileName) == DialogResult.OK)
                ((TextBox)row["batchTextbox"]).Text = fileName;
        }

        private DialogResult getBatchFile(string type, out string fileName)
        {
            return getFile("Select your " + type + " batch file:", out fileName);
        }

        private void profitLogFileButton_Click(object sender, EventArgs e)
        {
            string fileName;
            if (getFile("Specify your profit log file location:", out fileName) == DialogResult.OK)
                profitLogFileText.Text = fileName;
        }

        private DialogResult getFile(string prompt, out string fileName)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Multiselect = false;
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.Title = prompt;
            DialogResult result = dialog.ShowDialog();
            fileName = dialog.FileName;
            return result;
        }

        private void applySettingsButton_Click(object sender, EventArgs e)
        {
            if (storeConfig())
                refreshSettings = true;
        }

        private void saveSettingsButton_Click(object sender, EventArgs e)
        {
            if (storeConfig())
            {
                refreshSettings = true;
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.CheckPathExists = true;
                dialog.DefaultExt = ".conf";
                dialog.FileName = configFileLocation;
                dialog.Title = "Save Settings To:";
                DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                    Utilities.SaveConfig(config, dialog.FileName);
            }
        }

        #endregion

        #region Profit Graph Event Handlers

        private void graphCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox checkbox = sender as CheckBox;
            if (checkbox == null)
                return;
            DataRow row = checkbox.Tag as DataRow;
            if (row == null)
                return;
            Series series = profitChart.Series.FindByName("filtered" + row["name"].ToString());
            if (series != null)
                series.Enabled = ((CheckBox)row["graphCheckbox"]).Checked;
        }

        private void graphCompositeCheck_CheckedChanged(object sender, EventArgs e)
        {
            Series series = profitChart.Series.FindByName("filteredcomposite");
            if (series != null)
                series.Enabled = graphCompositeCheck.Checked;
        }

        private void graphAverageCheck_CheckedChanged(object sender, EventArgs e)
        {
            Series series = profitChart.Series.FindByName("MA");
            if (series != null)
                series.Enabled = graphAverageCheck.Checked;
        }

        private void profitChart_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                string caption = "";
                HitTestResult[] results = profitChart.HitTest(e.Location.X, e.Location.Y, true, ChartElementType.DataPoint);
                if (results[0].ChartElementType == ChartElementType.DataPoint)
                {
                    DataPoint hitPoint = results[0].Object as DataPoint;
                    if (hitPoint != null)
                    {
                        if (lastProfitHitPoint == hitPoint)
                            return;
                        int pointIndex = results[0].PointIndex;
                        int movingAverageStart = Math.Min(profitChart.Series["filteredcomposite"].Points.Count - 1, graphMovingAveragePoints - 1);
                        if(results[0].Series.Name == "MA")
                            pointIndex += movingAverageStart;
                        switch (lastProfitSeriesName)
                        {
                            case "MA":
                                lastProfitHitPoint.MarkerSize = 6;
                                break;
                            case "filteredcomposite":
                                lastProfitHitPoint.MarkerSize = 6;
                                break;
                            case null:
                                break;
                            default:
                                lastProfitHitPoint.MarkerSize = 4;
                                break;
                        }
                        lastProfitHitPoint = hitPoint;
                        lastProfitSeriesName = results[0].Series.Name;
                        caption += string.Format("Time: {0}\n", DateTime.FromOADate(hitPoint.XValue));
                        hitPoint.MarkerSize = 10;
                        foreach (DataRow algorithm in algorithms.Rows)
                        {
                            bool enabled = false;
                            string algorithmName = algorithm["name"].ToString();
                            enabled = ((CheckBox)algorithm["graphCheckbox"]).Checked;
                            if (enabled)
                            {
                                DataPoint point = profitChart.Series["filtered" + algorithmName].Points[pointIndex];
                                caption += string.Format("{0}: {1}\n", algorithm["displayName"].ToString(), point.YValues[0]);
                            }
                        }
                        if (graphCompositeCheck.Checked)
                        {
                            DataPoint point = profitChart.Series["filteredcomposite"].Points[pointIndex];
                            caption += string.Format("Your Profit: {0}\n", point.YValues[0]);
                        }
                        if (graphAverageCheck.Checked)
                        {
                            if (pointIndex >= movingAverageStart)
                            {
                                DataPoint point = profitChart.Series["MA"].Points[pointIndex - movingAverageStart];
                                caption += string.Format("Average Profit: {0}\n", Utilities.formatProfit(point.YValues[0]));
                            }
                            else
                            {
                                caption += "Average Profit: N/A\n";
                            }
                        }
                    }
                }
                else
                    tooltip.SetToolTip(profitChart, null);

                if (!string.IsNullOrEmpty(caption))
                    tooltip.SetToolTip(profitChart, caption);
            }
            catch { }
        }

        private void profitHistoryBar_LeftMoving(object sender, int e)
        {
            DateTime now = DateTime.Now;
            profitHistoryBar.setTooltip(tooltip, string.Format("{0} - {1}", now.AddHours(24 - e), now.AddHours(24 - profitHistoryBar.RightValue)));
        }

        private void profitHistoryBar_RightMoving(object sender, int e)
        {
            DateTime now = DateTime.Now;
            profitHistoryBar.setTooltip(tooltip, string.Format("{0} - {1}", now.AddHours(24 - profitHistoryBar.LeftValue), now.AddHours(24 - e)));
        }

        private void profitHistoryBar_ValueChanged(object sender, int e)
        {
            profitHistoryLabel.Text = string.Format("History ({0} to {1} hours ago):", 24 - profitHistoryBar.LeftValue, 24 - profitHistoryBar.RightValue);
        }

        #endregion

        #region Device/Pool Selection Event Handlers

        private void deviceView_SelectionChanged(object sender, EventArgs e)
        {
            if (deviceView.SelectedRows.Count > 0)
            {
                DataGridViewRow row = deviceView.SelectedRows[0];
                StringBuilder deviceInfo = new StringBuilder();
                deviceInfo.AppendLine("Selected Device Information:\n");
                foreach (DataGridViewColumn col in deviceView.Columns)
                {
                    string columnName = col.Name;
                    string value = row.Cells[col.Index].Value.ToString();
                    int filler = Math.Max(70 - columnName.Length - value.Length, 2);
                    deviceInfo.Append(columnName);
                    deviceInfo.Append('.', filler);
                    if (value.Length > 0)
                        deviceInfo.AppendLine(value);
                    else
                        deviceInfo.Append('.');
                }
                deviceInfoText.Text = deviceInfo.ToString();
            }
        }

        private void poolView_SelectionChanged(object sender, EventArgs e)
        {
            if (poolView.SelectedRows.Count > 0)
            {
                DataGridViewRow row = poolView.SelectedRows[0];
                StringBuilder poolInfo = new StringBuilder();
                poolInfo.AppendLine("Selected Pool Information:\n");
                foreach (DataGridViewColumn col in poolView.Columns)
                {
                    string columnName = col.Name;
                    string value = row.Cells[col.Index].Value.ToString();
                    int filler = Math.Max(70 - columnName.Length - value.Length, 2);
                    poolInfo.Append(columnName);
                    poolInfo.Append('.', filler);
                    if (value.Length > 0)
                        poolInfo.AppendLine(value);
                    else
                        poolInfo.Append('.');
                }
                poolInfoText.Text = poolInfo.ToString();
            }
        }

        #endregion

        #region Pool Button Event Handlers

        private void poolUpButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (poolView.SelectedRows.Count <= 0 || minerAPI == null)
                    return;
                int priority = Convert.ToInt32(poolView.SelectedRows[0].Cells["Priority"].Value);
                if (priority == 0)
                    return;
                changePoolPriority(priority, -1);
            }
            catch (Exception ex)
            {
                // Log failure
                Utilities.WriteLog(ex.Message);
                Utilities.WriteLog(ex.StackTrace);
                if (verbose)
                    MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        private void poolDownButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (poolView.SelectedRows.Count <= 0 || minerAPI == null)
                    return;
                int priority = Convert.ToInt32(poolView.SelectedRows[0].Cells["Priority"].Value);
                if (priority == poolView.Rows.Count - 1)
                    return;
                changePoolPriority(priority, 1);
            }
            catch (Exception ex)
            {
                // Log failure
                Utilities.WriteLog(ex.Message);
                Utilities.WriteLog(ex.StackTrace);
                if (verbose)
                    MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        private void enablePoolButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (poolView.SelectedRows.Count <= 0 || minerAPI == null)
                    return;
                string pool = poolView.SelectedRows[0].Cells["POOL"].Value.ToString();
                string result = minerAPI.runCommand("enablepool", pool);
                if (result != null && result[0] == 'S')
                {
                    refreshMiner = true;
                }
            }
            catch (Exception ex)
            {
                // Log failure
                Utilities.WriteLog(ex.Message);
                Utilities.WriteLog(ex.StackTrace);
                if (verbose)
                    MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        private void disablePoolButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (poolView.SelectedRows.Count <= 0 || minerAPI == null)
                    return;
                string pool = poolView.SelectedRows[0].Cells["POOL"].Value.ToString();
                string result = minerAPI.runCommand("disablepool", pool);
                if (result != null && result[0] == 'S')
                {
                    refreshMiner = true;
                }
            }
            catch (Exception ex)
            {
                // Log failure
                Utilities.WriteLog(ex.Message);
                Utilities.WriteLog(ex.StackTrace);
                if (verbose)
                    MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        private void removePoolButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (poolView.SelectedRows.Count <= 0 || minerAPI == null)
                    return;
                string pool = poolView.SelectedRows[0].Cells["POOL"].Value.ToString();
                DialogResult dialogResult = MessageBox.Show(string.Format("Are you sure you want to remove pool {0}?", pool), "TMB Switcher", MessageBoxButtons.YesNo);
                if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                {
                    string result = minerAPI.runCommand("removepool", pool);
                    if (result != null && result[0] == 'S')
                    {
                        refreshMiner = true;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log failure
                Utilities.WriteLog(ex.Message);
                Utilities.WriteLog(ex.StackTrace);
                if (verbose)
                    MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        private void addPoolButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (minerAPI == null)
                    return;
                PoolAddDialog dialog = new PoolAddDialog();
                DialogResult dialogResult = dialog.ShowDialog(this);
                if (dialogResult == System.Windows.Forms.DialogResult.OK)
                {
                    StringBuilder poolInfo = new StringBuilder();
                    poolInfo.Append(Utilities.EscapeJSON(dialog.urlText.Text));
                    poolInfo.Append(',');
                    poolInfo.Append(Utilities.EscapeJSON(dialog.userText.Text));
                    poolInfo.Append(',');
                    poolInfo.Append(Utilities.EscapeJSON(dialog.passText.Text));
                    poolInfo.Append(',');
                    poolInfo.Append(Utilities.EscapeJSON(dialog.nameText.Text));
                    poolInfo.Append(',');
                    poolInfo.Append(Utilities.EscapeJSON(dialog.descText.Text));
                    poolInfo.Append(',');
                    poolInfo.Append(Utilities.EscapeJSON(dialog.algorithmCombo.Text));
                    string result = minerAPI.runCommand("addpool", poolInfo.ToString());
                    if (result != null && result[0] == 'S')
                    {
                        refreshMiner = true;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log failure
                Utilities.WriteLog(ex.Message);
                Utilities.WriteLog(ex.StackTrace);
                if (verbose)
                    MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        private void changePoolPriority(int priority, int direction)
        {
            // Get a list sorted in the order we want them to be in
            SortedList<int, int> list = new SortedList<int, int>();
            foreach (DataGridViewRow row in poolView.Rows)
            {
                int poolPriority = Convert.ToInt32(row.Cells["Priority"].Value);
                int pool = Convert.ToInt32(row.Cells["POOL"].Value);
                if (poolPriority == priority)
                    list.Add(priority + direction, pool);
                else if (poolPriority == priority + direction)
                    list.Add(priority, pool);
                else
                    list.Add(poolPriority, pool);
            }
            // Construct the parameter string
            StringBuilder param = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                param.Append(list[i]);
                param.Append(',');
            }
            string result = minerAPI.runCommand("poolpriority", param.ToString(0, param.Length - 1));
            if (result != null && result[0] == 'S')
            {
                refreshMiner = true;
                poolView.Rows[poolView.SelectedRows[0].Index + direction].Selected = true;
            }
        }

        #endregion

        #region About Screen Event Handlers

        private void linkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://bitcointalk.org/index.php?topic=661827");
        }

        private void address_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                LinkLabel label = sender as LinkLabel;
                Clipboard.SetText(label.Text.Substring(label.LinkArea.Start, label.LinkArea.Length - label.LinkArea.Start));
                trayIcon.ShowBalloonTip(2000, "Thank You", "Donation Address Copied", ToolTipIcon.Info);
            }
            catch { }
        }

        #endregion

        private void currentlyMiningCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (switchingAlgorithm)
                return;
            string currentAlgo = "none";
            foreach (DataRow algorithm in algorithms.Rows)
            {
                if (algorithm["displayName"].ToString() == currentlyMiningCombo.Text)
                {
                    currentAlgo = algorithm["name"].ToString();
                    break;
                }
            }
            switchAlgorithm(currentAlgo);
        }

        #endregion

        #region Miner Start/Stop Methods

        private bool minerRunning()
        {
            try
            {
                string[] miners = minerName.Split(',');
                foreach (string miner in miners)
                {
                    Process[] procs = Process.GetProcessesByName(miner.Trim());
                    if (procs.Length > 0)
                        return true;
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Utilities.WriteLog(ex.Message);
                Utilities.WriteLog(ex.StackTrace);
                return false;
            }
            return false;
        }

        private void killMiner()
        {
            try
            {
                currentState = MiningState.Stopped;
                string[] miners = minerName.Split(',');
                foreach (string miner in miners)
                {
                    Process[] procs = Process.GetProcessesByName(miner.Trim());
                    foreach (Process proc in procs)
                        proc.Kill();
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Utilities.WriteLog(ex.Message);
                Utilities.WriteLog(ex.StackTrace);
            }
        }

        private void pauseMiner()
        {
            try
            {
                currentState = MiningState.Paused;
                switchAlgorithm("none");
            }
            catch (Exception ex)
            {
                // Log exception
                Utilities.WriteLog(ex.Message);
                Utilities.WriteLog(ex.StackTrace);
            }
        }

        private void resumeMiner()
        {
            currentState = MiningState.Mining;
        }

        private void startMiner(string batchFile)
        {
            try
            {
                Process proc = new Process();
                proc.StartInfo.FileName = batchFile;
                proc.StartInfo.WindowStyle = config.GetBool("startMinerMinimized") ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal;
                proc.Start();
                currentState = MiningState.Starting;
                lastStartTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                // Log exception
                Utilities.WriteLog(ex.Message);
                Utilities.WriteLog(ex.StackTrace);
            }
        }

        #endregion
    }
}
