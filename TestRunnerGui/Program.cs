using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using NUnit.Engine; // NuGet: NUnit.Engine

namespace TestRunnerGui
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private ComboBox cbCategories;
        private Button btnDiscover;
        private Button btnRun;
        private TextBox txtOutput;
        private DataGridView dgvResults;
        private Label lblProjectPath;
        private TextBox txtProjectPath;
        private OpenFileDialog openFileDialog;

        public MainForm()
        {
            Text = "NUnit Category Test Runner (NUnit Engine)";
            Width = 1000;
            Height = 700;

            lblProjectPath = new Label { Left = 10, Top = 12, Text = "Test .csproj or project folder:", AutoSize = true };
            Controls.Add(lblProjectPath);

            txtProjectPath = new TextBox { Left = 240, Top = 8, Width = 580 };
            Controls.Add(txtProjectPath);

            var btnBrowse = new Button { Left = 830, Top = 6, Width = 120, Text = "Browse .csproj" };
            btnBrowse.Click += BtnBrowse_Click;
            Controls.Add(btnBrowse);

            cbCategories = new ComboBox { Left = 10, Top = 40, Width = 400, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(cbCategories);

            btnDiscover = new Button { Left = 420, Top = 40, Width = 150, Text = "Discover Categories" };
            btnDiscover.Click += BtnDiscover_Click;
            Controls.Add(btnDiscover);

            btnRun = new Button { Left = 580, Top = 40, Width = 150, Text = "Run Selected Category", Enabled = false };
            btnRun.Click += BtnRun_Click;
            Controls.Add(btnRun);

            txtOutput = new TextBox { Left = 10, Top = 80, Width = 940, Height = 380, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true };
            Controls.Add(txtOutput);

            dgvResults = new DataGridView { Left = 10, Top = 470, Width = 940, Height = 170, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            dgvResults.Columns.Add("TestName", "Test");
            dgvResults.Columns.Add("Outcome", "Outcome");
            dgvResults.Columns.Add("Message", "Message");
            Controls.Add(dgvResults);

            openFileDialog = new OpenFileDialog { Filter = "C# Project (*.csproj)|*.csproj", Title = "Select test .csproj" };
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                txtProjectPath.Text = openFileDialog.FileName;
            }
        }

        private async void BtnDiscover_Click(object? sender, EventArgs e)
        {
            cbCategories.Items.Clear();
            btnRun.Enabled = false;
            txtOutput.Clear();
            dgvResults.Rows.Clear();

            string projectPath = txtProjectPath.Text.Trim();
            if (string.IsNullOrEmpty(projectPath))
            {
                MessageBox.Show("Specify the test .csproj file path (or folder containing it).);
                return;
            }

            AppendOutput($"Discovering categories in project: {projectPath}");
            try
            {
                var categories = await Task.Run(() => DiscoverCategoriesWithNUnitEngine(projectPath));
                if (categories.Any())
                {
                    foreach (var c in categories.OrderBy(x => x))
                        cbCategories.Items.Add(c);
                    cbCategories.SelectedIndex = 0;
                    btnRun.Enabled = true;
                    AppendOutput($"Found {categories.Count} categories.");
                }
                else
                {
                    AppendOutput("No categories found.");
                }
            }
            catch (Exception ex)
            {
                AppendOutput($"Error while discovering categories: {ex}");
            }
        }

        private async void BtnRun_Click(object? sender, EventArgs e)
        {
            if (cbCategories.SelectedItem == null) return;
            string category = cbCategories.SelectedItem.ToString()!;
            string projectPath = txtProjectPath.Text.Trim();
            if (string.IsNullOrEmpty(projectPath))
            {
                MessageBox.Show("Select the test .csproj first.");
                return;
            }

            btnRun.Enabled = false;
            btnDiscover.Enabled = false;
            dgvResults.Rows.Clear();

            AppendOutput($"Running tests with Category={category}");
            try
            {
                var results = await Task.Run(() => RunTestsWithNUnitEngine(projectPath, category, AppendOutput));
                ShowResults(results);
                AppendOutput("Done.");
            }
            catch (Exception ex)
            {
                AppendOutput($"Error running tests: {ex}");
            }
            finally
            {
                btnRun.Enabled = true;
                btnDiscover.Enabled = true;
            }
        }

        private void AppendOutput(string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => AppendOutput(text));
                return;
            }
            txtOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            txtOutput.SelectionStart = txtOutput.TextLength;
            txtOutput.ScrollToCaret();
        }

        private void ShowResults(List<TestResult> results)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ShowResults(results));
                return;
            }
            dgvResults.Rows.Clear();
            foreach (var r in results)
            {
                dgvResults.Rows.Add(r.TestName, r.Outcome, r.Message);
            }
        }

        private class TestResult
        {
            public string TestName { get; set; } = "";
            public string Outcome { get; set; } = "";
            public string Message { get; set; } = "";
        }

        // ----------------- NUnit Engine discovery -----------------
        private HashSet<string> DiscoverCategoriesWithNUnitEngine(string projectOrCsprojPath)
        {
            // Resolve csproj path
            string csprojPath = projectOrCsprojPath;
            if (Directory.Exists(projectOrCsprojPath))
            {
                var files = Directory.GetFiles(projectOrCsprojPath, "*.csproj", SearchOption.TopDirectoryOnly);
                if (files.Length == 0) throw new FileNotFoundException("No .csproj found in folder.");
                csprojPath = files[0];
            }
            if (!File.Exists(csprojPath)) throw new FileNotFoundException("Project file not found: " + csprojPath);

            // Build project to ensure assembly exists
            var projectDir = Path.GetDirectoryName(csprojPath)!;
            var buildCfg = "Debug";
            var tfm = "net10.0";
            var buildResult = RunProcess("dotnet", $"build \"{csprojPath}\" -c {buildCfg} -f {tfm}", out var buildOutput);
            AppendOutput(buildOutput);
            if (buildResult != 0)
            {
                AppendOutput("dotnet build failed (discovery will attempt to inspect existing assembly if present).");
            }

            // Find the test assembly DLL
            string assemblyName = Path.GetFileNameWithoutExtension(csprojPath);
            string assemblyPath = Path.Combine(projectDir, "bin", buildCfg, tfm, assemblyName + ".dll");
            if (!File.Exists(assemblyPath))
            {
                var binDir = Path.Combine(projectDir, "bin", buildCfg, tfm);
                if (!Directory.Exists(binDir)) throw new FileNotFoundException($"Build output not found: {assemblyPath}");
                var dlls = Directory.GetFiles(binDir, "*.dll", SearchOption.TopDirectoryOnly);
                if (dlls.Length == 0) throw new FileNotFoundException($"No dlls found in: {binDir}");
                assemblyPath = dlls.OrderBy(d => d).First();
            }

            AppendOutput($"Inspecting assembly with NUnit Engine: {assemblyPath}");

            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Create the engine and runner
            var engine = TestEngineActivator.CreateInstance();
            var package = new TestPackage(assemblyPath);

            // Use default settings; the engine will spawn runners so tests won't run in this UI process.
            using (var runner = engine.GetRunner(package))
            {
                // Explore returns an XmlNode describing test tree and properties
                var exploreXml = runner.Explore(TestFilter.Empty);
                if (exploreXml != null)
                {
                    // The exploreXml is an XmlNode; get OuterXml to parse
                    var xml = exploreXml.OuterXml;
                    var doc = new XmlDocument();
                    doc.LoadXml(xml);

                    // test-case nodes contain properties/property elements with name="Category"
                    var propNodes = doc.SelectNodes("//test-case/properties/property[@name='Category']");
                    if (propNodes != null)
                    {
                        foreach (XmlNode n in propNodes)
                        {
                            var val = n.Attributes?["value"]?.Value;
                            if (!string.IsNullOrEmpty(val))
                                categories.Add(val);
                        }
                    }

                    // Also look for class-level categories (may appear as property on test-suite nodes)
                    var suitePropNodes = doc.SelectNodes("//test-suite/properties/property[@name='Category']");
                    if (suitePropNodes != null)
                    {
                        foreach (XmlNode n in suitePropNodes)
                        {
                            var val = n.Attributes?["value"]?.Value;
                            if (!string.IsNullOrEmpty(val))
                                categories.Add(val);
                        }
                    }
                }
            }

            // Dispose engine
            engine?.Dispose();

            return categories;
        }

        // ----------------- NUnit Engine test run -----------------
        private List<TestResult> RunTestsWithNUnitEngine(string projectOrCsprojPath, string categoryFilter, Action<string> outputCallback)
        {
            var results = new List<TestResult>();

            // Resolve and build as in discovery
            string csprojPath = projectOrCsprojPath;
            if (Directory.Exists(projectOrCsprojPath))
            {
                var files = Directory.GetFiles(projectOrCsprojPath, "*.csproj", SearchOption.TopDirectoryOnly);
                if (files.Length == 0) throw new FileNotFoundException("No .csproj found in folder.");
                csprojPath = files[0];
            }
            if (!File.Exists(csprojPath)) throw new FileNotFoundException("Project file not found: " + csprojPath);

            var projectDir = Path.GetDirectoryName(csprojPath)!;
            var buildCfg = "Debug";
            var tfm = "net10.0";
            var buildResult = RunProcess("dotnet", $"build \"{csprojPath}\" -c {buildCfg} -f {tfm}", out var buildOutput);
            outputCallback(buildOutput);

            string assemblyName = Path.GetFileNameWithoutExtension(csprojPath);
            string assemblyPath = Path.Combine(projectDir, "bin", buildCfg, tfm, assemblyName + ".dll");
            if (!File.Exists(assemblyPath))
            {
                var binDir = Path.Combine(projectDir, "bin", buildCfg, tfm);
                var dlls = Directory.GetFiles(binDir, "*.dll", SearchOption.TopDirectoryOnly);
                if (dlls.Length == 0) throw new FileNotFoundException($"No dlls found in: {binDir}");
                assemblyPath = dlls.OrderBy(d => d).First();
            }

            outputCallback($"Running tests in assembly: {assemblyPath} with Category={categoryFilter}");

            var engine = TestEngineActivator.CreateInstance();
            var package = new TestPackage(assemblyPath);

            // Settings: you can adjust processes, domain usage, etc.
            // For Playwright tests it's usually safer to run in a separate process (default engine runner behavior).
            package.Settings["ProcessModel"] = "Separate"; // Run tests in a separate process
            package.Settings["DomainUsage"] = "Separate";

            using (var runner = engine.GetRunner(package))
            {
                var filterBuilder = engine.Services.GetService<ITestFilterService>().GetTestFilterBuilder();
                // AddCategory method available on builder for NUnit Engine
                filterBuilder.AddCategory(categoryFilter);
                var filter = filterBuilder.GetFilter();

                var listener = new UiTestEventListener(outputCallback, results);
                // Run returns an XmlNode results summary; but listener receives per-test events
                runner.Run(listener, filter);
            }

            engine?.Dispose();

            return results;
        }

        // Helper listener that implements ITestEventListener
        private class UiTestEventListener : ITestEventListener
        {
            private readonly Action<string> _output;
            private readonly List<TestResult> _results;

            public UiTestEventListener(Action<string> output, List<TestResult> results)
            {
                _output = output;
                _results = results;
            }

            public void OnTestEvent(string report)
            {
                // report is XML for events such as start-test, test-case, test-suite etc.
                // We write live output for visibility and parse test-case results into the list.
                _output(report);

                try
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(report);
                    var testCase = doc.SelectSingleNode("//test-case");
                    if (testCase != null)
                    {
                        var fullname = testCase.Attributes?["fullname"]?.Value ?? testCase.Attributes?["name"]?.Value ?? "";
                        var result = testCase.Attributes?["result"]?.Value ?? "";
                        string message = "";

                        var failureNode = testCase.SelectSingleNode("failure");
                        if (failureNode != null)
                        {
                            var msgNode = failureNode.SelectSingleNode("message");
                            var stackNode = failureNode.SelectSingleNode("stack-trace");
                            message = (msgNode?.InnerText ?? "") + (stackNode != null ? ("\n" + stackNode.InnerText) : "");
                        }

                        lock (_results)
                        {
                            _results.Add(new TestResult { TestName = fullname, Outcome = result, Message = message });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _output($"[Listener Parse Error] {ex.Message}");
                }
            }
        }

        // ---------- Process helpers ----------
        private int RunProcess(string exe, string args, out string combinedOutput)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var sb = new StringBuilder();
            using (var p = Process.Start(psi)!)
            {
                p.OutputDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.BeginOutputReadLine();
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.BeginErrorReadLine();
                p.WaitForExit();
                combinedOutput = sb.ToString();
                return p.ExitCode;
            }
        }
    }
}
