using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Configuration;

namespace FileSearch
{
    enum SearchState
    {
        Begin,
        Stop,
        Continue
    };

    public partial class Form1 : Form
    {
        private SearchState searchState;
        private string searchPattern = "";
        private int processedFiles = 0;
        private CancellationTokenSource cts;
        private CancellationToken token;
        private ManualResetEventSlim limiter;
        private Stopwatch stopwatch;

        public Form1()
        {
            InitializeComponent();

            searchState = SearchState.Begin;
            limiter = new ManualResetEventSlim(true);
            stopwatch = new Stopwatch();
            timer1.Enabled = true;

            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            textBox1.Text = config.AppSettings.Settings["directory"].Value;
            textBox2.Text = config.AppSettings.Settings["filePattern"].Value;
            richTextBox1.Text = config.AppSettings.Settings["fileContent"].Value;
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            switch (searchState)
            {
                case SearchState.Begin:
                    if (!String.IsNullOrEmpty(textBox2.Text) && !String.IsNullOrWhiteSpace(textBox2.Text))
                        searchPattern = "*" + textBox2.Text + "*";

                    cts = new CancellationTokenSource();
                    token = cts.Token;
                    stopwatch.Restart();
                    DirSearchAsync(textBox1.Text, token);
                    button1.Text = "Остановить поиск";
                    button2.Text = "Начать новый поиск";
                    button2.Enabled = true;
                    searchState = SearchState.Stop;
                    break;
                case SearchState.Stop:
                    limiter.Reset();
                    stopwatch.Stop();
                    button1.Text = "Продолжить поиск";
                    searchState = SearchState.Continue;
                    break;
                case SearchState.Continue:
                    limiter.Set();
                    stopwatch.Start();
                    button1.Text = "Остановить поиск";
                    searchState = SearchState.Stop;
                    break;
                default: break;
            }
        }

        private void DirSearch(string startDirectory, CancellationToken token)
        {
            try
            {
                foreach (string file in Directory.GetFiles(startDirectory, searchPattern))
                {
                    string fileText = "";
                    using (FileStream fileStream = new FileStream(file, FileMode.Open))
                    {
                        if (token.IsCancellationRequested) return;

                        BeginInvoke(new Action(delegate { label4.Text = file; }));
                        using (StreamReader streamReader = new StreamReader(fileStream))
                        {
                            if (token.IsCancellationRequested) return;
                            limiter.Wait();

                            fileText = streamReader.ReadToEnd();
                        }
                    }

                    if (fileText.Contains(richTextBox1.Text))
                    {
                        if (token.IsCancellationRequested) return;

                        BeginInvoke(new Action(delegate { UpdateTree(file, token); }));
                    }

                    if (token.IsCancellationRequested) return;
                    processedFiles++;
                    BeginInvoke(new Action(delegate { label5.Text = processedFiles.ToString(); }));
                }

                foreach (string directory in Directory.GetDirectories(startDirectory))
                {
                    if (token.IsCancellationRequested) return;

                    DirSearch(directory, token);
                }
            }
            catch (System.Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }
        }

        private async void DirSearchAsync(string startDirectory, CancellationToken token)
        {
            await Task.Run(() => DirSearch(startDirectory, token)).ContinueWith((task) =>
            {
                if (!token.IsCancellationRequested)
                {
                    stopwatch.Stop();
                    BeginInvoke(new Action(delegate { button1.Text = ""; button1.Enabled = false; }));
                    //BeginInvoke(new Action(delegate { button1.Enabled = false; }));
                    searchState = SearchState.Begin;
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void UpdateTree(string path, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            var childs = treeView1.Nodes;
            var parts = path.Split('\\');
            int n = parts.Length;
            for (int i = 0; i < n; i++)
            {
                var foundNodes = childs.Find(parts[i], false);

                if (token.IsCancellationRequested) return;

                if (foundNodes.Length > 0)
                    childs = foundNodes[0].Nodes;
                else
                    childs = childs.Add(parts[i], parts[i]).Nodes;
            }
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            if (searchState != SearchState.Begin)
            {
                cts.Cancel();
                if (!limiter.IsSet)
                    limiter.Set();

                searchState = SearchState.Begin;
            }

            searchPattern = "";
            label4.Text = "";
            processedFiles = 0;
            label5.Text = "";
            treeView1.Nodes.Clear();
            button1.Enabled = true;

            Button1_Click(sender, e);
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            TimeSpan timeSpan = stopwatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds,
                timeSpan.Milliseconds / 10);
            label6.Text = elapsedTime;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["directory"].Value = textBox1.Text;
            config.AppSettings.Settings["filePattern"].Value = textBox2.Text;
            config.AppSettings.Settings["fileContent"].Value = richTextBox1.Text;
            config.Save();

            ConfigurationManager.RefreshSection("appSettings");
        }
    }
}
